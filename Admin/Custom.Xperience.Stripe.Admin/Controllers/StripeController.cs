using System.Linq;
using System.Net.Http;
using System.Net;
using System.Web.Http;
using System.IO;
using System.Web;
using System.Threading.Tasks;
using System.Collections.Generic;
using Stripe;
using Stripe.Checkout;
using CMS.Core;
using CMS.Ecommerce;


namespace Custom.Xperience.Stripe.Endpoint
{
    public class StripeController : ApiController
    {

        private IEventLogService eventLogService;
        private IAppSettingsService appSettingsService;
        private ILocalizationService localizationService;
        private IOrderInfoProvider orderInfoProvider;
        private IOrderStatusInfoProvider orderStatusInfoProvider;
        private IPaymentOptionInfoProvider paymentOptionInfoProvider;

        public StripeController()
        {
            eventLogService = Service.Resolve<IEventLogService>();
            appSettingsService = Service.Resolve<IAppSettingsService>();
            localizationService = Service.Resolve<ILocalizationService>();
            orderInfoProvider = Service.Resolve<IOrderInfoProvider>();
            orderStatusInfoProvider = Service.Resolve<IOrderStatusInfoProvider>();
            paymentOptionInfoProvider = Service.Resolve<IPaymentOptionInfoProvider>();
        }

        [HttpPost]
        public virtual async Task<HttpResponseMessage> Update()
        {
            var json = await new StreamReader(HttpContext.Current.Request.InputStream, HttpContext.Current.Request.ContentEncoding).ReadToEndAsync();
            var webhookSecret = appSettingsService["CustomStripeWebhookSecret"];
            if (!string.IsNullOrEmpty(webhookSecret))
            {
                if (Request.Headers.TryGetValues("Stripe-Signature", out var values))
                {
                    var stripeEvent = GetStripeEvent(values, json, webhookSecret);
                    if(stripeEvent != null && stripeEvent.Data != null && stripeEvent.Data.Object != null)
                    {
                        if (stripeEvent.Data.Object.Object == "checkout.session")
                        {
                            UpdateOrderFromCheckoutSessionEvent(stripeEvent);
                        }
                        else if (stripeEvent.Data.Object.Object == "payment_intent")
                        {
                            UpdateOrderFromPaymentIntent(stripeEvent);
                        }
                        else
                        {
                            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.unsupportedobjecttype"), stripeEvent.Data.Object.Object);
                        }
                    }
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.signaturenotfound"));
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.webhooksecretmissing"));
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }


        protected virtual void UpdateOrderToPaid(OrderInfo order)
        {
            if (order != null)
            {
                order.OrderIsPaid = true;

                var paymentOption = paymentOptionInfoProvider.Get(order.OrderPaymentOptionID);
                if (paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionSucceededOrderStatusID, out OrderStatusInfo status))
                {
                    order.OrderStatusID = status.StatusID;
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.paidstatusnotset"));
                }
            }
        }


        protected virtual void UpdateOrderToFailed(OrderInfo order)
        {
            if (order != null)
            {
                order.OrderIsPaid = false;

                var paymentOption = paymentOptionInfoProvider.Get(order.OrderPaymentOptionID);
                if (paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionFailedOrderStatusID, out OrderStatusInfo status))
                {
                    order.OrderStatusID = status.StatusID;
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.failedstatusnotset"));
                }
            }
        }


        protected virtual void UpdateOrderToAuthorized(OrderInfo order, string paymentIntentId)
        {
            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                if (order != null)
                {
                    order.OrderIsPaid = false;
                    order.OrderCustomData.SetValue(XperienceStripeConstants.PAYMENT_INTENT_ID_KEY, paymentIntentId);

                    var paymentOption = paymentOptionInfoProvider.Get(order.OrderPaymentOptionID);
                    if (paymentOption != null && TryGetValidStatus(paymentOption.PaymentOptionAuthorizedOrderStatusID, out OrderStatusInfo status))
                    {
                        order.OrderStatusID = status.StatusID;
                    }
                    else
                    {
                        eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.authorizedstatusnotset"));
                    }
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.nopaymentintentid"));
            }
            
        }


        protected virtual OrderInfo GetOrderFromPaymentIntent(string paymentIntentID)
        {
            var orders = orderInfoProvider.Get().WhereLike("OrderCustomData", $"%<{XperienceStripeConstants.PAYMENT_INTENT_ID_KEY}>{paymentIntentID}</{XperienceStripeConstants.PAYMENT_INTENT_ID_KEY}>%");
            try
            {
                return orders.Single();
            }
            catch
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.ordersfrompaymentintent"), $"PaymentIntentId: {paymentIntentID }\r\nNumber of orders: {orders.Count}");
                return orders.FirstOrDefault();
            }
        }


        protected virtual OrderInfo GetOrderFromCheckoutSession(string clientReferenceID)
        {
            var orders = orderInfoProvider.Get().WhereEquals("OrderID", clientReferenceID);
            if (orders.Count > 0)
            {
                return orders.SingleOrDefault();
            }
            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.noordersfromcheckout"), $"clientReferenceID: {clientReferenceID}");
            return null;
        }


        protected virtual void UpdateOrderFromCheckoutSessionEvent(Event stripeEvent)
        {
            if(stripeEvent != null && stripeEvent.Data != null && stripeEvent.Data.Object != null)
            {
                var checkoutSession = stripeEvent.Data.Object as Session;
                if (checkoutSession != null)
                {
                    if (!string.IsNullOrEmpty(checkoutSession.ClientReferenceId))
                    {
                        var order = GetOrderFromCheckoutSession(checkoutSession.ClientReferenceId);
                        if (order != null)
                        {
                            UpdateOrderFromCheckoutSession(checkoutSession, order, stripeEvent);
                        }
                        else
                        {
                            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.orderNotFound"), $"OrderID: {checkoutSession.ClientReferenceId} \r\nPaymentIntentId: {checkoutSession.PaymentIntentId}");
                        }
                    }
                    else
                    {
                        eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.clientreferenceidempty"), $"Session.Id: {checkoutSession.Id} \r\nPaymentIntentId: {checkoutSession.PaymentIntentId}");
                    }
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.nocheckoutsessioninevent"));
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.noobjectinevent"));
            }
        }


        protected virtual void UpdateOrderFromCheckoutSession(Session checkoutSession, OrderInfo order, Event stripeEvent)
        {
            if (checkoutSession != null && order != null && stripeEvent != null)
            {
                order.OrderCustomData.SetValue(XperienceStripeConstants.CHECKOUT_ID_KEY, checkoutSession.Id);

                if ((stripeEvent.Type == Events.CheckoutSessionCompleted && checkoutSession.PaymentStatus == "paid"))
                {
                    UpdateOrderToPaid(order);
                }
                else if (stripeEvent.Type == Events.CheckoutSessionCompleted && checkoutSession.PaymentStatus == "unpaid")
                {
                    UpdateOrderToAuthorized(order, checkoutSession.PaymentIntentId);
                }
                else if (stripeEvent.Type == Events.CheckoutSessionExpired)
                {
                    UpdateOrderToFailed(order);
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
                }
                order.Update();
            }
        }


        protected virtual void UpdateOrderFromPaymentIntent(Event stripeEvent)
        {
            if(stripeEvent != null && stripeEvent.Data != null && stripeEvent.Data.Object != null)
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent != null)
                {
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
                            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.unsupportedeventtype"), stripeEvent.Type);
                        }
                        order.Update();
                    }
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.nopaymentintentinevent"));
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.noobjectinevent"));
            }
        }


        protected virtual Event GetStripeEvent(IEnumerable<string> headerValues, string json, string webhookSecret)
        {
            var signatureHeader = headerValues.FirstOrDefault();
            Event stripeEvent = null;

            if (!string.IsNullOrEmpty(signatureHeader))
            {
                try
                {
                    //this both creates the event object and checks if the secret key matches
                    stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
                }
                catch (StripeException ex)
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.noevent"), ex.Message + "\r\n" + ex.StackTrace);
                }
            }
            return stripeEvent;
        }


        bool TryGetValidStatus(int statusID, out OrderStatusInfo status)
        {
            status = orderStatusInfoProvider.Get(statusID);
            return status != null;
        }
    }
}
