using Stripe;
using CMS.Core;
using CMS.Ecommerce;

namespace Custom.Xperience.Stripe
{
    public static class CaptureHelper
    {
        private static IResolvablePaymentIntentService paymentIntentService;
        private static IEventLogService eventLogService;
        private static ILocalizationService localizationService;

        public static void Init()
        {
            paymentIntentService = Service.Resolve<IResolvablePaymentIntentService>();
            eventLogService = Service.Resolve<IEventLogService>();
            localizationService = Service.Resolve<ILocalizationService>();
        }

        /// <summary>
        /// Tries to capture the payment intent with the supplied ID.
        /// </summary>
        /// <param name="paymentIntentID">The ID of the payment intent to be captured.</param>
        /// <returns>True if the full amount of the payment intent was captured</returns>
        /// <exception cref="StripeException">Throws exception if secret key is missing from web.config, or if something goes wrong with the capture.</exception>
        public static PaymentIntent CapturePayment(string paymentIntentID)
        {
            if (string.IsNullOrEmpty(StripeConfiguration.ApiKey))
            {
                throw new StripeException(localizationService.GetString("custom.stripe.error.secretkeymissing"));
            }
            if (string.IsNullOrEmpty(paymentIntentID))
            {
                throw new StripeException(localizationService.GetString("custom.stripe.error.paymentintentmissing"));
            }
            return paymentIntentService.Capture(paymentIntentID);

        }

        /// <summary>
        /// Tries to capture payment for the supplied order. 
        /// Logs errors to Event log if capture fails or less than the full amount is captured
        /// </summary>
        /// <param name="order">The order to capture payment for</param>
        public static void CaptureOrder(OrderInfo order)
        {
            if (order != null)
            {
                //Get the payment intent from the order's custom data.
                var paymentIntentID = (string)order.OrderCustomData.GetValue(XperienceStripeConstants.PAYMENT_INTENT_ID_KEY);

                if (!string.IsNullOrEmpty(paymentIntentID))
                {
                    try
                    {
                        //Capture the payment.
                        if (CapturePayment(paymentIntentID).AmountCapturable != 0)
                        {
                            //log a warning if the full amount was not captured
                            eventLogService.LogEvent(EventTypeEnum.Warning, "Stripe", localizationService.GetString("custom.stripe.warning.partialamount"), $"OrderID: {order.OrderID} \r\nPaymentIntentID: {paymentIntentID}");
                        }
                    }
                    catch (StripeException ex)
                    {
                        eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", "Stripe", ex.Message + "\r\n" + ex.StackTrace);
                    }
                }
                else
                {
                    eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.paymentintentmissing"), $"OrderID {order.OrderID}");
                }
            }
            else
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.ordernotfound"), $"OrderID {order.OrderID}");
            }
        }
    }
}
