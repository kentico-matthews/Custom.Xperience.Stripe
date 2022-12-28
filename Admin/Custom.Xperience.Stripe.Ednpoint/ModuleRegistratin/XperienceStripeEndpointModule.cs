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
        public XperienceStripeEndpointModule() : base("XperienceStripeEndpoint")
        {
        }

        protected override void OnInit()
        {
            base.OnInit();

            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                "xperience-stripe",
                "xperience-stripe/updateorder",
                defaults: new { controller = "Stripe", action = "Update" }
            );

            OrderInfo.TYPEINFO.Events.Update.Before += Order_Update_Before;
        }


        private void Order_Update_Before(object sender, ObjectEventArgs e)
        {
            //only do anything if the setting is configured            
            if(int.TryParse(SettingsKeyInfoProvider.GetValue("OrderStatusForCapture"), out int captureStatusID) && captureStatusID > 0)
            {
                var order = (OrderInfo)e.Object;
                var paymentOption = PaymentOptionInfo.Provider.Get().WhereEquals("PaymentOptionName", "Stripe").First();
                int approvedStatusID = 0;
                if (paymentOption != null)
                {
                    approvedStatusID = OrderStatusInfo.Provider.Get(paymentOption.PaymentOptionAuthorizedOrderStatusID).StatusID;
                }

                int originalStatus = (int)order.GetOriginalValue("OrderStatusID");
                int currentStatus = order.OrderStatusID;

                if (currentStatus == captureStatusID)
                {
                    if (originalStatus == approvedStatusID)
                    {
                        var paymentIntentID = (string)order.OrderCustomData.GetValue("StripePaymentIntentID");
                        if(!string.IsNullOrEmpty(paymentIntentID))
                        {
                            try
                            {
                                CaptureHelper.CapturePayment(paymentIntentID);
                            }
                            catch(StripeException ex)
                            {
                                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe","Stripe", ex.Message + "\r\n" + ex.StackTrace);
                            }
                        }
                        else
                        {
                            Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paymentintentmissing"));
                        }
                    }
                }
            }
        }
    }
}
