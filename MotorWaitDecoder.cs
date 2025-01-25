using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal struct MotorWaitDecoder
    {
        internal int StatusCode { get; private set; }

        internal ErrorCodes Error { get; private set; }

        internal string Diagnostic
        {
            get
            {
                switch (this.StatusCode)
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
                        return this.StatusCode.ToString();
                }
            }
        }

        internal string FormatError(Axis axis, string response)
        {
            return string.Format("Unable to reach target on axis {0}. Response {1} ( diagnostic = {2} )", (object)axis.ToString().ToUpper(), (object)response, (object)this.Diagnostic);
        }

        internal bool MotorRunning(string response, MoveOperation operation)
        {
            int result;
            if (!int.TryParse(response, out result))
            {
                this.StatusCode = 999;
                this.Error = ErrorCodes.MotorError;
                return false;
            }
            this.StatusCode = result;
            this.Error = ErrorCodes.Success;
            bool flag = this.StatusCode != 0;
            if (16 == this.StatusCode)
            {
                if (operation == MoveOperation.Dropback)
                {
                    this.Error = ErrorCodes.Success;
                    this.StatusCode = 0;
                    flag = false;
                }
                else if (MoveOperation.Normal == operation)
                {
                    this.Error = ErrorCodes.LowerLimitError;
                    flag = false;
                }
            }
            return flag;
        }
    }
}
