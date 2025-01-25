using Redbox.HAL.Component.Model;
using System.Threading;

namespace Redbox.HAL.Controller.Framework
{
    internal class IceQubeAirExchangerService : IAirExchangerService, IConfigurationObserver
    {
        private const string ResetFailures = "IceQubeResetFailures";

        public void NotifyConfigurationChangeStart()
        {
            LogHelper.Instance.Log("[IceQubeService] Configuration change start.");
            this.FanStatus = ExchangerFanStatus.Unknown;
        }

        public void NotifyConfigurationChangeEnd()
        {
            LogHelper.Instance.Log("[IceQubeService] Configuration change end.");
            this.Configured = ControllerConfiguration.Instance.EnableIceQubePolling;
            this.FanStatus = this.Configured ? ExchangerFanStatus.On : ExchangerFanStatus.NotConfigured;
            if (this.Configured)
                return;
            this.TurnOnFan();
        }

        public void NotifyConfigurationLoaded()
        {
            LogHelper.Instance.Log("[IceQubeService] Configuration loaded.");
            this.Configured = ControllerConfiguration.Instance.EnableIceQubePolling;
            this.FanStatus = this.Configured ? ExchangerFanStatus.On : ExchangerFanStatus.NotConfigured;
        }

        public AirExchangerStatus CheckStatus()
        {
            return !this.Configured ? AirExchangerStatus.NotConfigured : this.CheckStatusInner(ServiceLocator.Instance.GetService<IControlSystem>());
        }

        public void TurnOnFan()
        {
            if (!this.Configured)
                return;
            if (ExchangerFanStatus.On == this.FanStatus)
            {
                LogHelper.Instance.Log("Fan is already on - bypass.");
            }
            else
            {
                IControlSystem service = ServiceLocator.Instance.GetService<IControlSystem>();
                this.TurnOnFanChecked(service);
                Thread.Sleep(500);
                int num = (int)this.ResetChecked(service);
            }
        }

        public void TurnOffFan()
        {
            if (!this.Configured)
                return;
            if (ExchangerFanStatus.Off == this.FanStatus)
            {
                LogHelper.Instance.WithContext("Air exchanger fan is already off - bypass.");
            }
            else
            {
                this.TurnOffFanChecked(ServiceLocator.Instance.GetService<IControlSystem>());
                Thread.Sleep(10500);
            }
        }

        public AirExchangerStatus Reset()
        {
            return !this.Configured ? AirExchangerStatus.NotConfigured : this.ResetChecked(ServiceLocator.Instance.GetService<IControlSystem>());
        }

        public int PersistentFailureCount()
        {
            IPersistentCounter persistentCounter = ServiceLocator.Instance.GetService<IPersistentCounterService>().Find("IceQubeResetFailures");
            return persistentCounter != null ? persistentCounter.Value : 0;
        }

        public bool ShouldRetry()
        {
            IPersistentCounter persistentCounter = ServiceLocator.Instance.GetService<IPersistentCounterService>().Find("IceQubeResetFailures");
            return persistentCounter != null && persistentCounter.Value < 3;
        }

        public void ResetFailureCount()
        {
            ServiceLocator.Instance.GetService<IPersistentCounterService>().Reset("IceQubeResetFailures");
        }

        public void IncrementResetFailureCount()
        {
            ServiceLocator.Instance.GetService<IPersistentCounterService>().Increment("IceQubeResetFailures");
        }

        public bool Configured { get; private set; }

        public ExchangerFanStatus FanStatus { get; private set; }

        internal IceQubeAirExchangerService()
        {
            this.FanStatus = ExchangerFanStatus.NotConfigured;
            ControllerConfiguration.Instance.AddObserver((IConfigurationObserver)this);
        }

        private AirExchangerStatus ResetChecked(IControlSystem cs)
        {
            int millisecondsTimeout = 1500;
            this.TurnOffFanChecked(cs);
            Thread.Sleep(millisecondsTimeout);
            this.TurnOnFanChecked(cs);
            Thread.Sleep(millisecondsTimeout);
            return this.CheckStatusInner(cs);
        }

        private bool TurnOnFanChecked(IControlSystem cs)
        {
            IControlResponse controlResponse = cs.LockQlmDoor();
            if (controlResponse.Success)
                this.FanStatus = ExchangerFanStatus.On;
            return controlResponse.Success;
        }

        private bool TurnOffFanChecked(IControlSystem cs)
        {
            IControlResponse controlResponse = cs.UnlockQlmDoor();
            if (controlResponse.Success)
                this.FanStatus = ExchangerFanStatus.Off;
            return controlResponse.Success;
        }

        private AirExchangerStatus CheckStatusInner(IControlSystem cs)
        {
            IReadInputsResult<AuxInputs> readInputsResult = cs.ReadAuxInputs();
            return !readInputsResult.Success || !readInputsResult.IsInputActive(AuxInputs.QlmBayDoor) ? AirExchangerStatus.Error : AirExchangerStatus.Ok;
        }
    }
}
