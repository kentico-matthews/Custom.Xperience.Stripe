using Stripe;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Custom.Xperience.Stripe
{
    public interface IResolvablePaymentIntentService
    {
        PaymentIntent ApplyCustomerBalance(string id, PaymentIntentApplyCustomerBalanceOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> ApplyCustomerBalanceAsync(string id, PaymentIntentApplyCustomerBalanceOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Cancel(string id, PaymentIntentCancelOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> CancelAsync(string id, PaymentIntentCancelOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Capture(string id, PaymentIntentCaptureOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> CaptureAsync(string id, PaymentIntentCaptureOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Confirm(string id, PaymentIntentConfirmOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> ConfirmAsync(string id, PaymentIntentConfirmOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Create(PaymentIntentCreateOptions options, RequestOptions requestOptions = null);

        Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Get(string id, PaymentIntentGetOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> GetAsync(string id, PaymentIntentGetOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent IncrementAuthorization(string id, PaymentIntentIncrementAuthorizationOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> IncrementAuthorizationAsync(string id, PaymentIntentIncrementAuthorizationOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        StripeList<PaymentIntent> List(PaymentIntentListOptions options = null, RequestOptions requestOptions = null);

        Task<StripeList<PaymentIntent>> ListAsync(PaymentIntentListOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        IEnumerable<PaymentIntent> ListAutoPaging(PaymentIntentListOptions options = null, RequestOptions requestOptions = null);

        IAsyncEnumerable<PaymentIntent> ListAutoPagingAsync(PaymentIntentListOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        StripeSearchResult<PaymentIntent> Search(PaymentIntentSearchOptions options = null, RequestOptions requestOptions = null);

        Task<StripeSearchResult<PaymentIntent>> SearchAsync(PaymentIntentSearchOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        IEnumerable<PaymentIntent> SearchAutoPaging(PaymentIntentSearchOptions options = null, RequestOptions requestOptions = null);

        IAsyncEnumerable<PaymentIntent> SearchAutoPagingAsync(PaymentIntentSearchOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent Update(string id, PaymentIntentUpdateOptions options, RequestOptions requestOptions = null);

        Task<PaymentIntent> UpdateAsync(string id, PaymentIntentUpdateOptions options, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

        PaymentIntent VerifyMicrodeposits(string id, PaymentIntentVerifyMicrodepositsOptions options = null, RequestOptions requestOptions = null);

        Task<PaymentIntent> VerifyMicrodepositsAsync(string id, PaymentIntentVerifyMicrodepositsOptions options = null, RequestOptions requestOptions = null, CancellationToken cancellationToken = default);

    }
}
