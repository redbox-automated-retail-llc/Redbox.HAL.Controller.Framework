using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class DumpbinService : IDumpbinService
    {
        private int m_itemCount;
        private readonly IDataTable<IDumpBinInventoryItem> Table;
        private readonly int Deck;
        private readonly IRange<int> Slots;

        public bool IsFull() => this.CurrentCount() >= this.Capacity;

        public bool IsBin(ILocation loc)
        {
            return loc.Deck == this.Deck && loc.Slot >= this.Slots.Start && loc.Slot <= this.Slots.End;
        }

        public int CurrentCount() => this.m_itemCount;

        public int RemainingSpace() => !this.IsFull() ? this.Capacity - this.CurrentCount() : 0;

        public bool ClearItems()
        {
            LogHelper.Instance.Log("Reset dumpbin counter: current bin count = {0}", (object)this.CurrentCount());
            ServiceLocator.Instance.GetService<IRuntimeService>();
            try
            {
                using (StreamWriter log = new StreamWriter((Stream)File.Open(Path.Combine(ServiceLocator.Instance.GetService<IFormattedLogFactoryService>().CreateSubpath("Service"), "DumpBinInventory.log"), FileMode.Append, FileAccess.Write, FileShare.Read)))
                {
                    this.DumpContents((TextWriter)log);
                    log.WriteLine();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("Failed to log dump bin contents.", ex);
            }
            int num = this.CurrentCount();
            this.m_itemCount = 0;
            return this.Table.DeleteAll() == num;
        }

        public void DumpContents(TextWriter log)
        {
            List<IDumpBinInventoryItem> list = this.Table.LoadEntries();
            using (new DisposeableList<IDumpBinInventoryItem>((IList<IDumpBinInventoryItem>)list))
            {
                log.WriteLine("-- Dump bin inventory ( {0} total items ) --", (object)list.Count);
                foreach (IDumpBinInventoryItem binInventoryItem in list)
                    log.WriteLine("Barcode {0} put in at {1}", (object)binInventoryItem.ID, (object)binInventoryItem.PutTime.ToString());
            }
        }

        public IList<IDumpBinInventoryItem> GetBarcodesInBin()
        {
            return (IList<IDumpBinInventoryItem>)this.Table.LoadEntries();
        }

        public bool AddBinItem(string matrix)
        {
            return this.AddBinItem(ServiceLocator.Instance.GetService<ITableTypeFactory>().NewBinItem(matrix, DateTime.Now));
        }

        public bool AddBinItem(IDumpBinInventoryItem item)
        {
            ++this.m_itemCount;
            return this.Table.Insert(item);
        }

        public void GetState(XmlTextWriter writer)
        {
            List<IDumpBinInventoryItem> list = this.Table.LoadEntries();
            using (new DisposeableList<IDumpBinInventoryItem>((IList<IDumpBinInventoryItem>)list))
            {
                foreach (IDumpBinInventoryItem binInventoryItem in list)
                {
                    writer.WriteStartElement("bin-item");
                    writer.WriteAttributeString("PutTime", binInventoryItem.PutTime.ToString());
                    writer.WriteAttributeString("id", binInventoryItem.ID);
                    writer.WriteEndElement();
                }
            }
        }

        public void ResetState(XmlDocument xmlDocument, ErrorList errors)
        {
            ITableTypeFactory service = ServiceLocator.Instance.GetService<ITableTypeFactory>();
            this.ClearItems();
            XmlNodeList xmlNodeList = xmlDocument.DocumentElement.SelectNodes("bin-item");
            if (xmlNodeList == null || xmlNodeList.Count <= 0)
                return;
            List<IDumpBinInventoryItem> binInventoryItemList = new List<IDumpBinInventoryItem>();
            using (new DisposeableList<IDumpBinInventoryItem>((IList<IDumpBinInventoryItem>)binInventoryItemList))
            {
                foreach (XmlNode node in xmlNodeList)
                {
                    string attributeValue1 = node.GetAttributeValue<string>("id", "UNKNOWN");
                    string attributeValue2 = node.GetAttributeValue<string>("PutTime", (string)null);
                    DateTime now = DateTime.Now;
                    if (attributeValue2 != null)
                    {
                        try
                        {
                            now = DateTime.Parse(attributeValue2);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Instance.Log(LogEntryType.Error, "[DumpbinService] ResetState: unable to parse date time {0}", (object)attributeValue2);
                            now = DateTime.Now;
                        }
                    }
                    binInventoryItemList.Add(service.NewBinItem(attributeValue1, now));
                }
                if (binInventoryItemList.Count <= 0)
                    return;
                this.Table.Insert((IList<IDumpBinInventoryItem>)binInventoryItemList);
                this.m_itemCount += binInventoryItemList.Count;
            }
        }

        public ILocation PutLocation
        {
            get
            {
                return ServiceLocator.Instance.GetService<IInventoryService>().Get(this.Deck, this.Slots.Start + 1);
            }
        }

        public int Capacity => 60;

        public ILocation RotationLocation
        {
            get => ServiceLocator.Instance.GetService<IInventoryService>().Get(this.Deck, 1);
        }

        internal DumpbinService()
        {
            this.Deck = ServiceLocator.Instance.GetService<IDecksService>().Last.Number;
            this.Slots = (IRange<int>)new Range(82, 84);
            this.Table = ServiceLocator.Instance.GetService<IDataTableService>().GetTable<IDumpBinInventoryItem>();
            this.m_itemCount = this.Table.GetRowCount();
            LogHelper.Instance.Log("[DumpbinService] Loaded {0} items.", (object)this.m_itemCount);
        }
    }
}
