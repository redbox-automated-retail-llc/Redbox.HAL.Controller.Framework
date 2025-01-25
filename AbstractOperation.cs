using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Timers;
using System;

namespace Redbox.HAL.Controller.Framework
{
    public abstract class AbstractOperation<T> : IDisposable
    {
        protected readonly IInventoryService InventoryService;
        protected readonly IRuntimeService RuntimeService;
        protected readonly IControlSystem Controller;
        protected readonly IMotionControlService MotionService;
        protected readonly IDecksService DeckService;
        protected readonly IPersistentCounterService PersistentCounterService;
        protected readonly IControllerService ControllerService;
        private bool Disposed;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize((object)this);
        }

        public abstract T Execute();

        protected bool WaitToClear() => this.WaitToClear(8000);

        protected bool WaitToClear(int timeout)
        {
            int sensorPauseDelay = ControllerConfiguration.Instance.ClearSensorPauseDelay;
            using (ExecutionTimer executionTimer = new ExecutionTimer())
            {
                do
                {
                    this.RuntimeService.SpinWait(sensorPauseDelay);
                    IPickerSensorReadResult readResult = this.Controller.ReadPickerSensors();
                    if (!readResult.Success)
                        return true;
                    if (this.ClearPickerBlockedCount(readResult) == 0)
                        return false;
                }
                while (executionTimer.ElapsedMilliseconds <= (long)timeout);
                return true;
            }
        }

        protected bool ClearDiskFromPicker()
        {
            IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
            int sensorPauseDelay = ControllerConfiguration.Instance.ClearSensorPauseDelay;
            bool flag1 = this.WaitToClear(8000);
            if (!flag1)
            {
                service.SpinWait(sensorPauseDelay);
                bool flag2 = true;
                for (int index = 0; index < 3 & flag2; ++index)
                {
                    if (flag2)
                    {
                        if (index > 0)
                            LogHelper.Instance.Log("2-5 didn't timeout; sensors show something in picker.", LogEntryType.Info);
                        this.WaitToClear(3000);
                    }
                    IPickerSensorReadResult readResult = this.Controller.ReadPickerSensors();
                    if (!readResult.Success)
                        return true;
                    flag2 = this.ClearPickerBlockedCount(readResult) > 0;
                    service.SpinWait(sensorPauseDelay);
                }
                IPickerSensorReadResult readResult1 = this.Controller.ReadPickerSensors();
                if (!readResult1.Success)
                    return true;
                flag1 = this.ClearPickerBlockedCount(readResult1) > 0;
            }
            return flag1;
        }

        protected ErrorCodes WaitSensor(PickerInputs sensor, InputState state)
        {
            return this.WaitSensor(sensor, state, 4000);
        }

        protected ErrorCodes WaitSensor(PickerInputs sensor, InputState state, int timeout)
        {
            int waitSensorPauseTime = ControllerConfiguration.Instance.WaitSensorPauseTime;
            ErrorCodes errorCodes = ErrorCodes.Success;
            using (ExecutionTimer executionTimer = new ExecutionTimer())
            {
                do
                {
                    this.RuntimeService.SpinWait(waitSensorPauseTime);
                    IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                    if (!sensorReadResult.Success)
                    {
                        errorCodes = ErrorCodes.SensorReadError;
                        goto label_9;
                    }
                    else if (sensorReadResult.IsInState(sensor, state))
                        goto label_9;
                }
                while (executionTimer.ElapsedMilliseconds <= (long)timeout);
                errorCodes = ErrorCodes.Timeout;
            }
        label_9:
            if (errorCodes != ErrorCodes.Success && ErrorCodes.Timeout != errorCodes)
                LogHelper.Instance.WithContext(LogEntryType.Error, "WaitSensor returned error {0}.", (object)errorCodes.ToString().ToUpper());
            return errorCodes;
        }

        protected ErrorCodes SettleDiskInSlot()
        {
            try
            {
                if (!this.Controller.SetFinger(GripperFingerState.Open).Success)
                    return ErrorCodes.GripperOpenTimeout;
                if (this.Controller.ExtendArm().TimedOut)
                {
                    this.Controller.RetractArm();
                    this.Controller.SetFinger(GripperFingerState.Closed);
                    this.Controller.SetFinger(GripperFingerState.Open);
                    if (!this.Controller.ExtendArm().Success)
                    {
                        this.Controller.RetractArm();
                        return ErrorCodes.GripperExtendTimeout;
                    }
                }
                if (!this.Controller.SetFinger(GripperFingerState.Closed).Success)
                    return ErrorCodes.GripperCloseTimeout;
                return !this.Controller.SetFinger(GripperFingerState.Open).Success ? ErrorCodes.GripperOpenTimeout : (this.Controller.RetractArm().Success ? ErrorCodes.Success : ErrorCodes.GripperRetractTimeout);
            }
            finally
            {
                this.Controller.SetFinger(GripperFingerState.Open);
                this.Controller.RetractArm();
            }
        }

        protected ErrorCodes PullFrom(ILocation location)
        {
            return this.PullFrom(location, ControllerConfiguration.Instance.NumberOfPulls);
        }

        protected ErrorCodes PullFrom(ILocation location, int pulls)
        {
            GripperFingerState state = !location.IsWide ? GripperFingerState.Rent : GripperFingerState.Open;
            for (int index = 0; index < pulls; ++index)
            {
                if (!this.Controller.SetFinger(state).Success)
                    return state != GripperFingerState.Open ? ErrorCodes.GripperRentTimeout : ErrorCodes.GripperOpenTimeout;
                if (this.DeckService.GetByNumber(this.MotionService.CurrentLocation.Deck).IsQlm)
                {
                    int qlmExtendTime = ControllerConfiguration.Instance.QlmExtendTime;
                    if (ControllerConfiguration.Instance.QlmTimedExtend)
                        this.Controller.TimedExtend(qlmExtendTime);
                    else
                        this.Controller.ExtendArm(ControllerConfiguration.Instance.QlmExtendTime);
                }
                else if (this.Controller.ExtendArm().TimedOut)
                {
                    this.Controller.RetractArm();
                    this.Controller.SetFinger(GripperFingerState.Closed);
                    this.Controller.SetFinger(state);
                    if (!this.Controller.ExtendArm().Success)
                    {
                        this.Controller.RetractArm();
                        return ErrorCodes.GripperExtendTimeout;
                    }
                }
                if (!this.Controller.SetFinger(GripperFingerState.Closed).Success)
                    return ErrorCodes.GripperCloseTimeout;
                if (!this.Controller.RetractArm().Success)
                    return ErrorCodes.GripperRetractTimeout;
            }
            return !this.Controller.SetFinger(GripperFingerState.Rent).Success ? ErrorCodes.GripperRentTimeout : ErrorCodes.Success;
        }

        protected ErrorCodes PushIntoSlot()
        {
            ErrorCodes errorCodes1 = this.PushWithArm(ControllerConfiguration.Instance.RollInExtendTime);
            if (errorCodes1 != ErrorCodes.Success)
                return errorCodes1;
            ErrorCodes errorCodes2 = this.PushWithArm(ControllerConfiguration.Instance.PushTime);
            if (errorCodes2 != ErrorCodes.Success)
                return errorCodes2;
            if (ControllerConfiguration.Instance.AdditionalPutPush)
            {
                int num = (int)this.PushWithArm(ControllerConfiguration.Instance.PushTime);
            }
            return ErrorCodes.Success;
        }

        protected int ClearPickerBlockedCount(IPickerSensorReadResult readResult)
        {
            int num = 0;
            if (readResult.IsInputActive(PickerInputs.Sensor2))
                ++num;
            if (readResult.IsInputActive(PickerInputs.Sensor3))
                ++num;
            if (readResult.IsInputActive(PickerInputs.Sensor4))
                ++num;
            if (readResult.IsInputActive(PickerInputs.Sensor5))
                ++num;
            return num;
        }

        protected ErrorCodes OpenDoorChecked()
        {
            return VendDoorState.Rent != this.Controller.VendDoorState && !this.Controller.VendDoorRent().Success ? ErrorCodes.VendDoorRentTimeout : ErrorCodes.Success;
        }

        protected ErrorCodes CloseTrackChecked()
        {
            return this.Controller.TrackState != TrackState.Closed && !this.Controller.TrackClose().Success ? ErrorCodes.TrackCloseTimeout : ErrorCodes.Success;
        }

        protected virtual void OnDispose()
        {
        }

        protected AbstractOperation()
        {
            this.DeckService = ServiceLocator.Instance.GetService<IDecksService>();
            this.MotionService = ServiceLocator.Instance.GetService<IMotionControlService>();
            this.Controller = ServiceLocator.Instance.GetService<IControlSystem>();
            this.RuntimeService = ServiceLocator.Instance.GetService<IRuntimeService>();
            this.InventoryService = ServiceLocator.Instance.GetService<IInventoryService>();
            this.PersistentCounterService = ServiceLocator.Instance.GetService<IPersistentCounterService>();
            this.ControllerService = ServiceLocator.Instance.GetService<IControllerService>();
        }

        private ErrorCodes PushWithArm(int timeout)
        {
            if (!this.Controller.SetFinger(GripperFingerState.Closed).Success)
                return ErrorCodes.GripperCloseTimeout;
            this.Controller.TimedExtend(timeout);
            if (!this.Controller.SetFinger(GripperFingerState.Rent).Success)
                return ErrorCodes.GripperRentTimeout;
            return this.Controller.RetractArm().Success ? ErrorCodes.Success : ErrorCodes.GripperRetractTimeout;
        }

        private void Dispose(bool disposing)
        {
            if (this.Disposed)
                return;
            this.Disposed = true;
            if (!disposing)
                return;
            this.OnDispose();
        }
    }
}
