using Redbox.HAL.Component.Model;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class KioskFunctionCheckService : IKioskFunctionCheckService
    {
        private readonly IDataTable<IKioskFunctionCheckData> Table;

        public bool Add(IKioskFunctionCheckData data) => this.Table.Insert(data);

        public IList<IKioskFunctionCheckData> Load()
        {
            return (IList<IKioskFunctionCheckData>)this.Table.LoadEntries();
        }

        public int CleanOldEntries() => this.Table.Clean();

        internal KioskFunctionCheckService()
        {
            this.Table = ServiceLocator.Instance.GetService<IDataTableService>().GetTable<IKioskFunctionCheckData>();
            if (this.Table.Exists)
                return;
            LogHelper.Instance.Log("[KioskFunctionCheckService] KFC table doesn't exist; create returned {0}", this.Table.Create() ? (object)"SUCCESS" : (object)"FAILURE");
        }
    }
}
