using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ClearGripperOperation : AbstractOperation<ErrorCodes>
    {
        public override ErrorCodes Execute()
        {
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            ErrorCodes errorCodes = this.CloseTrackChecked();
            if (errorCodes != ErrorCodes.Success)
                return errorCodes;
            for (int index = 0; index < 2; ++index)
            {
                IPickerSensorReadResult readResult = this.Controller.ReadPickerSensors();
                if (!readResult.Success)
                    return readResult.Error;
                if (!readResult.IsFull)
                    return ErrorCodes.Success;
                if (!readResult.IsInputActive(PickerInputs.Sensor1))
                {
                    if (!readResult.IsInputActive(PickerInputs.Sensor6))
                        return ErrorCodes.Success;
                    this.Controller.SetFinger(GripperFingerState.Rent);
                    try
                    {
                        if (service.AtVendDoor && VendDoorState.Closed != this.Controller.VendDoorState)
                            this.Controller.VendDoorRent();
                        this.Controller.RollerToPosition(RollerPosition.Position3, 3000);
                    }
                    finally
                    {
                        if (VendDoorState.Closed != this.Controller.VendDoorState)
                            this.Controller.VendDoorClose();
                    }
                }
                else
                {
                    if (readResult.IsInputActive(PickerInputs.Sensor6))
                        return ErrorCodes.PickerObstructed;
                    if (this.ClearPickerBlockedCount(readResult) == 0)
                    {
                        try
                        {
                            this.Controller.StartRollerIn();
                            int num = (int)this.PushIntoSlot();
                        }
                        finally
                        {
                            this.Controller.StopRoller();
                        }
                    }
                    else
                    {
                        this.Controller.SetFinger(GripperFingerState.Rent);
                        this.Controller.RollerToPosition(RollerPosition.Position5);
                    }
                }
            }
            return ErrorCodes.PickerObstructed;
        }
    }
}
