using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Payments.VodafoneCash.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.VodafoneCash.Components
{
    [ViewComponent(Name = "PaymentVodafoneCash")]
    public class PaymentVodafoneCashViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel();

            return View("~/Plugins/Payments.VodafoneCash/Views/PaymentInfo.cshtml", model);
        }
    }
}

