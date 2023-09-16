using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.VodafoneCash.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //PDT
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.VodafoneCash.PDTHandler", "Plugins/PaymentVodafoneCash/PDTHandler",
                 new { controller = "PaymentVodafoneCash", action = "PDTHandler" });

            //Cancel
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.VodafoneCash.CancelOrder", "Plugins/PaymentVodafoneCash/CancelOrder",
                 new { controller = "PaymentVodafoneCash", action = "CancelOrder" });

        }

        public int Priority => -1;
    }
}