using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Timers;
using System;
using System.Reflection;
using System.Text;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class CoreCommand
    {
        internal readonly int OperationTimeout;
        internal readonly ICommPort Port;
        internal readonly int? StatusBit;
        internal readonly int CommandWait;
        internal readonly string ResetCommand;
        internal readonly AddressSelector Address;
        internal readonly int WaitPauseTime;
        internal readonly string CommandName;
        private static readonly char[] CommaSplit = new char[1]
        {
      ','
        };

        internal string Command { get; private set; }

        internal static CoreCommand Create(CommandType type, int? timeout, ICommPort port)
        {
            return new CoreCommand(type, timeout, port);
        }

        internal static CoreCommand Create(CommandType type, ICommPort port)
        {
            return new CoreCommand(type, new int?(), port);
        }

        internal CoreResponse Execute()
        {
            using (CommandTrace trace = new CommandTrace(ControllerConfiguration.Instance.EnableCommandTrace))
            {
                trace.Trace("[CoreCommand] Executing command {0}", (object)this.CommandName);
                CoreResponse coreResponse = this.SendCommand(this.Command, trace);
                try
                {
                    if (coreResponse.CommError || !this.StatusBit.HasValue)
                        return coreResponse;
                    coreResponse.Error = this.WaitForCommand(trace);
                    if (coreResponse.TimedOut && !string.IsNullOrEmpty(this.ResetCommand))
                        Array.ForEach<string>(this.ResetCommand.Split(CoreCommand.CommaSplit, StringSplitOptions.RemoveEmptyEntries), (Action<string>)(s => this.SendCommand(s, trace)));
                    return coreResponse;
                }
                finally
                {
                    trace.Trace("[CoreCommand] {0} returned {1}", (object)this.CommandName, (object)coreResponse.ToString());
                }
            }
        }

        private ErrorCodes WaitForCommand(CommandTrace trace)
        {
            IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
            TimeSpan timespan = new TimeSpan(0, 0, 0, 0, this.WaitPauseTime);
            trace.Enter();
            trace.Trace("[WaitForCommand] Start.");
            try
            {
                using (ExecutionTimer executionTimer = new ExecutionTimer())
                {
                    do
                    {
                        service.SpinWait(timespan);
                        CoreResponse coreResponse = this.SendCommand("S", trace);
                        if (coreResponse.CommError)
                            return ErrorCodes.CommunicationError;
                        trace.Trace("[WaitForCommand] {0}", (object)coreResponse.OpCodeResponse);
                        if (!coreResponse.IsBitSet(this.StatusBit.Value))
                            return ErrorCodes.Success;
                    }
                    while (executionTimer.ElapsedMilliseconds <= (long)this.OperationTimeout);
                    return ErrorCodes.Timeout;
                }
            }
            finally
            {
                trace.Exit();
            }
        }

        private CoreResponse SendCommand(string command, CommandTrace trace)
        {
            trace.Enter();
            try
            {
                CoreResponse coreResponse = new CoreResponse(this.Address);
                using (IChannelResponse channelResponse = this.Port.SendRecv(this.Address.ToString(), 5000))
                {
                    coreResponse.Error = !channelResponse.CommOk ? ErrorCodes.CommunicationError : ErrorCodes.Success;
                    if (!channelResponse.CommOk)
                    {
                        LogHelper.Instance.Log(" [SendCommand] Selector {0} ( port = {1} )communication error.", (object)this.Address.ToString(), (object)this.Port.DisplayName);
                        coreResponse.Diagnostic = this.ComputeCommunicationError(this.Address);
                        return coreResponse;
                    }
                    trace.Trace("[SendCommand] Command {0} selector response {1}", (object)command, (object)Encoding.ASCII.GetString(channelResponse.RawResponse));
                }
                using (IChannelResponse channelResponse = this.Port.SendRecv(command, this.CommandWait))
                {
                    coreResponse.Error = !channelResponse.CommOk ? ErrorCodes.CommunicationError : ErrorCodes.Success;
                    if (channelResponse.CommOk)
                    {
                        coreResponse.OpCodeResponse = Encoding.ASCII.GetString(channelResponse.RawResponse);
                        trace.Trace("[SendCommand] Command {0} response {1}", (object)command, (object)coreResponse.OpCodeResponse);
                    }
                    else
                        LogHelper.Instance.Log(" [SendCommand] Command {0} on address {1} ( port = {2} ) communication error.", (object)command, (object)this.Address.ToString(), (object)this.Port.DisplayName);
                    return coreResponse;
                }
            }
            finally
            {
                trace.Exit();
            }
        }

        private string ComputeCommunicationError(AddressSelector address)
        {
            string str;
            switch (address)
            {
                case AddressSelector.H001:
                    str = "PCB";
                    break;
                case AddressSelector.H002:
                    str = "AUX board";
                    break;
                case AddressSelector.H101:
                    str = "SER board";
                    break;
                case AddressSelector.H555:
                    str = "QR device";
                    break;
                default:
                    str = string.Format("Unknown board {0}", (object)address.ToString());
                    break;
            }
            string message = string.Format("{0} is not responsive.", (object)str);
            LogHelper.Instance.Log(message, LogEntryType.Error);
            return message;
        }

        private CoreCommand(CommandType type, int? timeout, ICommPort port)
        {
            this.Port = port;
            this.CommandName = type.ToString();
            FieldInfo field = typeof(CommandType).GetField(type.ToString());
            if (field == null)
            {
                LogHelper.Instance.Log("[CoreCommand] Could not locate type field on {0}", (object)this.CommandName);
            }
            else
            {
                CommandPropertiesAttribute properties = CommandPropertiesAttribute.GetProperties(field);
                if (properties == null)
                {
                    LogHelper.Instance.Log("[CoreCommand] Could not locate PropertiesAttribute on {0}", (object)this.CommandName);
                }
                else
                {
                    this.Command = properties.Command;
                    this.Address = properties.Address;
                    this.ResetCommand = properties.ResetCommand;
                    this.WaitPauseTime = -1 != properties.WaitPauseTime ? properties.WaitPauseTime : 0;
                    this.OperationTimeout = timeout.GetValueOrDefault();
                    this.StatusBit = new int?();
                    if (-1 != properties.StatusBit)
                    {
                        this.StatusBit = new int?(properties.StatusBit);
                        if (this.OperationTimeout == 0 || this.WaitPauseTime == 0)
                            LogHelper.Instance.Log("[CoreCommand] On CommandType {0} there is a status bit but Timeout = {1} and WaitPause = {2}", (object)this.CommandName, (object)this.OperationTimeout, (object)this.WaitPauseTime);
                    }
                    this.CommandWait = -1 != properties.CommandWait ? properties.CommandWait : 8000;
                }
            }
        }
    }
}
