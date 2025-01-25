using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class UnconfiguredDoorSensorService : IDoorSensorService, IMoveVeto
    {
        public ErrorCodes CanMove() => ErrorCodes.Success;

        public DoorSensorResult Query() => DoorSensorResult.Ok;

        public DoorSensorResult QueryStateForDisplay() => DoorSensorResult.Ok;

        public bool SensorsEnabled => false;

        public bool SoftwareOverride { get; set; }

        internal UnconfiguredDoorSensorService()
        {
        }
    }
}
