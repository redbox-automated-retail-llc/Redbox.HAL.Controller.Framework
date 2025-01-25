using Redbox.HAL.Component.Model;
using System;
using System.IO.Ports;
using System.Text;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class FmeControlSystem : AbstractRedboxControlSystem, ICoreCommandExecutor
    {
        private readonly ControlBoards[] ControllerBoards = new ControlBoards[3]
        {
      ControlBoards.Serial,
      ControlBoards.Picker,
      ControlBoards.Aux
        };
        private readonly ICommPort Port;
        private const int LifterTimeout = 120000;

        public CoreResponse ExecuteControllerCommand(CommandType type)
        {
            return this.SendCommand(CoreCommand.Create(type, new int?(), this.Port));
        }

        public CoreResponse ExecuteControllerCommand(CommandType type, int? timeout)
        {
            return this.SendCommand(CoreCommand.Create(type, timeout, this.Port));
        }

        internal override CoreResponse Initialize()
        {
            CoreResponse coreResponse = this.ExecuteControllerCommand(CommandType.Reset);
            this.RuntimeService.Wait(2000);
            this.SetAudio(AudioChannelState.Off);
            return coreResponse;
        }

        internal override bool Shutdown() => this.Port.Close();

        internal override CoreResponse SetAudio(AudioChannelState newState)
        {
            return this.ExecuteControllerCommand(AudioChannelState.On == newState ? CommandType.AudioOn : CommandType.AudioOff);
        }

        internal override CoreResponse SetRinglight(bool on)
        {
            return this.ExecuteControllerCommand(on ? CommandType.RinglightOn : CommandType.RinglightOff);
        }

        internal override CoreResponse SetPickerSensors(bool on)
        {
            return this.ExecuteControllerCommand(on ? CommandType.SensorBarOn : CommandType.SensorBarOff);
        }

        internal override CoreResponse SetFinger(GripperFingerState state)
        {
            CommandType command = CommandType.GripperClose;
            switch (state)
            {
                case GripperFingerState.Closed:
                    command = CommandType.GripperClose;
                    break;
                case GripperFingerState.Open:
                    if (ControllerConfiguration.Instance.EnableSecureDiskValidator)
                        return CoreResponse.TimedOutResponse;
                    command = CommandType.GripperOpen;
                    break;
                case GripperFingerState.Rent:
                    command = !ControllerConfiguration.Instance.EnableSecureDiskValidator ? CommandType.GripperRent : CommandType.GripperOpen;
                    break;
            }
            CoreResponse response = this.OnRetryable(command, 2, 5000);
            this.LogError(response, string.Format("RetryableFingerFunction {0}", (object)state), ControlBoards.Picker);
            return response;
        }

        internal override CoreResponse SetRoller(RollerState state)
        {
            CommandType type = CommandType.RollerStop;
            switch (state)
            {
                case RollerState.None:
                    throw new ArgumentException("Cannot be none");
                case RollerState.In:
                    type = CommandType.RollerIn;
                    break;
                case RollerState.Out:
                    type = CommandType.RollerOut;
                    break;
                case RollerState.Stop:
                    type = CommandType.RollerStop;
                    break;
            }
            CoreResponse response = this.ExecuteControllerCommand(type);
            this.LogError(response, string.Format("Roller{0}", (object)state), ControlBoards.Picker);
            return response;
        }

        internal override CoreResponse RollerToPosition(RollerPosition position, int opTimeout)
        {
            CommandType type = CommandType.RollerToPos1;
            switch (position)
            {
                case RollerPosition.Position1:
                    type = CommandType.RollerToPos1;
                    break;
                case RollerPosition.Position2:
                    type = CommandType.RollerToPos2;
                    break;
                case RollerPosition.Position3:
                    type = CommandType.RollerToPos3;
                    break;
                case RollerPosition.Position4:
                    type = CommandType.RollerToPos4;
                    break;
                case RollerPosition.Position5:
                    type = CommandType.RollerToPos5;
                    break;
                case RollerPosition.Position6:
                    type = CommandType.RollerToPos6;
                    break;
            }
            return this.ExecuteControllerCommand(type, new int?(opTimeout));
        }

        internal override CoreResponse TimedArmExtend(int timeout)
        {
            try
            {
                LogHelper.Instance.Log(LogEntryType.Debug, "[FmeControlSystem] ExtendGripperArmForTime {0} ms", (object)timeout);
                CoreResponse coreResponse = this.ExecuteControllerCommand(CommandType.ExtendGripperArmForTime);
                this.RuntimeService.Wait(timeout);
                return coreResponse;
            }
            finally
            {
                this.ExecuteControllerCommand(CommandType.GripperExtendHalt);
            }
        }

        internal override CoreResponse ExtendArm(int timeout)
        {
            CoreResponse response = this.ExecuteControllerCommand(CommandType.GripperExtend, new int?(timeout));
            this.LogError(response, "SetGripperArm( Extend )", ControlBoards.Picker);
            if (response.TimedOut)
            {
                ReadPickerInputsResult pickerInputsResult = this.ReadPickerInputs();
                if (pickerInputsResult.Success && pickerInputsResult.IsInputActive(PickerInputs.Extend))
                {
                    LogHelper.Instance.WithContext("GripperExtend status returned timeout; however, picker sensors read shows the forward sensor reached.");
                    pickerInputsResult.Log();
                    response.Error = ErrorCodes.Success;
                }
            }
            return response;
        }

        internal override CoreResponse RetractArm(int timeout)
        {
            CoreResponse response = this.OnRetryable(CommandType.GripperRetract, 2, timeout);
            this.LogError(response, "SetGripperArm( RETRACT )", ControlBoards.Picker);
            if (response.TimedOut)
            {
                ReadPickerInputsResult pickerInputsResult = this.ReadPickerInputs();
                if (pickerInputsResult.Success && pickerInputsResult.IsInputActive(PickerInputs.Retract))
                {
                    LogHelper.Instance.WithContext("GripperRetract status returned timeout; however, picker sensors read shows the sensor tripped.");
                    pickerInputsResult.Log();
                }
            }
            return response;
        }

        internal override CoreResponse SetTrack(TrackState state)
        {
            CoreResponse response = this.OnRetryable(state == TrackState.Open ? CommandType.TrackOpen : CommandType.TrackClose, 2, 5000);
            this.LogError(response, string.Format("Track {0}", (object)state.ToString()), ControlBoards.Picker);
            return response;
        }

        internal override CoreResponse SetVendDoor(VendDoorState state)
        {
            int timeout = 5500;
            CoreResponse response = VendDoorState.Rent != state ? this.OnCloseVendDoor(timeout) : this.OnRetryable(CommandType.VendDoorRent, 1, timeout);
            this.LogError(response, string.Format("VendDoor {0}", (object)state), ControlBoards.Aux);
            return response;
        }

        internal override VendDoorState ReadVendDoorState()
        {
            ReadAuxInputsResult readAuxInputsResult = this.ReadAuxInputs();
            if (!readAuxInputsResult.Success)
            {
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "Read AUX sensors returned error {0}", (object)readAuxInputsResult.Error.ToString().ToUpper());
                return VendDoorState.Unknown;
            }
            if (readAuxInputsResult.IsInputActive(AuxInputs.VendDoorClosed))
                return VendDoorState.Closed;
            return readAuxInputsResult.IsInputActive(AuxInputs.VendDoorRent) ? VendDoorState.Rent : VendDoorState.Unknown;
        }

        internal override QlmStatus GetQlmStatus()
        {
            ReadAuxInputsResult readAuxInputsResult = this.ReadAuxInputs();
            if (!readAuxInputsResult.Success)
                return QlmStatus.AuxNotResponsive;
            if (!readAuxInputsResult.IsInputActive(AuxInputs.QlmPresence))
                return QlmStatus.Empty;
            return !readAuxInputsResult.IsInputActive(AuxInputs.QlmUp) ? QlmStatus.Disengaged : QlmStatus.Engaged;
        }

        internal override CoreResponse OnQlm(QlmOperation op)
        {
            CoreResponse response = (CoreResponse)null;
            switch (op)
            {
                case QlmOperation.None:
                    throw new ArgumentException("[FmeControlSystem] OnQlm: operation cannot be none");
                case QlmOperation.Engage:
                    response = this.ExecuteControllerCommand(CommandType.QlmEngage, new int?(120000));
                    break;
                case QlmOperation.Disengage:
                    response = this.ExecuteControllerCommand(CommandType.QlmDisengage, new int?(120000));
                    break;
                case QlmOperation.Lift:
                    response = this.ExecuteControllerCommand(CommandType.QlmLift);
                    break;
                case QlmOperation.Drop:
                    response = this.ExecuteControllerCommand(CommandType.QlmDrop);
                    break;
                case QlmOperation.Halt:
                    response = this.ExecuteControllerCommand(CommandType.QlmHalt);
                    break;
                case QlmOperation.LockDoor:
                    response = this.ExecuteControllerCommand(CommandType.QlmDoorLock);
                    break;
                case QlmOperation.UnlockDoor:
                    response = this.ExecuteControllerCommand(CommandType.QlmDoorUnlock);
                    break;
            }
            this.LogError(response, op.ToString().ToUpper(), ControlBoards.Aux);
            return response;
        }

        internal override ReadAuxInputsResult ReadAuxInputs()
        {
            return new ReadAuxInputsResult(this.ExecuteControllerCommand(CommandType.AuxSensorsRead));
        }

        internal override ReadPickerInputsResult ReadPickerInputs()
        {
            return new ReadPickerInputsResult(this.ExecuteControllerCommand(CommandType.ReadPickerInputs));
        }

        internal override BoardVersionResponse GetBoardVersion(ControlBoards board)
        {
            CommandType type = CommandType.Version101;
            switch (board)
            {
                case ControlBoards.Picker:
                    type = CommandType.Version001;
                    break;
                case ControlBoards.Aux:
                    type = CommandType.Version002;
                    break;
                case ControlBoards.Serial:
                    type = CommandType.Version101;
                    break;
            }
            return new BoardVersionResponse(board, this.ExecuteControllerCommand(type));
        }

        internal override IControlSystemRevision GetRevision()
        {
            IBoardVersionResponse[] boardVersionResponseArray = new IBoardVersionResponse[this.ControllerBoards.Length];
            int num = 0;
            for (int index = 0; index < this.ControllerBoards.Length; ++index)
            {
                boardVersionResponseArray[index] = (IBoardVersionResponse)this.GetBoardVersion(this.ControllerBoards[index]);
                if (!boardVersionResponseArray[index].ReadSuccess)
                    ++num;
            }
            return (IControlSystemRevision)new FmeControlSystem.FmeControllerRevision()
            {
                Responses = boardVersionResponseArray,
                Success = (num == 0)
            };
        }

        internal FmeControlSystem(IRuntimeService rts)
          : base(rts)
        {
            SerialPort port = new SerialPort(ControllerConfiguration.Instance.ControllerPortName, 9600, Parity.None, 8, StopBits.One);
            this.Port = ServiceLocator.Instance.GetService<IPortManagerService>().Create(port);
            this.Port.WriteTerminator = new byte[1] { (byte)13 };
            this.Port.ValidateResponse = (Predicate<IChannelResponse>)(response =>
            {
                string str = Encoding.ASCII.GetString(response.RawResponse);
                if (string.IsNullOrEmpty(str))
                    return false;
                return str.IndexOf("OK") != -1 || str.IndexOf("EER") != -1;
            });
        }

        private CoreResponse OnRetryable(CommandType command, int retryCount, int timeout)
        {
            CoreResponse coreResponse = (CoreResponse)null;
            for (int index = 0; index < retryCount; ++index)
            {
                coreResponse = this.ExecuteControllerCommand(command, new int?(timeout));
                if (coreResponse.Success || coreResponse.CommError)
                    return coreResponse;
            }
            return coreResponse;
        }

        private CoreResponse SendCommand(CoreCommand command)
        {
            if (command.Port != null)
            {
                if (command.Port.Open())
                {
                    try
                    {
                        return command.Execute();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Instance.Log(LogEntryType.Error, string.Format("[FmeControlSystem] Send command on port {0} caught an exception.", (object)command.Port.DisplayName), (object)ex);
                        return CoreResponse.CommErrorResponse;
                    }
                }
            }
            LogHelper.Instance.WithContext(LogEntryType.Error, "Unable to open control port {0}", (object)command.Port.DisplayName);
            return CoreResponse.CommErrorResponse;
        }

        private CoreResponse OnCloseVendDoor(int timeout)
        {
            CoreResponse coreResponse = (CoreResponse)null;
            for (int index = 0; index < 2; ++index)
            {
                coreResponse = this.ExecuteControllerCommand(CommandType.VendDoorClose, new int?(timeout));
                if (coreResponse.CommError)
                    return coreResponse;
                if (coreResponse.TimedOut)
                    this.OnCloseBackoff();
                else if (this.ReadVendDoorState() == VendDoorState.Closed || this.MoveToCloseSensor())
                    return coreResponse;
            }
            return coreResponse;
        }

        private bool MoveToCloseSensor()
        {
            if (!ControllerConfiguration.Instance.MoveVendDoorToAuxSensor)
                return true;
            this.OnCloseBackoff();
            CoreResponse coreResponse = this.ExecuteControllerCommand(CommandType.VendDoorClose, new int?(5000));
            if (coreResponse.CommError)
                return false;
            this.RuntimeService.SpinWait(100);
            return !coreResponse.TimedOut;
        }

        private void OnCloseBackoff()
        {
            this.RuntimeService.Wait(200);
            this.ExecuteControllerCommand(CommandType.UnknownVendDoorCloseCommand);
            this.RuntimeService.Wait(100);
            this.ExecuteControllerCommand(CommandType.VendDoorKillCommand);
            this.RuntimeService.Wait(100);
        }

        private void LogError(CoreResponse response, string command, ControlBoards board)
        {
            if (response.Success)
                return;
            LogHelper.Instance.WithContext(false, LogEntryType.Error, "{0} returned error status {1}.", (object)command, (object)response.Error.ToString().ToUpper());
            if (response.CommError)
                return;
            if (ControlBoards.Aux == board)
            {
                this.ReadAuxInputs().Log(LogEntryType.Error);
            }
            else
            {
                if (ControlBoards.Picker != board)
                    return;
                this.ReadPickerInputs().Log(LogEntryType.Error);
            }
        }

        private class FmeControllerRevision : IControlSystemRevision
        {
            public bool Success { get; internal set; }

            public IBoardVersionResponse[] Responses { get; internal set; }

            public string Revision => "A";
        }
    }
}
