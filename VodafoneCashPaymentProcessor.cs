using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.VodafoneCash.Models;
using Nop.Plugin.Payments.VodafoneCash.Services;
using Nop.Plugin.Payments.VodafoneCash.Validators;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Tax;

namespace Nop.Plugin.Payments.VodafoneCash
{
    public class VodafoneCashPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly IAddressService _addressService;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly VodafoneCashHttpClient _vodafoneCashHttpClient;
        private readonly VodafoneCashPaymentSettings _vodafoneCashPaymentSettings;
        private readonly IEncryptionService _encryptionService;

        #endregion

        #region Ctor

        public VodafoneCashPaymentProcessor(CurrencySettings currencySettings,
            IAddressService addressService,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IOrderService orderService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IProductService productService,
            ISettingService settingService,
            IStateProvinceService stateProvinceService,
            ITaxService taxService,
            IWebHelper webHelper,
            VodafoneCashHttpClient vodafoneCashHttpClient,
            VodafoneCashPaymentSettings vodafoneCashPaymentSettings,
            IEncryptionService encryptionService)
        {
            _currencySettings = currencySettings;
            _addressService = addressService;
            _checkoutAttributeParser = checkoutAttributeParser;
            _countryService = countryService;
            _currencyService = currencyService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _productService = productService;
            _settingService = settingService;
            _stateProvinceService = stateProvinceService;
            _taxService = taxService;
            _webHelper = webHelper;
            _vodafoneCashHttpClient = vodafoneCashHttpClient;
            _vodafoneCashPaymentSettings = vodafoneCashPaymentSettings;
            _encryptionService = encryptionService;
        }

        #endregion
        #region Utilities
        public bool GetPdtDetails(string tx, out Dictionary<string, string> values, out string response)
        {
            response = WebUtility.UrlDecode(_vodafoneCashHttpClient.GetPdtDetailsAsync(tx).Result);

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool firstLine = true, success = false;
            foreach (var l in response.Split('\n'))
            {
                var line = l.Trim();
                if (firstLine)
                {
                    success = line.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
                    firstLine = false;
                }
                else
                {
                    var equalPox = line.IndexOf('=');
                    if (equalPox >= 0)
                        values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
                }
            }

            return success;
        }

        public bool VerifyIpn(string formString, out Dictionary<string, string> values)
        {
            var response = WebUtility.UrlDecode(_vodafoneCashHttpClient.VerifyIpnAsync(formString).Result);
            var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in formString.Split('&'))
            {
                var line = l.Trim();
                var equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }

            return success;
        }

        private async Task<IDictionary<string, string>> CreateQueryParameters(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var storeLocation = _webHelper.GetStoreLocation();

            var orderAddress = await _addressService.GetAddressByIdAsync(
                (postProcessPaymentRequest.Order.PickupInStore ? postProcessPaymentRequest.Order.PickupAddressId : postProcessPaymentRequest.Order.ShippingAddressId) ?? 0);

            return new Dictionary<string, string>
            {

                ["charset"] = "utf-8",

                ["rm"] = "2",

                ["bn"] = VodafoneCashHelper.NopCommercePartnerCode,
                ["currency_code"] =( await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,

                ["invoice"] = postProcessPaymentRequest.Order.CustomOrderNumber,
                ["custom"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),

                ["return"] = $"{storeLocation}Plugins/PaymentVodafoneCash/PDTHandler",
                ["notify_url"] = $"{storeLocation}Plugins/PaymentVodafoneCash/IPNHandler",
                ["cancel_return"] = $"{storeLocation}Plugins/PaymentVodafoneCash/CancelOrder",

                ["no_shipping"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "1" : "2",
                ["address_override"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "0" : "1",
                ["first_name"] = orderAddress?.FirstName,
                ["last_name"] = orderAddress?.LastName,
                ["address1"] = orderAddress?.Address1,
                ["address2"] = orderAddress?.Address2,
                ["city"] = orderAddress?.City,
                ["state"] = (await _stateProvinceService.GetStateProvinceByAddressAsync(orderAddress))?.Abbreviation,
                ["country"] = (await _countryService.GetCountryByAddressAsync(orderAddress))?.TwoLetterIsoCode,
                ["zip"] = orderAddress?.ZipPostalCode,
                ["email"] = orderAddress?.Email
            };
        }

        private async void AddItemsParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            parameters.Add("cmd", "_cart");
            parameters.Add("upload", "1");

            var cartTotal = decimal.Zero;
            var roundedCartTotal = decimal.Zero;
            var itemCount = 1;

            foreach (var item in await _orderService.GetOrderItemsAsync(postProcessPaymentRequest.Order.Id))
            {
                var roundedItemPrice = Math.Round(item.UnitPriceExclTax, 2);

                var product = await _productService.GetProductByIdAsync(item.ProductId);

                parameters.Add($"item_name_{itemCount}", product.Name);
                parameters.Add($"amount_{itemCount}", roundedItemPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", item.Quantity.ToString());

                cartTotal += item.PriceExclTax;
                roundedCartTotal += roundedItemPrice * item.Quantity;
                itemCount++;
            }

            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
            var customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);

          await  foreach (var (attribute, values) in checkoutAttributeValues)
            {
             await   foreach (var attributeValue in values)
                {
                    var (attributePrice, _) = await _taxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, false, customer);
                    var roundedAttributePrice = Math.Round(attributePrice, 2);

                    if (attribute == null)
                        continue;

                    parameters.Add($"item_name_{itemCount}", attribute.Name);
                    parameters.Add($"amount_{itemCount}", roundedAttributePrice.ToString("0.00", CultureInfo.InvariantCulture));
                    parameters.Add($"quantity_{itemCount}", "1");

                    cartTotal += attributePrice;
                    roundedCartTotal += roundedAttributePrice;
                    itemCount++;
                }
            }

            var roundedShippingPrice = Math.Round(postProcessPaymentRequest.Order.OrderShippingExclTax, 2);
            if (roundedShippingPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Shipping fee");
                parameters.Add($"amount_{itemCount}", roundedShippingPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderShippingExclTax;
                roundedCartTotal += roundedShippingPrice;
                itemCount++;
            }

            var roundedPaymentMethodPrice = Math.Round(postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax, 2);
            if (roundedPaymentMethodPrice > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Payment method fee");
                parameters.Add($"amount_{itemCount}", roundedPaymentMethodPrice.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax;
                roundedCartTotal += roundedPaymentMethodPrice;
                itemCount++;
            }

            var roundedTaxAmount = Math.Round(postProcessPaymentRequest.Order.OrderTax, 2);
            if (roundedTaxAmount > decimal.Zero)
            {
                parameters.Add($"item_name_{itemCount}", "Tax amount");
                parameters.Add($"amount_{itemCount}", roundedTaxAmount.ToString("0.00", CultureInfo.InvariantCulture));
                parameters.Add($"quantity_{itemCount}", "1");

                cartTotal += postProcessPaymentRequest.Order.OrderTax;
                roundedCartTotal += roundedTaxAmount;
            }

            if (cartTotal > postProcessPaymentRequest.Order.OrderTotal)
            {
                var discountTotal = Math.Round(cartTotal - postProcessPaymentRequest.Order.OrderTotal, 2);
                roundedCartTotal -= discountTotal;

                parameters.Add("discount_amount_cart", discountTotal.ToString("0.00", CultureInfo.InvariantCulture));
            }

           await _genericAttributeService.SaveAttributeAsync(postProcessPaymentRequest.Order, VodafoneCashHelper.OrderTotalSentToVodafoneCash, roundedCartTotal);
        }

        private async void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("cmd", "_xclick");
            parameters.Add("item_name", $"Order Number {postProcessPaymentRequest.Order.CustomOrderNumber}");
            parameters.Add("amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

           await _genericAttributeService.SaveAttributeAsync(postProcessPaymentRequest.Order, VodafoneCashHelper.OrderTotalSentToVodafoneCash, roundedOrderTotal);
        }

        #endregion

        #region Methods

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult
            {
                AllowStoringCreditCardNumber = true
            };
        }

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var order = await _orderService.GetOrderByIdAsync(postProcessPaymentRequest.Order.Id);

            var IntegrationId = _vodafoneCashPaymentSettings.WalletIntegrationId;

            var BillingAddress = await _addressService.GetAddressByIdAsync(postProcessPaymentRequest.Order.BillingAddressId);
            var OrderRegisterationID = postProcessPaymentRequest.Order.Id;
            var paymentKey = await _vodafoneCashHttpClient.PaymentKeyRequest(postProcessPaymentRequest, IntegrationId, BillingAddress, OrderRegisterationID);


                var PayMobPaymentNumber = _encryptionService.DecryptText(order.CardNumber);

                var redirectionURL = await _vodafoneCashHttpClient.PostWalletPayment(paymentKey, PayMobPaymentNumber);

                var url = redirectionURL;

            if (!string.IsNullOrEmpty(url))
            {
                _httpContextAccessor.HttpContext.Response.Redirect(url);
            }



        }

        public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return decimal.Zero;
        }

        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        public async Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                VodafoneCashPaymentNumber = form["VodafoneCashPaymentNumber"],
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return new ProcessPaymentRequest
            {
                CreditCardType = form["VodafoneCashPaymentType"],
                CreditCardNumber = form["VodafoneCashPaymentNumber"],
            };
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentVodafoneCash/Configure";
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentVodafoneCash";
        }


        public async override Task InstallAsync()
        {
            await _settingService.SaveSettingAsync(new VodafoneCashPaymentSettings
            {
                UseSandbox = true
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.VodafoneCash.Fields.MerchantCode"] = "MerchantCode",
                ["Plugins.Payments.VodafoneCash.Fields.MerchantCode.Hint"] = "Enter Merchant Code.",
                ["Plugins.Payments.VodafoneCash.Fields.PaymentExpiry"] = "Payment Expiry",
                ["Plugins.Payments.VodafoneCash.Fields.PaymentExpiry.Hint"] = "Enter Payment Expiry.",
                ["Plugins.Payments.VodafoneCash.Fields.SecurityKey"] = "Security Key",
                ["Plugins.Payments.VodafoneCash.Fields.SecurityKey.Hint"] = "Enter Security Key.",
               
                ["Plugins.Payments.VodafoneCash.Fields.RedirectionTip"] = "You will be redirected to PayMob site to complete the order.",
                ["Plugins.Payments.VodafoneCash.Fields.UseSandbox"] = "Use Sandbox",
                ["Plugins.Payments.VodafoneCash.Fields.UseSandbox.Hint"] = "Check to enable Sandbox (testing environment).",
            });

            base.InstallAsync();
        }

        public async override Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<VodafoneCashPaymentSettings>();

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.VodafoneCash");

            base.UninstallAsync();
        }


        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payment.VodafoneCash.PaymentMethodDescription");

        }

        #endregion

        #region Properties

        public bool SupportCapture => false;

        public bool SupportPartiallyRefund => false;

        public bool SupportRefund => false;

        public bool SupportVoid => false;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;

        public  Task<string> PaymentMethodDescription => _localizationService.GetResourceAsync("Plugins.Payments.VodafoneCash.PaymentMethodDescription");

        #endregion


    }
}