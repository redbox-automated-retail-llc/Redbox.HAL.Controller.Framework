using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class GetOperation : AbstractOperation<GetResult>
    {
        private IFormattedLog Log;
        private readonly ILocation Location;
        private readonly IGetObserver Observer;
        private readonly IDeck Deck;
        private readonly int Slot;
        private readonly GetResult Result;

        public override GetResult Execute()
        {
            if (this.Validate())
                this.FetchDisk();
            return this.Result;
        }

        protected override void OnDispose() => this.Log = (IFormattedLog)null;

        internal GetOperation(ILocation location, IGetObserver observer, IFormattedLog log)
        {
            this.Observer = observer;
            this.Log = log;
            this.Deck = ServiceLocator.Instance.GetService<IDecksService>().GetFrom(location);
            this.Slot = location.Slot;
            this.Location = location;
            this.Result = new GetResult(this.Location);
        }

        private void FetchDisk()
        {
            if (this.Location.IsWide)
            {
                int num1 = (int)this.SettleDiskInSlot();
            }
            if (!this.Controller.TrackOpen().Success)
            {
                this.Result.Update(ErrorCodes.TrackOpenTimeout);
            }
            else
            {
                this.Controller.StartRollerOut();
                ErrorCodes newError = this.PullFrom(this.Location);
                if (newError != ErrorCodes.Success)
                {
                    this.Controller.StopRoller();
                    this.Controller.TrackClose();
                    this.Result.Update(newError);
                }
                else if (!this.Controller.TrackClose().Success)
                {
                    this.Controller.StopRoller();
                    int num2 = (int)this.PushIntoSlot();
                    this.Result.Update(ErrorCodes.TrackCloseTimeout);
                }
                else
                {
                    IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
                    if (!sensorReadResult1.Success)
                    {
                        this.Result.Update(sensorReadResult1.Error);
                    }
                    else
                    {
                        if (sensorReadResult1.IsInputActive(PickerInputs.Sensor1) && !sensorReadResult1.IsInputActive(PickerInputs.Sensor2))
                            this.PullFrom(this.Location, 1);
                        this.RuntimeService.SpinWait(300);
                        IInventoryService service = ServiceLocator.Instance.GetService<IInventoryService>();
                        if (this.Controller.RollerToPosition(RollerPosition.Position4, 6000, false).Success)
                        {
                            service.Reset(this.Location);
                        }
                        else
                        {
                            IPickerSensorReadResult sensorReadResult2 = this.Controller.ReadPickerSensors();
                            if (!sensorReadResult2.Success)
                                this.Result.Update(ErrorCodes.SensorReadError);
                            else if (sensorReadResult2.IsFull)
                            {
                                LogHelper.Instance.WithContext(false, LogEntryType.Info, "[GET] Disk did not make it to sensor 4.");
                                sensorReadResult2.Log(LogEntryType.Error);
                                this.OnStuck();
                            }
                            else
                            {
                                LogHelper.Instance.WithContext(false, LogEntryType.Info, "[GET] no disk in picker after pull.");
                                sensorReadResult2.Log();
                                this.Result.Update(ErrorCodes.SlotEmpty);
                                if (!this.Observer.OnEmpty((IGetResult)this.Result))
                                    return;
                                service.Reset(this.Location);
                            }
                        }
                    }
                }
            }
        }

        private void OnStuck()
        {
            this.Controller.StartRollerIn();
            try
            {
                bool flag = this.ClearDiskFromPicker();
                int num = (int)this.PushIntoSlot();
                LogHelper.Instance.WithContext(false, LogEntryType.Error, "[GET] couldn't get disc, ClearDiscFromPicker returned {0}", flag ? (object)"TIMEOUT" : (object)"SUCCESS");
                IPickerSensorReadResult sensorReadResult = this.Controller.ReadPickerSensors();
                sensorReadResult.Log(LogEntryType.Error);
                if (!sensorReadResult.Success)
                    this.Result.Update(ErrorCodes.SensorReadError);
                else
                    this.Result.Update(sensorReadResult.IsFull ? ErrorCodes.PickerObstructed : ErrorCodes.ItemStuck);
            }
            finally
            {
                this.Controller.StopRoller();
            }
            this.Observer.OnStuck((IGetResult)this.Result);
        }

        private bool Validate()
        {
            if (this.Controller.TrackState != TrackState.Closed && !this.Controller.TrackClose().Success)
            {
                this.Result.Update(ErrorCodes.TrackCloseTimeout);
                return false;
            }
            if (!this.Controller.ReadPickerSensors().IsFull)
                return true;
            this.Result.Update(ErrorCodes.PickerFull);
            return false;
        }
    }
}
