using CMS.Core;
using CMS.Helpers;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Custom.Xperience.Stripe
{
    public static class CaptureHelper
    {
        /// <summary>
        /// Tries to capture the payment intent with the supplied ID.
        /// </summary>
        /// <param name="paymentIntentID">The ID of the payment intent to be captured.</param>
        /// <returns>True if the full amount of the payment intent was captured</returns>
        /// <exception cref="StripeException">Throws exception if secret key is missing from web.config</exception>
        public static bool CapturePayment(string paymentIntentID)
        {
            StripeConfiguration.ApiKey = Service.Resolve<IAppSettingsService>()["CustomStripeSecretKey"];
            if (string.IsNullOrEmpty(StripeConfiguration.ApiKey))
            {
                throw new StripeException(ResHelper.GetString("custom.stripe.error.secretkeymissing"));
            }
            var service = new PaymentIntentService();
            PaymentIntent intent = service.Capture(paymentIntentID);
            return intent.AmountReceived == intent.AmountCapturable;
            
        }
        
    }
}
