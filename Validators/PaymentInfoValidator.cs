using System;
using FluentValidation;
using Nop.Plugin.Payments.VodafoneCash.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.VodafoneCash.Validators
{
    public partial class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {

            RuleFor(x => x.VodafoneCashPaymentNumber).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Payment.VodafoneCashPaymentNumber.Required"));
        }
    }
}