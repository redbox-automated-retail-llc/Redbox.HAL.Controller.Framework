using Redbox.HAL.Component.Model;
using Redbox.HAL.Controller.Framework.Operations;
using Redbox.HAL.Controller.Framework.Services;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class ControllerService : IMoveVeto, IControllerService
    {
        private readonly DefaultPutObserver PutObserver = new DefaultPutObserver();
        private readonly DumpbinPutObserver DumpObserver = new DumpbinPutObserver();
        private readonly DefaultGetObserver GetObserver = new DefaultGetObserver();

        public ErrorCodes CanMove()
        {
            IControlSystem service = ServiceLocator.Instance.GetService<IControlSystem>();
            if (VendDoorState.Closed != service.VendDoorState && !service.VendDoorClose().Success)
                return ErrorCodes.VendDoorNotClosed;
            if (this.ClearGripper() != ErrorCodes.Success)
                return ErrorCodes.ObstructionDetected;
            IReadInputsResult<PickerInputs> readInputsResult = service.ReadPickerInputs();
            if (!readInputsResult.Success)
                return ErrorCodes.SensorReadError;
            if (!readInputsResult.IsInputActive(PickerInputs.Retract))
            {
                if (ControllerConfiguration.Instance.GripperRentOnMove && !readInputsResult.IsInputActive(PickerInputs.FingerRent) && !service.SetFinger(GripperFingerState.Rent).Success)
                    return ErrorCodes.GripperRentTimeout;
                if (!service.RetractArm().Success)
                    return ErrorCodes.GripperRetractTimeout;
            }
            if (!ControllerConfiguration.Instance.CheckGripperArmSensorsOnMove || !readInputsResult.IsInputActive(PickerInputs.Extend) || !readInputsResult.IsInputActive(PickerInputs.Retract))
                return ErrorCodes.Success;
            LogHelper.Instance.WithContext(false, LogEntryType.Error, "MOVE: can't move because extend/retract both triggered.");
            readInputsResult.Log(LogEntryType.Error);
            return ErrorCodes.SensorError;
        }

        public void Initialize(ErrorList errors, IDictionary<string, object> initProperties)
        {
            ControllerConfiguration.Instance.Initialize();
            IDataTableService service1 = ServiceLocator.Instance.GetService<IDataTableService>();
            ServiceLocator.Instance.AddService<IKioskConfiguration>((object)new KioskConfigurationService());
            IInventoryService inventoryService = (IInventoryService)new InventoryService();
            ServiceLocator.Instance.AddService<IInventoryService>((object)inventoryService);
            PersistentCounterService persistentCounterService = new PersistentCounterService(service1);
            ServiceLocator.Instance.AddService<IPersistentCounterService>((object)persistentCounterService);
            ServiceLocator.Instance.AddService<IHardwareCorrectionStatisticService>((object)new HardwareCorrectionStatisticService(service1));
            ServiceLocator.Instance.AddService<IKioskFunctionCheckService>((object)new KioskFunctionCheckService());
            ServiceLocator.Instance.AddService<IEmptySearchPatternService>((object)new EmptySearchPatternService(inventoryService));
            MotionControlService_v2 instance1 = new MotionControlService_v2();
            ServiceLocator.Instance.AddService<IMotionControlService>((object)instance1);
            instance1.AddVeto((IMoveVeto)this);
            IRuntimeService service2 = ServiceLocator.Instance.GetService<IRuntimeService>();
            ControlSystemBridgeModern instance2 = new ControlSystemBridgeModern(service2, (IPersistentCounterService)persistentCounterService);
            ServiceLocator.Instance.AddService<IControlSystemService>((object)instance2);
            ServiceLocator.Instance.AddService<IControlSystem>((object)instance2);
            ServiceLocator.Instance.AddService<IAirExchangerService>((object)new IceQubeAirExchangerService());
            ServiceLocator.Instance.AddService<IDumpbinService>((object)new DumpbinServiceBridge());
            ServiceLocator.Instance.AddService<IDoorSensorService>((object)new DoorSensorService());
            ServiceLocator.Instance.AddService<IPowerCycleDeviceService>((object)new PowerCycleDeviceService(service2));
        }

        public void Shutdown()
        {
            ServiceLocator.Instance.GetService<IMotionControlService>().Shutdown();
            ServiceLocator.Instance.GetService<IControlSystem>().Shutdown();
        }

        public IGetResult Get() => this.Get((IGetObserver)this.GetObserver);

        public IGetResult Get(IGetObserver observer)
        {
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            return (IGetResult)this.OnGet(observer, service.CurrentLocation);
        }

        public IGetFromResult GetFrom(ILocation location)
        {
            return this.GetFrom((IGetFromObserver)this.GetObserver, location);
        }

        public IGetFromResult GetFrom(IGetFromObserver observer, ILocation location)
        {
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            GetFromResult from = new GetFromResult();
            from.MoveResult = service.MoveTo(location, MoveMode.Get);
            if (from.MoveResult == ErrorCodes.Success)
                from.GetResult = this.OnGet((IGetObserver)observer, location);
            else
                observer?.OnMoveError(from.MoveResult);
            return (IGetFromResult)from;
        }

        public IPutResult Put(string id)
        {
            IDumpbinService service1 = ServiceLocator.Instance.GetService<IDumpbinService>();
            IMotionControlService service2 = ServiceLocator.Instance.GetService<IMotionControlService>();
            IPutObserver observer = (IPutObserver)this.PutObserver;
            ILocation currentLocation = service2.CurrentLocation;
            if (service1.IsBin(currentLocation))
                observer = (IPutObserver)this.DumpObserver;
            return (IPutResult)this.OnPut(observer, id, service2.CurrentLocation);
        }

        public IPutResult Put(IPutObserver observer, string id)
        {
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            return (IPutResult)this.OnPut(observer, id, service.CurrentLocation);
        }

        public IPutToResult PutTo(string id, ILocation location)
        {
            IDumpbinService service = ServiceLocator.Instance.GetService<IDumpbinService>();
            IPutToObserver observer = (IPutToObserver)this.PutObserver;
            ILocation loc = location;
            if (service.IsBin(loc))
                observer = (IPutToObserver)this.DumpObserver;
            return this.PutTo(observer, id, location);
        }

        public IPutToResult PutTo(IPutToObserver observer, string id, ILocation location)
        {
            IMotionControlService service = ServiceLocator.Instance.GetService<IMotionControlService>();
            PutToResult putToResult = new PutToResult();
            putToResult.MoveResult = service.MoveTo(location, MoveMode.Put);
            if (putToResult.MoveResult == ErrorCodes.Success)
            {
                int num = (int)ServiceLocator.Instance.GetService<IControlSystem>().TrackCycle();
                putToResult.PutResult = this.OnPut((IPutObserver)observer, id, location);
            }
            else
                observer?.OnMoveError(putToResult.MoveResult);
            return (IPutToResult)putToResult;
        }

        public ITransferResult Transfer(ILocation source, ILocation destination)
        {
            return this.Transfer(source, destination, true);
        }

        public ITransferResult Transfer(ILocation source, ILocation destination, bool preserveFlags)
        {
            using (TransferOperation transferOperation = new TransferOperation((IControllerService)this, source))
            {
                transferOperation.PreserveFlagsOnPut = preserveFlags;
                return transferOperation.Transfer(destination);
            }
        }

        public ITransferResult Transfer(
          ILocation source,
          IList<ILocation> destinations,
          IGetObserver observer)
        {
            return this.Transfer(source, destinations, observer, true);
        }

        public ITransferResult Transfer(
          ILocation source,
          IList<ILocation> destinations,
          IGetObserver observer,
          bool preserveFlags)
        {
            using (TransferOperation transferOperation = new TransferOperation((IControllerService)this, source))
            {
                transferOperation.PreserveFlagsOnPut = preserveFlags;
                return transferOperation.Transfer(destinations, observer);
            }
        }

        public ErrorCodes PushOut()
        {
            using (PushOutDiskOperation outDiskOperation = new PushOutDiskOperation((IControllerService)this))
            {
                ErrorCodes errorCodes = outDiskOperation.Execute();
                if (errorCodes != ErrorCodes.Success)
                {
                    LogHelper.Instance.WithContext(LogEntryType.Error, "Push out disk returned error status {0}", (object)errorCodes.ToString().ToUpper());
                    ServiceLocator.Instance.GetService<IControlSystem>().LogPickerSensorState(LogEntryType.Error);
                }
                return errorCodes;
            }
        }

        public ErrorCodes ClearGripper()
        {
            using (ClearGripperOperation gripperOperation = new ClearGripperOperation())
            {
                ErrorCodes errorCodes = gripperOperation.Execute();
                if (errorCodes != ErrorCodes.Success)
                {
                    LogHelper.Instance.WithContext(LogEntryType.Error, "Clear gripper returned error status {0}", (object)errorCodes.ToString().ToUpper());
                    ServiceLocator.Instance.GetService<IControlSystem>().LogPickerSensorState(LogEntryType.Error);
                }
                return errorCodes;
            }
        }

        public IVendItemResult VendItemInPicker()
        {
            return this.VendItemInPicker(ControllerConfiguration.Instance.VendDiskPollCount);
        }

        public IVendItemResult VendItemInPicker(int attempts)
        {
            using (VendItemOperation vendItemOperation = new VendItemOperation(attempts))
            {
                VendItemResult vendItemResult = vendItemOperation.Execute();
                if (!vendItemResult.Presented)
                    LogHelper.Instance.WithContext(LogEntryType.Error, "VendItemInPicker: The disk was not presented to the user from the vend door.");
                else if (ErrorCodes.PickerFull == vendItemResult.Status || ErrorCodes.PickerEmpty == vendItemResult.Status)
                    LogHelper.Instance.WithContext(false, LogEntryType.Info, "Vend item in picker returned status {0}", (object)vendItemResult.Status.ToString().ToUpper());
                else
                    LogHelper.Instance.WithContext(LogEntryType.Error, "Vend item in picker returned error code {0}", (object)vendItemResult.Status.ToString().ToUpper());
                return (IVendItemResult)vendItemResult;
            }
        }

        public ErrorCodes AcceptDiskAtDoor()
        {
            using (AcceptDiskOperation acceptDiskOperation = new AcceptDiskOperation())
            {
                ErrorCodes errorCodes = acceptDiskOperation.Execute();
                if (ErrorCodes.PickerEmpty == errorCodes || ErrorCodes.PickerFull == errorCodes)
                    LogHelper.Instance.WithContext(false, LogEntryType.Info, "Accept disk in picker returned status {0}", (object)errorCodes);
                else
                    LogHelper.Instance.WithContext("Accept disk at door returned error {0}", (object)errorCodes.ToString().ToUpper());
                return errorCodes;
            }
        }

        public ErrorCodes RejectDiskInPicker()
        {
            return this.RejectDiskInPicker(ControllerConfiguration.Instance.RejectAtDoorAttempts);
        }

        public ErrorCodes RejectDiskInPicker(int attempts)
        {
            using (RejectDiskOperation rejectDiskOperation = new RejectDiskOperation(attempts))
            {
                ErrorCodes errorCodes = rejectDiskOperation.Execute();
                if (ErrorCodes.PickerEmpty == errorCodes || ErrorCodes.PickerFull == errorCodes)
                    LogHelper.Instance.WithContext(false, LogEntryType.Info, "Reject disk returned status {0}", (object)errorCodes.ToString().ToUpper());
                else
                    LogHelper.Instance.WithContext(LogEntryType.Error, "Reject disk returned error {0}", (object)errorCodes.ToString().ToUpper());
                return errorCodes;
            }
        }

        private void OnCheckInventory(string expected, IFormattedLog log)
        {
            IInventoryService service = ServiceLocator.Instance.GetService<IInventoryService>();
            ILocation currentLocation = ServiceLocator.Instance.GetService<IMotionControlService>().CurrentLocation;
            int deck = currentLocation.Deck;
            int slot = currentLocation.Slot;
            ILocation location = service.Get(deck, slot);
            if (!(location.ID != expected))
                return;
            LogHelper.Instance.WithContext(false, LogEntryType.Error, "** INVENTORY CHECK ERROR** - location shows ID {0} expected {1}", (object)location.ID, (object)expected);
        }

        private GetResult OnGet(IGetObserver observer, ILocation location)
        {
            if (location == null)
            {
                GetResult getResult = new GetResult(location);
                getResult.Update(ErrorCodes.LocationOutOfRange);
                return getResult;
            }
            IFormattedLog contextLog = ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext().ContextLog;
            using (GetOperation getOperation = new GetOperation(location, observer, contextLog))
            {
                GetResult getResult = getOperation.Execute();
                if (getResult.IsSlotEmpty)
                {
                    LogHelper.Instance.WithContext(string.Format("GET {0} returned SLOTEMPTY", (object)location.ToString()));
                    return getResult;
                }
                if (!getResult.Success)
                {
                    LogHelper.Instance.WithContext(LogEntryType.Error, string.Format("GET {0} returned error status {1}", (object)location.ToString(), (object)getResult.ToString().ToUpper()));
                    return getResult;
                }
                string msg = string.Format("GET {0} ID={1}", (object)location.ToString(), (object)getResult.Previous);
                contextLog.WriteFormatted(msg);
                this.OnCheckInventory("EMPTY", contextLog);
                return getResult;
            }
        }

        private PutResult OnPut(IPutObserver observer, string id, ILocation location)
        {
            IControlSystem service = ServiceLocator.Instance.GetService<IControlSystem>();
            IPickerSensorReadResult sensorReadResult = service.ReadPickerSensors();
            if (!sensorReadResult.Success)
                return new PutResult(id, location)
                {
                    Code = ErrorCodes.SensorReadError
                };
            if (!sensorReadResult.IsFull)
            {
                LogHelper.Instance.WithContext(LogEntryType.Error, "PUT: picker is empty.");
                service.LogPickerSensorState();
                service.LogInputs(ControlBoards.Picker, LogEntryType.Info);
                return new PutResult(id, location)
                {
                    Code = ErrorCodes.PickerEmpty
                };
            }
            string str = ServiceLocator.Instance.GetService<IDumpbinService>().IsBin(location) ? "DUMPBIN" : string.Format("Deck = {0} Slot = {1}", (object)location.Deck, (object)location.Slot);
            IFormattedLog contextLog = ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext().ContextLog;
            using (PutOperation putOperation = new PutOperation(id, observer, location, contextLog))
            {
                PutResult putResult = putOperation.Execute();
                if (putResult.Success)
                {
                    contextLog.WriteFormatted(string.Format("PUT {0} ID={1}", (object)str, (object)id));
                    this.OnCheckInventory(id, contextLog);
                }
                else
                    LogHelper.Instance.WithContext(LogEntryType.Error, string.Format("PUT {0} ID={1} returned error status {2}", (object)str, (object)id, (object)putResult.ToString().ToUpper()));
                return putResult;
            }
        }
    }
}
