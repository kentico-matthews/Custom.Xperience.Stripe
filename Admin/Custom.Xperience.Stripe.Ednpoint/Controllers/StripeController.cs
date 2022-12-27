using System.Net.Http;
using System.Net;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using CMS.Core;
using System;
using Stripe;
using CMS.Helpers;
using System.Linq;
using Stripe.Checkout;
using CMS.Ecommerce;
using System.IO;
using System.Web;
using System.Threading.Tasks;

namespace Custom.Xperience.Stripe.Endpoint
{
    public class StripeController : ApiController
    {
        private const string WEBHOOK_SECRET_SETTING = "CustomStripeWebhookSecret";

        [HttpPost]
        public virtual async Task<HttpResponseMessage> Update()
        {
            var json = await new StreamReader(HttpContext.Current.Request.InputStream, HttpContext.Current.Request.ContentEncoding).ReadToEndAsync();
            var stripeEvent = EventUtility.ParseEvent(json.ToString());
            var webhookSecret = Service.Resolve<IAppSettingsService>()[WEBHOOK_SECRET_SETTING];
            if (!String.IsNullOrEmpty(webhookSecret))
            {
                try
                {
                    if (Request.Headers.TryGetValues("Stripe-Signature", out var values))
                    {
                        var signatureHeader = values.First();
                        
                        //create event object, and check if webhook secret key matchees
                        stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

                        if (stripeEvent.Data.Object.Object == "checkout.session")
                        {
                            var checkoutSession = stripeEvent.Data.Object as Session;
                            if (!String.IsNullOrEmpty(checkoutSession.ClientReferenceId))
                            {
                                var order = GetOrderFromCheckoutSession(checkoutSession.ClientReferenceId);
                                if (order != null)
                                {
                                    order.OrderCustomData.SetValue("StripeCheckoutID", checkoutSession.Id);

                                    if ((stripeEvent.Type == "checkout.session.completed" && checkoutSession.PaymentStatus == "paid") || stripeEvent.Type == "checkout.session.async_payment_succeeded")
                                    {
                                        UpdateOrderStatusToPaid(order);
                                        order.OrderIsPaid = true;
                                    }
                                    else if (stripeEvent.Type == "checkout.session.completed" && checkoutSession.PaymentStatus == "unpaid")
                                    {
                                        UpdateOrderStatusToAuthorized(order);
                                        order.OrderIsPaid = false;
                                        order.OrderCustomData.SetValue("StripePaymentIntentID", checkoutSession.PaymentIntentId);
                                    }
                                    else if (stripeEvent.Type == "checkout.session.async_payment_failed" || stripeEvent.Type == "checkout.session.expired")
                                    {
                                        UpdateOrderStatusToFailed(order);
                                        order.OrderIsPaid = false;
                                    }
                                    order.Update();
                                }
                            }
                        }
                        else if (stripeEvent.Data.Object.Object == "payment_intent")
                        {
                            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

                            var order = GetOrderFromPaymentIntent(paymentIntent.Id);
                            if (order != null)
                            {
                                if (stripeEvent.Type == "payment_intent.succeeded")
                                {
                                    UpdateOrderStatusToPaid(order);
                                    order.OrderIsPaid = true;
                                }
                                else if (stripeEvent.Type == "payment_intent.payment_failed")
                                {
                                    UpdateOrderStatusToFailed(order);
                                    order.OrderIsPaid = false;
                                }
                            }
                            order.Update();
                        }
                        else
                        {
                            Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedobjecttype"), stripeEvent.Data.Object.Object);
                        }
                    }
                    else
                    {
                        Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.signaturenotfound"));
                    }
                }
                catch(Exception ex)
                {
                    Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.webhookgeneric"),ex.Message + "\r\n" + ex.StackTrace);
                }
            }
            else
            {
                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.webhooksecretmissing"));

            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }
        protected virtual void UpdateOrderStatusToPaid(OrderInfo order)
        {
            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if(paymentOption != null && paymentOption.PaymentOptionSucceededOrderStatusID > 0)
            {
                var status = OrderStatusInfo.Provider.Get(paymentOption.PaymentOptionSucceededOrderStatusID);
                if (status != null)
                {
                    order.OrderStatusID = status.StatusID;
                }
            }
            else
            {
                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paidstatusnotset"));
            }
        }
        protected virtual void UpdateOrderStatusToFailed(OrderInfo order)
        {
            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if (paymentOption != null && paymentOption.PaymentOptionFailedOrderStatusID > 0)
            {
                var status = OrderStatusInfo.Provider.Get(paymentOption.PaymentOptionSucceededOrderStatusID);
                if (status != null)
                {
                    order.OrderStatusID = status.StatusID;
                }
            }
            else
            {
                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.failedstatusnotset"));
            }
        }

        protected virtual void UpdateOrderStatusToAuthorized(OrderInfo order)
        {
            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if (paymentOption != null && paymentOption.PaymentOptionFailedOrderStatusID > 0)
            {
                var status = OrderStatusInfo.Provider.Get(paymentOption.PaymentOptionAuthorizedOrderStatusID);
                if (status != null)
                {
                    order.OrderStatusID = status.StatusID;
                }
            }
            else
            {
                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.failedstatusnotset"));
            }
        }

        protected virtual OrderInfo GetOrderFromPaymentIntent(string paymentIntentID)
        {
            var orders = OrderInfo.Provider.Get().WhereLike("OrderCustomData", $"%{paymentIntentID}%");           
            return orders.First();
        }
        protected virtual OrderInfo GetOrderFromCheckoutSession(string clientReferenceID)
        {
            var order = OrderInfo.Provider.Get().WhereEquals("OrderID", clientReferenceID).First();
            return orders.First();
        }
    }
}
