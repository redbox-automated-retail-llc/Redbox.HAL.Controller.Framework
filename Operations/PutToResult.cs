using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework.Operations
{
    internal sealed class PutToResult : IPutToResult, IPutResult
    {
        internal PutResult PutResult;

        public ErrorCodes MoveResult { get; internal set; }

        public bool Success
        {
            get
            {
                this.Validate();
                return this.PutResult.Success;
            }
        }

        public bool IsSlotInUse
        {
            get
            {
                this.Validate();
                return this.PutResult.IsSlotInUse;
            }
        }

        public bool PickerEmpty
        {
            get
            {
                this.Validate();
                return this.PutResult.PickerEmpty;
            }
        }

        public bool PickerObstructed
        {
            get
            {
                this.Validate();
                return this.PutResult.PickerObstructed;
            }
        }

        public bool IsDuplicate
        {
            get
            {
                this.Validate();
                return this.PutResult.IsDuplicate;
            }
        }

        public ILocation PutLocation
        {
            get
            {
                this.Validate();
                return this.PutResult.PutLocation;
            }
        }

        public ErrorCodes Code
        {
            get
            {
                this.Validate();
                return this.PutResult.Code;
            }
        }

        public string OriginalMatrix
        {
            get
            {
                this.Validate();
                return this.PutResult.OriginalMatrix;
            }
        }

        public string StoredMatrix
        {
            get
            {
                this.Validate();
                return this.PutResult.StoredMatrix;
            }
        }

        public ILocation OriginalMatrixLocation
        {
            get
            {
                this.Validate();
                return this.PutResult.OriginalMatrixLocation;
            }
        }

        internal PutToResult() => this.MoveResult = ErrorCodes.Success;

        private void Validate()
        {
            if (this.PutResult == null)
                throw new InvalidOperationException("No put result");
        }
    }
}
