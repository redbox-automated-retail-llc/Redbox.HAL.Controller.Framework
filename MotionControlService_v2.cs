using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Threading;
using System.Collections.Generic;
using System.Threading;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class MotionControlService_v2 : IConfigurationObserver, IMotionControlService
    {
        private readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();
        private readonly List<IMoveVeto> Vetoers = new List<IMoveVeto>();
        private AbstractMotionController Controller;
        private bool YAxisHomed;
        private bool XAxisHomed;

        public void NotifyConfigurationLoaded()
        {
            this.Controller = (AbstractMotionController)new ArcusMotionController2();
            this.Controller.OnConfigurationLoad();
        }

        public void NotifyConfigurationChangeStart() => this.Controller.OnConfigurationChangeStart();

        public void NotifyConfigurationChangeEnd() => this.Controller.OnConfigurationChangeEnd();

        public void AddVeto(IMoveVeto veto)
        {
            using (new WithWriteLock(this.Lock))
                this.Vetoers.Add(veto);
        }

        public void RemoveVeto(IMoveVeto veto)
        {
            using (new WithWriteLock(this.Lock))
                this.Vetoers.Remove(veto);
        }

        public bool Initialize() => this.Controller.OnStartup();

        public bool CommunicationOk() => this.Controller.CommunicationOk();

        public IControllerPosition ReadPositions() => this.Controller.ReadPositions();

        public IMotionControlLimitResponse ReadLimits() => this.Controller.ReadLimits();

        public ErrorCodes MoveAbsolute(Axis axis, int? xunits, int? yunits, bool checkSensors)
        {
            if (checkSensors)
            {
                ErrorCodes errorCodes = this.AxesInitialized();
                if (errorCodes != ErrorCodes.Success)
                    return errorCodes;
            }
            MoveTarget target = new MoveTarget()
            {
                Axis = axis,
                XCoordinate = xunits,
                YCoordinate = yunits
            };
            return checkSensors ? this.MoveWithCheck(ref target) : this.Controller.MoveToTarget(ref target);
        }

        public ErrorCodes MoveTo(int deck, int slot, MoveMode mode)
        {
            IExecutionService service = ServiceLocator.Instance.GetService<IExecutionService>();
            return this.MoveTo(deck, slot, mode, service.GetActiveContext().ContextLog);
        }

        public ErrorCodes MoveTo(int deck, int slot, MoveMode mode, IFormattedLog log)
        {
            OffsetMoveData data = new OffsetMoveData();
            return this.MoveTo(deck, slot, mode, log, ref data);
        }

        public ErrorCodes MoveTo(
          int deck,
          int slot,
          MoveMode mode,
          IFormattedLog _log,
          ref OffsetMoveData data)
        {
            IDeck byNumber = ServiceLocator.Instance.GetService<IDecksService>().GetByNumber(deck);
            if (byNumber == null)
                return ErrorCodes.DeckOutOfRange;
            if (!byNumber.IsSlotValid(slot))
                return ErrorCodes.SlotOutOfRange;
            ErrorCodes error1 = this.AxesInitialized();
            if (error1 != ErrorCodes.Success)
            {
                this.LogMoveError(deck, slot, error1);
                return error1;
            }
            int num1 = byNumber.YOffset;
            int num2 = byNumber.IsQlm ? ControllerConfiguration.Instance.QlmYOffset : 50;
            switch (mode)
            {
                case MoveMode.Put:
                    num1 += num2 * (int)ControllerConfiguration.Instance.GearY.GetStepRatio();
                    break;
                case MoveMode.Get:
                    num1 -= num2 * (int)ControllerConfiguration.Instance.GearY.GetStepRatio();
                    break;
            }
            int num3 = byNumber.GetSlotOffset(slot);
            MoveTarget moveTarget;
            int? nullable;
            if (byNumber.IsQlm)
            {
                IControllerPosition controllerPosition = this.Controller.ReadPositions();
                if (controllerPosition.ReadOk)
                {
                    int num4 = num3 - ControllerConfiguration.Instance.QlmApproachOffset;
                    moveTarget = new MoveTarget();
                    moveTarget.Axis = Axis.XY;
                    moveTarget.XCoordinate = new int?(num4);
                    moveTarget.YCoordinate = new int?(num1);
                    MoveTarget target = moveTarget;
                    switch (mode)
                    {
                        case MoveMode.None:
                        case MoveMode.Get:
                            int num5 = (int)this.MoveWithCheck(ref target);
                            break;
                        case MoveMode.Put:
                            int num6 = num3;
                            nullable = controllerPosition.XCoordinate;
                            int num7 = nullable.Value;
                            if (num6 < num7)
                            {
                                int num8 = (int)this.MoveWithCheck(ref target);
                                break;
                            }
                            break;
                    }
                }
            }
            nullable = data.XOffset;
            if (nullable.HasValue)
            {
                int num9 = num3;
                nullable = data.XOffset;
                int num10 = nullable.Value;
                num3 = num9 + num10;
            }
            nullable = data.YOffset;
            if (nullable.HasValue)
            {
                int num11 = num1;
                nullable = data.YOffset;
                int num12 = nullable.Value;
                num1 = num11 + num12;
            }
            moveTarget = new MoveTarget();
            moveTarget.Axis = Axis.XY;
            moveTarget.XCoordinate = new int?(num3);
            moveTarget.YCoordinate = new int?(num1);
            MoveTarget target1 = moveTarget;
            ErrorCodes error2 = this.MoveWithCheck(ref target1);
            switch (error2)
            {
                case ErrorCodes.ArcusNotResponsive:
                    if (this.ResetMotionControllerChecked())
                    {
                        error2 = this.MoveWithCheck(ref target1);
                        break;
                    }
                    break;
                case ErrorCodes.LowerLimitError:
                    if (!ControllerConfiguration.Instance.LowerLimitAsError)
                    {
                        error2 = ErrorCodes.Timeout;
                        break;
                    }
                    break;
            }
            this.LogMoveError(deck, slot, error2);
            if (error2 == ErrorCodes.Success)
            {
                this.AtVendDoor = false;
                this.CurrentLocation = ServiceLocator.Instance.GetService<IInventoryService>().Get(deck, slot);
            }
            return error2;
        }

        public ErrorCodes MoveTo(ILocation location, MoveMode mode)
        {
            IExecutionService service = ServiceLocator.Instance.GetService<IExecutionService>();
            return this.MoveTo(location.Deck, location.Slot, mode, service.GetActiveContext().ContextLog);
        }

        public ErrorCodes MoveTo(ILocation location, MoveMode mode, IFormattedLog log)
        {
            return this.MoveTo(location.Deck, location.Slot, mode, log);
        }

        public ErrorCodes MoveTo(
          ILocation location,
          MoveMode mode,
          IFormattedLog log,
          ref OffsetMoveData data)
        {
            return this.MoveTo(location.Deck, location.Slot, mode, log, ref data);
        }

        public ErrorCodes MoveVend(MoveMode mode, IFormattedLog writer)
        {
            ErrorCodes error1 = this.AxesInitialized();
            if (error1 != ErrorCodes.Success)
            {
                this.LogMoveVendError(error1);
                return error1;
            }
            ErrorCodes error2 = this.CheckAndReset();
            if (error2 != ErrorCodes.Success)
            {
                this.LogMoveVendError(error2);
                return error2;
            }
            ErrorCodes vend = this.Controller.MoveToVend(mode);
            if (ErrorCodes.ArcusNotResponsive == vend && this.ResetMotionControllerChecked())
                vend = this.Controller.MoveToVend(mode);
            this.LogMoveVendError(vend);
            this.AtVendDoor = vend == ErrorCodes.Success;
            return vend;
        }

        public ErrorCodes InitAxes() => this.InitAxes(false);

        public ErrorCodes InitAxes(bool fast)
        {
            if (ControllerConfiguration.Instance.RestartControllerDuringUserJobs && !this.CommunicationOk())
            {
                ErrorCodes failure = this.OnReset();
                this.InsertCorrectionStat(failure == ErrorCodes.Success);
                if (failure != ErrorCodes.Success)
                {
                    this.OnResetFailure(failure);
                    return failure;
                }
            }
            return this.OnInitAxes(fast);
        }

        public ErrorCodes HomeAxis(Axis axis) => this.HomeAxisChecked(axis);

        public ErrorCodes Reset(bool quick)
        {
            ErrorCodes failure = this.OnReset();
            if (failure == ErrorCodes.Success)
                failure = this.OnInitAxes(quick);
            if (failure != ErrorCodes.Success)
                this.OnResetFailure(failure);
            else
                LogHelper.Instance.WithContext("RESET of motion controller returned {0}", (object)failure.ToString().ToUpper());
            return failure;
        }

        public bool TestAndReset() => this.TestAndReset(true);

        public bool TestAndReset(bool quick)
        {
            return this.CommunicationOk() || this.Reset(true) == ErrorCodes.Success;
        }

        public void Shutdown()
        {
            this.XAxisHomed = this.YAxisHomed = false;
            this.Controller.OnShutdown();
        }

        public string GetPrintableLocation()
        {
            string printableLocation = "Location unknown";
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            if (service.CurrentLocation != null)
                printableLocation = service.CurrentLocation.ToString();
            else if (service.AtVendDoor)
                printableLocation = "Vend Door";
            return printableLocation;
        }

        public ILocation CurrentLocation { get; private set; }

        public bool AtVendDoor { get; private set; }

        public bool IsInitialized => this.AxesInitialized() == ErrorCodes.Success;

        internal MotionControlService_v2()
        {
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }

        private ErrorCodes MoveWithCheck(ref MoveTarget target)
        {
            ErrorCodes errorCodes = this.CheckAndReset();
            return errorCodes == ErrorCodes.Success ? this.Controller.MoveToTarget(ref target) : errorCodes;
        }

        private ErrorCodes HomeAxisChecked(Axis axis)
        {
            ErrorCodes errorCodes1 = this.CheckAndReset();
            if (errorCodes1 != ErrorCodes.Success)
                return errorCodes1;
            if (Axis.Y == axis)
                this.YAxisHomed = false;
            else if (axis == Axis.X)
                this.XAxisHomed = false;
            ErrorCodes errorCodes2 = this.Controller.HomeAxis(axis);
            if (errorCodes2 != ErrorCodes.Success)
            {
                LogHelper.Instance.WithContext(LogEntryType.Error, string.Format("HOME {0} returned an error status {1}", (object)axis.ToString().ToUpper(), (object)errorCodes2.ToString().ToUpper()));
            }
            else
            {
                switch (axis)
                {
                    case Axis.X:
                        this.XAxisHomed = true;
                        break;
                    case Axis.Y:
                        this.YAxisHomed = true;
                        break;
                }
            }
            return errorCodes2;
        }

        private ErrorCodes AxesInitialized()
        {
            if (!ControllerConfiguration.Instance.ValidateControllerHomeStatus)
                return ErrorCodes.Success;
            int num = 0;
            if (!this.XAxisHomed)
            {
                ++num;
                LogHelper.Instance.WithContext(false, LogEntryType.Info, "The X axis did not init.");
            }
            if (!this.YAxisHomed)
            {
                ++num;
                LogHelper.Instance.WithContext(false, LogEntryType.Info, "The Y axis did not init.");
            }
            return num != 0 ? ErrorCodes.MotorNotHomed : ErrorCodes.Success;
        }

        private ErrorCodes CheckAndReset()
        {
            using (new WithReadLock(this.Lock))
            {
                foreach (IMoveVeto vetoer in this.Vetoers)
                {
                    ErrorCodes errorCodes = vetoer.CanMove();
                    if (errorCodes != ErrorCodes.Success)
                    {
                        LogHelper.Instance.WithContext(false, LogEntryType.Error, "Move veto returned code {0}", (object)errorCodes.ToString());
                        return errorCodes;
                    }
                }
            }
            this.CurrentLocation = (ILocation)null;
            this.AtVendDoor = false;
            return ErrorCodes.Success;
        }

        private ErrorCodes OnInitAxes(bool fast)
        {
            using (ClearPickerFrontOperation pickerFrontOperation = new ClearPickerFrontOperation())
            {
                if (ControllerConfiguration.Instance.ClearPickerOnHome)
                    pickerFrontOperation.Execute();
                IFormattedLog contextLog = ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext().ContextLog;
                contextLog.WriteFormatted("Home the X motor.");
                int num = fast ? 1 : 2;
                for (int index = 0; index < num; ++index)
                {
                    ErrorCodes errorCodes = this.HomeAxisChecked(Axis.X);
                    if (errorCodes != ErrorCodes.Success)
                        return errorCodes;
                    if (index == 0)
                    {
                        MoveTarget target1 = new MoveTarget()
                        {
                            Axis = Axis.X,
                            XCoordinate = new int?(-200),
                            YCoordinate = new int?()
                        };
                        int target2 = (int)this.Controller.MoveToTarget(ref target1);
                    }
                }
                contextLog.WriteFormatted("Home the Y Motor.");
                return this.HomeAxisChecked(Axis.Y);
            }
        }

        private bool ResetMotionControllerChecked()
        {
            if (!ControllerConfiguration.Instance.RestartControllerDuringUserJobs)
                return false;
            IExecutionContext activeContext = ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext();
            HardwareCorrectionEventArgs e = new HardwareCorrectionEventArgs(HardwareCorrectionStatistic.Arcus);
            activeContext?.HardwareCorrectionStart(e);
            e.CorrectionOk = this.Reset(true) == ErrorCodes.Success;
            activeContext?.HardwareCorrectionEnd(e);
            this.InsertCorrectionStat(activeContext, e.CorrectionOk);
            return e.CorrectionOk;
        }

        private void InsertCorrectionStat(bool ok)
        {
            this.InsertCorrectionStat(ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext(), ok);
        }

        private void InsertCorrectionStat(IExecutionContext activeContext, bool ok)
        {
            if (activeContext == null)
                return;
            ServiceLocator.Instance.GetService<IHardwareCorrectionStatisticService>().Insert(HardwareCorrectionStatistic.Arcus, activeContext, ok);
        }

        private void LogMoveVendError(ErrorCodes error)
        {
            if (error == ErrorCodes.Success)
                return;
            LogHelper.Instance.WithContext(LogEntryType.Error, "MOVEVEND returned an error status {0}", (object)error.ToString().ToUpper());
        }

        private void LogMoveError(int deck, int slot, ErrorCodes error)
        {
            if (error == ErrorCodes.Success)
                return;
            LogHelper.Instance.WithContext(LogEntryType.Error, "MOVE Deck = {0} Slot = {1} returned an error status {2}", (object)deck, (object)slot, (object)error.ToString().ToUpper());
        }

        private ErrorCodes OnReset()
        {
            this.Shutdown();
            if (!this.Controller.OnResetDeviceDriver())
                return ErrorCodes.ArcusNotResponsive;
            this.Initialize();
            return this.Controller.CommunicationOk() ? ErrorCodes.Success : ErrorCodes.ArcusNotResponsive;
        }

        private void OnResetFailure(ErrorCodes failure)
        {
            LogHelper.Instance.WithContext(false, LogEntryType.Error, "Reset of motion controller returned error {0}", (object)failure);
        }
    }
}
