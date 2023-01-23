using CMS.Helpers;
using Stripe;
using CMS.Core;

namespace Custom.Xperience.Stripe
{
    public static class CaptureHelper
    {
        private static IResolvablePaymentIntentService paymentIntentService;

        public static void Init()
        {
            paymentIntentService = Service.Resolve<IResolvablePaymentIntentService>();
        }

        /// <summary>
        /// Tries to capture the payment intent with the supplied ID.
        /// </summary>
        /// <param name="paymentIntentID">The ID of the payment intent to be captured.</param>
        /// <returns>True if the full amount of the payment intent was captured</returns>
        /// <exception cref="StripeException">Throws exception if secret key is missing from web.config</exception>
        public static PaymentIntent CapturePayment(string paymentIntentID)
        {
            if (string.IsNullOrEmpty(StripeConfiguration.ApiKey))
            {
                throw new StripeException(ResHelper.GetString("custom.stripe.error.secretkeymissing"));
            }
            PaymentIntent intent = paymentIntentService.Capture(paymentIntentID);
            return intent;
            
        }
        
    }
}
