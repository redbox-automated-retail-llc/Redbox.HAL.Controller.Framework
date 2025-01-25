using Redbox.HAL.Component.Model;
using Redbox.HAL.Core.Descriptors;
using System;
using System.Diagnostics;
using System.Threading;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ArcusDeviceDescriptor : AbstractDeviceDescriptor
    {
        protected override bool OnResetDriver()
        {
            Thread.Sleep(3000);
            if (!this.UsbService.SetDeviceState((IDeviceDescriptor)this, DeviceState.Disable) || !this.UsbService.SetDeviceState((IDeviceDescriptor)this, DeviceState.Enable))
                return false;
            if (this.UsbService.SetDeviceState((IDeviceDescriptor)this, DeviceState.Enable))
                return true;
            try
            {
                Process process = Process.Start("c:\\Program Files\\Redbox\\hal-tools\\halutilities.exe", "-resetProteus");
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("Unable to reset Proteus driver.", ex);
                return false;
            }
        }

        protected override bool OnLocate() => false;

        protected override DeviceStatus OnGetStatus() => DeviceStatus.Unknown;

        protected override bool OnMatchDriver() => false;

        internal ArcusDeviceDescriptor(IUsbDeviceService usb)
          : base(usb, DeviceClass.None)
        {
            this.Vendor = "1589";
            this.Product = "a001";
            this.Friendlyname = "Proteus XES";
        }
    }
}
