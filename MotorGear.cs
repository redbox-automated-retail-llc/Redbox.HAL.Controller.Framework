using Redbox.HAL.Component.Model.Extensions;
using System.ComponentModel;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class MotorGear
    {
        public MotorGear(double stepRatio, int pulseRatio, int encoderRatio, int stepResolution)
        {
            this.StepRatio = stepRatio;
            this.PulseRatio = pulseRatio;
            this.EncoderRatio = encoderRatio;
            this.StepResolution = stepResolution;
        }

        public override string ToString() => "(Motor Gear Properties)";

        public static MotorGear FromXmlNode(XmlNode node)
        {
            double nodeValue1 = node.SelectSingleNode("StepRatio").GetNodeValue<double>(10.0);
            int nodeValue2 = node.SelectSingleNode("PulseRatio").GetNodeValue<int>(0);
            int nodeValue3 = node.SelectSingleNode("EncoderRatio").GetNodeValue<int>(0);
            int nodeValue4 = node.SelectSingleNode("StepResolution").GetNodeValue<int>(4);
            int pulseRatio = nodeValue2;
            int encoderRatio = nodeValue3;
            int stepResolution = nodeValue4;
            return new MotorGear(nodeValue1, pulseRatio, encoderRatio, stepResolution);
        }

        public void SaveToXml(XmlNode node)
        {
            if (node == null)
                return;
            node.SelectSingleNodeAndSetValue<double>("StepRatio", this.StepRatio);
            node.SelectSingleNodeAndSetValue<int>("PulseRatio", this.PulseRatio);
            node.SelectSingleNodeAndSetValue<int>("EncoderRatio", this.EncoderRatio);
            node.SelectSingleNodeAndSetValue<int>("StepResolution", this.StepResolution);
        }

        public double GetStepRatio() => (double)this.StepResolution * (this.StepRatio / 10.0);

        public int GetEncoderRatio()
        {
            return this.PulseRatio * 4096 * (int)this.GetStepRatio() + this.EncoderRatio;
        }

        [DisplayName("Step Ratio")]
        [Description("The ratio of the Step Resolution value.  Step Ratio is divided by 10 and then multiplied by Step Resolution to yield the multiplied Step Ratio.")]
        public double StepRatio { get; private set; }

        [DisplayName("Pulse Ratio")]
        [Description("The Pulse Ratio in pulse units.  Each Pulse Ratio unit is multiplied by 4096, then multiplied by the multiplied Step Ratio, and finally added to the Encoder Ratio value.")]
        public int PulseRatio { get; private set; }

        [DisplayName("Encoder Ratio")]
        [Description("The Encoder Ratio in encoder units.")]
        public int EncoderRatio { get; private set; }

        [DisplayName("Step Resolution")]
        [Description("The Step Resolution in encoder units.")]
        public int StepResolution { get; private set; }
    }
}
