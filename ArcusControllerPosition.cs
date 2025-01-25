using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal class ArcusControllerPosition : IControllerPosition
    {
        public int? XCoordinate { get; internal set; }

        public int? YCoordinate { get; internal set; }

        public bool ReadOk => this.XCoordinate.HasValue && this.YCoordinate.HasValue;

        internal ArcusControllerPosition()
        {
            int? nullable = new int?();
            this.YCoordinate = nullable;
            this.XCoordinate = nullable;
        }
    }
}
