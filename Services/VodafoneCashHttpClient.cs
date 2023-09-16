using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.VodafoneCash.Services
{

    public partial class VodafoneCashHttpClient
    {
        #region Fields

        private readonly HttpClient _httpClient;
        private readonly VodafoneCashPaymentSettings _vodafoneCashPaymentSettings;

        #endregion

        #region Ctor

        public VodafoneCashHttpClient(HttpClient client,
            VodafoneCashPaymentSettings vodafoneCashPaymentSettings)
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");
            
            _httpClient = client;
            _vodafoneCashPaymentSettings = vodafoneCashPaymentSettings;
        }

        #endregion

        #region Methods

        public async Task<string> GetPdtDetailsAsync(string tx)
        {
            var url = _vodafoneCashPaymentSettings.UseSandbox ?
                "https://migs-mtf.mastercard.com.au/vpcpay" :
                "https://migs-mtf.mastercard.com.au/vpcpay";
            var requestContent = new StringContent($"cmd=_notify-synch&at=&tx={tx}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> VerifyIpnAsync(string formString)
        {
            var url = _vodafoneCashPaymentSettings.UseSandbox ?
                "https://migs-mtf.mastercard.com.au/vpcpay" :
                "https://migs-mtf.mastercard.com.au/vpcpay";
            var requestContent = new StringContent($"cmd=_notify-validate&{formString}",
                Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
            var response = await _httpClient.PostAsync(url, requestContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> AuthenticationRequest()
        {
            //get response
            var url = "https://accept.paymob.com/api/auth/tokens";
            var payload = new
            {
                api_key = _vodafoneCashPaymentSettings.ApiKey
            };

            // Serialize our concrete class into a JSON String
            var stringPayload = JsonConvert.SerializeObject(payload);

            // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
            var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();
            var ResponseString = await response.Content.ReadAsStringAsync();
            var token = "";
            if (!string.IsNullOrEmpty(ResponseString))
            {
                token = JObject.Parse(ResponseString).GetValue("token").ToString();
            }

            return token;
        }

        private async Task<object> OrderRegistrationAPI(PostProcessPaymentRequest postedData)
        {
            //get response
            var url = "https://accept.paymob.com/api/ecommerce/orders";
            var _authToken = await AuthenticationRequest();
            var _orderID = "";
            if (!string.IsNullOrEmpty(_authToken))
            {
                var payload = new
                {
                    auth_token = _authToken,
                    delivery_needed = "false",
                    amount_cents = postedData.Order.OrderTotal * 100,
                    currency = "EGP",
                    // terminal_id = postedData.Order.Id,
                    merchant_order_id = postedData.Order.Id.ToString(),
                    items = new object[] { },

                };

                // Serialize our concrete class into a JSON String
                var stringPayload = JsonConvert.SerializeObject(payload);

                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, httpContent);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrEmpty(jsonString))
                {
                    _orderID = JObject.Parse(jsonString).GetValue("id").ToString();
                }
            }
            //await _orderProcessingService.MarkOrderAsRegisteredAsync(postedData.Order);
            return new { orderID = _orderID, authToken = _authToken };

        }
        public async Task<string> PaymentKeyRequest(PostProcessPaymentRequest postData, string IntegrationId, Address BillingAddress, int orderID)
        {
            //get response
            var url = "https://accept.paymob.com/api/acceptance/payment_keys";
            var _authToken = "";
            var _orderID = orderID;
            if (_orderID != null)
            {
                var result = await OrderRegistrationAPI(postData);
                _orderID = Convert.ToInt32(result?.GetType().GetProperty("orderID")?.GetValue(result, null).ToString());
                _authToken = result?.GetType().GetProperty("authToken")?.GetValue(result, null).ToString();
            }
            else
            {
                _authToken = await AuthenticationRequest();
            }



            var paymentKey = "";
            if (!string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_orderID.ToString()))
            {
                var FirstName = BillingAddress == null || string.IsNullOrEmpty(BillingAddress.FirstName) ? "" : BillingAddress.FirstName;
                var LastName = BillingAddress == null || string.IsNullOrEmpty(BillingAddress.LastName) ? "" : BillingAddress.LastName;
                var Phone = BillingAddress == null || string.IsNullOrEmpty(BillingAddress.PhoneNumber) ? "" : BillingAddress.PhoneNumber;
                var Email = BillingAddress == null || string.IsNullOrEmpty(BillingAddress.Email) ? "" : BillingAddress.Email;

                var payload = new
                {
                    auth_token = _authToken,
                    amount_cents = postData.Order.OrderTotal * 100,
                    expiration = 3600,
                    order_id = _orderID,
                    billing_data = new
                    {
                        apartment = "1",
                        email = Email,
                        floor = "1",
                        first_name = FirstName,
                        street = "1",
                        building = "1",
                        phone_number = Phone,
                        shipping_method = "PKG",
                        postal_code = "01898",
                        city = "Cairo",
                        country = "Egypt",
                        last_name = LastName,
                        state = "Cairo"
                    },
                    currency = "EGP",
                    integration_id = IntegrationId,
                    lock_order_when_paid = "false"
                };

                // Serialize our concrete class into a JSON String
                var stringPayload = JsonConvert.SerializeObject(payload);

                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, httpContent);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrEmpty(jsonString))
                {
                    // var responseObject = JsonConvert.DeserializeObject<object>(jsonString);
                    paymentKey = JObject.Parse(jsonString).GetValue("token").ToString();
                }
            }
            return paymentKey;
        }
        public async Task<string> PostWalletPayment(string paymentToken, string mobileNumber)
        {
            //get response
            var url = "https://accept.paymob.com/api/acceptance/payments/pay";
            var payload = new
            {
                source = new
                {
                    identifier = mobileNumber,
                    subtype = "WALLET"
                },
                payment_token = paymentToken
            };

            // Serialize our concrete class into a JSON String
            var stringPayload = JsonConvert.SerializeObject(payload);

            // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
            var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var redirectionURL = "";
            if (!string.IsNullOrEmpty(jsonString))
            {
                //var responseObject = JsonConvert.DeserializeObject<object>(jsonString);

                //  var pending = JObject.Parse(jsonString).GetValue("pending").ToString();
                //  var success = JObject.Parse(jsonString).GetValue("success").ToString();
                //  if (pending == "true" && success == "false")

                redirectionURL = JObject.Parse(jsonString).GetValue("iframe_redirection_url").ToString();



            }

            return redirectionURL;
        }

        #endregion
    }
}