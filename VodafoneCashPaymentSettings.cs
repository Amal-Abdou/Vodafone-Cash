using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.VodafoneCash
{
    public class VodafoneCashPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }

        public string ApiKey { get; set; }

        public string FrameId { get; set; }

        public string WalletIntegrationId { get; set; }

    }
}
