using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class PutOperation : AbstractOperation<PutResult>
    {
        private readonly string ID;
        private readonly IPutObserver Observer;
        private readonly ILocation PutLocation;
        private readonly PutResult Result;
        private readonly IFormattedLog Log;
        private const string AggressivePutFailuresCounter = "AggressivePutFailures";
        private const string AggressivePutSuccessCounter = "AggressivePutSuccesses";

        public override PutResult Execute()
        {
            this.Result.Code = this.OnCorePut();
            if (this.Result.Code != ErrorCodes.Success)
                this.Observer.OnFailedPut((IPutResult)this.Result, this.Log);
            else
                this.Observer.OnSuccessfulPut((IPutResult)this.Result, this.Log);
            return this.Result;
        }

        internal PutOperation(
          string id,
          IPutObserver observer,
          ILocation putLocation,
          IFormattedLog log)
        {
            this.Result = new PutResult(id, putLocation);
            this.Log = log;
            this.PutLocation = putLocation;
            this.ID = id;
            this.Observer = observer;
        }

        private ErrorCodes OnCorePut()
        {
            if (this.PutLocation.IsWide)
            {
                int num1 = (int)this.SettleDiskInSlot();
            }
            if (this.Controller.TrackState != TrackState.Closed && !this.Controller.TrackClose().Success)
                return ErrorCodes.TrackCloseTimeout;
            if (!this.Controller.RetractArm().Success)
                return ErrorCodes.GripperRetractTimeout;
            if (!this.Controller.SetFinger(GripperFingerState.Rent).Success)
            {
                LogHelper.Instance.Log("PUT: SetGripperFinger to Rent timed out.", LogEntryType.Error);
                return ErrorCodes.GripperRentTimeout;
            }
            this.Controller.StartRollerIn();
            int num2 = this.ClearDiskFromPicker() ? 1 : 0;
            this.RuntimeService.SpinWait(100);
            if (num2 != 0)
            {
                LogHelper.Instance.WithContext(LogEntryType.Error, "PUT: Disk cannot be placed into {0} because it cannot clear sensor 2.", (object)this.PutLocation.ToString());
                this.Controller.LogPickerSensorState(LogEntryType.Error);
                this.Controller.StopRoller();
                this.RuntimeService.SpinWait(100);
                if (!this.Controller.TrackOpen().Success)
                    return ErrorCodes.TrackOpenTimeout;
                this.Controller.TrackClose();
                this.RuntimeService.SpinWait(100);
                if (this.Controller.RollerToPosition(RollerPosition.Position5, 8000).Success)
                    return this.OnSlotInUse();
                LogHelper.Instance.Log("Put: roll item back to sensor 5 returned TIMEOUT");
                LogHelper.Instance.Log("PUT: unable to roll item into slot, and cannot move it back into the picker.", LogEntryType.Error);
                this.Controller.LogPickerSensorState(LogEntryType.Error);
                return ErrorCodes.PickerObstructed;
            }
            if (LogHelper.Instance.IsLevelEnabled(LogEntryType.Debug))
                LogHelper.Instance.Log("Sensor wait for 2, 5 didn't time out.", LogEntryType.Info);
            ErrorCodes errorCodes = this.PushIntoSlot();
            if (errorCodes != ErrorCodes.Success)
                LogHelper.Instance.Log(LogEntryType.Error, "PUT: item_cleared_gripper: PushIntoSlot returned {0}", (object)errorCodes.ToString().ToUpper());
            this.Controller.StopRoller();
            IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
            if (!sensorReadResult1.Success)
                return ErrorCodes.SensorReadError;
            if (!sensorReadResult1.IsInputActive(PickerInputs.Sensor1))
            {
                this.UpdateInventory();
                return ErrorCodes.Success;
            }
            LogHelper.Instance.Log("[PUT] can't put disk in slot.", LogEntryType.Error);
            this.Controller.LogPickerSensorState(LogEntryType.Error);
            if (ControllerConfiguration.Instance.AggressiveClearPickerOnPut)
                return this.OnAggressivePut();
            if (!this.Controller.TrackOpen().Success)
                return ErrorCodes.TrackOpenTimeout;
            int num3 = (int)this.PullFrom(this.PutLocation);
            if (!this.Controller.TrackClose().Success)
                return ErrorCodes.TrackCloseTimeout;
            if (!this.Controller.RollerToPosition(RollerPosition.Position5, 5000).Success)
            {
                this.Controller.LogPickerSensorState(LogEntryType.Error);
                this.RuntimeService.SpinWait(100);
                this.Controller.StopRoller();
                return ErrorCodes.PickerObstructed;
            }
            IPickerSensorReadResult sensorReadResult2 = this.Controller.ReadPickerSensors();
            return sensorReadResult2.IsInputActive(PickerInputs.Sensor1) || sensorReadResult2.IsInputActive(PickerInputs.Sensor6) ? ErrorCodes.PickerObstructed : this.OnSlotInUse();
        }

        private ErrorCodes OnAggressivePut()
        {
            int num1 = (int)this.Controller.TrackCycle();
            this.Controller.StartRollerIn();
            int num2 = (int)this.PushIntoSlot();
            this.Controller.StopRoller();
            IPickerSensorReadResult sensorReadResult1 = this.Controller.ReadPickerSensors();
            if (!sensorReadResult1.Success)
                return ErrorCodes.SensorReadError;
            if (!sensorReadResult1.IsInputActive(PickerInputs.Sensor1))
            {
                this.UpdateAggressivePutCounter();
                this.UpdateInventory();
                return ErrorCodes.Success;
            }
            this.Controller.TrackOpen();
            int num3 = (int)this.PushIntoSlot();
            this.Controller.TrackClose();
            IPickerSensorReadResult sensorReadResult2 = this.Controller.ReadPickerSensors();
            if (!sensorReadResult2.Success)
                return ErrorCodes.SensorReadError;
            if (!sensorReadResult2.IsInputActive(PickerInputs.Sensor1))
            {
                this.UpdateInventory();
                this.UpdateAggressivePutCounter();
                return ErrorCodes.Success;
            }
            LogHelper.Instance.Log("AggressivePut not able to clear the disk.", LogEntryType.Error);
            this.Controller.LogPickerSensorState(LogEntryType.Error);
            this.PersistentCounterService.Increment("AggressivePutFailures");
            this.Controller.StartRollerOut();
            int num4 = (int)this.PullFrom(this.PutLocation);
            LogHelper.Instance.Log(LogEntryType.Error, "[PUT] gripperClear: false, rollerTo 5 returned {0}", (object)this.Controller.RollerToPosition(RollerPosition.Position5, 5000).ToString());
            this.Controller.ReadPickerSensors().Log(LogEntryType.Error);
            this.RuntimeService.SpinWait(100);
            this.Controller.StopRoller();
            return ErrorCodes.PickerObstructed;
        }

        private ErrorCodes OnSlotInUse()
        {
            this.PutLocation.ID = "UNKNOWN";
            this.InventoryService.Save(this.PutLocation);
            return ErrorCodes.SlotInUse;
        }

        private void UpdateAggressivePutCounter()
        {
            LogHelper.Instance.Log("Aggressive PUT was able to clear disk.", LogEntryType.Info);
            this.PersistentCounterService.Increment("AggressivePutSuccesses");
        }

        private void UpdateInventory()
        {
            ILocation original = (ILocation)null;
            this.Result.IsDuplicate = this.InventoryService.IsBarcodeDuplicate(this.ID, out original);
            this.Result.StoredMatrix = this.IsSuspiciousId(this.ID) ? "UNKNOWN" : this.ID;
            if (this.PutLocation.Deck == 7 && this.PutLocation.IsWide)
                this.Result.StoredMatrix = "UNKNOWN";
            if (this.Result.IsDuplicate)
            {
                ServiceLocator.Instance.GetService<IExecutionService>().GetActiveContext();
                this.Result.OriginalMatrixLocation = original;
                this.Result.StoredMatrix = ControllerConfiguration.Instance.MarkDuplicatesUnknown ? "UNKNOWN" : string.Format("{0}{1}", (object)this.ID, (object)"(DUPLICATE)");
                LogHelper.Instance.WithContext("The ID {0} is a duplicate ( original at Deck = {1} Slot = {2} ); marking as {3}", (object)this.ID, (object)original.Deck, (object)original.Slot, (object)this.Result.StoredMatrix);
                if (ControllerConfiguration.Instance.MarkOriginalMatrixUnknown)
                {
                    LogHelper.Instance.WithContext("Mark the original matrix ID = {0} at Deck = {1} Slot = {2} ); marking as {3}", (object)this.ID, (object)original.Deck, (object)original.Slot, (object)"UNKNOWN");
                    original.ID = "UNKNOWN";
                    this.InventoryService.Save(original);
                }
                this.PersistentCounterService.Increment("DUPLICATE-COUNT");
            }
            this.PutLocation.ID = this.Result.StoredMatrix;
            this.InventoryService.Save(this.PutLocation);
        }

        private bool IsSuspiciousId(string id)
        {
            if (InventoryConstants.IsKnownInventoryToken(id))
                return false;
            try
            {
                return Enum.IsDefined(typeof(ErrorCodes), (object)id);
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
