using Redbox.HAL.Component.Model;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class DumpbinServiceBridge : IDumpbinService, IConfigurationObserver
    {
        private IDumpbinService Implementor;

        public void NotifyConfigurationLoaded()
        {
            LogHelper.Instance.Log("[DumpbinServiceBridge] Configuration loaded.");
            if (ControllerConfiguration.Instance.IsVMZMachine)
                this.Implementor = (IDumpbinService)new DumpbinService();
            else
                this.Implementor = (IDumpbinService)new NilDumpbinService();
        }

        public void NotifyConfigurationChangeStart()
        {
        }

        public void NotifyConfigurationChangeEnd()
        {
        }

        public bool IsFull()
        {
            bool flag = this.Implementor.IsFull();
            if (flag)
                LogHelper.Instance.WithContext("The dumpbin is full.");
            return flag;
        }

        public bool IsBin(ILocation loc) => this.Implementor.IsBin(loc);

        public int CurrentCount() => this.Implementor.CurrentCount();

        public int RemainingSpace() => this.Implementor.RemainingSpace();

        public bool ClearItems() => this.Implementor.ClearItems();

        public void DumpContents(TextWriter writer) => this.Implementor.DumpContents(writer);

        public IList<IDumpBinInventoryItem> GetBarcodesInBin() => this.Implementor.GetBarcodesInBin();

        public bool AddBinItem(string matrix) => this.Implementor.AddBinItem(matrix);

        public bool AddBinItem(IDumpBinInventoryItem item) => this.Implementor.AddBinItem(item);

        public void GetState(XmlTextWriter writer) => this.Implementor.GetState(writer);

        public void ResetState(XmlDocument document, ErrorList errors)
        {
            this.Implementor.ResetState(document, errors);
        }

        public ILocation PutLocation => this.Implementor.PutLocation;

        public int Capacity => this.Implementor.Capacity;

        public ILocation RotationLocation => this.Implementor.RotationLocation;

        internal DumpbinServiceBridge()
        {
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }
    }
}
