using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class AcceptDiskOperation : AbstractOperation<ErrorCodes>
    {
        public override ErrorCodes Execute()
        {
            ErrorCodes errorCodes = this.OnExecute();
            if ((ErrorCodes.PickerEmpty == errorCodes || ErrorCodes.PickerFull == errorCodes) && !this.Controller.VendDoorClose().Success)
                errorCodes = ErrorCodes.VendDoorCloseTimeout;
            return errorCodes;
        }

        internal AcceptDiskOperation()
        {
        }

        private ErrorCodes OnExecute()
        {
            LogHelper.Instance.WithContext(false, LogEntryType.Info, "[AcceptDiskAtDoor] Start");
            if (this.OpenDoorChecked() != ErrorCodes.Success)
                return ErrorCodes.VendDoorRentTimeout;
            this.Controller.StartRollerIn();
            ErrorCodes errorCodes = this.WaitSensor(PickerInputs.Sensor5, InputState.Active, ControllerConfiguration.Instance.AcceptDiskTimeout);
            if (ErrorCodes.SensorReadError == errorCodes)
                return errorCodes;
            if (ErrorCodes.Timeout == errorCodes)
            {
                this.Controller.StopRoller();
                IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                if (!sensorReadResult.Success)
                    return ErrorCodes.SensorReadError;
                if (!sensorReadResult.IsFull)
                    return ErrorCodes.PickerEmpty;
            }
            LogHelper.Instance.WithContext(false, LogEntryType.Info, "[AcceptDiskAtDoor] Sensor 5 or 6 was tripped; pull into the picker.");
            if (!this.Controller.RollerToPosition(RollerPosition.Position3).Success)
            {
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "[AcceptDiskAtDoor] Unable to roll the disk to sensor 3.");
                IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                sensorReadResult.Log();
                return sensorReadResult.IsFull ? ErrorCodes.PickerFull : ErrorCodes.PickerEmpty;
            }
            IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
            if (!sensorReadResult1.Success)
                return ErrorCodes.SensorReadError;
            if (sensorReadResult1.BlockedCount > 3)
            {
                LogHelper.Instance.WithContext(false, LogEntryType.Info, "[AcceptDiskAtDoor] the gripper is obstructed.");
                sensorReadResult1.Log();
                return ErrorCodes.PickerObstructed;
            }
            return !sensorReadResult1.IsFull ? ErrorCodes.PickerEmpty : ErrorCodes.PickerFull;
        }
    }
}
