using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class PutResult : IPutResult
    {
        public override string ToString() => this.Code.ToString();

        public bool Success => this.Code == ErrorCodes.Success;

        public bool IsSlotInUse => ErrorCodes.SlotInUse == this.Code;

        public bool PickerEmpty => ErrorCodes.PickerEmpty == this.Code;

        public bool PickerObstructed => ErrorCodes.PickerObstructed == this.Code;

        public bool IsDuplicate { get; internal set; }

        public ILocation PutLocation { get; private set; }

        public ErrorCodes Code { get; internal set; }

        public string OriginalMatrix { get; private set; }

        public string StoredMatrix { get; internal set; }

        public ILocation OriginalMatrixLocation { get; internal set; }

        internal PutResult(string matrix, ILocation putLocation)
        {
            this.Code = ErrorCodes.Success;
            this.IsDuplicate = false;
            this.OriginalMatrix = matrix;
            this.PutLocation = putLocation;
            this.StoredMatrix = this.OriginalMatrix;
        }
    }
}
