using CMS;
using CMS.Core;
using CMS.DataEngine;
using CMS.Ecommerce;
using CMS.Helpers;
using Custom.Xperience.Stripe.Endpoint;
using Stripe;
using System;
using System.Linq;
using System.Web.Http;

[assembly: RegisterModule(typeof(XperienceStripeEndpointModule))]
namespace Custom.Xperience.Stripe.Endpoint
{
    public class XperienceStripeEndpointModule : Module
    {
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

            StripeConfiguration.ApiKey = Service.Resolve<IAppSettingsService>()["CustomStripeSecretKey"];

            //Register event handler.
            OrderInfo.TYPEINFO.Events.Update.Before += Order_Update_Before;
        }

        
        private void Order_Update_Before(object sender, ObjectEventArgs e)
        {
            //Only do anything if the setting is configured, and get the ID of the order status in Settings that triggers order capture.
            if (int.TryParse(CacheHelper.Cache(cs => LoadSetting(cs), new CacheSettings(60, "customxperiencestripe|settingkey")), out int captureStatusID) && captureStatusID > 0)
            {                
                var order = (OrderInfo)e.Object;
                PaymentOptionInfo paymentOption = CacheHelper.Cache(cs => LoadOption(cs), new CacheSettings(60, "customxperiencestripe|paymentoption"));
                if(order.OrderPaymentOptionID == paymentOption.PaymentOptionID)
                { 
                    int approvedStatusID = 0;

                    if (paymentOption != null)
                    {
                        approvedStatusID = OrderStatusInfo.Provider.Get(paymentOption.PaymentOptionAuthorizedOrderStatusID).StatusID;
                    }

                    //Get previous and current status for the updated order.
                    int originalStatus = (int)order.GetOriginalValue("OrderStatusID");
                    int currentStatus = order.OrderStatusID;

                    //If the order is in the status that triggers payment capture.
                    if (currentStatus == captureStatusID)
                    {
                        //Get the payment intent from the order's custom data.
                        var paymentIntentID = (string)order.OrderCustomData.GetValue("StripePaymentIntentID");

                        //If the order was previously approved.
                        if (originalStatus == approvedStatusID)
                        { 
                            if (!String.IsNullOrEmpty(paymentIntentID))
                            {
                                try
                                {
                                    //Capture the payment.
                                    CaptureHelper.CapturePayment(paymentIntentID);
                                }
                                catch (StripeException ex)
                                {
                                    Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", "Stripe", ex.Message + "\r\n" + ex.StackTrace);
                                    order.OrderStatusID = paymentOption.PaymentOptionFailedOrderStatusID;
                                }
                            }
                            else
                            {
                                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paymentintentmissing"), $"OrderID {order.OrderID}");
                            }
                        }
                        else
                        {
                            Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paymentnotapproved"), $"OrderID: {order.OrderID}, StripePaymentIntentID: {paymentIntentID ?? "null"}");
                        }
                    }
                }
            }
        }

        //Cache the payment option that is compared to each order to minimize extraneous database calls.
        private PaymentOptionInfo LoadOption(CacheSettings cs)
        {
            PaymentOptionInfo paymentOption = PaymentOptionInfo.Provider.Get().WhereEquals("PaymentOptionName", "Stripe").First();
            cs.CacheDependency = CacheHelper.GetCacheDependency("ecommerce.paymentoption|byname|stripe");
            return paymentOption;
        }

        private string LoadSetting(CacheSettings cs)
        {
            string setting = SettingsKeyInfoProvider.GetValue("OrderStatusForCapture");
            cs.CacheDependency = CacheHelper.GetCacheDependency("cms.settingskey|byname|orderstatusforcapture");
            return setting;
        }
    }
}
