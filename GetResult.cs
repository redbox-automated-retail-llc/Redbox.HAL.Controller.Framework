using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class GetResult : IGetResult
    {
        public override string ToString() => this.HardwareError.ToString();

        public void Update(ErrorCodes newError) => this.HardwareError = newError;

        public ErrorCodes HardwareError { get; private set; }

        public bool Success => this.HardwareError == ErrorCodes.Success;

        public bool EmptyOrStuck => this.IsSlotEmpty || this.ItemStuck;

        public bool IsSlotEmpty => ErrorCodes.SlotEmpty == this.HardwareError;

        public bool ItemStuck => ErrorCodes.ItemStuck == this.HardwareError;

        public ILocation Location { get; private set; }

        public string Previous { get; private set; }

        public DateTime? ReturnTime { get; private set; }

        public MerchFlags Flags { get; private set; }

        internal GetResult(ILocation location)
        {
            this.Location = location;
            this.Previous = this.Location.ID;
            this.Flags = this.Location.Flags;
            this.ReturnTime = location.ReturnDate;
            this.HardwareError = ErrorCodes.Success;
            LogHelper.Instance.Log(LogEntryType.Debug, "[GET Result] Loc = {0} Prev = {1} Flags = {2} r/t = {3}", (object)location.ToString(), (object)this.Previous, (object)this.Flags.ToString(), (object)this.ReturnTime.ToString());
        }
    }
}
