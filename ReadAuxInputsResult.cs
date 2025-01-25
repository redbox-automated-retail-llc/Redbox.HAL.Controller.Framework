using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ReadAuxInputsResult : AbstractReadInputsResult<AuxInputs>
    {
        protected override string LogHeader => "AUX Inputs";

        protected override InputState OnGetInputState(AuxInputs input)
        {
            return this.GetInputState((int)input);
        }

        protected override void OnForeachInput(Action<AuxInputs> a)
        {
            foreach (AuxInputs auxInputs in (IEnumerable<AuxInputs>)Enum<AuxInputs>.GetValues())
                a(auxInputs);
        }

        internal ReadAuxInputsResult(CoreResponse response)
          : base(response)
        {
        }
    }
}
