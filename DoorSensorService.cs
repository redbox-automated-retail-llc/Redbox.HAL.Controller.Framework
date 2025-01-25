using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class DoorSensorService : IConfigurationObserver, IDoorSensorService, IMoveVeto
    {
        private bool m_softwareOverride;
        private bool m_enabled = true;

        public ErrorCodes CanMove()
        {
            DoorSensorResult doorSensorResult = this.Query();
            if (doorSensorResult == DoorSensorResult.Ok)
                return ErrorCodes.Success;
            LogHelper.Instance.Log(string.Format("Door sensor query returned {0}", (object)doorSensorResult.ToString()), LogEntryType.Error);
            return ErrorCodes.DoorOpen;
        }

        public void NotifyConfigurationLoaded()
        {
            LogHelper.Instance.Log("[DoorSensorService] Configuration load.");
            this.m_enabled = ControllerConfiguration.Instance.IsVMZMachine;
            if (!this.m_enabled)
                LogHelper.Instance.Log("The door sensors are not configured.");
            else if (this.SoftwareOverride)
            {
                LogHelper.Instance.Log("** WARNING **: Door sensors are configured, however a software override in place.");
                LogHelper.Instance.Log("** WARNING **: Kiosk is operating without door sensors.");
            }
            else
                LogHelper.Instance.Log("Door sensors are configured.");
        }

        public void NotifyConfigurationChangeStart()
        {
        }

        public void NotifyConfigurationChangeEnd()
        {
        }

        public DoorSensorResult Query()
        {
            return !this.m_enabled || this.SoftwareOverride ? DoorSensorResult.Ok : this.RawQuery();
        }

        public DoorSensorResult QueryStateForDisplay()
        {
            if (!this.m_enabled)
                return DoorSensorResult.Ok;
            return !this.m_softwareOverride ? this.RawQuery() : DoorSensorResult.SoftwareOverride;
        }

        public bool SensorsEnabled => this.m_enabled && !this.SoftwareOverride;

        public bool SoftwareOverride
        {
            get => this.m_softwareOverride;
            set
            {
                this.m_softwareOverride = value;
                ServiceLocator.Instance.GetService<IPersistentMapService>().GetMap().SetValue<bool>("SensorSoftwareOverride", this.m_softwareOverride);
            }
        }

        internal DoorSensorService()
        {
            this.m_softwareOverride = ServiceLocator.Instance.GetService<IPersistentMapService>().GetMap().GetValue<bool>("SensorSoftwareOverride", false);
            ServiceLocator.Instance.GetService<IMotionControlService>().AddVeto((IMoveVeto)this);
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }

        private DoorSensorResult RawQuery()
        {
            IReadInputsResult<AuxInputs> readInputsResult = ServiceLocator.Instance.GetService<IControlSystem>().ReadAuxInputs();
            if (!readInputsResult.Success)
                return DoorSensorResult.AuxReadError;
            if (readInputsResult.IsInputActive(AuxInputs.QlmDown))
                return DoorSensorResult.Ok;
            LogHelper.Instance.WithContext(LogEntryType.Error, "[DoorSensorService] read inputs shows door not closed");
            readInputsResult.Log(LogEntryType.Error);
            return DoorSensorResult.FrontDoor;
        }
    }
}
