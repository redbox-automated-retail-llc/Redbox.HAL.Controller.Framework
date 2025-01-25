using Redbox.HAL.Component.Model;
using System;
using System.Text;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class CommandTrace : IDisposable
    {
        private int spaceCount;
        private readonly bool TraceEnabled;
        private readonly StringBuilder TraceBuffer;

        public void Dispose()
        {
            if (!this.TraceEnabled)
                return;
            LogHelper.Instance.Log(this.TraceBuffer.ToString());
        }

        internal void Enter() => ++this.spaceCount;

        internal void Exit() => --this.spaceCount;

        internal void Trace(string fmt, params object[] p)
        {
            if (!this.TraceEnabled)
                return;
            this.Trace(string.Format(fmt, p));
        }

        internal void Trace(string msg)
        {
            if (!this.TraceEnabled)
                return;
            for (int index = 0; index < this.spaceCount; ++index)
                this.TraceBuffer.Append(" ");
            this.TraceBuffer.AppendLine(msg);
        }

        internal CommandTrace(bool enabled)
        {
            this.TraceEnabled = enabled;
            if (!this.TraceEnabled)
                return;
            this.TraceBuffer = new StringBuilder();
        }
    }
}
