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
using System.Collections.Generic;

namespace Custom.Xperience.Stripe.Endpoint
{
    public class StripeController : ApiController
    {
        private IEventLogService eventLogService;
        private IAppSettingsService appSettingsService;
        public StripeController()
        {
            this.eventLogService = Service.Resolve<IEventLogService>();
            this.appSettingsService = Service.Resolve<IAppSettingsService>();
        }

        [HttpPost]
        public virtual async Task<HttpResponseMessage> Update()
        {
            var json = await new StreamReader(HttpContext.Current.Request.InputStream, HttpContext.Current.Request.ContentEncoding).ReadToEndAsync();
            var webhookSecret = appSettingsService["CustomStripeWebhookSecret"];
            if (!String.IsNullOrEmpty(webhookSecret))
            {
                if (Request.Headers.TryGetValues("Stripe-Signature", out var values))
                {
                    var stripeEvent = GetStripeEvent(values, json, webhookSecret);

                    if (stripeEvent != null && stripeEvent.Data.Object.Object == "checkout.session")
                    {
                        UpdateOrderFromCheckoutSessioEvent(stripeEvent);
                    }
                    else if (stripeEvent != null && stripeEvent.Data.Object.Object == "payment_intent")
                    {
                        UpdateOrderFromPaymentIntent(stripeEvent);
                    }
                    else
                    {
                        eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedobjecttype"), stripeEvent.Data.Object.Object);
                    }
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.signaturenotfound"));
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.webhooksecretmissing"));
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }


        protected virtual void UpdateOrderToPaid(OrderInfo order)
        {
            order.OrderIsPaid = true;

            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if(paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionSucceededOrderStatusID, out OrderStatusInfo status))
            {
                    order.OrderStatusID = status.StatusID;
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.paidstatusnotset"));
            }
        }


        protected virtual void UpdateOrderToFailed(OrderInfo order)
        {
            order.OrderIsPaid = false;

            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if (paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionFailedOrderStatusID, out OrderStatusInfo status))
            {
                    order.OrderStatusID = status.StatusID;
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.failedstatusnotset"));
            }
        }


        protected virtual void UpdateOrderToAuthorized(OrderInfo order, string paymentIntentId)
        {
            order.OrderIsPaid = false;
            order.OrderCustomData.SetValue("StripePaymentIntentID", paymentIntentId);

            var paymentOption = PaymentOptionInfo.Provider.Get(order.OrderPaymentOptionID);
            if (paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionAuthorizedOrderStatusID, out OrderStatusInfo status))
            {
                    order.OrderStatusID = status.StatusID;
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.authorizedstatusnotset"));
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


        protected virtual void UpdateOrderFromCheckoutSessioEvent(Event stripeEvent)
        {
            var checkoutSession = stripeEvent.Data.Object as Session;
            if (!String.IsNullOrEmpty(checkoutSession.ClientReferenceId))
            {
                var order = GetOrderFromCheckoutSession(checkoutSession.ClientReferenceId);
                if (order != null)
                {
                    UpdateOrderFromCheckoutSession(checkoutSession, order, stripeEvent);
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.orderNotFound"), $"OrderID: {order.OrderID} \r\nPaymentIntentId: {checkoutSession.PaymentIntentId}");
                }
            }
        }


        protected virtual void UpdateOrderFromCheckoutSession(Session checkoutSession, OrderInfo order, Event stripeEvent)
        {
            order.OrderCustomData.SetValue("StripeCheckoutID", checkoutSession.Id);

            if ((stripeEvent.Type == Events.CheckoutSessionCompleted && checkoutSession.PaymentStatus == "paid") || stripeEvent.Type == Events.CheckoutSessionAsyncPaymentSucceeded)
            {
                UpdateOrderToPaid(order);
            }
            else if (stripeEvent.Type == "checkout.session.completed" && checkoutSession.PaymentStatus == "unpaid")
            {
                UpdateOrderToAuthorized(order, checkoutSession.PaymentIntentId);
            }
            else if (stripeEvent.Type == Events.CheckoutSessionAsyncPaymentFailed || stripeEvent.Type == Events.CheckoutSessionExpired)
            {
                UpdateOrderToFailed(order);
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
            }
            order.Update();
        }


        protected virtual void UpdateOrderFromPaymentIntent(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

            var order = GetOrderFromPaymentIntent(paymentIntent.Id);
            if (order != null)
            {
                if (stripeEvent.Type == Events.PaymentIntentSucceeded)
                {
                    UpdateOrderToPaid(order);
                }
                else if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
                {
                    UpdateOrderToFailed(order);
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
                }
            }
            order.Update();
        }


        protected virtual Event GetStripeEvent(IEnumerable<string> headerValues, string json, string webhookSecret)
        {
            var signatureHeader = headerValues.First();

            Event stripeEvent = null;
            try
            {
                //this both creates the event object and checks if the secret key matches
                stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
            }
            catch (StripeException ex)
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", ResHelper.GetString("custom.stripe.error.noevent"), ex.Message + "\r\n" + ex.StackTrace);
            }
            return stripeEvent;
        }


        bool TryGetValidStatus(int statusID, out OrderStatusInfo status)
        {
            status = OrderStatusInfo.Provider.Get(statusID);
            return status != null;
        }
    }
}
