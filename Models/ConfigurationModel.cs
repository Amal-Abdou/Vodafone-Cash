using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.VodafoneCash.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.VodafoneCash.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.VodafoneCash.Fields.ApiKey")]
        public string ApiKey { get; set; }
        public bool ApiKey_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.VodafoneCash.Fields.FrameId")]
        public string FrameId { get; set; }
        public bool FrameId_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.VodafoneCash.Fields.WalletIntegrationId")]
        public string WalletIntegrationId { get; set; }
        public bool WalletIntegrationId_OverrideForStore { get; set; }

    }
}