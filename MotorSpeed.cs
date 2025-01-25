using Redbox.HAL.Component.Model.Extensions;
using System.ComponentModel;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class MotorSpeed
    {
        public MotorSpeed(int low, int high, int accelerationTime, MotorGear gear)
        {
            this.Low = low;
            this.High = high;
            this.Gear = gear;
            this.AccelerationTime = accelerationTime;
        }

        public override string ToString() => "(Motor Speed Properties)";

        public static MotorSpeed FromXmlNode(XmlNode node, MotorGear gear)
        {
            int nodeValue1 = node.SelectSingleNode("Low").GetNodeValue<int>(500);
            int nodeValue2 = node.SelectSingleNode("High").GetNodeValue<int>(3000);
            int nodeValue3 = node.SelectSingleNode("AccelerationTime").GetNodeValue<int>(300);
            int high = nodeValue2;
            int accelerationTime = nodeValue3;
            MotorGear gear1 = gear;
            return new MotorSpeed(nodeValue1, high, accelerationTime, gear1);
        }

        public void SaveToXml(XmlNode node)
        {
            if (node == null)
                return;
            node.SelectSingleNodeAndSetValue<int>("Low", this.Low);
            node.SelectSingleNodeAndSetValue<int>("High", this.High);
            node.SelectSingleNodeAndSetValue<int>("AccelerationTime", this.AccelerationTime);
        }

        public string GetHighSpeedCommand()
        {
            return string.Format("HSPD {0}", (object)((double)this.High * this.Gear.GetStepRatio()));
        }

        public string GetLowSpeedCommand()
        {
            return string.Format("LSPD {0}", (object)((double)this.Low * this.Gear.GetStepRatio()));
        }

        public string GetAccelerationCommand()
        {
            return string.Format("ACCEL {0}", (object)this.AccelerationTime);
        }

        [DisplayName("Low Speed")]
        [Description("The low (start) speed of the motor specified in pulses per second.")]
        public int Low { get; private set; }

        [DisplayName("High Speed")]
        [Description("The high (target) speed of the motor specified in pulses per second.")]
        public int High { get; private set; }

        [Browsable(false)]
        public MotorGear Gear { get; private set; }

        [DisplayName("Acceleration Time (in milliseconds)")]
        [Description("The amount of time in milliseconds between acceleration from the low speed to the high speed.")]
        public int AccelerationTime { get; private set; }
    }
}
