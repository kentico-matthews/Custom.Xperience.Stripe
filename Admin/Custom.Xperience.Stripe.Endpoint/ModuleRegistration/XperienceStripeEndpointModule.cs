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

            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                "xperience-stripe",
                "xperience-stripe/updateorder",
                defaults: new { controller = "Stripe", action = "Update" }
            );

            //Register event handler
            OrderInfo.TYPEINFO.Events.Update.Before += Order_Update_Before;
        }


        private void Order_Update_Before(object sender, ObjectEventArgs e)
        {
            //only do anything if the setting is configured, and get the ID of the order status in Settings that triggers order capture            
            if(int.TryParse(SettingsKeyInfoProvider.GetValue("OrderStatusForCapture"), out int captureStatusID) && captureStatusID > 0)
            {
                var order = (OrderInfo)e.Object;
                var paymentOption = PaymentOptionInfo.Provider.Get().WhereEquals("PaymentOptionName", "Stripe").First();
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
                    //If the order was previously approved.
                    if (originalStatus == approvedStatusID)
                    {
                        //Get the payment intent from the order's custom data.
                        var paymentIntentID = (string)order.OrderCustomData.GetValue("StripePaymentIntentID");
                        if(!String.IsNullOrEmpty(paymentIntentID))
                        {
                            try
                            {
                                //Capture the payment.
                                CaptureHelper.CapturePayment(paymentIntentID);
                            }
                            catch(StripeException ex)
                            {
                                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe","Stripe", ex.Message + "\r\n" + ex.StackTrace);
                            }
                        }
                        else
                        {
                            Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paymentintentmissing"), $"OrderID {order.OrderID}");
                        }
                    }
                }

            }
        }
    }
}
