using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class ClearPickerFrontOperation : AbstractOperation<bool>
    {
        public override bool Execute()
        {
            if (!this.Controller.SetFinger(GripperFingerState.Rent).Success)
                return false;
            if (!this.Controller.RetractArm().Success)
            {
                this.Controller.SetFinger(GripperFingerState.Rent);
                if (!this.Controller.RetractArm().Success)
                    return false;
            }
            if (!this.Controller.SetFinger(GripperFingerState.Closed).Success)
                return false;
            this.Controller.TimedExtend(ControllerConfiguration.Instance.PushTime);
            if (!this.Controller.SetFinger(GripperFingerState.Rent).Success || !this.Controller.RetractArm().Success || !this.Controller.SetFinger(GripperFingerState.Closed).Success)
                return false;
            this.Controller.TimedExtend(ControllerConfiguration.Instance.PushTime);
            return this.Controller.RetractArm().Success && this.Controller.SetFinger(GripperFingerState.Rent).Success;
        }
    }
}
