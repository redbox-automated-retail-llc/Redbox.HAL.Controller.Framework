using Redbox.HAL.Component.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class EmptySearchPatternService : IInventoryObserver, IEmptySearchPatternService
    {
        private readonly List<IExcludeEmptySearchLocationObserver> Observers = new List<IExcludeEmptySearchLocationObserver>();
        private readonly List<EmptySearchPatternService.ESPNode> Pattern = new List<EmptySearchPatternService.ESPNode>();
        private readonly object Lock = new object();
        private readonly IInventoryService InventoryService;

        public void OnInventoryInitialize()
        {
            LogHelper.Instance.Log("[EmptySearchService] Initialize.");
            this.Rebuild();
        }

        public void OnInventoryChange()
        {
            LogHelper.Instance.Log("[EmptySearchService] OnInventoryRebuild.");
            this.Rebuild();
        }

        public void OnInventoryRebuild()
        {
            LogHelper.Instance.Log("[EmptySearchService] OnInventoryRebuild.");
            this.Rebuild();
        }

        public void DumpESP(bool dumpStore)
        {
            string path = Path.Combine(ServiceLocator.Instance.GetService<IFormattedLogFactoryService>().CreateSubpath("Service"), "ESP.log");
            using (StreamWriter log = new StreamWriter((Stream)File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                string str = string.Format("{0} {1}: *** Dump of ESP list ***", (object)DateTime.Now.ToShortDateString(), (object)DateTime.Now.ToShortTimeString());
                log.WriteLine(str);
                this.Pattern.ForEach((Action<EmptySearchPatternService.ESPNode>)(node =>
                {
                    ILocation location = this.InventoryService.Get(node.Deck, node.Slot);
                    log.WriteLine("{0} Excluded = {1}", (object)location.ToString(), (object)location.Excluded.ToString());
                }));
                log.WriteLine();
            }
        }

        public void AddObserver(IExcludeEmptySearchLocationObserver veto)
        {
            lock (this.Lock)
                this.Observers.Add(veto);
        }

        public void RemoveObserver(IExcludeEmptySearchLocationObserver v)
        {
            lock (this.Lock)
                this.Observers.Remove(v);
        }

        public IEmptySearchResult FindEmptyLocations()
        {
            EmptySearchResult emptyLocations = new EmptySearchResult();
            int num = this.SlotsAvailable();
            if (num == 0)
                return (IEmptySearchResult)emptyLocations;
            foreach (EmptySearchPatternService.ESPNode espNode in this.Pattern)
            {
                ILocation location = this.InventoryService.Get(espNode.Deck, espNode.Slot);
                if (location.IsEmpty)
                {
                    emptyLocations.EmptyLocations.Add(location);
                    if (emptyLocations.EmptyLocations.Count == num)
                        break;
                }
            }
            return (IEmptySearchResult)emptyLocations;
        }

        public IEmptySearchResult FindEmptyOutsideOf(ILocation top)
        {
            EmptySearchResult emptyOutsideOf = new EmptySearchResult();
            int num = this.SlotsAvailable();
            if (num == 0)
                return (IEmptySearchResult)emptyOutsideOf;
            foreach (EmptySearchPatternService.ESPNode espNode in this.Pattern)
            {
                ILocation location = this.InventoryService.Get(espNode.Deck, espNode.Slot);
                if (!top.Equals((object)location))
                {
                    if (location.IsEmpty)
                        emptyOutsideOf.EmptyLocations.Add(location);
                    if (emptyOutsideOf.EmptyLocations.Count == num)
                        break;
                }
                else
                    break;
            }
            return (IEmptySearchResult)emptyOutsideOf;
        }

        internal EmptySearchPatternService(IInventoryService iis)
        {
            this.InventoryService = iis;
            this.InventoryService.AddObserver((IInventoryObserver)this);
        }

        private void Rebuild()
        {
            lock (this.Lock)
            {
                this.Pattern.Clear();
                this.ComputeList();
                LogHelper.Instance.Log(" ESP statistics:");
                LogHelper.Instance.Log("  Total entries: {0}", (object)this.Pattern.Count);
                LogHelper.Instance.Log("  There are {0} total excluded slots.", (object)this.InventoryService.GetExcludedSlotsCount());
            }
        }

        private int SlotsAvailable()
        {
            if (this.InventoryService.CheckIntegrity() != ErrorCodes.Success)
                return 0;
            int returnSlotBuffer = ControllerConfiguration.Instance.ReturnSlotBuffer;
            int num = this.InventoryService.GetMachineEmptyCount() - returnSlotBuffer;
            if (num <= 0)
            {
                LogHelper.Instance.Log("[EmptySearchService] There are no empty slots available.");
                return 0;
            }
            if (num > ControllerConfiguration.Instance.PutAwayItemAttempts)
                num = ControllerConfiguration.Instance.PutAwayItemAttempts;
            return num;
        }

        private void AddLocation(IDeck deck, int slot, ref int idx)
        {
            ILocation location = this.InventoryService.Get(deck.Number, slot);
            if (location.Excluded)
            {
                LogHelper.Instance.Log(LogEntryType.Debug, "Location {0} is already excluded.", (object)location);
            }
            else
            {
                foreach (IExcludeEmptySearchLocationObserver observer in this.Observers)
                {
                    if (observer.ShouldExclude(location))
                    {
                        LogHelper.Instance.Log("[EmptySearchService] Apply exclude policy to {0}", (object)location);
                        location.Excluded = true;
                        this.InventoryService.Save(location);
                        return;
                    }
                }
                this.Pattern.Add(new EmptySearchPatternService.ESPNode(deck.Number, slot, idx));
            }
        }

        private void ComputeList()
        {
            IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
            int[] numArray = new int[5] { 3, 2, 4, 1, 5 };
            int idx = 0;
            if (!ControllerConfiguration.Instance.IsVMZMachine)
            {
                for (int index = 0; index < 6; ++index)
                {
                    foreach (int number in numArray)
                    {
                        IDeck byNumber = service.GetByNumber(number);
                        int num1 = byNumber.IsSparse ? 12 : 15;
                        int num2 = index * num1 + 1;
                        for (int slot = num2; slot <= num2 + num1 - 1; ++slot)
                            this.AddLocation(byNumber, slot, ref idx);
                    }
                }
                IDeck byNumber1 = service.GetByNumber(6);
                for (int slot = 1; slot <= byNumber1.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber1, slot, ref idx);
                IDeck byNumber2 = service.GetByNumber(7);
                for (int slot = 1; slot <= byNumber2.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber2, slot, ref idx);
                IDeck byNumber3 = service.GetByNumber(8);
                if (byNumber3.IsQlm)
                    return;
                for (int slot = 1; slot <= byNumber3.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber3, slot, ref idx);
            }
            else
            {
                for (int index = 1; index < 6; ++index)
                {
                    foreach (int number in numArray)
                    {
                        IDeck byNumber = service.GetByNumber(number);
                        int num = index * byNumber.SlotsPerQuadrant + 1;
                        for (int slot = num; slot <= num + byNumber.SlotsPerQuadrant - 1; ++slot)
                            this.AddLocation(byNumber, slot, ref idx);
                    }
                }
                IDeck byNumber4 = service.GetByNumber(6);
                for (int slot = 16; slot <= byNumber4.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber4, slot, ref idx);
                IDeck byNumber5 = service.GetByNumber(8);
                for (int slot = 16; slot <= byNumber5.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber5, slot, ref idx);
                IDeck byNumber6 = service.GetByNumber(7);
                for (int slot = 16; slot <= byNumber6.NumberOfSlots; ++slot)
                    this.AddLocation(byNumber6, slot, ref idx);
                for (int number = 1; number <= 8; ++number)
                {
                    IDeck byNumber7 = service.GetByNumber(number);
                    for (int slot = 15; slot >= 1; --slot)
                        this.AddLocation(byNumber7, slot, ref idx);
                }
            }
        }

        private class ESPNode
        {
            internal readonly int Deck;
            internal readonly int Slot;
            internal readonly int Index;

            internal ESPNode(int deck, int slot, int idx)
            {
                this.Deck = deck;
                this.Slot = slot;
                this.Index = idx;
            }
        }
    }
}
