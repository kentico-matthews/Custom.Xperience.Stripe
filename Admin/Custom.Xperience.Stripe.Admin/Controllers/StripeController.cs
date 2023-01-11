using System.Net.Http;
using System.Net;
using System.Web.Http;
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
        [HttpPost]
        public virtual async Task<HttpResponseMessage> Update()
        {
            var json = await new StreamReader(HttpContext.Current.Request.InputStream, HttpContext.Current.Request.ContentEncoding).ReadToEndAsync();
            var webhookSecret = Service.Resolve<IAppSettingsService>()["CustomStripeWebhookSecret"];
            if (!String.IsNullOrEmpty(webhookSecret))
            {
                try
                {
                    if (Request.Headers.TryGetValues("Stripe-Signature", out var values))
                    {
                        var signatureHeader = values.First();
                        
                        //create event object, and check if webhook secret key matchees
                        var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

                        if (stripeEvent.Data.Object.Object == "checkout.session")
                        {
                            var checkoutSession = stripeEvent.Data.Object as Session;
                            if (!String.IsNullOrEmpty(checkoutSession.ClientReferenceId))
                            {
                                var order = GetOrderFromCheckoutSession(checkoutSession.ClientReferenceId);
                                if (order != null)
                                {
                                    order.OrderCustomData.SetValue("StripeCheckoutID", checkoutSession.Id);

                                    if ((stripeEvent.Type == Events.CheckoutSessionCompleted && checkoutSession.PaymentStatus == "paid") || stripeEvent.Type == Events.CheckoutSessionAsyncPaymentSucceeded)
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
                                    else if (stripeEvent.Type == Events.CheckoutSessionAsyncPaymentFailed || stripeEvent.Type == Events.CheckoutSessionExpired)
                                    {
                                        UpdateOrderStatusToFailed(order);
                                        order.OrderIsPaid = false;
                                    }
                                    else
                                    {
                                        Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
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
                                if (stripeEvent.Type == Events.PaymentIntentSucceeded)
                                {
                                    UpdateOrderStatusToPaid(order);
                                    order.OrderIsPaid = true;
                                }
                                else if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
                                {
                                    UpdateOrderStatusToFailed(order);
                                    order.OrderIsPaid = false;
                                }
                                else
                                {
                                    Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
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
                Service.Resolve<IEventLogService>().LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.authorizedstatusnotset"));
            }
        }


        protected virtual OrderInfo GetOrderFromPaymentIntent(string paymentIntentID)
        {
            var orders = OrderInfo.Provider.Get().WhereLike("OrderCustomData", $"%{paymentIntentID}%");           
            return orders.First();
        }


        protected virtual OrderInfo GetOrderFromCheckoutSession(string clientReferenceID)
        {
            var orders = OrderInfo.Provider.Get().WhereEquals("OrderID", clientReferenceID);
            return orders.First();
        }
    }
}
