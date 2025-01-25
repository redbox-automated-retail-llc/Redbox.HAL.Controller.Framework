using Redbox.HAL.Component.Model;
using System;
using System.IO;

namespace Redbox.HAL.Controller.Framework
{
    internal abstract class AbstractMotionController
    {
        protected readonly TextWriter LogFile;

        protected void WriteToLog(string fmt, params object[] stuff)
        {
            fmt = string.Format(fmt, stuff);
            this.WriteToLog(fmt);
        }

        protected void WriteToLog(string msg)
        {
            try
            {
                DateTime now = DateTime.Now;
                this.LogFile.WriteLine(string.Format("{0} {1} {2}", (object)now.ToShortDateString(), (object)now.ToShortTimeString(), (object)msg));
            }
            catch
            {
            }
        }

        protected AbstractMotionController()
        {
            try
            {
                this.LogFile = (TextWriter)new StreamWriter((Stream)File.Open(Path.Combine(ServiceLocator.Instance.GetService<IFormattedLogFactoryService>().CreateSubpath("Service"), "MotionControlErrorLog.log"), FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("[MotionControl] Create motion control error log caught an exception.", ex);
                this.LogFile = (TextWriter)StreamWriter.Null;
            }
        }

        internal abstract bool OnStartup();

        internal abstract bool OnShutdown();

        internal abstract IMotionControlLimitResponse ReadLimits();

        internal abstract bool CommunicationOk();

        internal abstract IControllerPosition ReadPositions();

        internal abstract ErrorCodes MoveToTarget(ref MoveTarget target);

        internal abstract ErrorCodes MoveToVend(MoveMode mode);

        internal abstract ErrorCodes HomeAxis(Axis axis);

        internal abstract bool OnResetDeviceDriver();

        internal virtual void OnConfigurationLoad()
        {
        }

        internal virtual void OnConfigurationChangeStart()
        {
        }

        internal virtual void OnConfigurationChangeEnd()
        {
        }
    }
}
