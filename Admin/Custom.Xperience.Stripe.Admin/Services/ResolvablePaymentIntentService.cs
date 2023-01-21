using Stripe;
using Custom.Xperience.Stripe;
using CMS;
using CMS.Core;

[assembly: RegisterImplementation(typeof(IResolvablePaymentIntentService), typeof(ResolvablePaymentIntentService), Priority = RegistrationPriority.Default)]
namespace Custom.Xperience.Stripe
{
    internal class ResolvablePaymentIntentService : PaymentIntentService, IResolvablePaymentIntentService
    {

    }
}
