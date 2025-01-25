using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class DumpbinPutObserver : IPutToObserver, IPutObserver
    {
        public void OnSuccessfulPut(IPutResult res, IFormattedLog log)
        {
            IDumpbinService service1 = ServiceLocator.Instance.GetService<IDumpbinService>();
            IInventoryService service2 = ServiceLocator.Instance.GetService<IInventoryService>();
            service1.AddBinItem(res.StoredMatrix);
            int num = (int)ServiceLocator.Instance.GetService<IMotionControlService>().MoveTo(service1.RotationLocation, MoveMode.None, log);
            ILocation putLocation = service1.PutLocation;
            service2.Reset(putLocation);
        }

        public void OnFailedPut(IPutResult res, IFormattedLog log)
        {
        }

        public void OnMoveError(ErrorCodes e)
        {
        }
    }
}
