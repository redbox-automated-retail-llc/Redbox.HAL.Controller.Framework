using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal struct ArcusMotorStatus
    {
        private MoveOperation Operation;

        internal int ArcusStatusCode { get; private set; }

        internal ErrorCodes Error { get; private set; }

        internal int Timeout { get; private set; }

        internal string Diagnostic
        {
            get
            {
                switch (this.ArcusStatusCode)
                {
                    case 0:
                        return "Motor stopped";
                    case 1:
                        return "Accelerating";
                    case 2:
                        return "Decelerating";
                    case 4:
                        return "Constant speed";
                    case 8:
                        return "Upper Limit Error";
                    case 16:
                        return "Lower Limit Error";
                    default:
                        return this.ArcusStatusCode.ToString();
                }
            }
        }

        internal bool MotorRunning { get; private set; }

        internal Axis[] Axes { get; private set; }

        internal void TestResponse(ArcusCommandResponse response)
        {
            this.TestResponse(response.Response);
        }

        internal void TestResponse(string rawResponse)
        {
            this.MotorRunning = true;
            int result;
            if (!int.TryParse(rawResponse, out result))
            {
                this.ArcusStatusCode = 999;
                this.MotorRunning = false;
                this.Error = ErrorCodes.MotorError;
            }
            else
            {
                this.ArcusStatusCode = result;
                this.Error = ErrorCodes.Success;
                this.MotorRunning = this.ArcusStatusCode != 0;
                switch (this.Operation)
                {
                    case MoveOperation.Dropback:
                        if (16 != this.ArcusStatusCode)
                            break;
                        this.Error = ErrorCodes.Success;
                        this.ArcusStatusCode = 0;
                        this.MotorRunning = false;
                        break;
                    case MoveOperation.Normal:
                        if (16 != this.ArcusStatusCode)
                            break;
                        this.Error = ErrorCodes.LowerLimitError;
                        this.MotorRunning = false;
                        break;
                }
            }
        }

        internal ArcusMotorStatus(string instruction, int timeout)
          : this()
        {
            this.Operation = MoveOperation.Normal;
            this.Timeout = timeout;
            this.Decode(instruction);
        }

        private ErrorCodes ComputeError()
        {
            if (this.ArcusStatusCode < 8)
                return ErrorCodes.Success;
            if (8 == this.ArcusStatusCode)
                return ErrorCodes.UpperLimitError;
            return 16 == this.ArcusStatusCode ? ErrorCodes.LowerLimitError : ErrorCodes.MotorError;
        }

        private void Decode(string waitCommand)
        {
            if (waitCommand.Equals("WAITYDB", StringComparison.CurrentCultureIgnoreCase))
            {
                this.Operation = MoveOperation.Dropback;
                this.Axes = new Axis[1] { Axis.Y };
            }
            else
                this.ComputeAxis(!waitCommand.StartsWith("WAITH") ? waitCommand.Substring("WAIT".Length) : waitCommand.Substring("WAITH".Length));
        }

        private void ComputeAxis(string axisAsString)
        {
            if ("X".Equals(axisAsString.ToUpper()))
                this.Axes = new Axis[1];
            else if ("Y".Equals(axisAsString.ToUpper()))
                this.Axes = new Axis[1] { Axis.Y };
            else if ("XY".Equals(axisAsString.ToUpper()))
                this.Axes = new Axis[2] { Axis.Y, Axis.X };
            else
                this.Axes = (Axis[])null;
        }
    }
}
