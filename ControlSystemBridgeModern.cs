using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Threading;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ControlSystemBridgeModern :
      IConfigurationObserver,
      IControlSystemService,
      IControlSystem
    {
        private AbstractRedboxControlSystem Implementor;
        private readonly IPersistentCounterService PersistentCounterService;
        private readonly IRuntimeService RuntimeService;
        private readonly List<IControlSystemObserver> Observers = new List<IControlSystemObserver>();
        private readonly ReaderWriterLockSlim ObserverLock = new ReaderWriterLockSlim();

        public void NotifyConfigurationChangeStart()
        {
        }

        public void NotifyConfigurationChangeEnd()
        {
        }

        public void NotifyConfigurationLoaded()
        {
            LogHelper.Instance.Log("[ControlSystemBridge] Configuration load.");
            FmeControlSystem instance = new FmeControlSystem(this.RuntimeService);
            this.Implementor = (AbstractRedboxControlSystem)instance;
            ServiceLocator.Instance.AddService<ICoreCommandExecutor>((object)instance);
        }

        public void AddHandler(IControlSystemObserver observer)
        {
            using (new WithWriteLock(this.ObserverLock))
            {
                if (this.Observers.Contains(observer))
                    return;
                this.Observers.Add(observer);
            }
        }

        public void RemoveHandler(IControlSystemObserver observer)
        {
            using (new WithWriteLock(this.ObserverLock))
                this.Observers.Remove(observer);
        }

        public bool Restart()
        {
            if (!this.Shutdown())
                return false;
            this.RuntimeService.Wait(1200);
            return this.Initialize().Success;
        }

        public IControlResponse Initialize()
        {
            CoreResponse coreResponse = this.Implementor.Initialize();
            this.IsInitialized = coreResponse.Success;
            if (coreResponse.Success)
            {
                using (new WithReadLock(this.ObserverLock))
                    this.Observers.ForEach((Action<IControlSystemObserver>)(each => each.OnSystemInitialize(ErrorCodes.Success)));
                this.VendDoorState = this.Implementor.ReadVendDoorState();
            }
            return (IControlResponse)coreResponse;
        }

        public bool Shutdown()
        {
            using (new WithReadLock(this.ObserverLock))
                this.Observers.ForEach((Action<IControlSystemObserver>)(observer => observer.OnSystemShutdown()));
            this.IsInitialized = false;
            return this.Implementor.Shutdown();
        }

        public IControlResponse SetAudio(AudioChannelState newState)
        {
            return (IControlResponse)this.Implementor.SetAudio(newState);
        }

        public IControlResponse ToggleRingLight(bool on, int? sleepAfter)
        {
            CoreResponse coreResponse = this.Implementor.SetRinglight(on);
            if (!coreResponse.Success)
                return (IControlResponse)coreResponse;
            if (!sleepAfter.HasValue)
                return (IControlResponse)coreResponse;
            this.RuntimeService.Wait(sleepAfter.Value);
            return (IControlResponse)coreResponse;
        }

        public IControlResponse VendDoorRent()
        {
            this.VendDoorState = VendDoorState.Unknown;
            CoreResponse coreResponse = this.Implementor.SetVendDoor(VendDoorState.Rent);
            if (!coreResponse.Success)
            {
                this.PersistentCounterService.Increment(TimeoutCounters.VendDoorRent);
                return (IControlResponse)coreResponse;
            }
            this.VendDoorState = VendDoorState.Rent;
            return (IControlResponse)coreResponse;
        }

        public IControlResponse VendDoorClose()
        {
            this.VendDoorState = VendDoorState.Unknown;
            CoreResponse coreResponse = this.Implementor.SetVendDoor(VendDoorState.Closed);
            if (!coreResponse.Success)
            {
                this.PersistentCounterService.Increment(TimeoutCounters.VendDoorClose);
                return (IControlResponse)coreResponse;
            }
            this.VendDoorState = VendDoorState.Closed;
            return (IControlResponse)coreResponse;
        }

        public VendDoorState ReadVendDoorPosition() => this.Implementor.ReadVendDoorState();

        public IControlResponse TrackOpen()
        {
            this.TrackState = TrackState.Unknown;
            CoreResponse coreResponse = this.Implementor.SetTrack(TrackState.Open);
            if (coreResponse.Success)
            {
                this.TrackState = TrackState.Open;
                return (IControlResponse)coreResponse;
            }
            this.PersistentCounterService.Increment(TimeoutCounters.TrackOpen);
            return (IControlResponse)coreResponse;
        }

        public IControlResponse TrackClose()
        {
            this.TrackState = TrackState.Unknown;
            CoreResponse coreResponse = this.Implementor.SetTrack(TrackState.Closed);
            if (coreResponse.Success)
            {
                this.TrackState = TrackState.Closed;
                return (IControlResponse)coreResponse;
            }
            this.PersistentCounterService.Increment(TimeoutCounters.TrackClose);
            return (IControlResponse)coreResponse;
        }

        public ErrorCodes TrackCycle()
        {
            if (!this.TrackOpen().Success)
                return ErrorCodes.TrackOpenTimeout;
            return !this.TrackClose().Success ? ErrorCodes.TrackCloseTimeout : ErrorCodes.Success;
        }

        public void TimedExtend() => this.TimedExtend(ControllerConfiguration.Instance.PushTime);

        public void TimedExtend(int timeout) => this.Implementor.TimedArmExtend(timeout);

        public IControlResponse ExtendArm()
        {
            int timeout = ControllerConfiguration.Instance.GripperArmExtendRetractTimeout;
            IDecksService service = ServiceLocator.Instance.GetService<IDecksService>();
            ILocation currentLocation = ServiceLocator.Instance.GetService<IMotionControlService>().CurrentLocation;
            bool flag = false;
            if (currentLocation != null)
                flag = service.GetByNumber(currentLocation.Deck).IsQlm;
            if (flag)
                timeout = ControllerConfiguration.Instance.QlmExtendTime;
            return this.ExtendArm(timeout);
        }

        public IControlResponse ExtendArm(int timeout)
        {
            CoreResponse coreResponse = this.Implementor.ExtendArm(timeout);
            if (coreResponse.Success)
                return (IControlResponse)coreResponse;
            this.PersistentCounterService.Increment(TimeoutCounters.GripperExtend);
            return (IControlResponse)coreResponse;
        }

        public IControlResponse RetractArm()
        {
            CoreResponse coreResponse = this.Implementor.RetractArm(ControllerConfiguration.Instance.GripperArmExtendRetractTimeout);
            if (coreResponse.Success)
                return (IControlResponse)coreResponse;
            this.PersistentCounterService.Increment(TimeoutCounters.GripperRetract);
            return (IControlResponse)coreResponse;
        }

        public IControlResponse SetFinger(GripperFingerState state)
        {
            CoreResponse coreResponse = this.Implementor.SetFinger(state);
            if (coreResponse.Success)
                return (IControlResponse)coreResponse;
            IPersistentCounterService service = ServiceLocator.Instance.GetService<IPersistentCounterService>();
            if (state == GripperFingerState.Closed)
            {
                service.Increment(TimeoutCounters.FingerClose);
                return (IControlResponse)coreResponse;
            }
            if (state == GripperFingerState.Open)
            {
                service.Increment(TimeoutCounters.FingerOpen);
                return (IControlResponse)coreResponse;
            }
            service.Increment(TimeoutCounters.FingerRent);
            return (IControlResponse)coreResponse;
        }

        public ErrorCodes Center(CenterDiskMethod method)
        {
            ErrorCodes errorCodes = ErrorCodes.Success;
            if (method == CenterDiskMethod.None)
                return errorCodes;
            IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
            int milliseconds = 250;
            RollerPosition position1 = CenterDiskMethod.DrumAndBack == method ? RollerPosition.Position1 : RollerPosition.Position6;
            if (!this.RollerToPosition(position1, ControllerConfiguration.Instance.DefaultRollSensorTimeout, false).Success)
            {
                errorCodes = RollerPosition.Position1 == position1 ? ErrorCodes.RollerToPos1Timeout : ErrorCodes.RollerToPos6Timeout;
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "Center disk: Roller {0} timed out.", (object)position1);
            }
            service.SpinWait(milliseconds);
            RollerPosition position2 = CenterDiskMethod.DrumAndBack == method ? RollerPosition.Position5 : RollerPosition.Position3;
            if (!this.RollerToPosition(position2, ControllerConfiguration.Instance.DefaultRollSensorTimeout, false).Success)
            {
                errorCodes = RollerPosition.Position5 == position2 ? ErrorCodes.RollerToPos5Timeout : ErrorCodes.RollerToPos3Timeout;
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "Center disk: Roller {0} timed out.", (object)position2);
            }
            service.SpinWait(milliseconds);
            int num = (int)this.TrackCycle();
            return errorCodes;
        }

        public IBoardVersionResponse GetBoardVersion(ControlBoards board)
        {
            return (IBoardVersionResponse)this.Implementor.GetBoardVersion(board);
        }

        public IControlSystemRevision GetRevision() => this.Implementor.GetRevision();

        public IReadInputsResult<PickerInputs> ReadPickerInputs()
        {
            ReadPickerInputsResult pickerInputsResult = this.Implementor.ReadPickerInputs();
            if (!pickerInputsResult.Success)
                LogHelper.Instance.WithContext(LogEntryType.Error, "Read Picker inputs failed with error {0}", (object)pickerInputsResult.Error);
            return (IReadInputsResult<PickerInputs>)pickerInputsResult;
        }

        public IReadInputsResult<AuxInputs> ReadAuxInputs()
        {
            ReadAuxInputsResult readAuxInputsResult = this.Implementor.ReadAuxInputs();
            if (!readAuxInputsResult.Success)
                LogHelper.Instance.WithContext(LogEntryType.Error, "Read AUX inputs failed with error {0}", (object)readAuxInputsResult.Error);
            return (IReadInputsResult<AuxInputs>)readAuxInputsResult;
        }

        public void LogPickerSensorState() => this.LogPickerSensorState(LogEntryType.Info);

        public void LogPickerSensorState(LogEntryType type) => this.ReadPickerSensors().Log(type);

        public void LogInputs(ControlBoards board) => this.LogInputs(board, LogEntryType.Info);

        public void LogInputs(ControlBoards board, LogEntryType type)
        {
            if (ControlBoards.Picker == board)
                this.Implementor.ReadPickerInputs().Log(type);
            else
                this.Implementor.ReadAuxInputs().Log(type);
        }

        public IControlResponse SetSensors(bool on)
        {
            return (IControlResponse)this.Implementor.SetPickerSensors(on);
        }

        public IPickerSensorReadResult ReadPickerSensors() => this.ReadPickerSensors(true);

        public IPickerSensorReadResult ReadPickerSensors(bool closeTrack)
        {
            if (closeTrack && TrackState.Closed != this.TrackState && !this.TrackClose().Success)
                return (IPickerSensorReadResult)new PickerSensorReadResult(ErrorCodes.TrackCloseTimeout);
            try
            {
                CoreResponse coreResponse = this.Implementor.SetPickerSensors(true);
                if (!coreResponse.Success)
                    return (IPickerSensorReadResult)new PickerSensorReadResult(coreResponse.Error);
                int pickerSensorSpinTime = ControllerConfiguration.Instance.PickerSensorSpinTime;
                this.RuntimeService.SpinWait(pickerSensorSpinTime);
                ReadPickerInputsResult result = this.Implementor.ReadPickerInputs();
                this.RuntimeService.SpinWait(pickerSensorSpinTime);
                return (IPickerSensorReadResult)new PickerSensorReadResult(result);
            }
            finally
            {
                this.Implementor.SetPickerSensors(false);
            }
        }

        public IControlResponse StartRollerIn() => this.SetRollerState(RollerState.In);

        public IControlResponse StartRollerOut() => this.SetRollerState(RollerState.Out);

        public IControlResponse StopRoller() => this.SetRollerState(RollerState.Stop);

        public IControlResponse SetRollerState(RollerState state)
        {
            return (IControlResponse)this.Implementor.SetRoller(state);
        }

        public IControlResponse RollerToPosition(RollerPosition position)
        {
            return this.RollerToPosition(position, ControllerConfiguration.Instance.DefaultRollSensorTimeout);
        }

        public IControlResponse RollerToPosition(RollerPosition position, int opTimeout)
        {
            return this.RollerToPosition(position, opTimeout, true);
        }

        public IControlResponse RollerToPosition(
          RollerPosition position,
          int opTimeout,
          bool logSensors)
        {
            CoreResponse position1 = this.Implementor.RollerToPosition(position, opTimeout);
            if (position1.Success)
                return (IControlResponse)position1;
            if (logSensors && !position1.CommError)
            {
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "Roller to {0} timed out.", (object)position.ToString());
                this.LogPickerSensorState(LogEntryType.Error);
            }
            return (IControlResponse)position1;
        }

        public QlmStatus GetQlmStatus() => this.Implementor.GetQlmStatus();

        public ErrorCodes EngageQlm(IFormattedLog log) => this.EngageQlm(true, log);

        public ErrorCodes EngageQlm(bool home, IFormattedLog log)
        {
            return this.OnLifterOperation(QlmOperation.Engage, log, home);
        }

        public ErrorCodes DisengageQlm(IFormattedLog log) => this.DisengageQlm(true, log);

        public ErrorCodes DisengageQlm(bool home, IFormattedLog log)
        {
            return this.OnLifterOperation(QlmOperation.Disengage, log, home);
        }

        public IControlResponse LockQlmDoor()
        {
            return (IControlResponse)this.Implementor.OnQlm(QlmOperation.LockDoor);
        }

        public IControlResponse UnlockQlmDoor()
        {
            return (IControlResponse)this.Implementor.OnQlm(QlmOperation.UnlockDoor);
        }

        public IControlResponse DropQlm()
        {
            return (IControlResponse)this.Implementor.OnQlm(QlmOperation.Drop);
        }

        public IControlResponse LiftQlm()
        {
            return (IControlResponse)this.Implementor.OnQlm(QlmOperation.Lift);
        }

        public IControlResponse HaltQlmLifter()
        {
            return (IControlResponse)this.Implementor.OnQlm(QlmOperation.Halt);
        }

        public bool IsInitialized { get; private set; }

        public VendDoorState VendDoorState { get; private set; }

        public TrackState TrackState { get; private set; }

        internal ControlSystemBridgeModern(IRuntimeService rts, IPersistentCounterService pcs)
        {
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
            this.IsInitialized = false;
            this.RuntimeService = rts;
            this.PersistentCounterService = pcs;
        }

        private ErrorCodes OnLifterOperation(QlmOperation operation, IFormattedLog log, bool home)
        {
            if (ControllerConfiguration.Instance.IsVMZMachine)
                return ErrorCodes.Timeout;
            if (home && ServiceLocator.Instance.GetService<IMotionControlService>().HomeAxis(Axis.X) != ErrorCodes.Success)
                return ErrorCodes.HomeXTimeout;
            CoreResponse coreResponse = this.Implementor.OnQlm(operation);
            ErrorCodes errorCodes = !coreResponse.Success ? ErrorCodes.Timeout : ErrorCodes.Success;
            string msg = string.Format("{0} returned status {1}.", (object)operation.ToString().ToUpper(), (object)errorCodes.ToString().ToUpper());
            log.WriteFormatted(msg);
            if (!coreResponse.Success)
                this.PersistentCounterService.Increment(QlmOperation.Engage == operation ? TimeoutCounters.QlmEngage : TimeoutCounters.QlmDisengage);
            return errorCodes;
        }
    }
}
