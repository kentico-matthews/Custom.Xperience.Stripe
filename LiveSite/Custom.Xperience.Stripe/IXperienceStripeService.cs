using Stripe.Checkout;
using CMS.Ecommerce;

namespace Custom.Xperience.Stripe
{
    /// <summary>
    /// interface for preparing data for stripe checkout
    /// </summary>
    public interface IXperienceStripeService
    {
        /// <summary>
        /// Prepares session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        SessionCreateOptions? GetDirectOptions(OrderInfo order, string successUrl, string cancelUrl);


        /// <summary>
        /// Prepares session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        SessionCreateOptions? GetDelayedOptions(OrderInfo order, string successUrl, string cancelUrl);
    }
}
