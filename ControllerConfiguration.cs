using Microsoft.Win32;
using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Attributes;
using Redbox.HAL.Component.Model.Extensions;
using Redbox.HAL.Configuration;
using Redbox.HAL.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class ControllerConfiguration : AttributeXmlConfiguration
    {
        internal readonly DecksManager Manager = new DecksManager();
        private MerchandizingMode MerchMode;

        public static ControllerConfiguration Instance => Singleton<ControllerConfiguration>.Instance;

        public string[] GetMotionControllerTypes()
        {
            return new string[1] { "Arcus" };
        }

        public void ConvertDeckConfigurationToXml(XmlWriter writer)
        {
            this.Manager.ToPropertyXml(writer);
        }

        public void UpdateDeckConfigurationFromXml(XmlNode propertyNode)
        {
            this.Manager.UpdateFromPropertyXml(propertyNode);
        }

        [Category("Configuration")]
        [DisplayName("Decks")]
        [CustomEditor("(Decks Collection)", GetMethodName = "ConvertDeckConfigurationToXml", SetMethodName = "UpdateDeckConfigurationFromXml")]
        [Description("The physical description of the decks for the host of the connected HAL Service.")]
        public int DecksCount => this.Manager.DeckCount;

        [Category("Behavior")]
        [DisplayName("Gripper Push Timeout")]
        [Description("The amount of time the gripper will overrun the limit switch beforing raising a timeout.")]
        [XmlConfiguration(DefaultValue = "350")]
        public int PushTime { get; set; }

        [Category("Behavior")]
        [DisplayName("Gripper Extend Timeout")]
        [Description("The amount of time the gripper will extend the arm before timing out; used when putting a DVD into the slot.")]
        [XmlConfiguration(DefaultValue = "120")]
        public int RollInExtendTime { get; set; }

        [Category("Behavior")]
        [DisplayName("Test Extend Timeout")]
        [Description("The amount of time the gripper will overrun the limit switch beforing raising a timeout while testing for a DVD in the slot.")]
        [XmlConfiguration(DefaultValue = "800")]
        public int TestExtendTime { get; set; }

        [Category("Behavior")]
        [DisplayName("Vend Position Receive Offset")]
        [Description("The amount of backoff to back off when receiving an item from the vend position.")]
        [XmlConfiguration(DefaultValue = "100")]
        public int VendPositionReceiveOffset { get; set; }

        [Category("Behavior")]
        [DisplayName("Arcus Write Pause")]
        [Description("Controls how long (in milliseconds) HAL waits for the Arcus controller to fill its internal buffer before reading it.")]
        [XmlConfiguration(DefaultValue = "20")]
        public int ArcusWritePause { get; set; }

        [Category("Behavior")]
        [DisplayName("Arcus Motor Query Pause")]
        [Description("Controls how long (in milliseconds) HAL waits between MST commands during a move.")]
        [XmlConfiguration(DefaultValue = "75")]
        public int ArcusMotorQueryPause { get; set; }

        [Category("Configuration")]
        [DisplayName("Y Position for Vend")]
        [Description("Known as the vend position, this is the point on the Y axis that is equal to the vend door, allowing the picker to vend and return disks. This must be a negative integer value.")]
        [XmlConfiguration(DefaultValue = "-86200")]
        public int VendYPosition { get; set; }

        [Category("Behavior")]
        [DisplayName("Y drop back for HOMEY")]
        [Description("The amount of back off movement on the Y axis during a HOMEY instruction.  This value should be used only on older machines where the upper Y axis limit switch is too close to the Y axis home switch.")]
        [XmlConfiguration(DefaultValue = "0")]
        public int HomeYDropBack { get; set; }

        [Category("Behavior")]
        [DisplayName("Attempts to put an item away.")]
        [Description("The number of attempts to try and put something in the picker away.")]
        [XmlConfiguration(DefaultValue = "4")]
        public int PutAwayItemAttempts { get; set; }

        [Category("Behavior")]
        [DisplayName("Number of Pulls")]
        [Description("The number of times the picker will attempt to pull the disk from the slot.")]
        [XmlConfiguration(DefaultValue = "2")]
        public int NumberOfPulls { get; set; }

        [Category("Configuration")]
        [DisplayName("Motion Controller Port Name")]
        [ValidValueListProvider("GetComPorts")]
        [Description("The COM port to which to which the motion controller board is connected.")]
        [XmlConfiguration(DefaultValue = "COM3")]
        public string MotionControllerPortName { get; set; }

        [Category("Configuration")]
        [DisplayName("Motion Controller Timeout (ms)")]
        [Description("The read timeout from the Motion Controller COM Port in milliseconds.")]
        [XmlConfiguration(DefaultValue = "5000")]
        public int MotionControllerTimeout { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("X Motor Gear")]
        [Description("The X axis motor gear configuration.  The configuration of this property and its children affect the X Axis Speed properties.")]
        public MotorGear GearX { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("Y Motor Gear")]
        [Description("The Y axis motor gear configuration.  The configuration of this property and its children affect the Y Axis Speed properties.")]
        public MotorGear GearY { get; private set; }

        [Category("Configuration")]
        [DisplayName("Controller Port Name")]
        [ValidValueListProvider("GetComPorts")]
        [Description("The COM port the main, picker, auxiliary, and alternate motion controller boards are connected to.")]
        [XmlConfiguration(DefaultValue = "COM1")]
        public string ControllerPortName { get; set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("X Axis Initialization Speed")]
        [Description("The speed of the X axis for the HOMEX instruction.")]
        public MotorSpeed InitAxisXSpeed { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("Y Axis Initialization Speed")]
        [Description("The speed of the Y axis for the HOMEY instruction.")]
        public MotorSpeed InitAxisYSpeed { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("Y Axis Speed")]
        [Description("The speed of the Y axis for the MOVE instruction.")]
        public MotorSpeed MoveAxisYSpeed { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("X Axis Speed")]
        [Description("The speed of the X axis for the MOVE instruction.")]
        public MotorSpeed MoveAxisXSpeed { get; private set; }

        [Recurse]
        [ExcludeType]
        [Category("Configuration")]
        [DisplayName("XY Axis Speed")]
        [Description("The speed of the combined X and Y axis for the MOVE instruction.")]
        public MotorSpeed MoveAxisXYSpeed { get; private set; }

        [Category("Configuration")]
        [DisplayName("Sync Ringlight Warmup Time")]
        [Description("If value is greater than 0, wait value ms after turning on the ringlight during sync.")]
        [XmlConfiguration(DefaultValue = "1500")]
        public int RinglightWarmupPause2 { get; set; }

        [Category("Configuration")]
        [DisplayName("Ringlight Shutdown Pause")]
        [Description("If value is greater than 0, wait value ms after turning off the ringlight.")]
        [XmlConfiguration(DefaultValue = "500")]
        public int RinglightShutdownPause { get; set; }

        [Category("Configuration")]
        [DisplayName("IR Ringlight Startup Pause")]
        [Description("If value is greater than 0, wait value ms after turning off the ringlight.")]
        [XmlConfiguration(DefaultValue = "1000")]
        public int IRRinglightStartupPause { get; set; }

        [Category("Behavior")]
        [DisplayName("Move Timeout")]
        [Description("The timeout (in milliseconds) for an ARCUS move.")]
        [XmlConfiguration(DefaultValue = "30000")]
        public int MoveTimeout { get; set; }

        [Category("Behavior")]
        [DisplayName("Qlm Timed Extend")]
        [Description("If set to true, will time-extend the fingers using the QlmExtendTime, ignoring the extend sensor.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool QlmTimedExtend { get; set; }

        [Category("Behavior")]
        [DisplayName("Timeout for a gripper extend/retract operation.")]
        [Description("The timeout value for a gripper extend/retract operation.")]
        [XmlConfiguration(DefaultValue = "2000")]
        public int GripperArmExtendRetractTimeout { get; set; }

        [Category("Configuration")]
        [DisplayName("Validate Position After Move")]
        [Description("If set to Yes, validate the position after a move.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool PrintEncoderPositionAfterMove2 { get; set; }

        [Category("Behavior")]
        [DisplayName("Additional PUT push.")]
        [Description("If set to true ( the default ), PUT will perform an extra gripper push to seat the disk.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool AdditionalPutPush { get; set; }

        [Category("Behavior")]
        [DisplayName("Reboot Kiosk During QLM")]
        [Description("If set to true ( the default ), qlm/thin/qlm-thin will reboot when detecting an Arcus problem after engage/disengage.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool RebootKioskDuringQlm { get; set; }

        [Category("Behavior")]
        [DisplayName("Dump VMZ State")]
        [Description("If set to true, clean/thin will dump the zone after the job.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool DumpVMZState { get; set; }

        [Category("Behavior")]
        [DisplayName("Vend Disk Poll Count")]
        [Description("The number of times, during a VEND, to poll for a taken disk.")]
        [XmlConfiguration(DefaultValue = "15")]
        public int VendDiskPollCount { get; set; }

        [Category("Behavior")]
        [DisplayName("Sync Error Threshold")]
        [Description("The number of errors encountered before exiting a sync.")]
        [XmlConfiguration(DefaultValue = "20")]
        public int SyncErrorThreshold { get; set; }

        [Category("Behavior")]
        [DisplayName("Reject At Door Attempts")]
        [Description("The number of attempts to reject a disk at the vend door.")]
        [XmlConfiguration(DefaultValue = "10")]
        public int RejectAtDoorAttempts { get; set; }

        [Category("Behavior")]
        [DisplayName("Error Sync On Camera Failure")]
        [Description("The number of errors encountered before exiting a sync.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool ErrorSyncOnCameraFailure { get; set; }

        [Category("Behavior")]
        [DisplayName("Move Vend Door To AUX Sensor")]
        [Description("If true, will try to move the vend door to the appropriate sensor on the AUX board.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool MoveVendDoorToAuxSensor { get; set; }

        [Category("Behavior")]
        [DisplayName("Query Position For Vend Move")]
        [Description("If true, query the current position before moving; if at the target position, wont' request a move.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool QueryPositionForVendMove { get; set; }

        [Category("Behavior")]
        [DisplayName("QLM Approach Offset")]
        [Description("If true, will try to move the vend door to the appropriate sensor on the AUX board.")]
        [XmlConfiguration(DefaultValue = "500")]
        public int QlmApproachOffset { get; set; }

        [Category("Behavior")]
        [DisplayName("QLM Y Offset")]
        [Description("If true, will try to move the vend door to the appropriate sensor on the AUX board.")]
        [XmlConfiguration(DefaultValue = "50")]
        public int QlmYOffset { get; set; }

        [Category("Behavior")]
        [DisplayName("QLM Extend Timeout")]
        [Description("The amount of time the gripper will overrun the limit switch beforing raising a timeout while doing a grab on the QLM.")]
        [XmlConfiguration(DefaultValue = "1500")]
        public int QlmExtendTime { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Min Init Wakeup Time")]
        [Description("The time, in milliseconds, for the init job to wait for the ports to come up.")]
        [XmlConfiguration(DefaultValue = "5000")]
        public int MinInitWakeupTime { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Allow Program Cache Replacement")]
        [Description("If set to true, allows the program cache to accept overwrites of programs.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool AllowProgramCacheReplacement2 { get; set; }

        [Category("Behavior")]
        [DisplayName("Check Gripper Arm Sensors On Move")]
        [Description("If set to true, will check both the forward and retract sensors if they're both set, and if they are, won't move.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool CheckGripperArmSensorsOnMove { get; set; }

        [Category("Behavior")]
        [DisplayName("Gripper Rent On Move")]
        [Description("If set to true, will check if the gripper is in the rent position, and if not, will move it to Rent.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool GripperRentOnMove { get; set; }

        [Category("Behavior")]
        [DisplayName("Mark Duplicate Unknown")]
        [Description("If set to true, mark duplicate barcodes as UNKNOWN.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool MarkDuplicatesUnknown { get; set; }

        [Category("Behavior")]
        [DisplayName("Mark Original Matrix Unknown")]
        [Description("If set to true, when a duplicate is recognized, will mark the original barcode as UNKNOWN.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool MarkOriginalMatrixUnknown { get; set; }

        [Category("Behavior")]
        [DisplayName("Rotate Drum During Unknown Removal")]
        [Description("If set to true, will do a slot test, and if the slot is full, will rotate the drum 5 slots during an unknown removal if unable to grab a disk.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool RotateDrumDuringUnknownRemoval2 { get; set; }

        [Category("Behavior")]
        [DisplayName("Leave Duplicate Result In Return")]
        [Description("If set to true, will leave a CAMERA CAPTURE result during a return job if duplicates are detected.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool LeaveDuplicateResultInReturn { get; set; }

        [Category("Behavior")]
        [DisplayName("Test Slot On Empty")]
        [Description("If set to true, perform a PEEK (test slot) on the slot if GET returns SLOTEMPTY.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool TestSlotOnEmpty { get; set; }

        [Category("Behavior")]
        [DisplayName("Mark Location Unknown Threshold")]
        [Description("The count for errors before the slot is marked UNKNOWN.")]
        [XmlConfiguration(DefaultValue = "2")]
        public int MarkLocationUnknownThreshold { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Track Push Out Failures")]
        [Description("If set to true, will fail a vend after 2 push out tries show any sensors 1-4 lit.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool TrackPushOutFailures { get; set; }

        [Category("Behavior")]
        [DisplayName("Track Problem Locations")]
        [Description("If set to true, will track problems with slots.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool TrackProblemLocations { get; set; }

        [Category("Configuration")]
        [DisplayName("Push Out Sleep Time")]
        [Description("If greater than 0, will spin for the configured time in ms after clearing sensor 4 during a vend disk.")]
        [XmlConfiguration(DefaultValue = "50")]
        public int PushOutSleepTime2 { get; set; }

        [Category("Behavior")]
        [DisplayName("AggressiveClearPickerOnPut")]
        [Description("If true, try to clear the picker more aggressively during a PUT.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool AggressiveClearPickerOnPut { get; set; }

        [Category("Configuration")]
        [DisplayName("EmptyStuckAlertThreshold")]
        [Description("If Empty or stuck count exceeds this number during vend in a 24 hour period, generate a result that should trigger an alert.")]
        [XmlConfiguration(DefaultValue = "15")]
        public int EmptyStuckAlertThreshold { get; set; }

        [Category("Behavior")]
        [DisplayName("Alert For Empty Stuck")]
        [Description("If true, will leave a RESULT code for alert generation when the empty/stuck counter has exceeded its threshold.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool AlertForEmptyStuck { get; set; }

        [Category("Behavior")]
        [DisplayName("Fix Camera On Init")]
        [Description("If true, will test if the camera is generating images, and if not, will try and reset it.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool FixCameraOnInit2 { get; set; }

        [Category("Behavior")]
        [DisplayName("Validate Controller Home Status")]
        [Description("If true, tells the hardware-status job to include the init status of the motion controller.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool ValidateControllerHomeStatus { get; set; }

        [Category("Configuration")]
        [DisplayName("Reboot Execution Prevention Window")]
        [Description("In minutes, the time before kiosk reboot that the scheduler prevents jobs from running.")]
        [XmlConfiguration(DefaultValue = "10")]
        public int RebootExecutionPreventionWindow { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Command Trace")]
        [Description("If true, will dump diagnostic information to the log about commands.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableCommandTrace { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Arcus Trace")]
        [Description("If true, will dump diagnostic information to the log about Arcus commands.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableArcusTrace { get; set; }

        [Category("Configuration")]
        [DisplayName("Return Slot Buffer")]
        [Description("The number of slots the kiosk must not to try and file disks to.")]
        [XmlConfiguration(DefaultValue = "1")]
        public int ReturnSlotBuffer { get; set; }

        [Category("Configuration")]
        [DisplayName("Quick Return Power Off Window")]
        [Description("The number of slots the kiosk must not to try and file disks to.")]
        [XmlConfiguration(DefaultValue = "0:6")]
        public string QuickReturnPowerOffWindow { get; set; }

        [Category("Configuration")]
        [DisplayName("Quick Return Port")]
        [Description("The COM port on which the Quick return device is configured.")]
        [XmlConfiguration(DefaultValue = "NONE")]
        public string QuickReturnComPort { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Widen Arcus Tolerance")]
        [Description("If true, will give the arcus a larger tolerance window.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool WidenArcusTolerance { get; set; }

        [Category("Behavior")]
        [DisplayName("Take Invalid Disk On Return")]
        [Description("If true, will take the disk if the barcode can't be read.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool TakeInvalidDiskOnReturn { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Door Check In Status")]
        [Description("If set to true, will check the door sensors during the hardware status job.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableDoorCheckInStatus { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Check Gen4 Camera On Init")]
        [Description("If set to true, will establish the correct driver is loaded and the device is enabled.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool CheckGen4CameraOnInit { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Camera Reset")]
        [Description("If set to true, will reset the camera during sync & return operations.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableCameraReset { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Fix Camera After Return")]
        [Description("If set to true, will reset the camera at the very end of return.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool FixCameraAfterReturn { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Threshold for Camera alert")]
        [Description("An integer describing how many consecutive failures happen before an alert is generated from return.")]
        [XmlConfiguration(DefaultValue = "2")]
        public int ReturnCameraAlertThreshold { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Reset Timeout Counters Weekly")]
        [Description("If set to true, will reset the hardware timeout counters every week on Sunday morning.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool ResetTimeoutCountersWeekly { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Restart Controller During User Jobs")]
        [Description("If true, will restart the controller during a user job.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool RestartControllerDuringUserJobs { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Track Hardware Corrections")]
        [Description("If true, will track hardware correction stats.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool TrackHardwareCorrections { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Hardware Healing")]
        [Description("If true, will schedule a healing test.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableHardwareHealing { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Arcus Healing")]
        [Description("Sets the mask of options for the hardware healer.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableArcusHealing { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Camera Healing")]
        [Description("Sets the mask of options for the hardware healer.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableCameraHealing { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Leiwe Healing")]
        [Description("Sets the mask of options for the hardware healer.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableLeiweHealing { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Ice Qube Polling")]
        [Description("If true, will enable polling & control of Ice Qube board.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableIceQubePolling { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Watchdog Image Day Age")]
        [Description("In days, the age of images to be deleted.")]
        [XmlConfiguration(DefaultValue = "90")]
        public int WatchdogImageDayAge { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Applog Day Age")]
        [Description("In days, the age of applogs to be deleted.")]
        [XmlConfiguration(DefaultValue = "60")]
        public int ApplogDayAge { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Fix Arcus During Status")]
        [Description("If true, will try to correct an Arcus comm error during the hardware status job.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool FixArcusDuringStatusJob { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Secure Disk Validator")]
        [Description("If true, will enable secure disk check in security service.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableSecureDiskValidator { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Peek During Vend")]
        [Description("If true, will PEEK slot during vend.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool PeekDuringVend { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Test Slot During Removal")]
        [Description("If true, will PEEK slot during vend-unknown.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool TestSlotDuringRemoval { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Wait Sensor Pause Time")]
        [Description("In ms, the time wait sensor routine will pause between reads.")]
        [XmlConfiguration(DefaultValue = "100")]
        public int WaitSensorPauseTime { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Clear Picker On Home")]
        [Description("If true, will clear the picker during HOME operations.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool ClearPickerOnHome { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Lower Limit As Error")]
        [Description("If true, will treat lower limit as error during normal moves.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool LowerLimitAsError { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Unrecognized Barcode Reject")]
        [Description("If true, will reject disks not recognized as Redbox barcodes during return.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnableUnrecognizedBarcodeReject { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Arcus Smooth Move")]
        [Description("If true, will use smooth move motion.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool ArcusSmoothMove { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Preposition Vend Fraud Move")]
        [Description("If true, move below the vend door until auth signal arrives.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool PrepositionVendFraudMove { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Hardware Healer Pause Time")]
        [Description("In ms, the time the hw healer job will pause between checks.")]
        [XmlConfiguration(DefaultValue = "900000")]
        public int HardwareHealerPauseTime { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Router Power Cycle Pause")]
        [Description("In ms, the pause between router power cycle.")]
        [XmlConfiguration(DefaultValue = "0")]
        public int RouterPowerCyclePause { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Clear Sensor Pause Delay")]
        [Description("In ms, the pause between sensor reads during clear sensor.")]
        [XmlConfiguration(DefaultValue = "300")]
        public int ClearSensorPauseDelay { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Track Dirty Shutdown")]
        [Description("If true will test for dirty shutdown & capture the event.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool TrackDirtyShutdown { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Handle Hardware Correction Events")]
        [Description("If true will allow contexts to handle correction events.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool HandleHardwareCorrectionEvents { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Validate Inputs Read Response")]
        [Description("If true, will attempt response validation from controller.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool ValidateInputsReadResponse { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Picker Sensor Spin Time")]
        [Description("In ms, the time to spin during a picker sensor read.")]
        [XmlConfiguration(DefaultValue = "20")]
        public int PickerSensorSpinTime { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Inventory Database Check")]
        [Description("If true, will monitor the status of the inventory datastore.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableInventoryDatabaseCheck { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("CCR Watchdog Sleep")]
        [Description("In ms, the amount of time the CCR watchdog sleeps before checking the CCR.")]
        [XmlConfiguration(DefaultValue = "1800000")]
        public int CCRWatchdogSleep { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Inventory Backup Pause")]
        [Description("In ms, the amount of time the inventory backup watchdog will pause between backups.")]
        [XmlConfiguration(DefaultValue = "0")]
        public int InventoryBackupPause { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable VMZ Trace")]
        [Description("If true, will write detailed info about VMZ operations.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool EnableVMZTrace { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Default Roll Sensor Timeout")]
        [Description("In ms, the default timeout for roll to sensor operations.")]
        [XmlConfiguration(DefaultValue = "6000")]
        public int DefaultRollSensorTimeout { get; set; }

        [Browsable(false)]
        [Category("Configuration")]
        [DisplayName("Accept Disk Timeout")]
        [Description("In ms, the default timeout for accepting the disk at the door.")]
        [XmlConfiguration(DefaultValue = "15000")]
        public int AcceptDiskTimeout { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Enable Photocopy Scan")]
        [Description("If true, will perform a scan for photocopy disks.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool EnablePhotocopyScan { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Disable Auto Exposure At Init")]
        [Description("If true, will disable auto exposure on gen 4 cameras if set.")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableAutoExposureAtInit { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Auto License Cortex Decoder")]
        [Description("If true, will attempt to license the Cortex decoding software automatically.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool AutoLicenseCortexDecoder { get; set; }

        [Category("Configuration")]
        [DisplayName("Ringlight Shutdown Pause")]
        [Description("If value is greater than 0, wait value ms after turning off the ringlight.")]
        [XmlConfiguration(DefaultValue = "500")]
        public int IRRinglightShutdownPause { get; set; }

        [Category("Configuration")]
        [DisplayName("Signal Wait Timeout")]
        [Description("The time to wait for a signal from a client.")]
        [XmlConfiguration(DefaultValue = "30000")]
        public int SignalWaitTimeout { get; set; }

        [Browsable(false)]
        [Category("Behavior")]
        [DisplayName("Retry Read On No Markers Found")]
        [Description("If true, will retake image and scan for fraud markers.  Depends on EnableCameraReset to be set to true.")]
        [XmlConfiguration(DefaultValue = "true")]
        public bool RetryReadOnNoMarkersFound { get; set; }

        [Category("Behavior")]
        [DisplayName("Additional Fraud Read Attempts")]
        [Description("The number of attempts to try and read a disc without fraud markers. will recenter in some scenarios.")]
        [XmlConfiguration(DefaultValue = "1")]
        public int AdditionalFraudReadAttempts { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Check Drivers")]
        [Description("MS Hal Tester KFC Tab Disable Check Drivers Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCCheckDrivers { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Decode Test")]
        [Description("MS Hal Tester KFC Tab Disable Decode Test Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCDecodeTest { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Test Vend Door")]
        [Description("MS Hal Tester KFC Tab Disable Test Vend Door Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCTestVendDoor { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Init")]
        [Description("MS Hal Tester KFC Tab Disable Init Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCInit { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Vertical Slot Test")]
        [Description("MS Hal Tester KFC Tab Disable Vertical Slot Test Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCVerticalSlotTest { get; set; }

        [Category("Configuration")]
        [DisplayName("Disable KFC Unknown Count")]
        [Description("MS Hal Tester KFC Tab Disable Unknown Count Button")]
        [XmlConfiguration(DefaultValue = "false")]
        public bool DisableKFCUnknownCount { get; set; }

        [Browsable(false)]
        public bool IsVMZMachine => this.MerchMode != 0;

        protected override void ImportInner(ErrorList errors)
        {
            GampHelper gampHelper = new GampHelper();
            int platterSlots = gampHelper.GetPlatterSlots();
            if (platterSlots == -1)
            {
                errors.Add(Error.NewError("H322", "The Slotdata.dat file was not found or had no entries.", "Check the C:\\gamp folder to ensure the file exists."));
            }
            else
            {
                int registrySlotCount = this.RegistrySlotCount;
                if (platterSlots != registrySlotCount)
                {
                    errors.Add(Error.NewError("H323", "Configuration mismatch error.", string.Format("The machine is configured for {0} slots; the gamp data says there are {1} slots.", (object)registrySlotCount, (object)platterSlots)));
                }
                else
                {
                    int? nullable1 = new int?();
                    int? nullable2 = new int?();
                    List<int> intList1 = new List<int>();
                    List<bool> boolList = new List<bool>();
                    IDictionary<string, int> dictionary = gampHelper.ReadLegacySystemDataFile();
                    if (dictionary.Count == 0)
                    {
                        errors.Add(Error.NewError("H321", "The SystemData.dat file was not found or had no properties.", "Check the C:\\gamp folder to ensure the file exists."));
                    }
                    else
                    {
                        if (dictionary.ContainsKey("PushTime"))
                            this.PushTime = dictionary["PushTime"];
                        if (dictionary.ContainsKey("RollInExtendTime"))
                            this.RollInExtendTime = dictionary["RollInExtendTime"];
                        if (dictionary.ContainsKey("TestExtendTime"))
                            this.TestExtendTime = dictionary["TestExtendTime"];
                        if (dictionary.ContainsKey("PickExtendTime"))
                            this.QlmExtendTime = dictionary["PickExtendTime"];
                        if (dictionary.ContainsKey("QLMaproch"))
                            this.QlmApproachOffset = dictionary["QLMaproch"];
                        if (dictionary.ContainsKey("QLMZoffset"))
                            this.QlmYOffset = dictionary["QLMZoffset"];
                        if (dictionary.ContainsKey("ReceivePositionOffSet"))
                            this.VendPositionReceiveOffset = dictionary["ReceivePositionOffSet"];
                        if (dictionary.ContainsKey("ArcusComPort"))
                            this.MotionControllerPortName = string.Format("COM{0}", (object)dictionary["ArcusComPort"]);
                        if (dictionary.ContainsKey("ControlComPort"))
                            this.ControllerPortName = string.Format("COM{0}", (object)dictionary["ControlComPort"]);
                        this.VendYPosition = dictionary.ContainsKey("VendPosition") ? dictionary["VendPosition"] : -86200;
                        if (dictionary.ContainsKey("ZHomeDropBack"))
                            this.HomeYDropBack = -dictionary["ZHomeDropBack"];
                        this.NumberOfPulls = dictionary.ContainsKey("NumberOfPulls") ? dictionary["NumberOfPulls"] : 2;
                        if (dictionary.ContainsKey("SellThrughOffSet"))
                            nullable2 = new int?(dictionary["SellThrughOffSet"]);
                        if (dictionary.ContainsKey("QLMDeckNumber"))
                            nullable1 = new int?(dictionary["QLMDeckNumber"]);
                        this.GearX = new MotorGear(dictionary.ContainsKey("RGearRatio") ? (double)dictionary["RGearRatio"] : 10.0, dictionary.ContainsKey("RPulseRatio") ? dictionary["RPulseRatio"] : 398, dictionary.ContainsKey("REncoderRatio") ? dictionary["REncoderRatio"] : 114, dictionary.ContainsKey("RStepRes") ? dictionary["RStepRes"] : 8);
                        this.GearY = new MotorGear(dictionary.ContainsKey("ZGearRatio") ? (double)dictionary["ZGearRatio"] : 20.0, 0, 0, dictionary.ContainsKey("ZStepRes") ? dictionary["ZStepRes"] : 4);
                        this.InitAxisXSpeed = new MotorSpeed(dictionary.ContainsKey("InitAxis1LS") ? dictionary["InitAxis1LS"] : 600, dictionary.ContainsKey("InitAxis1HS") ? dictionary["InitAxis1HS"] : 3000, 300, this.GearX);
                        this.InitAxisYSpeed = new MotorSpeed(dictionary.ContainsKey("InitAxis2LS") ? dictionary["InitAxis2LS"] : 800, dictionary.ContainsKey("InitAxis2HS") ? dictionary["InitAxis2HS"] : 2400, 300, this.GearY);
                        this.MoveAxisYSpeed = new MotorSpeed(500, dictionary.ContainsKey("MoveZHS") ? dictionary["MoveZHS"] : 15000, dictionary.ContainsKey("MoveZAcc") ? dictionary["MoveZAcc"] : 500, this.GearY);
                        this.MoveAxisXSpeed = new MotorSpeed(500, dictionary.ContainsKey("MoveRHS") ? dictionary["MoveRHS"] : 15000, dictionary.ContainsKey("MoveRAcc") ? dictionary["MoveRAcc"] : 500, this.GearY);
                        this.MoveAxisXYSpeed = new MotorSpeed(500, dictionary.ContainsKey("MoveZRHS") ? dictionary["MoveZRHS"] : 15000, dictionary.ContainsKey("MoveZRAcc") ? dictionary["MoveZRAcc"] : 500, this.GearY);
                        int num1 = 1;
                        while (true)
                        {
                            string key = string.Format("PlatterMaxSlots{0}", (object)num1++);
                            if (dictionary.ContainsKey(key))
                            {
                                int num2 = dictionary.ContainsKey(key) ? dictionary[key] : 90;
                                intList1.Add(num2);
                            }
                            else
                                break;
                        }
                        int num3 = 1;
                        while (true)
                        {
                            string key = string.Format("DeckSellStat{0}", (object)num3++);
                            if (dictionary.ContainsKey(key))
                                boolList.Add(dictionary[key] == 1);
                            else
                                break;
                        }
                        List<List<int>> intListList = gampHelper.ReadLegacySlotDataFile(false);
                        this.Manager.Clear();
                        int num4 = 1;
                        Range[] rangeArray = new Range[5]
                        {
              new Range(1, 1),
              new Range(1, 19),
              new Range(20, 40),
              new Range(41, 61),
              new Range(62, 80)
                        };
                        foreach (List<int> intList2 in intListList)
                        {
                            int num5 = num4;
                            int? nullable3 = nullable1;
                            int valueOrDefault = nullable3.GetValueOrDefault();
                            int num6 = num5 == valueOrDefault & nullable3.HasValue ? 80 : intList1[num4 - 1];
                            List<Quadrant> quadrantList = new List<Quadrant>();
                            for (int index = 1; index < intList2.Count && intList2[index] != 0; ++index)
                            {
                                if (nullable1.HasValue && num4 == nullable1.Value && index < rangeArray.Length)
                                    quadrantList.Add(new Quadrant(intList2[index], (IRange<int>)rangeArray[index]));
                                else
                                    quadrantList.Add(new Quadrant(intList2[index]));
                            }
                            Decimal num7 = 166.6667M;
                            if (nullable1.HasValue && num4 == nullable1.Value)
                                num7 = 177.7M;
                            bool flag = nullable1.HasValue && num4 == nullable1.Value;
                            DecksManager manager = this.Manager;
                            int number = num4;
                            int yoffset = intList2[0];
                            int num8 = flag ? 1 : 0;
                            int numberOfSlots = num6;
                            Decimal slotWidth = num6 == 72 ? 173.3M : num7;
                            int? sellThruSlots;
                            if (num6 != 72)
                            {
                                nullable3 = new int?();
                                sellThruSlots = nullable3;
                            }
                            else
                                sellThruSlots = new int?(6);
                            int? sellThruOffset;
                            if (num6 != 72)
                            {
                                nullable3 = new int?();
                                sellThruOffset = nullable3;
                            }
                            else
                                sellThruOffset = nullable2;
                            Quadrant[] array = quadrantList.ToArray();
                            Deck deck = new Deck(number, yoffset, num8 != 0, numberOfSlots, slotWidth, sellThruSlots, sellThruOffset, array);
                            manager.Add((IDeck)deck);
                            ++num4;
                        }
                    }
                }
            }
        }

        protected override void UpgradeInner(XmlDocument document, ErrorList errors)
        {
            document.DocumentElement.SelectSingleNodeAndSetValue<bool>("Controller/AggressiveClearPickerOnPut", false);
            document.DocumentElement.SelectSingleNodeAndSetValue<bool>("Controller/RestartControllerDuringUserJobs", true);
            document.DocumentElement.SelectSingleNodeAndSetValue<bool>("Controller/TrackHardwareCorrections", true);
            document.DocumentElement.SelectSingleNodeAndSetValue<bool>("Controller/PrepositionVendFraudMove", true);
        }

        protected override void StorePropertiesInner(XmlDocument document, ErrorList errors)
        {
            this.GearX.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/GearX"));
            this.GearY.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/GearY"));
            this.InitAxisXSpeed.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/InitAxisXSpeed"));
            this.InitAxisYSpeed.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/InitAxisYSpeed"));
            this.MoveAxisXSpeed.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/MoveXAxisSpeed"));
            this.MoveAxisYSpeed.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/MoveYAxisSpeed"));
            this.MoveAxisXYSpeed.SaveToXml(document.DocumentElement.SelectSingleNode("Controller/MoveXYAxisSpeed"));
            this.Manager.SaveConfiguration(document);
        }

        protected override void LoadPropertiesInner(XmlDocument document, ErrorList errors)
        {
            XmlElement documentElement = document.DocumentElement;
            XmlNode node1 = document.DocumentElement.SelectSingleNode("Controller/GearX");
            if (node1 == null)
            {
                errors.Add(Error.NewError("P002", "The required GearX node does not exist.", "Add a valid GearX node to the configuration file."));
            }
            else
            {
                this.GearX = MotorGear.FromXmlNode(node1);
                XmlNode node2 = document.DocumentElement.SelectSingleNode("Controller/GearY");
                if (node2 == null)
                {
                    errors.Add(Error.NewError("P002", "The required GearY node does not exist.", "Add a valid GearY node to the configuration file."));
                }
                else
                {
                    this.GearY = MotorGear.FromXmlNode(node2);
                    XmlNode node3 = document.DocumentElement.SelectSingleNode("Controller/InitAxisXSpeed");
                    if (node3 == null)
                    {
                        errors.Add(Error.NewError("P003", "The required InitAxisXSpeed node does not exist.", "Add a valid InitAxisXSpeed node to the configuration file."));
                    }
                    else
                    {
                        this.InitAxisXSpeed = MotorSpeed.FromXmlNode(node3, this.GearX);
                        XmlNode node4 = document.DocumentElement.SelectSingleNode("Controller/InitAxisYSpeed");
                        if (node4 == null)
                        {
                            errors.Add(Error.NewError("P003", "The required InitYAxisSpeed node does not exist.", "Add a valid InitYAxisSpeed node to the configuration file."));
                        }
                        else
                        {
                            this.InitAxisYSpeed = MotorSpeed.FromXmlNode(node4, this.GearY);
                            XmlNode node5 = document.DocumentElement.SelectSingleNode("Controller/MoveXAxisSpeed");
                            if (node5 == null)
                            {
                                errors.Add(Error.NewError("P004", "The required MoveXAxisSpeed node does not exist.", "Add a valid MoveXAxisSpeed node to the configuration file."));
                            }
                            else
                            {
                                this.MoveAxisXSpeed = MotorSpeed.FromXmlNode(node5, this.GearX);
                                XmlNode node6 = document.DocumentElement.SelectSingleNode("Controller/MoveYAxisSpeed");
                                if (node6 == null)
                                {
                                    errors.Add(Error.NewError("P004", "The required MoveYAxisSpeed node does not exist.", "Add a valid MoveYAxisSpeed node to the configuration file."));
                                }
                                else
                                {
                                    this.MoveAxisYSpeed = MotorSpeed.FromXmlNode(node6, this.GearY);
                                    XmlNode node7 = document.DocumentElement.SelectSingleNode("Controller/MoveXYAxisSpeed");
                                    if (node7 == null)
                                    {
                                        errors.Add(Error.NewError("P004", "The required MoveXYAxisSpeed node does not exist.", "Add a valid MoveXYAxisSpeed node to the configuration file."));
                                    }
                                    else
                                    {
                                        this.MoveAxisXYSpeed = MotorSpeed.FromXmlNode(node7, this.GearY);
                                        XmlNode decksNode = document.DocumentElement.SelectSingleNode("Controller/Decks");
                                        if (decksNode == null)
                                        {
                                            LogHelper.Instance.Log("There are no decks nodes in the configuration file.");
                                            errors.Add(Error.NewError("P006", "No configured decks.", "There are no decks nodes in the configuration file."));
                                        }
                                        else
                                        {
                                            this.Manager.Initialize(decksNode);
                                            int registrySlotCount = this.RegistrySlotCount;
                                            IDeck first = this.Manager.First;
                                            if (registrySlotCount != first.NumberOfSlots)
                                                errors.Add(Error.NewError("P005", "Machine configuration mismatch.", string.Format("The store key shows {0}, but the deck shows {1}", (object)registrySlotCount, (object)first.NumberOfSlots)));
                                            this.MerchMode = this.Manager.QlmDeck == null ? MerchandizingMode.VMZ_Gen2 : MerchandizingMode.QLM;
                                            LogHelper.Instance.Log("Kiosk is configured for merchandizing mode {0}", (object)this.MerchMode.ToString().ToUpper());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override string FileNodeName => "Controller";

        internal void Initialize()
        {
            ServiceLocator.Instance.GetService<IConfigurationService>().RegisterConfiguration(Configurations.Controller.ToString(), (IAttributeXmlConfiguration)this);
            ServiceLocator.Instance.AddService<IDecksService>((object)this.Manager);
        }

        internal int RegistrySlotCount
        {
            get
            {
                int registrySlotCount = 90;
                RegistryKey registryKey = (RegistryKey)null;
                try
                {
                    registryKey = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Redbox\\REDS\\Kiosk Engine\\Store");
                    if (registryKey != null)
                    {
                        object obj = registryKey.GetValue("MaxSlots");
                        if (obj != null)
                            registrySlotCount = ConversionHelper.ChangeType<int>(obj);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Instance.Log("Unable to read secret option 'MaxSlots'", ex);
                    registrySlotCount = -1;
                }
                finally
                {
                    registryKey?.Close();
                }
                return registrySlotCount;
            }
        }

        private ControllerConfiguration()
          : base("controllerConfig", typeof(ControllerConfiguration))
        {
            this.GearY = new MotorGear(20.0, 0, 0, 4);
            this.GearX = new MotorGear(10.0, 398, 114, 8);
            this.InitAxisXSpeed = new MotorSpeed(500, 3000, 300, this.GearX);
            this.InitAxisYSpeed = new MotorSpeed(600, 1500, 300, this.GearY);
            this.MoveAxisYSpeed = new MotorSpeed(500, 15000, 500, this.GearY);
            this.MoveAxisXSpeed = new MotorSpeed(500, 15000, 500, this.GearX);
            this.MoveAxisXYSpeed = new MotorSpeed(500, 15000, 500, this.GearY);
        }
    }
}
