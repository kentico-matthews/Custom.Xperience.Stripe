using CMS;
using CMS.Core;
using CMS.DataEngine;
using CMS.Ecommerce;
using CMS.Helpers;
using Custom.Xperience.Stripe.Endpoint;
using Stripe;
using System.Linq;
using System.Web.Http;

[assembly: RegisterModule(typeof(XperienceStripeEndpointModule))]
namespace Custom.Xperience.Stripe.Endpoint
{
    public class XperienceStripeEndpointModule : Module
    {
        private IEventLogService eventLogService;
        private IAppSettingsService appSettingsService;
        private IOrderStatusInfoProvider orderStatusInfoProvider;
        private IPaymentOptionInfoProvider paymentOptionInfoProvider;
        private ISettingsService settingsService;
        private ILocalizationService localizationService;

        public XperienceStripeEndpointModule() : base("XperienceStripeEndpoint")
        {
        }

        protected override void OnInit()
        {
            base.OnInit();

            //Map route to endpoint.
            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                "xperience-stripe",
                "xperience-stripe/updateorder",
                defaults: new { controller = "Stripe", action = "Update" }
            );

            eventLogService = Service.Resolve<IEventLogService>();
            appSettingsService = Service.Resolve<IAppSettingsService>();
            orderStatusInfoProvider = Service.Resolve<IOrderStatusInfoProvider>();
            paymentOptionInfoProvider = Service.Resolve<IPaymentOptionInfoProvider>();
            settingsService = Service.Resolve<ISettingsService>();
            localizationService = Service.Resolve<ILocalizationService>();

            StripeConfiguration.ApiKey = appSettingsService["CustomStripeSecretKey"];

            CaptureHelper.Init();
            
            //Register event handler.
            OrderInfo.TYPEINFO.Events.Update.Before += Order_Update_Before;
        }

        
        private void Order_Update_Before(object sender, ObjectEventArgs e)
        {
            //Only do anything if the setting is configured, and get the ID of the order status in Settings that triggers order capture.
            if (int.TryParse(CacheHelper.Cache(cs => LoadSetting(cs), new CacheSettings(60, "customxperiencestripe|settingkey")), out int captureStatusID) && captureStatusID > 0)
            {                
                var order = e.Object as OrderInfo;

                if (order != null)
                {
                    PaymentOptionInfo paymentOption = CacheHelper.Cache(cs => LoadOption(cs), new CacheSettings(60, "customxperiencestripe|paymentoption"));

                    if (paymentOption != null && order.OrderPaymentOptionID == paymentOption.PaymentOptionID)
                    {
                        //Get previous and current status for the updated order.
                        int originalStatus = (int)order.GetOriginalValue("OrderStatusID");
                        int currentStatus = order.OrderStatusID;

                        //If the order is in the status that triggers payment capture.
                        if (currentStatus == captureStatusID)
                        {
                            var approvedStatus = orderStatusInfoProvider.Get(paymentOption.PaymentOptionAuthorizedOrderStatusID);

                            if(approvedStatus != null)
                            {
                                int approvedStatusID = approvedStatus.StatusID;
                                //If the order was previously approved.
                                if (originalStatus == approvedStatusID)
                                {
                                    CaptureHelper.CaptureOrder(order);
                                }
                                else
                                {
                                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.paymentnotapproved"), $"OrderID: {order.OrderID}, PaymentIntentID: {order.OrderCustomData.GetValue(XperienceStripeConstants.PAYMENT_INTENT_ID_KEY) ?? "null"}");
                                }
                            }
                            else
                            {
                                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.authorizedstatusnotfound"), $"PaymentOptionAuthorizedOrderStatusID: {paymentOption.PaymentOptionAuthorizedOrderStatusID}");
                            }
                        }
                    }
                }
            }
        }



        //Cache the payment option that is compared to each order to minimize extraneous database calls.
        private PaymentOptionInfo LoadOption(CacheSettings cs)
        {
            PaymentOptionInfo paymentOption = paymentOptionInfoProvider.Get().WhereEquals("PaymentOptionName", "Stripe").First();
            cs.CacheDependency = CacheHelper.GetCacheDependency("ecommerce.paymentoption|byname|stripe");
            return paymentOption;
        }


        //Cache the value of the settings key that signifies the order status for payment capture
        private string LoadSetting(CacheSettings cs)
        {
            string setting = settingsService["OrderStatusForCapture"];
            cs.CacheDependency = CacheHelper.GetCacheDependency("cms.settingskey|byname|orderstatusforcapture");
            return setting;
        }
    }
}
