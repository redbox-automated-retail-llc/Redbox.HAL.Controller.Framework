using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    [PowerCycleDevice(Device = PowerCycleDevices.Router)]
    internal sealed class RouterPowerCycleDevice : PowerCycleDevice, IConfigurationObserver
    {
        public void NotifyConfigurationLoaded() => this.OnConfigurationUpdate();

        public void NotifyConfigurationChangeStart()
        {
        }

        public void NotifyConfigurationChangeEnd() => this.OnConfigurationUpdate();

        protected internal override PowerCycleDevices Device => PowerCycleDevices.Router;

        protected override ErrorCodes OnPowerCut()
        {
            return ServiceLocator.Instance.GetService<ICoreCommandExecutor>().ExecuteControllerCommand(CommandType.PowerAux21).Error;
        }

        protected override ErrorCodes OnPowerSupply()
        {
            return ServiceLocator.Instance.GetService<ICoreCommandExecutor>().ExecuteControllerCommand(CommandType.DisableAux21).Error;
        }

        internal RouterPowerCycleDevice()
        {
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }

        private void OnConfigurationUpdate()
        {
            LogHelper.Instance.Log("[RouterPowerCycleDevice] Configuration update");
            this.CyclePause = ControllerConfiguration.Instance.RouterPowerCyclePause;
            this.Configured = this.CyclePause > 0;
        }
    }
}
