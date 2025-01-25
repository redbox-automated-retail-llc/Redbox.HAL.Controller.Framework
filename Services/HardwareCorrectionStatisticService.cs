using Redbox.HAL.Component.Model;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework.Services
{
    internal sealed class HardwareCorrectionStatisticService : IHardwareCorrectionStatisticService
    {
        private readonly ITableTypeFactory TypeFactory;
        private readonly IDataTableService DataTableService;
        private readonly IDataTable<IHardwareCorrectionStatistic> StatsTable;

        public bool Insert(HardwareCorrectionEventArgs args, IExecutionContext context)
        {
            return this.Insert(args.Statistic, context, args.CorrectionOk);
        }

        public bool Insert(
          HardwareCorrectionStatistic type,
          IExecutionContext context,
          bool correctionOk)
        {
            return this.Insert(type, context, correctionOk, DateTime.Now);
        }

        public bool Insert(
          HardwareCorrectionStatistic type,
          IExecutionContext context,
          bool correctionOk,
          DateTime ts)
        {
            return !ControllerConfiguration.Instance.TrackHardwareCorrections || this.StatsTable.Insert(this.TypeFactory.NewStatistic(type, context.ProgramName, correctionOk, ts));
        }

        public bool RemoveAll()
        {
            try
            {
                this.StatsTable.DeleteAll();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("[HardwareCorrectionStatisticService] Failed to delete all correction entries.", ex);
                return false;
            }
        }

        public bool RemoveAll(HardwareCorrectionStatistic stat)
        {
            try
            {
                List<IHardwareCorrectionStatistic> stats = this.GetStats(stat);
                using (new DisposeableList<IHardwareCorrectionStatistic>((IList<IHardwareCorrectionStatistic>)stats))
                    return this.StatsTable.Delete((IList<IHardwareCorrectionStatistic>)stats);
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log("[HardwareCorrectionStatisticService] Failed to delete HardwareCorrection info.", ex);
                return false;
            }
        }

        public List<IHardwareCorrectionStatistic> GetStats() => this.StatsTable.LoadEntries();

        public List<IHardwareCorrectionStatistic> GetStats(HardwareCorrectionStatistic key)
        {
            List<IHardwareCorrectionStatistic> list = this.StatsTable.LoadEntries();
            using (new DisposeableList<IHardwareCorrectionStatistic>((IList<IHardwareCorrectionStatistic>)list))
                return list.FindAll((Predicate<IHardwareCorrectionStatistic>)(each => each.Statistic == key));
        }

        internal HardwareCorrectionStatisticService(IDataTableService dts)
        {
            this.DataTableService = dts;
            this.TypeFactory = ServiceLocator.Instance.GetService<ITableTypeFactory>();
            this.StatsTable = this.DataTableService.GetTable<IHardwareCorrectionStatistic>();
            if (this.StatsTable.Exists)
                return;
            LogHelper.Instance.Log("[HardwareCorrectionStatisticsService] Stats table doesn't exist; create returned {0}", this.StatsTable.Create() ? (object)"SUCCESS" : (object)"FAILURE");
        }
    }
}
