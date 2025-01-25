using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class TransferResult : ITransferResult
    {
        public bool ReturnedToSource { get; internal set; }

        public bool Transferred => this.TransferError == ErrorCodes.Success && this.Destination != null;

        public ErrorCodes TransferError { get; internal set; }

        public ILocation Source { get; internal set; }

        public ILocation Destination { get; internal set; }

        internal TransferResult() => this.TransferError = ErrorCodes.Success;
    }
}
