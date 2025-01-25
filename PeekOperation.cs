using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class PeekOperation : AbstractOperation<IPeekResult>
    {
        public override IPeekResult Execute()
        {
            IPeekResult peekResult = this.OnPeek();
            string printableLocation = ServiceLocator.Instance.GetService<IMotionControlService>().GetPrintableLocation();
            if (!peekResult.TestOk)
                LogHelper.Instance.WithContext(true, LogEntryType.Error, "Peek {0} returned error status {1}", (object)printableLocation, (object)peekResult.Error.ToString().ToUpper());
            else
                LogHelper.Instance.WithContext(false, LogEntryType.Info, "Peek {0} returned status {1}", (object)printableLocation, peekResult.IsFull ? (object)"FULL" : (object)"EMPTY");
            return peekResult;
        }

        private IPeekResult OnPeek()
        {
            ILocation currentLocation = ServiceLocator.Instance.GetService<IMotionControlService>().CurrentLocation;
            LocationTestResult locationTestResult = new LocationTestResult(currentLocation.Deck, currentLocation.Slot);
            if (currentLocation.IsWide)
            {
                ErrorCodes errorCodes = this.SettleDiskInSlot();
                if (errorCodes != ErrorCodes.Success)
                {
                    locationTestResult.Error = errorCodes;
                    return (IPeekResult)locationTestResult;
                }
            }
            if (!this.Controller.SetFinger(GripperFingerState.Closed).Success)
            {
                locationTestResult.Error = ErrorCodes.GripperCloseTimeout;
                return (IPeekResult)locationTestResult;
            }
            IControlResponse controlResponse = this.Controller.ExtendArm(ControllerConfiguration.Instance.TestExtendTime);
            if (controlResponse.CommError)
            {
                locationTestResult.Error = ErrorCodes.CommunicationError;
                return (IPeekResult)locationTestResult;
            }
            locationTestResult.IsFull = controlResponse.TimedOut;
            locationTestResult.Error = ErrorCodes.Success;
            this.Controller.RetractArm();
            this.Controller.SetFinger(GripperFingerState.Rent);
            return (IPeekResult)locationTestResult;
        }
    }
}
