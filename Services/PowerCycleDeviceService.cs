using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Timers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Redbox.HAL.Controller.Framework.Services
{
    internal sealed class PowerCycleDeviceService : IPowerCycleDeviceService
    {
        private readonly Dictionary<PowerCycleDevices, IPowerCycleDevice> Devices = new Dictionary<PowerCycleDevices, IPowerCycleDevice>();

        public IPowerCycleDevice Get(PowerCycleDevices device)
        {
            return !this.Devices.ContainsKey(device) ? (IPowerCycleDevice)null : this.Devices[device];
        }

        internal PowerCycleDeviceService(IRuntimeService rts)
        {
            using (ExecutionTimer executionTimer = new ExecutionTimer())
            {
                string assemblyFile = string.Empty;
                try
                {
                    assemblyFile = rts.RuntimePath(Assembly.GetExecutingAssembly().GetName().Name + ".dll");
                    Type type1 = typeof(IPowerCycleDevice);
                    foreach (Type type2 in Assembly.LoadFrom(assemblyFile).GetTypes())
                    {
                        if (type1.IsAssignableFrom(type2) && !type2.IsInterface)
                        {
                            PowerCycleDeviceAttribute[] customAttributes = (PowerCycleDeviceAttribute[])type2.GetCustomAttributes(typeof(PowerCycleDeviceAttribute), false);
                            if (customAttributes.Length != 0)
                            {
                                IPowerCycleDevice instance = (IPowerCycleDevice)Activator.CreateInstance(type2, true);
                                if (!this.Devices.ContainsKey(customAttributes[0].Device))
                                    this.Devices[customAttributes[0].Device] = instance;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Instance.Log(string.Format("[PowerCycleDeviceService]Unable to load assembly '{0}' to scan for power cycle devices.", (object)assemblyFile), ex);
                }
                executionTimer.Stop();
                LogHelper.Instance.Log("[PowerCycleDeviceService] Time to scan for {0} power cycle devices: {1}", (object)this.Devices.Keys.Count, (object)executionTimer.Elapsed);
            }
        }
    }
}
