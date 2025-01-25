using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class VendItemOperation : AbstractOperation<VendItemResult>
    {
        private readonly int PollCount;

        public override VendItemResult Execute()
        {
            VendItemResult vendItemResult = this.OnExecute();
            if (ErrorCodes.PickerEmpty == vendItemResult.Status && !this.Controller.VendDoorClose().Success)
                vendItemResult.Status = ErrorCodes.VendDoorCloseTimeout;
            return vendItemResult;
        }

        internal VendItemOperation(int tries) => this.PollCount = tries;

        private VendItemResult OnExecute()
        {
            IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
            VendItemResult vendItemResult = new VendItemResult()
            {
                Presented = true,
                Status = ErrorCodes.PickerEmpty
            };
            LogHelper.Instance.WithContext(false, LogEntryType.Info, "VendDiskAtDoor...");
            this.Controller.RollerToPosition(RollerPosition.Position5, 3000);
            ErrorCodes errorCodes1 = this.OpenDoorChecked();
            if (errorCodes1 != ErrorCodes.Success)
            {
                vendItemResult.Status = errorCodes1;
                return vendItemResult;
            }
            int ms = 1000;
            ErrorCodes errorCodes2 = this.ControllerService.PushOut();
            if (ErrorCodes.TrackCloseTimeout == errorCodes2 || ErrorCodes.SensorReadError == errorCodes2)
            {
                vendItemResult.Status = errorCodes2;
                vendItemResult.Presented = false;
                return vendItemResult;
            }
            int num1 = (int)this.Controller.TrackCycle();
            int num2 = 0;
            for (int index = 0; index <= this.PollCount; ++index)
            {
                IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                if (!sensorReadResult.Success)
                {
                    vendItemResult.Status = ErrorCodes.SensorReadError;
                    return vendItemResult;
                }
                if (sensorReadResult.IsInputActive(PickerInputs.Sensor1) || sensorReadResult.IsInputActive(PickerInputs.Sensor2) || sensorReadResult.IsInputActive(PickerInputs.Sensor3) || sensorReadResult.IsInputActive(PickerInputs.Sensor4))
                {
                    sensorReadResult.Log(LogEntryType.Error);
                    if (num2 == 1 && ControllerConfiguration.Instance.TrackPushOutFailures)
                    {
                        vendItemResult.Presented = false;
                        LogHelper.Instance.WithContext(false, LogEntryType.Error, "[VendItemInPicker] After 2 pushes, the disk has not cleared sensor 4.");
                        vendItemResult.Status = ErrorCodes.PickerFull;
                        return vendItemResult;
                    }
                    ++num2;
                    LogHelper.Instance.WithContext(false, LogEntryType.Error, "[VendItemInPicker] Disc was pushed to the drum - push it back out.");
                    int num3 = (int)this.Controller.TrackCycle();
                    int num4 = (int)this.ControllerService.PushOut();
                    service.Wait(ms);
                }
                else
                {
                    num2 = 0;
                    if (sensorReadResult.IsInputActive(PickerInputs.Sensor5))
                    {
                        if (LogHelper.Instance.IsLevelEnabled(LogEntryType.Debug))
                            LogHelper.Instance.WithContext(false, LogEntryType.Info, "[VendItemInPicker] The item is still blocking sensor 5.");
                        service.Wait(ms);
                    }
                    else
                    {
                        if (!sensorReadResult.IsInputActive(PickerInputs.Sensor6))
                        {
                            vendItemResult.Status = ErrorCodes.PickerEmpty;
                            return vendItemResult;
                        }
                        service.Wait(ms);
                    }
                }
            }
            IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
            vendItemResult.Status = sensorReadResult1.Success ? (sensorReadResult1.IsFull ? ErrorCodes.PickerFull : ErrorCodes.PickerEmpty) : ErrorCodes.SensorReadError;
            return vendItemResult;
        }
    }
}
