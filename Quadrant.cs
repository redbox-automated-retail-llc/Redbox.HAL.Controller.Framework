using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class Quadrant : IQuadrant
    {
        public int Offset { get; private set; }

        public IRange<int> Slots { get; private set; }

        public bool IsExcluded { get; internal set; }

        internal Quadrant(int offset)
          : this(offset, (IRange<int>)null)
        {
        }

        internal Quadrant(int offset, IRange<int> slots)
        {
            this.Offset = offset;
            this.Slots = slots;
            this.IsExcluded = false;
        }
    }
}
