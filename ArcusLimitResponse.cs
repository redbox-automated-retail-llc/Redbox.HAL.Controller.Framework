using Redbox.HAL.Component.Model;
using System;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class ArcusLimitResponse : IMotionControlLimitResponse
    {
        public bool IsLimitBlocked(MotionControlLimits limit)
        {
            if (!this.ReadOk || this.Limits == null || limit == MotionControlLimits.None)
                throw new InvalidOperationException("Limits not setup.");
            foreach (IMotionControlLimit limit1 in this.Limits)
            {
                if (limit1.Limit == limit)
                    return limit1.Blocked;
            }
            LogHelper.Instance.Log("[ArcusLimitResponse] Unable to find limit {0}", (object)limit.ToString());
            return true;
        }

        public bool ReadOk { get; private set; }

        public IMotionControlLimit[] Limits { get; private set; }

        internal ArcusLimitResponse(string queryResponse)
        {
            this.ReadOk = false;
            this.Limits = (IMotionControlLimit[])null;
            if (queryResponse == null)
                return;
            string[] strArray = queryResponse.Trim().Split(',');
            if (strArray.Length != 28)
                return;
            int result;
            if (!int.TryParse(strArray[13].TrimEnd('.'), out result))
                return;
            this.ReadOk = true;
            this.Limits = new IMotionControlLimit[2];
            this.Limits[0] = (IMotionControlLimit)new ArcusControllerLimit(MotionControlLimits.Upper, (result & 16) != 0);
            this.Limits[1] = (IMotionControlLimit)new ArcusControllerLimit(MotionControlLimits.Lower, (result & 32) != 0);
        }
    }
}
