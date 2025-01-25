using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class LocationTestResult : IPeekResult
    {
        public bool TestOk => this.Error == ErrorCodes.Success;

        public bool IsFull { get; internal set; }

        public ErrorCodes Error { get; internal set; }

        public ILocation PeekLocation { get; private set; }

        internal LocationTestResult(int deck, int slot)
        {
            this.Error = ErrorCodes.Success;
            this.PeekLocation = ServiceLocator.Instance.GetService<IInventoryService>().Get(deck, slot);
        }
    }
}
