using Redbox.HAL.Component.Model;
using System;
using System.Text;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class PickerSensorReadResult :
      IPickerSensorReadResult,
      IReadInputsResult<PickerInputs>
    {
        private readonly ReadPickerInputsResult ReadResult;
        private readonly int m_blockedCount;
        private static readonly PickerInputs[] PickerSensors = new PickerInputs[6]
        {
      PickerInputs.Sensor1,
      PickerInputs.Sensor2,
      PickerInputs.Sensor3,
      PickerInputs.Sensor4,
      PickerInputs.Sensor5,
      PickerInputs.Sensor6
        };

        public void Log() => this.Log(LogEntryType.Info);

        public void Log(LogEntryType type)
        {
            if (!this.Success)
            {
                this.OnError();
            }
            else
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("PickerSensors: ");
                int count = 1;
                Array.ForEach<PickerInputs>(PickerSensorReadResult.PickerSensors, (Action<PickerInputs>)(each => builder.AppendFormat("{0} = {1};", (object)count++, this.IsInputActive(each) ? (object)"BLOCKED" : (object)"CLEAR")));
                LogHelper.Instance.Log(builder.ToString(), type);
            }
        }

        public bool IsInputActive(PickerInputs input)
        {
            return !this.Success || this.ReadResult.IsInputActive(input);
        }

        public bool IsInState(PickerInputs input, InputState state)
        {
            return this.ReadResult.IsInState(input, state);
        }

        public void Foreach(Action<PickerInputs> action)
        {
            Array.ForEach<PickerInputs>(PickerSensorReadResult.PickerSensors, (Action<PickerInputs>)(sensor => action(sensor)));
        }

        public ErrorCodes Error { get; private set; }

        public bool Success => this.Error == ErrorCodes.Success;

        public int InputCount => PickerSensorReadResult.PickerSensors.Length;

        public bool IsFull => this.m_blockedCount > 0;

        public int BlockedCount => this.m_blockedCount;

        internal PickerSensorReadResult(ErrorCodes notSuccess)
        {
            this.Error = notSuccess;
            this.m_blockedCount = PickerSensorReadResult.PickerSensors.Length;
            this.OnError();
        }

        internal PickerSensorReadResult(ReadPickerInputsResult result)
        {
            PickerSensorReadResult sensorReadResult = this;
            this.ReadResult = result;
            this.Error = result.Error;
            if (this.Error != ErrorCodes.Success)
            {
                this.m_blockedCount = PickerSensorReadResult.PickerSensors.Length;
                this.OnError();
            }
            else
            {
                int bc = 0;
                Array.ForEach<PickerInputs>(PickerSensorReadResult.PickerSensors, (Action<PickerInputs>)(each =>
                {
                    if (!sensorReadResult.ReadResult.IsInputActive(each))
                        return;
                    ++bc;
                }));
                this.m_blockedCount = bc;
            }
        }

        private void OnError()
        {
            if (this.Error == ErrorCodes.Success)
                return;
            LogHelper.Instance.WithContext(LogEntryType.Error, "Read Picker sensors failed with error {0}", (object)this.Error);
        }
    }
}
