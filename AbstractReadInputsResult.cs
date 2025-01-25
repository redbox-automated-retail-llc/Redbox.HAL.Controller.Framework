using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal abstract class AbstractReadInputsResult<T> : IReadInputsResult<T>
    {
        protected readonly InputState[] Inputs;

        public ErrorCodes Error { get; protected set; }

        public bool Success => this.Error == ErrorCodes.Success;

        public int InputCount => this.Inputs.Length;

        public void Log() => this.Log(LogEntryType.Info);

        public void Log(LogEntryType type)
        {
            if (!this.Success)
                LogHelper.Instance.Log(this.RawResponse.Diagnostic);
            else
                LogHelper.Instance.Log(type, "{0} {1}", (object)this.LogHeader, (object)this.RawResponse.OpCodeResponse);
        }

        public bool IsInputActive(T input) => this.IsInState(input, InputState.Active);

        public bool IsInState(T input, InputState state)
        {
            if (!this.Success)
                throw new InvalidOperationException("Sensor read state is invalid");
            return this.OnGetInputState(input) == state;
        }

        public void Foreach(Action<T> action) => this.OnForeachInput(action);

        protected abstract string LogHeader { get; }

        protected abstract InputState OnGetInputState(T input);

        protected abstract void OnForeachInput(Action<T> a);

        protected InputState GetInputState(int input)
        {
            if (input < 0 || input >= this.Inputs.Length)
                throw new InvalidOperationException("Specified input exceeds bounds");
            return this.Inputs[input];
        }

        protected AbstractReadInputsResult(CoreResponse response)
        {
            this.RawResponse = response;
            if (!this.RawResponse.Success)
            {
                this.Error = ErrorCodes.CommunicationError;
            }
            else
            {
                this.Inputs = new InputState[20];
                this.Error = ErrorCodes.Success;
                if (ControllerConfiguration.Instance.ValidateInputsReadResponse)
                    this.FromValidated();
                else
                    this.From();
            }
        }

        internal CoreResponse RawResponse { get; private set; }

        private void From()
        {
            try
            {
                int num = 0;
                for (int index = 0; index < this.RawResponse.OpCodeResponse.Length; ++index)
                {
                    if (char.IsDigit(this.RawResponse.OpCodeResponse[index]))
                        this.Inputs[num++] = this.RawResponse.OpCodeResponse[index] == '1' ? InputState.Active : InputState.Inactive;
                }
                if (20 == num)
                    return;
                this.OnBogusResponse();
            }
            catch (Exception ex)
            {
                this.OnResponseException(ex);
            }
        }

        private void FromValidated()
        {
            int num1 = this.RawResponse.OpCodeResponse.IndexOf("R");
            if (21 != num1 || !char.IsWhiteSpace(this.RawResponse.OpCodeResponse[16]))
            {
                this.OnBogusResponse();
            }
            else
            {
                int num2 = 0;
                try
                {
                    for (int index = 0; index < num1; ++index)
                    {
                        switch (this.RawResponse.OpCodeResponse[index])
                        {
                            case '0':
                                this.Inputs[num2++] = InputState.Inactive;
                                break;
                            case '1':
                                this.Inputs[num2++] = InputState.Active;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.OnResponseException(ex);
                    num2 = 0;
                }
                if (num2 == 20)
                    return;
                this.OnBogusResponse();
            }
        }

        private void OnResponseException(Exception e)
        {
            LogHelper.Instance.Log("[ReadInputs] Unhandled exception parsing response '{0}'", (object)this.RawResponse.OpCodeResponse);
            LogHelper.Instance.Log(e.Message);
            this.Error = ErrorCodes.CommunicationError;
        }

        private void OnBogusResponse()
        {
            LogHelper.Instance.WithContext(LogEntryType.Error, "[ReadInputs] Unexpected response from port {0}", (object)this.RawResponse.OpCodeResponse);
            this.Error = ErrorCodes.CommunicationError;
        }
    }
}
