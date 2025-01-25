using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework.Operations
{
    internal sealed class PushOutDiskOperation : AbstractOperation<ErrorCodes>
    {
        private readonly IControllerService Service;

        public override ErrorCodes Execute()
        {
            if (this.CloseTrackChecked() != ErrorCodes.Success)
                return ErrorCodes.TrackCloseTimeout;
            for (int index = 0; index < 3; ++index)
            {
                IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                if (!sensorReadResult.Success)
                    return ErrorCodes.SensorReadError;
                if (!sensorReadResult.IsFull)
                    return ErrorCodes.Success;
                if (sensorReadResult.IsInputActive(PickerInputs.Sensor4))
                {
                    this.Controller.StartRollerOut();
                    int num = (int)this.WaitSensor(PickerInputs.Sensor4, InputState.Inactive);
                    if (ControllerConfiguration.Instance.PushOutSleepTime2 > 0)
                        this.RuntimeService.Wait(ControllerConfiguration.Instance.PushOutSleepTime2);
                    this.Controller.StopRoller();
                }
                else
                {
                    if (!sensorReadResult.IsInputActive(PickerInputs.Sensor1) && !sensorReadResult.IsInputActive(PickerInputs.Sensor2) && !sensorReadResult.IsInputActive(PickerInputs.Sensor3))
                        return ErrorCodes.Success;
                    if (!this.Controller.RollerToPosition(RollerPosition.Position4).Success)
                    {
                        this.Controller.LogPickerSensorState(LogEntryType.Error);
                    }
                    else
                    {
                        this.RuntimeService.SpinWait(50);
                        this.Controller.StartRollerOut();
                        int num = (int)this.WaitSensor(PickerInputs.Sensor4, InputState.Inactive);
                        this.Controller.StopRoller();
                    }
                }
            }
            IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
            if (!sensorReadResult1.Success)
                return ErrorCodes.SensorReadError;
            return !sensorReadResult1.IsInputActive(PickerInputs.Sensor1) && !sensorReadResult1.IsInputActive(PickerInputs.Sensor2) && !sensorReadResult1.IsInputActive(PickerInputs.Sensor3) && !sensorReadResult1.IsInputActive(PickerInputs.Sensor4) ? ErrorCodes.Success : ErrorCodes.PickerFull;
        }

        internal PushOutDiskOperation(IControllerService cs) => this.Service = cs;
    }
}
