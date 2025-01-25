using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class QlmExcludePolicy : IExcludeEmptySearchLocationObserver
    {
        public bool ShouldExclude(ILocation location)
        {
            IDeck from = ServiceLocator.Instance.GetService<IDecksService>().GetFrom(location);
            return from.IsSparse && from.Number == 7 && from.IsSlotSellThru(location.Slot);
        }

        internal QlmExcludePolicy()
        {
        }
    }
}
