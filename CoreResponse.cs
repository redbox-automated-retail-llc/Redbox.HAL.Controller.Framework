using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class CoreResponse : IControlResponse
    {
        internal readonly AddressSelector Selector;
        private ErrorCodes m_errorCode;
        private string m_commandResponse = string.Empty;
        private static CoreResponse m_timeoutResponse = (CoreResponse)null;
        private static CoreResponse m_commErrorResponse = (CoreResponse)null;
        private static readonly char[] TrimChars = Environment.NewLine.ToCharArray();

        public override string ToString()
        {
            return !this.CommError ? this.Error.ToString().ToUpper() : this.Diagnostic;
        }

        public bool Success => this.Error == ErrorCodes.Success;

        public bool TimedOut => ErrorCodes.Timeout == this.Error;

        public bool CommError => ErrorCodes.CommunicationError == this.Error;

        public string Diagnostic { get; internal set; }

        internal bool IsBitSet(int bit)
        {
            if (this.m_errorCode != ErrorCodes.Success)
                return false;
            if (this.OpCodeResponse.Length >= bit + 1)
                return this.OpCodeResponse[bit] == '1';
            LogHelper.Instance.Log("IsBitSet: opcode response is {0}; this is insufficient for bit test.", (object)this.OpCodeResponse);
            return false;
        }

        internal static CoreResponse CommErrorResponse
        {
            get
            {
                if (CoreResponse.m_commErrorResponse == null)
                    CoreResponse.m_commErrorResponse = new CoreResponse(AddressSelector.H101)
                    {
                        Error = ErrorCodes.CommunicationError
                    };
                return CoreResponse.m_commErrorResponse;
            }
        }

        internal static CoreResponse TimedOutResponse
        {
            get
            {
                if (CoreResponse.m_timeoutResponse == null)
                    CoreResponse.m_timeoutResponse = new CoreResponse(AddressSelector.H101)
                    {
                        Error = ErrorCodes.Timeout
                    };
                return CoreResponse.m_timeoutResponse;
            }
        }

        internal ErrorCodes Error
        {
            get => this.m_errorCode;
            set
            {
                this.m_errorCode = value;
                if (ErrorCodes.CommunicationError != this.m_errorCode)
                    return;
                this.OpCodeResponse = string.Empty;
            }
        }

        internal string OpCodeResponse
        {
            get => this.m_commandResponse;
            set
            {
                if (string.IsNullOrEmpty(value))
                    this.m_commandResponse = string.Empty;
                else
                    this.m_commandResponse = value.TrimEnd(CoreResponse.TrimChars).TrimStart(CoreResponse.TrimChars);
            }
        }

        internal CoreResponse(ErrorCodes error, string diagnostic)
        {
            this.m_errorCode = error;
            this.Diagnostic = diagnostic;
        }

        internal CoreResponse(AddressSelector selector) => this.Selector = selector;
    }
}
