using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class VmzExcludePolicy : IExcludeEmptySearchLocationObserver
    {
        private readonly int Deck = 8;
        private readonly IRange<int> Slots = (IRange<int>)new Range(82, 84);

        public bool ShouldExclude(ILocation location)
        {
            return location.Deck == this.Deck && this.Slots.Includes(location.Slot);
        }

        internal VmzExcludePolicy()
        {
        }
    }
}
