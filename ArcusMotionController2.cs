using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Timers;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ArcusMotionController2 : AbstractMotionController
    {
        private readonly List<string> MovePreamble = new List<string>();
        private readonly string[] InvalidQueryResponse = new string[1]
        {
      "INVALID QUERY RESPONSE"
        };
        private readonly IDeviceDescriptor ArcusDescriptor;
        private readonly List<string> ErrorCorrectPreamble = new List<string>();
        private readonly List<string> InitCommands = new List<string>();
        private const string HaltMotorCommand = "RSTOP";
        private const string NopCommand = "NOP";
        private bool DebugEnabled;
        private bool UseSmoothMove;
        private ICommPort Port;

        internal override void OnConfigurationLoad()
        {
            LogHelper.Instance.Log("[ArcusMotionControl] Notify configuration loaded.");
            this.ComputeMoveCommands();
            this.UseSmoothMove = ControllerConfiguration.Instance.ArcusSmoothMove;
            this.DebugEnabled = ControllerConfiguration.Instance.EnableArcusTrace;
        }

        internal override void OnConfigurationChangeStart()
        {
        }

        internal override void OnConfigurationChangeEnd()
        {
            LogHelper.Instance.Log("[ArcusMotionControl] On configuration end.");
            this.ComputeMoveCommands();
            this.UseSmoothMove = ControllerConfiguration.Instance.ArcusSmoothMove;
            this.DebugEnabled = ControllerConfiguration.Instance.EnableArcusTrace;
        }

        internal override ErrorCodes MoveToTarget(ref MoveTarget target)
        {
            ErrorCodes errorCodes = this.ClearYMotorError();
            return errorCodes != ErrorCodes.Success ? errorCodes : this.MoveAbsolute(ref target);
        }

        internal override ErrorCodes HomeAxis(Axis axis)
        {
            return axis != Axis.X ? this.HomeYAxis() : this.HomeXAxis();
        }

        internal override ErrorCodes MoveToVend(MoveMode mode)
        {
            int vendYposition = ControllerConfiguration.Instance.VendYPosition;
            if (ControllerConfiguration.Instance.VendPositionReceiveOffset != 0)
            {
                switch (mode)
                {
                    case MoveMode.Put:
                        vendYposition += ControllerConfiguration.Instance.VendPositionReceiveOffset;
                        break;
                    case MoveMode.Get:
                        vendYposition -= ControllerConfiguration.Instance.VendPositionReceiveOffset;
                        break;
                }
            }
            if (ControllerConfiguration.Instance.QueryPositionForVendMove)
            {
                IControllerPosition controllerPosition = this.ReadPositions();
                if (controllerPosition.ReadOk && controllerPosition.YCoordinate.Value == vendYposition)
                {
                    if (LogHelper.Instance.IsLevelEnabled(LogEntryType.Debug))
                        LogHelper.Instance.Log("[ArcusMotionController2] Picker currently at position Y = {0}", (object)controllerPosition.YCoordinate.Value);
                    return ErrorCodes.Success;
                }
            }
            ErrorCodes vend = this.ClearYMotorError();
            if (vend != ErrorCodes.Success)
                return vend;
            MoveTarget target = new MoveTarget()
            {
                Axis = Axis.Y,
                YCoordinate = new int?(vendYposition)
            };
            return this.MoveAbsolute(ref target);
        }

        internal override bool CommunicationOk()
        {
            string str = this.SendReciveRaw("$");
            if (str == null)
                return false;
            LogHelper.Instance.Log(LogEntryType.Debug, "Arcus comm ok: command returns {0}", (object)str);
            return str.StartsWith("OK");
        }

        internal override IControllerPosition ReadPositions()
        {
            ArcusControllerPosition controllerPosition = new ArcusControllerPosition()
            {
                XCoordinate = new int?(),
                YCoordinate = new int?()
            };
            controllerPosition.XCoordinate = this.DecodePositionResponse(Axis.X);
            controllerPosition.YCoordinate = this.DecodePositionResponse(Axis.Y);
            return (IControllerPosition)controllerPosition;
        }

        internal override bool OnShutdown() => this.Port.Close();

        internal override IMotionControlLimitResponse ReadLimits()
        {
            string queryResponse = this.SendReciveRaw("???");
            ArcusLimitResponse arcusLimitResponse = new ArcusLimitResponse(queryResponse);
            if (!arcusLimitResponse.ReadOk)
                this.WriteToLog("[ReadLimits] The response is incorrect {0}", queryResponse == null ? (object)"NONE" : (object)queryResponse);
            return (IMotionControlLimitResponse)arcusLimitResponse;
        }

        internal override bool OnResetDeviceDriver()
        {
            this.OnShutdown();
            bool flag = this.ArcusDescriptor.ResetDriver();
            LogHelper.Instance.WithContext("Arcus device reset returned {0}", (object)flag.ToString().ToUpper());
            return flag;
        }

        internal override bool OnStartup()
        {
            if (!this.Port.Open())
            {
                LogHelper.Instance.Log(LogEntryType.Error, "[ArcusMotionControl] Unable to open port {0}.", (object)this.Port.DisplayName);
                this.Port.Close();
                return false;
            }
            this.InitCommands.ForEach((Action<string>)(each => this.SendReciveRaw(each)));
            return true;
        }

        internal ArcusMotionController2()
        {
            this.ArcusDescriptor = (IDeviceDescriptor)new ArcusDeviceDescriptor(ServiceLocator.Instance.GetService<IUsbDeviceService>());
            SerialPort port = new SerialPort(ControllerConfiguration.Instance.MotionControllerPortName, 115200, Parity.None, 8, StopBits.One);
            this.Port = ServiceLocator.Instance.GetService<IPortManagerService>().Create(port);
            this.Port.WriteTerminator = new byte[2]
            {
        (byte) 13,
        (byte) 0
            };
            this.Port.WritePause = ControllerConfiguration.Instance.ArcusWritePause;
            this.Port.WriteTimeout = ControllerConfiguration.Instance.MotionControllerTimeout;
            this.Port.ValidateResponse = (Predicate<IChannelResponse>)(response => response.GetIndex((byte)4) != -1);
            this.Port.DisplayName = "Motion Control";
            this.Port.EnableDebugging = this.DebugEnabled;
            LogHelper.Instance.Log("[ArcusMotionController2] ctor");
        }

        private void ComputeMoveCommands()
        {
            LogHelper.Instance.Log("Re-computing move commands.");
            this.InitCommands.Clear();
            this.InitCommands.AddRange((IEnumerable<string>)new string[19]
            {
        "$",
        "$",
        "$",
        "RSTOP",
        "MECLEARX",
        "MECLEARY",
        "MECLEARZ",
        "MECLEARU",
        "I1=4204544",
        "I2=3136",
        "I3=" + ControllerConfiguration.Instance.GearX.GetEncoderRatio().ToString(),
        "I4=0",
        "I5=0",
        "I6=0",
        "I7=10",
        "I8=0",
        "I9=2000",
        "I10=0",
        "I11=2"
            });
            this.ErrorCorrectPreamble.Clear();
            this.ErrorCorrectPreamble.Add(string.Format("LSPD {0}", (object)((int)ControllerConfiguration.Instance.GearX.GetStepRatio() * 100)));
            this.ErrorCorrectPreamble.Add(string.Format("I7={0}", (object)(ControllerConfiguration.Instance.WidenArcusTolerance ? 5 : 2)));
            this.MovePreamble.Clear();
            this.MovePreamble.AddRange((IEnumerable<string>)new string[10]
            {
        "$",
        "RSTOP",
        "MECLEARY",
        "MECLEARX",
        ControllerConfiguration.Instance.MoveAxisXYSpeed.GetHighSpeedCommand(),
        ControllerConfiguration.Instance.MoveAxisXYSpeed.GetLowSpeedCommand(),
        ControllerConfiguration.Instance.MoveAxisXYSpeed.GetAccelerationCommand(),
        "ABS",
        "I7=15",
        "I2=3136"
            });
        }

        private ErrorCodes OnWaitMotor(int timeout, Axis axis)
        {
            return this.OnWaitMotor(timeout, axis, MoveOperation.Normal);
        }

        private ErrorCodes OnWaitMotor(int timeout, Axis axis, MoveOperation operation)
        {
            IRuntimeService service1 = ServiceLocator.Instance.GetService<IRuntimeService>();
            TimeSpan timespan = new TimeSpan(0, 0, 0, 0, ControllerConfiguration.Instance.ArcusMotorQueryPause);
            IDoorSensorService service2 = ServiceLocator.Instance.GetService<IDoorSensorService>();
            using (ExecutionTimer executionTimer = new ExecutionTimer())
            {
                string command = "MST" + axis.ToString().ToUpper();
                string response;
                MotorWaitDecoder motorWaitDecoder;
                do
                {
                    DoorSensorResult doorSensorResult = service2.Query();
                    if (doorSensorResult != DoorSensorResult.Ok)
                    {
                        this.WriteToLog(string.Format("Door sensor query returned {0}: halting motion.", (object)doorSensorResult.ToString()));
                        this.HaltMotor();
                        return ErrorCodes.DoorOpen;
                    }
                    response = this.SendReciveRaw(command);
                    if (response == null)
                        return ErrorCodes.ArcusNotResponsive;
                    motorWaitDecoder = new MotorWaitDecoder();
                    if (!motorWaitDecoder.MotorRunning(response, operation))
                    {
                        if (motorWaitDecoder.Error != ErrorCodes.Success)
                        {
                            string str = motorWaitDecoder.FormatError(axis, response);
                            LogHelper.Instance.Log(str, LogEntryType.Error);
                            this.WriteToLog(str);
                        }
                        return motorWaitDecoder.Error;
                    }
                    service1.SpinWait(timespan);
                }
                while (executionTimer.ElapsedMilliseconds < (long)timeout);
                string str1 = motorWaitDecoder.FormatError(axis, response);
                LogHelper.Instance.Log(str1, LogEntryType.Error);
                this.WriteToLog(str1);
                this.HaltMotor();
                return ErrorCodes.Timeout;
            }
        }

        private string HaltMotor() => this.SendReciveRaw("RSTOP");

        private ErrorCodes HomeXAxis()
        {
            string[] strArray1 = new string[11]
            {
        "$",
        "RSTOP",
        "MECLEARX",
        "I1=4204544",
        ControllerConfiguration.Instance.InitAxisXSpeed.GetHighSpeedCommand(),
        ControllerConfiguration.Instance.InitAxisXSpeed.GetLowSpeedCommand(),
        ControllerConfiguration.Instance.InitAxisXSpeed.GetAccelerationCommand(),
        "INC",
        "EX=0",
        "PX=0",
        "I2=3072"
            };
            foreach (string command in strArray1)
            {
                if (this.SendReciveRaw(command) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            ErrorCodes errorCodes1 = this.OnWaitMotor(5000, Axis.X);
            if (errorCodes1 != ErrorCodes.Success)
                return errorCodes1;
            if (this.SendReciveRaw("HOMEX-") == null)
                return ErrorCodes.ArcusNotResponsive;
            ErrorCodes errorCodes2 = this.OnWaitMotor(64000, Axis.X);
            if (errorCodes2 != ErrorCodes.Success)
                return errorCodes2;
            string[] strArray2 = new string[2]
            {
        "I2=3136",
        "I7=15"
            };
            foreach (string command in strArray2)
            {
                if (this.SendReciveRaw(command) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            ErrorCodes errorCodes3 = this.OnWaitMotor(5000, Axis.X);
            if (errorCodes3 != ErrorCodes.Success)
                return errorCodes3;
            return this.SendReciveRaw("MECLEARX") != null ? ErrorCodes.Success : ErrorCodes.ArcusNotResponsive;
        }

        private ErrorCodes HomeYAxis()
        {
            string[] strArray = new string[9]
            {
        "$",
        "RSTOP",
        "MECLEARY",
        ControllerConfiguration.Instance.InitAxisYSpeed.GetHighSpeedCommand(),
        ControllerConfiguration.Instance.InitAxisYSpeed.GetLowSpeedCommand(),
        ControllerConfiguration.Instance.InitAxisYSpeed.GetAccelerationCommand(),
        "INC",
        "EY=0",
        "PY=0"
            };
            foreach (string command in strArray)
            {
                if (this.SendReciveRaw(command) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            if (ControllerConfiguration.Instance.HomeYDropBack != 0)
            {
                int num = ControllerConfiguration.Instance.HomeYDropBack;
                if (Math.Sign(num) == 1)
                    num = -num;
                if (this.SendReciveRaw("MIOY") == null || this.SendReciveRaw(string.Format("Y{0}", (object)num)) == null)
                    return ErrorCodes.ArcusNotResponsive;
                ErrorCodes errorCodes = this.OnWaitMotor(6000, Axis.Y, MoveOperation.Dropback);
                if (errorCodes != ErrorCodes.Success)
                    return errorCodes;
                if (this.SendReciveRaw("MECLEARY") == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            return this.SendReciveRaw("HOMEY+") != null ? this.OnWaitMotor(60000, Axis.Y) : ErrorCodes.ArcusNotResponsive;
        }

        private string SendReciveRaw(string command)
        {
            if (string.IsNullOrEmpty(command) || command.Equals("NOP", StringComparison.CurrentCultureIgnoreCase))
                return "OK";
            bool flag = LogHelper.Instance.IsLevelEnabled(LogEntryType.Debug) || this.DebugEnabled;
            StringBuilder stringBuilder = flag ? new StringBuilder() : (StringBuilder)null;
            if (flag)
                stringBuilder.AppendFormat("ArcusMotionController: Instruction={0}", (object)command);
            using (IChannelResponse channelResponse = this.Port.SendRecv(command, ControllerConfiguration.Instance.MotionControllerTimeout))
            {
                if (!channelResponse.CommOk)
                {
                    LogHelper.Instance.Log("ArcusMotionControl.SendCommand(): channel error returned {0}", (object)channelResponse.Error);
                    this.WriteToLog("[ArcusMotionController] Send command {0}; error = {1}", (object)command, (object)ErrorCodes.ArcusNotResponsive.ToString());
                    if (flag)
                    {
                        stringBuilder.AppendFormat(", Error = {0}", (object)ErrorCodes.ArcusNotResponsive.ToString());
                        this.WriteToLog(stringBuilder.ToString());
                    }
                    return (string)null;
                }
                int index = channelResponse.GetIndex((byte)13);
                int count = index == -1 ? channelResponse.RawResponse.Length - 1 : index;
                string str = Encoding.ASCII.GetString(channelResponse.RawResponse, 0, count);
                if (flag)
                {
                    stringBuilder.AppendFormat(", Response={0}", (object)str);
                    this.WriteToLog(stringBuilder.ToString());
                }
                return str;
            }
        }

        private int? DecodePositionResponse(Axis axis)
        {
            string str = this.SendReciveRaw(axis == Axis.X ? "EX" : "PY");
            int result;
            return str != null && int.TryParse(str.Substring(str.IndexOf('=') + 1), out result) ? new int?(result) : new int?();
        }

        private ErrorCodes MoveAbsolute(ref MoveTarget target)
        {
            foreach (string command in this.MovePreamble)
            {
                if (this.SendReciveRaw(command) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            ErrorCodes errorCodes1 = ErrorCodes.Success;
            switch (target.Axis)
            {
                case Axis.X:
                    errorCodes1 = this.OnAxisXMove(ref target);
                    break;
                case Axis.Y:
                    errorCodes1 = this.OnAxisYMove(ref target);
                    break;
                case Axis.XY:
                    errorCodes1 = this.OnMultiAxisMove(ref target);
                    break;
            }
            ErrorCodes errorCodes2 = this.IsMotorErrored(Axis.XY);
            if (errorCodes2 != ErrorCodes.Success)
                return errorCodes2;
            if (ControllerConfiguration.Instance.PrintEncoderPositionAfterMove2)
            {
                IControllerPosition controllerPosition = this.ReadPositions();
                if (controllerPosition.ReadOk)
                    LogHelper.Instance.Log("MoveAbsoluteInternal: encoder positions (x,y) = {0}, {1}.", (object)controllerPosition.XCoordinate.Value, (object)controllerPosition.YCoordinate.Value);
                else
                    LogHelper.Instance.Log("Unable to determine encoder position from ARCUS.");
            }
            return errorCodes1;
        }

        private ErrorCodes OnMultiAxisMove(ref MoveTarget target)
        {
            int moveTimeout = ControllerConfiguration.Instance.MoveTimeout;
            if (!this.UseSmoothMove)
            {
                if (this.SendReciveRaw(string.Format("X{0}", (object)target.XCoordinate.Value)) == null || this.SendReciveRaw(string.Format("Y{0}", (object)target.YCoordinate.Value)) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            else if (this.SendReciveRaw(string.Format("Y{0}X{1}", (object)target.YCoordinate.Value, (object)target.XCoordinate.Value)) == null)
                return ErrorCodes.ArcusNotResponsive;
            ErrorCodes errorCodes1 = this.OnWaitMotor(moveTimeout, Axis.Y);
            if (errorCodes1 != ErrorCodes.Success)
                return errorCodes1;
            ErrorCodes errorCodes2 = this.OnWaitMotor(moveTimeout, Axis.X);
            if (errorCodes2 != ErrorCodes.Success)
                return errorCodes2;
            foreach (string command in this.ErrorCorrectPreamble)
            {
                if (this.SendReciveRaw(command) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            if (this.SendReciveRaw(string.Format("Y{0}X{1}", (object)target.YCoordinate.Value, (object)target.XCoordinate.Value)) == null)
                return ErrorCodes.ArcusNotResponsive;
            ErrorCodes errorCodes3 = this.OnWaitMotor(moveTimeout, Axis.Y);
            if (errorCodes3 != ErrorCodes.Success)
                return errorCodes3;
            ErrorCodes errorCodes4 = this.OnWaitMotor(moveTimeout, Axis.X);
            if (errorCodes4 != ErrorCodes.Success)
                return errorCodes4;
            return this.SendReciveRaw("I7=10") != null ? ErrorCodes.Success : ErrorCodes.ArcusNotResponsive;
        }

        private ErrorCodes OnAxisYMove(ref MoveTarget target)
        {
            return this.SendReciveRaw(string.Format("Y{0}", (object)target.YCoordinate.Value)) != null ? this.OnWaitMotor(ControllerConfiguration.Instance.MoveTimeout, Axis.Y) : ErrorCodes.ArcusNotResponsive;
        }

        private ErrorCodes OnAxisXMove(ref MoveTarget target)
        {
            int moveTimeout = ControllerConfiguration.Instance.MoveTimeout;
            string command1 = string.Format("X{0}", (object)target.XCoordinate.Value);
            if (this.SendReciveRaw(command1) == null)
                return ErrorCodes.ArcusNotResponsive;
            ErrorCodes errorCodes1 = this.OnWaitMotor(moveTimeout, Axis.X);
            if (errorCodes1 != ErrorCodes.Success)
                return errorCodes1;
            foreach (string command2 in this.ErrorCorrectPreamble)
            {
                if (this.SendReciveRaw(command2) == null)
                    return ErrorCodes.ArcusNotResponsive;
            }
            if (this.SendReciveRaw(command1) == null)
                return ErrorCodes.ArcusNotResponsive;
            ErrorCodes errorCodes2 = this.OnWaitMotor(moveTimeout, Axis.X);
            if (errorCodes2 != ErrorCodes.Success)
                return errorCodes2;
            return this.SendReciveRaw("I7=10") != null ? ErrorCodes.Success : ErrorCodes.ArcusNotResponsive;
        }

        private ErrorCodes ClearYMotorError()
        {
            ErrorCodes errorCodes = this.IsMotorErrored(Axis.Y);
            if (ErrorCodes.MotorError != errorCodes)
                return errorCodes;
            int num = (int)this.HomeAxis(Axis.Y);
            return this.IsMotorErrored(Axis.Y);
        }

        private ErrorCodes IsMotorErrored(Axis axis)
        {
            if (Axis.XY != axis && Axis.Y != axis)
                return ErrorCodes.Success;
            string s = this.SendReciveRaw("MIOY");
            int result;
            if (s == null || this.SendReciveRaw("MECLEARY") == null || !int.TryParse(s, out result))
                return ErrorCodes.ArcusNotResponsive;
            return result <= 0 || result >= 8 ? ErrorCodes.Success : ErrorCodes.MotorError;
        }
    }
}
