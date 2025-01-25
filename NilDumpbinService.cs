using Redbox.HAL.Component.Model;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class NilDumpbinService : IDumpbinService
    {
        public bool IsFull() => true;

        public bool IsBin(ILocation loc) => false;

        public int CurrentCount() => 0;

        public int RemainingSpace() => 0;

        public bool ClearItems() => false;

        public void DumpContents(TextWriter writer)
        {
            writer.WriteLine("The dumpbin is not configured.");
        }

        public IList<IDumpBinInventoryItem> GetBarcodesInBin()
        {
            return (IList<IDumpBinInventoryItem>)new List<IDumpBinInventoryItem>();
        }

        public bool AddBinItem(string m) => false;

        public bool AddBinItem(IDumpBinInventoryItem i) => false;

        public void GetState(XmlTextWriter writer)
        {
        }

        public void ResetState(XmlDocument document, ErrorList errors)
        {
        }

        public ILocation PutLocation => (ILocation)null;

        public int Capacity => 0;

        public ILocation RotationLocation { get; private set; }

        internal NilDumpbinService()
        {
        }
    }
}
