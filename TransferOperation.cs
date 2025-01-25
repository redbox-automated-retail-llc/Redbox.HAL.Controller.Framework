using Redbox.HAL.Component.Model;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class TransferOperation : IDisposable, IPutObserver
    {
        private readonly IMotionControlService MotionService;
        private readonly IInventoryService InventoryService;
        private readonly IControlSystem ControlSystem;
        private readonly ILocation Source;
        private readonly IControllerService ControllerService;
        private bool m_disposed;
        private IGetResult GetResult;

        public void Dispose() => this.DisposeInner(true);

        public void OnSuccessfulPut(IPutResult result, IFormattedLog log)
        {
            this.OnRestoreState(result.PutLocation, this.PreserveFlagsOnPut);
        }

        public void OnFailedPut(IPutResult result, IFormattedLog log)
        {
        }

        internal ITransferResult Transfer(IList<ILocation> destinations)
        {
            TransferResult result = new TransferResult();
            this.FetchSource(result);
            if (result.TransferError != ErrorCodes.Success)
                return (ITransferResult)result;
            foreach (ILocation destination in (IEnumerable<ILocation>)destinations)
            {
                this.MoveAndPut(result, destination);
                if (result.TransferError == ErrorCodes.Success)
                    return (ITransferResult)result;
            }
            this.ReturnToSource(result);
            return (ITransferResult)result;
        }

        internal ITransferResult Transfer(IList<ILocation> destinations, IGetObserver observer)
        {
            TransferResult result = new TransferResult();
            this.FetchSource(result, observer);
            if (result.TransferError != ErrorCodes.Success)
                return (ITransferResult)result;
            foreach (ILocation destination in (IEnumerable<ILocation>)destinations)
            {
                this.MoveAndPut(result, destination);
                if (result.TransferError == ErrorCodes.Success)
                    return (ITransferResult)result;
            }
            this.ReturnToSource(result);
            return (ITransferResult)result;
        }

        internal ITransferResult Transfer(ILocation destination)
        {
            TransferResult result = new TransferResult();
            this.FetchSource(result);
            if (result.TransferError != ErrorCodes.Success)
                return (ITransferResult)result;
            this.MoveAndPut(result, destination);
            if (!result.Transferred)
                this.ReturnToSource(result);
            return (ITransferResult)result;
        }

        internal TransferOperation(IControllerService service, ILocation source)
        {
            this.InventoryService = ServiceLocator.Instance.GetService<IInventoryService>();
            this.MotionService = ServiceLocator.Instance.GetService<IMotionControlService>();
            this.ControlSystem = ServiceLocator.Instance.GetService<IControlSystem>();
            this.ControllerService = service;
            this.Source = source;
        }

        internal bool PreserveFlagsOnPut { get; set; }

        private void MoveAndPut(TransferResult result, ILocation destination)
        {
            ErrorCodes errorCodes = this.MotionService.MoveTo(destination, MoveMode.Put);
            if (errorCodes != ErrorCodes.Success)
            {
                result.TransferError = errorCodes;
            }
            else
            {
                int num = (int)this.ControlSystem.TrackCycle();
                IPutResult putResult = this.ControllerService.Put((IPutObserver)this, this.GetResult.Previous);
                result.TransferError = putResult.Code;
                if (!putResult.Success)
                    return;
                result.Destination = destination;
            }
        }

        private void ReturnToSource(TransferResult result)
        {
            ErrorCodes errorCodes = this.MotionService.MoveTo(this.Source, MoveMode.Put);
            if (errorCodes != ErrorCodes.Success)
            {
                result.TransferError = errorCodes;
            }
            else
            {
                int num = (int)this.ControlSystem.TrackCycle();
                if (!this.ControllerService.Put(this.GetResult.Previous).Success)
                    return;
                result.ReturnedToSource = true;
                this.OnRestoreState(this.Source, true);
            }
        }

        private void OnRestoreState(ILocation location, bool preserve)
        {
            if (preserve)
                location.Flags = this.GetResult.Flags;
            location.ReturnDate = this.GetResult.ReturnTime;
            this.InventoryService.Save(location);
        }

        private void FetchSource(TransferResult result, IGetObserver observer)
        {
            ErrorCodes errorCodes = this.MotionService.MoveTo(this.Source, MoveMode.Get);
            if (errorCodes != ErrorCodes.Success)
            {
                result.TransferError = errorCodes;
            }
            else
            {
                this.GetResult = this.ControllerService.Get(observer);
                result.TransferError = this.GetResult.Success ? ErrorCodes.Success : this.GetResult.HardwareError;
                if (!this.GetResult.Success)
                    return;
                result.Source = this.Source;
            }
        }

        private void FetchSource(TransferResult result)
        {
            ErrorCodes errorCodes = this.MotionService.MoveTo(this.Source, MoveMode.Get);
            if (errorCodes != ErrorCodes.Success)
            {
                result.TransferError = errorCodes;
            }
            else
            {
                this.GetResult = this.ControllerService.Get();
                result.TransferError = this.GetResult.Success ? ErrorCodes.Success : this.GetResult.HardwareError;
                if (!this.GetResult.Success)
                    return;
                result.Source = this.Source;
            }
        }

        private void DisposeInner(bool fromDispose)
        {
            if (this.m_disposed)
                return;
            this.m_disposed = true;
        }
    }
}
