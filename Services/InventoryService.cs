using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using Redbox.HAL.Component.Model.Threading;
using Redbox.HAL.Component.Model.Timers;
using Redbox.HAL.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Redbox.HAL.Controller.Framework.Services
{
    public sealed class InventoryService : IConfigurationObserver, IInventoryService
    {
        private readonly List<IInventoryObserver> Observers = new List<IInventoryObserver>();
        private readonly IDataTable<ILocation> Table;
        private readonly string EmptyStuckLogFile;
        private readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();
        private ErrorCodes m_storeState;
        private ILocation[][] LocationInfo;

        public void NotifyConfigurationLoaded()
        {
            LogHelper.Instance.Log("[InventoryService] Notify configuration load");
            IEmptySearchPatternService service = ServiceLocator.Instance.GetService<IEmptySearchPatternService>();
            if (ControllerConfiguration.Instance.IsVMZMachine)
                service.AddObserver((IExcludeEmptySearchLocationObserver)new VmzExcludePolicy());
            else
                service.AddObserver((IExcludeEmptySearchLocationObserver)new QlmExcludePolicy());
            using (new WithWriteLock(this.Lock))
                this.InitializeUnderLock(new ErrorList());
            if (this.m_storeState != ErrorCodes.Success)
                return;
            this.Observers.ForEach((Action<IInventoryObserver>)(each => each.OnInventoryInitialize()));
        }

        public void NotifyConfigurationChangeStart()
        {
        }

        public void NotifyConfigurationChangeEnd()
        {
            LogHelper.Instance.Log("[InventoryService] Configuration change end.");
            int num = 0;
            using (new WithWriteLock(this.Lock))
            {
                IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
                List<ILocation> updates = new List<ILocation>();
                using (new DisposeableList<ILocation>((IList<ILocation>)updates))
                {
                    service.ForAllDecksDo((Predicate<IDeck>)(deck =>
                    {
                        if (this.LocationInfo[deck.Number - 1].Length == deck.NumberOfSlots)
                            deck.Quadrants.ForEach((Action<IQuadrant>)(q =>
                  {
                            for (int start = q.Slots.Start; start <= q.Slots.End; ++start)
                            {
                                ILocation underLock = this.GetUnderLock(deck.Number, start);
                                if (underLock.Excluded != q.IsExcluded)
                                {
                                    this.ResetLocation(underLock);
                                    underLock.Excluded = q.IsExcluded;
                                    updates.Add(underLock);
                                }
                            }
                        }));
                        return true;
                    }));
                    if (updates.Count > 0)
                    {
                        num = updates.Count;
                        this.Table.Update((IList<ILocation>)updates);
                    }
                }
            }
            if (num > 0)
                this.Observers.ForEach((Action<IInventoryObserver>)(each => each.OnInventoryChange()));
            LogHelper.Instance.Log("[InventoryService] The service {0} configured to track problem locations.", this.IsTrackingEmptyStuck ? (object)"is" : (object)"is not");
        }

        public void AddObserver(IInventoryObserver o) => this.Observers.Add(o);

        public bool MachineIsFull()
        {
            int machineEmptyCount = this.GetMachineEmptyCount();
            bool flag = machineEmptyCount <= ControllerConfiguration.Instance.ReturnSlotBuffer;
            if (flag)
                LogHelper.Instance.WithContext(LogEntryType.Info, "The machine is full; there is {0} empty slot(s).", (object)machineEmptyCount);
            return flag;
        }

        public bool EmptyCountExceeds(int numberOfEmpty) => this.GetMachineEmptyCount() > numberOfEmpty;

        public ILocation Lookup(string id)
        {
            using (new WithReadLock(this.Lock))
            {
                ILocation location = this.LookupUnderLock(id);
                if (location == null)
                {
                    string msg = string.Format("LOOKUP of item {0} returned NOT FOUND.", (object)id);
                    LogHelper.Instance.WithContext(LogEntryType.Error, msg);
                    ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext().ContextLog.WriteFormatted(msg);
                }
                return location;
            }
        }

        public ILocation Get(int deck, int slot)
        {
            using (new WithReadLock(this.Lock))
                return this.GetUnderLock(deck, slot);
        }

        public bool Reset(ILocation loc)
        {
            using (new WithWriteLock(this.Lock))
            {
                this.ResetLocation(loc);
                return this.Table.Update(loc);
            }
        }

        public bool Reset(IList<ILocation> locs)
        {
            using (new WithWriteLock(this.Lock))
            {
                foreach (ILocation loc in (IEnumerable<ILocation>)locs)
                    this.ResetLocation(loc);
                return this.Table.Update(locs);
            }
        }

        public bool Save(ILocation location)
        {
            using (new WithWriteLock(this.Lock))
                return this.Table.Update(location);
        }

        public bool Save(IList<ILocation> locations)
        {
            using (new WithWriteLock(this.Lock))
                return this.Table.Update(locations);
        }

        public int GetExcludedSlotsCount()
        {
            return this.ComputeCount((Predicate<ILocation>)(loc => loc.Excluded));
        }

        public List<ILocation> GetExcludedSlots()
        {
            List<ILocation> list = this.ComputeList((Predicate<ILocation>)(loc => loc.Excluded));
            list.Sort((Comparison<ILocation>)((x, y) => x.Deck == y.Deck ? x.Slot.CompareTo(y.Slot) : x.Deck.CompareTo(y.Deck)));
            return list;
        }

        public List<ILocation> GetEmptySlots()
        {
            return this.ComputeList((Predicate<ILocation>)(loc => !loc.Excluded && loc.ID == "EMPTY"));
        }

        public List<ILocation> GetUnknowns()
        {
            return this.ComputeList((Predicate<ILocation>)(loc => !loc.Excluded && loc.ID == "UNKNOWN"));
        }

        public bool IsBarcodeDuplicate(string id, out ILocation original)
        {
            new DuplicateSearchResult().Original = (ILocation)null;
            original = (ILocation)null;
            if (InventoryConstants.IsKnownInventoryToken(id))
                return false;
            using (new WithReadLock(this.Lock))
            {
                original = this.LookupUnderLock(id);
                return original != null;
            }
        }

        public int GetMachineEmptyCount()
        {
            return this.ComputeCount((Predicate<ILocation>)(loc => !loc.Excluded && loc.ID == "EMPTY"));
        }

        public bool UpdateEmptyStuck(ILocation location)
        {
            if (!this.IsTrackingEmptyStuck)
            {
                LogHelper.Instance.Log("[UpdateEmptyStuck] Service is not tracking.");
                return false;
            }
            using (new WithWriteLock(this.Lock))
            {
                ++location.StuckCount;
                this.LogFormattedMessage("Incrementing Deck = {0} Slot = {1}; current stuck count = {2}", (object)location.Deck, (object)location.Slot, (object)location.StuckCount);
                if (location.StuckCount >= ControllerConfiguration.Instance.MarkLocationUnknownThreshold && location.ID != "UNKNOWN")
                {
                    this.LogFormattedMessage(" ** Error threshold met: set inventory to {0} at Deck = {1} Slot = {2}", (object)"UNKNOWN", (object)location.Deck, (object)location.Slot);
                    location.ID = "UNKNOWN";
                }
                return this.Table.Update(location);
            }
        }

        public bool IsStuck(ILocation location) => this.IsTrackingEmptyStuck && location.StuckCount > 0;

        public void DumpStore(TextWriter writer)
        {
            using (new WithReadLock(this.Lock))
            {
                List<ILocation> list = this.Table.LoadEntries();
                writer.WriteLine("-- {0} Dump inventory store ( {1} total items )--", (object)DateTime.Now.ToLongDateString(), (object)list.Count);
                using (new DisposeableList<ILocation>((IList<ILocation>)list))
                {
                    foreach (ILocation location in list)
                    {
                        TextWriter textWriter = writer;
                        object[] objArray = new object[6]
                        {
              (object) location.ToString(),
              (object) location.ID,
              null,
              null,
              null,
              null
                        };
                        DateTime? returnDate = location.ReturnDate;
                        string str;
                        if (!returnDate.HasValue)
                        {
                            str = "NONE";
                        }
                        else
                        {
                            returnDate = location.ReturnDate;
                            str = returnDate.ToString();
                        }
                        objArray[2] = (object)str;
                        objArray[3] = (object)location.Excluded;
                        objArray[4] = (object)location.StuckCount;
                        objArray[5] = (object)location.Flags;
                        textWriter.WriteLine("{0} ID = {1} ReturnTime = {2} Excluded = {3} StuckCount = {4} MerchFlags = {5}", objArray);
                    }
                }
            }
        }

        public bool MarkDeckInventory(IDeck deck, string newMatrix)
        {
            List<ILocation> locationList = new List<ILocation>();
            using (new WithWriteLock(this.Lock))
            {
                using (new DisposeableList<ILocation>((IList<ILocation>)locationList))
                {
                    for (int slot = 1; slot <= deck.NumberOfSlots; ++slot)
                    {
                        ILocation underLock = this.GetUnderLock(deck.Number, slot);
                        underLock.ID = newMatrix;
                        locationList.Add(underLock);
                    }
                    return this.Table.Update((IList<ILocation>)locationList);
                }
            }
        }

        public List<int> SwapEmptyWith(IDeck deck, string id, MerchFlags flags, IRange<int> range)
        {
            List<int> intList = new List<int>();
            List<ILocation> locationList = new List<ILocation>();
            using (new WithWriteLock(this.Lock))
            {
                using (new DisposeableList<ILocation>((IList<ILocation>)locationList))
                {
                    for (int start = range.Start; start <= range.End; ++start)
                    {
                        ILocation underLock = this.GetUnderLock(deck.Number, start);
                        if (underLock.ID == "EMPTY" && !underLock.Excluded)
                        {
                            underLock.ID = id;
                            underLock.Flags = flags;
                            locationList.Add(underLock);
                            intList.Add(start);
                        }
                    }
                    this.Table.Update((IList<ILocation>)locationList);
                }
            }
            return intList;
        }

        public bool ResetAndMark(IDeck deck, string id)
        {
            List<ILocation> locationList = new List<ILocation>();
            using (new DisposeableList<ILocation>((IList<ILocation>)locationList))
            {
                using (new WithWriteLock(this.Lock))
                {
                    for (int slot = 1; slot <= deck.NumberOfSlots; ++slot)
                    {
                        ILocation underLock = this.GetUnderLock(deck.Number, slot);
                        this.ResetLocation(underLock);
                        underLock.ID = id;
                        locationList.Add(underLock);
                    }
                    return this.Table.Update((IList<ILocation>)locationList);
                }
            }
        }

        public void GetState(XmlTextWriter writer)
        {
            using (new WithReadLock(this.Lock))
                this.ForeachUnderLock((Predicate<ILocation>)(loc =>
                {
                    writer.WriteStartElement("item");
                    writer.WriteAttributeString("deck", XmlConvert.ToString(loc.Deck));
                    writer.WriteAttributeString("slot", XmlConvert.ToString(loc.Slot));
                    writer.WriteAttributeString("id", loc.ID);
                    writer.WriteAttributeString("ReturnTime", !loc.ReturnDate.HasValue ? "NONE" : loc.ReturnDate.Value.ToString());
                    writer.WriteAttributeString("excluded", loc.Excluded.ToString());
                    writer.WriteAttributeString("emptyStuckCount", loc.StuckCount.ToString());
                    writer.WriteAttributeString("merchFlags", loc.Flags.ToString());
                    writer.WriteEndElement();
                    return true;
                }), false);
        }

        public void ResetState(XmlDocument xmlDocument, ErrorList errors)
        {
            List<ILocation> inventory = new List<ILocation>();
            using (new WithUpgradeableReadLock(this.Lock))
            {
                using (new DisposeableList<ILocation>((IList<ILocation>)inventory))
                {
                    try
                    {
                        XmlNodeList xmlNodeList = xmlDocument.DocumentElement.SelectNodes("item");
                        if (xmlNodeList == null)
                        {
                            errors.Add(Error.NewError("I001", "Invalid document.", "The specified document has no nodes."));
                        }
                        else
                        {
                            foreach (XmlNode node in xmlNodeList)
                            {
                                ILocation location = this.FromXML(node, errors);
                                if (location == null)
                                {
                                    errors.Add(Error.NewError("I002", "Load error.", "Unable to load the deck from the XML."));
                                    return;
                                }
                                inventory.Add(location);
                            }
                            using (new WithWriteLock(this.Lock))
                                ServiceLocator.Instance.GetService<IDecksService>().ForAllDecksDo((Predicate<IDeck>)(deck =>
                                {
                                    if (deck.Number - 1 < 0 || deck.Number - 1 > this.LocationInfo.GetLength(0))
                                    {
                                        errors.Add(Error.NewError("P004", string.Format("The deck {0} was not present in the deck configuration.", (object)deck.Number), "Ensure the inventory is valid."));
                                        return false;
                                    }
                                    List<ILocation> all = inventory.FindAll((Predicate<ILocation>)(each => each.Deck == deck.Number));
                                    if (all.Count != deck.NumberOfSlots)
                                    {
                                        errors.Add(Error.NewError("P004", string.Format("The slot count {0} did not match the deck slot count for deck {1} ({2} slots).", (object)all.Count, (object)deck.Number, (object)deck.NumberOfSlots), "Ensure the inventory is valid."));
                                        LogHelper.Instance.Log(string.Format("Deck {0} expected {1} slots to be imported but received {2}.", (object)deck.Number, (object)deck.NumberOfSlots, (object)all.Count), LogEntryType.Error);
                                        return true;
                                    }
                                    this.Table.Update((IList<ILocation>)all);
                                    this.LocationInfo[deck.Number - 1] = this.Sort(all);
                                    return true;
                                }));
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Error.NewError("P999", "An unhandled exception was raised in InventoryService ResetState.", ex));
                        LogHelper.Instance.Log("RESET INVENTORY Failed to import inventory.", ex, LogEntryType.Error);
                    }
                }
            }
        }

        public void Rebuild(ErrorList errors)
        {
            this.m_storeState = ErrorCodes.StoreError;
            using (new WithWriteLock(this.Lock))
            {
                if (!this.Table.Recreate(errors))
                {
                    LogHelper.Instance.Log("[InventoryService] Failed to recreate inventory table.");
                    errors.DumpToLog();
                    return;
                }
                this.InitializeUnderLock(errors);
            }
            if (this.m_storeState != ErrorCodes.Success)
                return;
            this.Observers.ForEach((Action<IInventoryObserver>)(each => each.OnInventoryRebuild()));
        }

        public void InstallFromLegacy(ErrorList errors, TextWriter writer)
        {
            int platterSlots = new GampHelper().GetPlatterSlots();
            int registrySlotCount = ControllerConfiguration.Instance.RegistrySlotCount;
            ITableTypeFactory factory = ServiceLocator.Instance.GetService<ITableTypeFactory>();
            if (platterSlots == registrySlotCount)
            {
                List<ILocation> locs = new List<ILocation>();
                ServiceLocator.Instance.GetService<IDecksService>().ForAllDecksDo((Predicate<IDeck>)(deck =>
                {
                    for (int slot = 1; slot <= deck.NumberOfSlots; ++slot)
                        locs.Add(factory.NewLocation(deck.Number, slot));
                    return true;
                }));
                if (this.Table.Insert((IList<ILocation>)locs))
                    return;
                errors.Add(Error.NewError("I055", "Database insert failed.", "Failed to insert entries into inventory table."));
            }
            else
                writer.WriteLine("The machine is configured for {0} slots; the gamp data says there are {1} slots - mark db UNKNOWN with {2} slots", (object)registrySlotCount, (object)platterSlots, (object)registrySlotCount);
        }

        public ErrorCodes CheckIntegrity() => this.CheckIntegrity(false);

        public ErrorCodes CheckIntegrity(bool testStore)
        {
            if (!ControllerConfiguration.Instance.EnableInventoryDatabaseCheck)
                return ErrorCodes.Success;
            if (!testStore)
                return this.m_storeState;
            using (new WithReadLock(this.Lock))
            {
                using (ExecutionTimer executionTimer = new ExecutionTimer())
                {
                    List<ILocation> inventory = this.Table.LoadEntries();
                    using (new DisposeableList<ILocation>((IList<ILocation>)inventory))
                    {
                        int total = 0;
                        ServiceLocator.Instance.GetService<IDecksService>().ForAllDecksDo((Predicate<IDeck>)(deck =>
                        {
                            List<ILocation> all = inventory.FindAll((Predicate<ILocation>)(each => each.Deck == deck.Number));
                            if (all.Count != deck.NumberOfSlots)
                                LogHelper.Instance.WithContext(LogEntryType.Error, "[InventoryService] Integrity mismatch: Found {0} entries for deck {1} ( # slots = {2} ) ", (object)all.Count, (object)deck.Number, (object)deck.NumberOfSlots);
                            else
                                total += deck.NumberOfSlots;
                            return true;
                        }));
                        executionTimer.Stop();
                        LogHelper.Instance.Log("[CheckStoreIntegrity] Execution time = {0}ms", (object)executionTimer.ElapsedMilliseconds);
                        return total != inventory.Count ? ErrorCodes.StoreError : ErrorCodes.Success;
                    }
                }
            }
        }

        public bool IsTrackingEmptyStuck => ControllerConfiguration.Instance.TrackProblemLocations;

        internal InventoryService()
        {
            this.Table = ServiceLocator.Instance.GetService<IDataTableService>().GetTable<ILocation>();
            this.EmptyStuckLogFile = Path.Combine(ServiceLocator.Instance.GetService<IFormattedLogFactoryService>().CreateSubpath("ErrorLogs"), "ErrorLocationsHistory.txt");
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }

        private void LogFormattedMessage(string format, params object[] parms)
        {
            if (!this.IsTrackingEmptyStuck)
                return;
            string str1 = string.Format(format, parms);
            try
            {
                using (StreamWriter streamWriter1 = new StreamWriter(this.EmptyStuckLogFile, true))
                {
                    StreamWriter streamWriter2 = streamWriter1;
                    DateTime now = DateTime.Now;
                    string shortDateString = now.ToShortDateString();
                    now = DateTime.Now;
                    string shortTimeString = now.ToShortTimeString();
                    string str2 = str1;
                    streamWriter2.WriteLine("{0} {1} {2}", (object)shortDateString, (object)shortTimeString, (object)str2);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("[InventoryService] Failed to write to empty/stuck log.", ex);
            }
        }

        private List<ILocation> ComputeList(Predicate<ILocation> predicate)
        {
            return this.ComputeList(predicate, true);
        }

        private List<ILocation> ComputeList(Predicate<ILocation> predicate, bool excludeQLM)
        {
            using (new WithReadLock(this.Lock))
            {
                List<ILocation> rv = new List<ILocation>();
                this.ForeachUnderLock((Predicate<ILocation>)(location =>
                {
                    if (predicate(location))
                        rv.Add(location);
                    return true;
                }), excludeQLM);
                return rv;
            }
        }

        private int ComputeCount(Predicate<ILocation> predicate) => this.ComputeCount(predicate, true);

        private int ComputeCount(Predicate<ILocation> predicate, bool excludeQLM)
        {
            using (new WithReadLock(this.Lock))
            {
                int count = 0;
                this.ForeachUnderLock((Predicate<ILocation>)(location =>
                {
                    if (predicate(location))
                        ++count;
                    return true;
                }), excludeQLM);
                return count;
            }
        }

        private void ForeachUnderLock(Predicate<ILocation> action, bool excludeQLM)
        {
            IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
            int length1 = this.LocationInfo.GetLength(0);
            for (int index1 = 0; index1 < length1; ++index1)
            {
                IDeck byNumber = service.GetByNumber(index1 + 1);
                if (!excludeQLM || !byNumber.IsQlm)
                {
                    int length2 = this.LocationInfo[index1].Length;
                    for (int index2 = 0; index2 < length2; ++index2)
                    {
                        ILocation location = this.LocationInfo[index1][index2];
                        if (!action(location))
                            return;
                    }
                }
            }
        }

        private ILocation LookupUnderLock(string id)
        {
            ILocation location = (ILocation)null;
            this.ForeachUnderLock((Predicate<ILocation>)(loc =>
            {
                if (!(loc.ID == id))
                    return true;
                location = loc;
                return false;
            }), true);
            return location;
        }

        private void InitializeUnderLock(ErrorList errors)
        {
            this.m_storeState = ErrorCodes.Success;
            IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
            try
            {
                List<ILocation> inventory = this.Table.LoadEntries();
                using (new DisposeableList<ILocation>((IList<ILocation>)inventory))
                {
                    this.LocationInfo = new ILocation[service.DeckCount][];
                    service.ForAllDecksDo((Predicate<IDeck>)(deck =>
                    {
                        List<ILocation> all = inventory.FindAll((Predicate<ILocation>)(each => each.Deck == deck.Number));
                        if (all.Count == deck.NumberOfSlots)
                        {
                            LogHelper.Instance.Log("[InventoryService] Loaded {0} entries for deck {1} ( # slots = {2} )", (object)all.Count, (object)deck.Number, (object)deck.NumberOfSlots);
                            this.LocationInfo[deck.Number - 1] = this.Sort(all);
                            return true;
                        }
                        if (all.Count > 0)
                        {
                            LogHelper.Instance.Log(LogEntryType.Error, "[InventoryService] LoadInventory: res.count = {0}; deck count = {1}; deleting deck & initializing to UNKNOWN", (object)all.Count, (object)deck.NumberOfSlots);
                            if (!this.Table.Delete((IList<ILocation>)all))
                            {
                                this.m_storeState = ErrorCodes.StoreError;
                                LogHelper.Instance.Log("[InventoryService] Delete returned false.");
                                return false;
                            }
                            all.Clear();
                        }
                        ILocation[] locationArray = this.InitializeDeck(deck);
                        if (locationArray != null)
                        {
                            this.LocationInfo[deck.Number - 1] = locationArray;
                            return true;
                        }
                        this.m_storeState = ErrorCodes.StoreError;
                        return false;
                    }));
                }
                LogHelper.Instance.Log("[InventoryService] The service {0} configured to track problem locations.", this.IsTrackingEmptyStuck ? (object)"is" : (object)"is not");
                LogHelper.Instance.Log("[InventoryService] Store integrity -> {0}", this.m_storeState == ErrorCodes.Success ? (object)"OK" : (object)"CORRUPT");
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("[InventoryService] Initialize under lock caught an exception.", ex);
                this.m_storeState = ErrorCodes.StoreError;
            }
        }

        private ILocation[] InitializeDeck(IDeck deck)
        {
            ITableTypeFactory service = ServiceLocator.Instance.GetService<ITableTypeFactory>();
            List<ILocation> locationList = new List<ILocation>();
            using (new DisposeableList<ILocation>((IList<ILocation>)locationList))
            {
                for (int slot = 1; slot <= deck.NumberOfSlots; ++slot)
                {
                    ILocation location = service.NewLocation(deck.Number, slot);
                    location.ID = "UNKNOWN";
                    locationList.Add(location);
                }
                if (this.Table.Insert((IList<ILocation>)locationList))
                    return this.Sort(locationList);
                LogHelper.Instance.Log("[InventoryService] Insert of new locations returned false.");
                return (ILocation[])null;
            }
        }

        private ILocation[] Sort(List<ILocation> deckEntries)
        {
            deckEntries.Sort((Comparison<ILocation>)((x, y) => x.Slot.CompareTo(y.Slot)));
            return deckEntries.ToArray();
        }

        private ILocation FromXML(XmlNode node, ErrorList errors)
        {
            IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
            int? attributeValue1 = node.GetAttributeValue<int?>("deck", new int?());
            int? attributeValue2 = node.GetAttributeValue<int?>("slot", new int?());
            string attributeValue3 = node.GetAttributeValue<string>("id", (string)null);
            string attributeValue4 = node.GetAttributeValue<string>("ReturnTime", (string)null);
            DateTime? returnTime = new DateTime?();
            if (!string.IsNullOrEmpty(attributeValue4))
            {
                if (!attributeValue4.Equals("NONE"))
                {
                    try
                    {
                        returnTime = new DateTime?(DateTime.Parse(attributeValue4));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Instance.Log(string.Format("Unable to parse date time {0}", (object)attributeValue4), LogEntryType.Error);
                        returnTime = new DateTime?();
                    }
                }
            }
            bool attributeValue5 = node.GetAttributeValue<bool>("excluded", false);
            int attributeValue6 = node.GetAttributeValue<int>("emptyStuckCount", 0);
            MerchFlags ignoringCase = Enum<MerchFlags>.ParseIgnoringCase(node.GetAttributeValue<string>("merchFlags", "None"), MerchFlags.None);
            if (!attributeValue1.HasValue || !attributeValue2.HasValue || string.IsNullOrEmpty(attributeValue3))
            {
                errors.Add(Error.NewError("P004", "A valid deck, slot, and id must be specified for each item element.", "Add a valid item element with a deck, slot, and id attributes."));
                LogHelper.Instance.Log(string.Format("Attempt to import invalid item. XmlNode: {0}", (object)node.OuterXml), LogEntryType.Error);
                return (ILocation)null;
            }
            IDeck byNumber = service.GetByNumber(attributeValue1.Value);
            if (byNumber == null)
            {
                errors.Add(Error.NewError("P003", string.Format("The deck value must be between 1 and {0}.", (object)service.DeckCount), "Ensure the deck attribute value is within the valid range."));
                LogHelper.Instance.Log(string.Format("Deck {0} doesn't match any decks in the configuration.", (object)attributeValue1), LogEntryType.Error);
                return (ILocation)null;
            }
            int? nullable1 = attributeValue2;
            int num = 1;
            if (!(nullable1.GetValueOrDefault() < num & nullable1.HasValue))
            {
                int numberOfSlots = byNumber.NumberOfSlots;
                int? nullable2 = attributeValue2;
                int valueOrDefault = nullable2.GetValueOrDefault();
                if (!(numberOfSlots < valueOrDefault & nullable2.HasValue))
                    return ServiceLocator.Instance.GetService<ITableTypeFactory>().NewLocation(attributeValue1.Value, attributeValue2.Value, attributeValue3, returnTime, attributeValue5, attributeValue6, ignoringCase);
            }
            errors.Add(Error.NewError("P003", string.Format("The slot must be between 1 and {0}.", (object)byNumber.NumberOfSlots), "Ensure the slot number is within the valid range."));
            LogHelper.Instance.Log(string.Format("Deck {0} expects a value between 1 and {1}, received {2}", (object)byNumber.Number, (object)byNumber.NumberOfSlots, (object)attributeValue2), LogEntryType.Error);
            return (ILocation)null;
        }

        private ILocation GetUnderLock(int deck, int slot) => this.LocationInfo[deck - 1][slot - 1];

        private void ResetLocation(ILocation loc)
        {
            loc.ReturnDate = new DateTime?();
            loc.ID = "EMPTY";
            loc.StuckCount = 0;
            loc.Flags = MerchFlags.None;
        }
    }
}
