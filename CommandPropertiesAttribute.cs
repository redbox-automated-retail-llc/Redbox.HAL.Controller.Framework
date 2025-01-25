using System;
using System.Reflection;

namespace Redbox.HAL.Controller.Framework
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class CommandPropertiesAttribute : Attribute
    {
        public CommandPropertiesAttribute()
        {
            this.StatusBit = this.WaitPauseTime = this.CommandWait = -1;
            this.Address = AddressSelector.None;
        }

        public static CommandPropertiesAttribute GetProperties(FieldInfo fieldInfo)
        {
            CommandPropertiesAttribute[] customAttributes = (CommandPropertiesAttribute[])Attribute.GetCustomAttributes((MemberInfo)fieldInfo, typeof(CommandPropertiesAttribute));
            return customAttributes != null && customAttributes.Length != 0 ? customAttributes[0] : (CommandPropertiesAttribute)null;
        }

        public int StatusBit { get; set; }

        public string Command { get; set; }

        public int CommandWait { get; set; }

        public string ResetCommand { get; set; }

        public AddressSelector Address { get; set; }

        public int WaitPauseTime { get; set; }
    }
}
