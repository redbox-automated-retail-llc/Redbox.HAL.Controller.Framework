using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ReadPickerInputsResult : AbstractReadInputsResult<PickerInputs>
    {
        protected override string LogHeader => "Picker Inputs";

        protected override InputState OnGetInputState(PickerInputs input)
        {
            return this.GetInputState((int)input);
        }

        protected override void OnForeachInput(Action<PickerInputs> a)
        {
            foreach (PickerInputs pickerInputs in (IEnumerable<PickerInputs>)Enum<PickerInputs>.GetValues())
                a(pickerInputs);
        }

        internal ReadPickerInputsResult(CoreResponse response)
          : base(response)
        {
        }
    }
}
