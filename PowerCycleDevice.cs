using Redbox.HAL.Component.Model;
using System.Threading;

namespace Redbox.HAL.Controller.Framework
{
    internal abstract class PowerCycleDevice : IPowerCycleDevice
    {
        protected int CyclePause;

        public bool Configured { get; protected set; }

        public ErrorCodes CutPower() => this.OnPowerCut();

        public ErrorCodes SupplyPower() => this.OnPowerSupply();

        public ErrorCodes CyclePower()
        {
            ErrorCodes errorCodes = this.CutPower();
            if (errorCodes != ErrorCodes.Success)
                return errorCodes;
            Thread.Sleep(this.CyclePause);
            return this.SupplyPower();
        }

        protected internal abstract PowerCycleDevices Device { get; }

        protected abstract ErrorCodes OnPowerCut();

        protected abstract ErrorCodes OnPowerSupply();
    }
}
