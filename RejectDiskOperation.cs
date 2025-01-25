using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class RejectDiskOperation : AbstractOperation<ErrorCodes>
    {
        private readonly int Attempts;

        public override ErrorCodes Execute()
        {
            ErrorCodes errorCodes = this.OnReject();
            if ((ErrorCodes.PickerEmpty == errorCodes || ErrorCodes.PickerFull == errorCodes) && VendDoorState.Closed != this.Controller.VendDoorState && !this.Controller.VendDoorClose().Success)
                errorCodes = ErrorCodes.VendDoorCloseTimeout;
            return errorCodes;
        }

        internal RejectDiskOperation(int attempts) => this.Attempts = attempts;

        private ErrorCodes OnReject()
        {
            IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
            int attempts = this.Attempts;
            int ms = 1000;
            if (this.OpenDoorChecked() != ErrorCodes.Success)
                return ErrorCodes.VendDoorRentTimeout;
            while (attempts-- >= 0)
            {
                int num1 = (int)this.Controller.TrackCycle();
                IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
                if (!sensorReadResult1.Success)
                    return ErrorCodes.SensorReadError;
                if (sensorReadResult1.BlockedCount == 0)
                    return !this.Controller.VendDoorClose().Success ? ErrorCodes.PickerObstructed : ErrorCodes.PickerEmpty;
                sensorReadResult1.Log();
                bool flag = sensorReadResult1.IsInputActive(PickerInputs.Sensor4) || sensorReadResult1.IsInputActive(PickerInputs.Sensor3) || sensorReadResult1.IsInputActive(PickerInputs.Sensor2) || sensorReadResult1.IsInputActive(PickerInputs.Sensor1);
                if (sensorReadResult1.IsInputActive(PickerInputs.Sensor6))
                {
                    if (flag)
                    {
                        int num2 = (int)this.ControllerService.PushOut();
                        service.Wait(ms);
                    }
                    else
                    {
                        if (this.Controller.RollerToPosition(RollerPosition.Position3, 8000).Success)
                        {
                            IPickerSensorReadResult sensorReadResult2 = this.Controller.ReadPickerSensors();
                            if (!sensorReadResult2.Success)
                                return ErrorCodes.SensorReadError;
                            if (!sensorReadResult2.IsFull)
                                return ErrorCodes.PickerEmpty;
                            if (sensorReadResult2.BlockedCount <= 3)
                                return !this.Controller.VendDoorClose().Success ? ErrorCodes.PickerObstructed : ErrorCodes.PickerFull;
                        }
                        service.Wait(ms);
                    }
                }
                else if (flag)
                {
                    int num3 = (int)this.ControllerService.PushOut();
                    service.Wait(1500);
                }
            }
            IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
            if (!sensorReadResult.Success)
                return ErrorCodes.SensorReadError;
            if (!sensorReadResult.IsFull)
                return ErrorCodes.PickerEmpty;
            return sensorReadResult.BlockedCount > 3 || !this.Controller.VendDoorClose().Success ? ErrorCodes.PickerObstructed : ErrorCodes.PickerFull;
        }
    }
}
