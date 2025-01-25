using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework.Operations
{
    internal sealed class GetFromResult : IGetFromResult, IGetResult
    {
        internal GetResult GetResult;

        public ErrorCodes MoveResult { get; internal set; }

        public void Update(ErrorCodes newError)
        {
            this.Validate();
            this.GetResult.Update(newError);
        }

        public bool Success
        {
            get
            {
                this.Validate();
                return this.GetResult.Success;
            }
        }

        public bool IsSlotEmpty
        {
            get
            {
                this.Validate();
                return this.GetResult.IsSlotEmpty;
            }
        }

        public bool ItemStuck
        {
            get
            {
                this.Validate();
                return this.GetResult.ItemStuck;
            }
        }

        public string Previous
        {
            get
            {
                this.Validate();
                return this.GetResult.Previous;
            }
        }

        public ILocation Location
        {
            get
            {
                this.Validate();
                return this.GetResult.Location;
            }
        }

        public DateTime? ReturnTime
        {
            get
            {
                this.Validate();
                return this.GetResult.ReturnTime;
            }
        }

        public MerchFlags Flags
        {
            get
            {
                this.Validate();
                return this.GetResult.Flags;
            }
        }

        public ErrorCodes HardwareError
        {
            get
            {
                this.Validate();
                return this.GetResult.HardwareError;
            }
        }

        internal GetFromResult() => this.MoveResult = ErrorCodes.Success;

        private void Validate()
        {
            if (this.GetResult == null)
                throw new InvalidOperationException("No GetResult");
        }
    }
}
