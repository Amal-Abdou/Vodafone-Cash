using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nop.Plugin.Payments.VodafoneCash.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        [NopResourceDisplayName("Payment.VodafoneCashPaymentNumber")]
        public string VodafoneCashPaymentNumber { get; set; }

    }
}