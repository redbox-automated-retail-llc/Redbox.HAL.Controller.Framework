using Redbox.HAL.Component.Model;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework.Services
{
    internal sealed class PersistentCounterService : IPersistentCounterService
    {
        private readonly IDataTable<IPersistentCounter> CounterTable;
        private readonly List<string> WeeklyResettableCounters = new List<string>();
        private readonly Dictionary<string, IPersistentCounter> Counters = new Dictionary<string, IPersistentCounter>();

        public IPersistentCounter Find(string name)
        {
            if (this.Counters.ContainsKey(name))
                return this.Counters[name];
            IPersistentCounter p = ServiceLocator.Instance.GetService<ITableTypeFactory>().NewCounter(name, 0);
            if (this.CounterTable.Insert(p))
            {
                this.Counters[name] = p;
                return p;
            }
            LogHelper.Instance.Log("[PersistentCounterService] Unable to create counter {0}", (object)name);
            return (IPersistentCounter)null;
        }

        public IPersistentCounter Find(TimeoutCounters counter) => this.Find(this.KeyFrom(counter));

        public IPersistentCounter Increment(string name)
        {
            try
            {
                IPersistentCounter persistentCounter = this.Find(name);
                if (persistentCounter == null)
                    return (IPersistentCounter)null;
                persistentCounter.Increment();
                return this.CounterTable.Update(persistentCounter) ? persistentCounter : (IPersistentCounter)null;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log(string.Format("FindAndIncrementCounter ( name = {0} ) caught an exception.", (object)name), ex);
                return (IPersistentCounter)null;
            }
        }

        public IPersistentCounter Increment(TimeoutCounters counter)
        {
            return this.Increment(this.KeyFrom(counter));
        }

        public IPersistentCounter Decrement(string name)
        {
            try
            {
                IPersistentCounter persistentCounter = this.Find(name);
                if (persistentCounter == null)
                    return (IPersistentCounter)null;
                persistentCounter.Decrement();
                return this.CounterTable.Update(persistentCounter) ? persistentCounter : (IPersistentCounter)null;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log(string.Format("[PersistentCounterService] Decrement counter ( {0} ) caught an exception.", (object)name), ex);
                return (IPersistentCounter)null;
            }
        }

        public IPersistentCounter Decrement(TimeoutCounters counter)
        {
            return this.Decrement(this.KeyFrom(counter));
        }

        public bool Reset(IPersistentCounter counter)
        {
            if (counter == null)
                return false;
            if (counter.Value == 0)
                return true;
            counter.Reset();
            return this.CounterTable.Update(counter);
        }

        public bool Reset(string name)
        {
            IPersistentCounter counter = this.Find(name);
            return counter != null && this.Reset(counter);
        }

        public void AddWeeklyResettable(IPersistentCounter counter)
        {
            if (this.WeeklyResettableCounters.Contains(counter.Name))
                return;
            this.WeeklyResettableCounters.Add(counter.Name);
        }

        public void ResetWeekly()
        {
            List<IPersistentCounter> persistentCounterList = new List<IPersistentCounter>();
            using (new DisposeableList<IPersistentCounter>((IList<IPersistentCounter>)persistentCounterList))
            {
                foreach (string resettableCounter in this.WeeklyResettableCounters)
                {
                    IPersistentCounter persistentCounter = this.Find(resettableCounter);
                    if (persistentCounter != null && persistentCounter.Value != 0)
                    {
                        persistentCounter.Reset();
                        persistentCounterList.Add(persistentCounter);
                    }
                }
                this.CounterTable.Update((IList<IPersistentCounter>)persistentCounterList);
            }
        }

        internal PersistentCounterService(IDataTableService dts)
        {
            foreach (TimeoutCounters counter in Enum.GetValues(typeof(TimeoutCounters)))
            {
                string str = this.KeyFrom(counter);
                if (!this.WeeklyResettableCounters.Contains(str))
                    this.WeeklyResettableCounters.Add(str);
            }
            this.CounterTable = dts.GetTable<IPersistentCounter>();
            List<IPersistentCounter> list = this.CounterTable.LoadEntries();
            using (new DisposeableList<IPersistentCounter>((IList<IPersistentCounter>)list))
            {
                foreach (IPersistentCounter persistentCounter in list)
                    this.Counters[persistentCounter.Name] = persistentCounter;
            }
        }

        private string KeyFrom(TimeoutCounters counter)
        {
            return string.Format("{0}Timeout", (object)counter.ToString());
        }
    }
}
