using CMS.Core;
using CMS.Ecommerce;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Custom.Xperience.Stripe
{
    public class XperienceStripeService : IXperienceStripeService
    {
        private readonly IOrderItemInfoProvider orderItemInfoProvider;
        private readonly ICurrencyInfoProvider currencyInfoProvider;
        private readonly ILocalizationService localizationService;
        private readonly IEventLogService eventLogService;


        /// <summary>
        /// Creates a new instance of <see cref="XperienceStripeService"/>.
        /// </summary>
        /// <param name="orderItemInfoProvider">An IOrderItemInfoProvider, provided by dependency injection.</param>
        /// <param name="currencyInfoProvider">An ICurrencyInfoProvider, provided by dependency injection.</param>
        /// <param name="localizationService">An ILocalizationService, provided by dependency injection.</param>
        public XperienceStripeService(IOrderItemInfoProvider orderItemInfoProvider, ICurrencyInfoProvider currencyInfoProvider, ILocalizationService localizationService, IEventLogService eventLogService)
        {
            this.orderItemInfoProvider = orderItemInfoProvider;
            this.currencyInfoProvider = currencyInfoProvider;
            this.localizationService = localizationService;
            this.eventLogService = eventLogService;
        }


        /// <summary>
        /// Prepares session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        public virtual SessionCreateOptions? GetDirectOptions(OrderInfo order, string successUrl, string cancelUrl)
        {
            if (order == null)
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.ordernotfound"));
                return null;
            }
            var lineItems = GetLineItems(order);
            if (lineItems.Count > 0 && !string.IsNullOrEmpty(successUrl) && !string.IsNullOrEmpty(cancelUrl))
            {
                var options = new SessionCreateOptions
                {
                    LineItems = lineItems,
                    Mode = "payment",
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    ClientReferenceId = order.OrderID.ToString()
                };
                return options;
            }
            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.failedsessioncreateoptions"), $"Line Items count: {lineItems.Count},\r\nSuccess url: {successUrl},\r\nCancel url: {cancelUrl} ");
            return null;
            
        }


        /// <summary>
        /// Prepares async session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        public virtual SessionCreateOptions? GetDelayedOptions(OrderInfo order, string successUrl, string cancelUrl)
        {
            if (order == null)
            {
                eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.ordernotfound"));
                return null;
            }
            var lineItems = GetLineItems(order);
            if (lineItems.Count > 0 && !string.IsNullOrEmpty(successUrl) && !string.IsNullOrEmpty(cancelUrl))
            {
                var options = new SessionCreateOptions
                {
                    LineItems = lineItems,
                    Mode = "payment",
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    ClientReferenceId = order.OrderID.ToString(),
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        CaptureMethod = "manual",
                    }
                };

                return options;
            }
            eventLogService.LogEvent(EventTypeEnum.Error, "Stripe", localizationService.GetString("custom.stripe.error.failedsessioncreateoptions"), $"Line Items count: {lineItems.Count},\r\nSuccess url: {successUrl},\r\nCancel url: {cancelUrl} ");
            return null;
        }


        //Lists items for the checkout description, as all the calculation happens on the Kentico side, meaning separate line items can't be used.
        protected virtual string CreateDescription(int orderId)
        {
            string description = String.Empty;
            var orderItemsQuery = orderItemInfoProvider.Get()
                .Columns("OrderItemParentGUID", "OrderItemSKUName", "OrderItemGUID", "OrderItemOrderID")
                .WhereEquals("OrderItemOrderID", orderId);

            //Execute the query now, so that multiple database calls don't happen when filtering the data
            var orderItems = orderItemsQuery.GetEnumerableTypedResult().ToArray();

            for(int i = 0; i < orderItems.Length; i++)
            {
                //Check if the item is a "parent" product, rather than a product option.
                if (orderItems[i].OrderItemParentGUID == System.Guid.Empty)
                {
                    //Add the product name.
                    string name = String.Empty;
                    name += orderItems[i].OrderItemSKUName;
                    
                    //Find and list any "child" product options for the item.
                    var options = orderItems.Where(x => x.OrderItemParentGUID.Equals(orderItems[i].OrderItemGUID));
                    foreach (OrderItemInfo option in options)
                    {
                        name += $" ({option.OrderItemSKUName})";
                    }
                    //If the previous stuff resulted in any text, and we're not on the last line, add a delimiter (line breaks don't work).
                    name += name.Equals(String.Empty) || (i == orderItems.Length - 1) ? String.Empty : ", ";
                    description += name;
                }
            }
            return description.Equals(String.Empty) ? localizationService.GetString("custom.stripe.checkout.defaultdescription") : description;
        }


        //Create line item list for the stripe checkout.
        protected virtual List<SessionLineItemOptions> GetLineItems(OrderInfo order)
        {
            var lineItems = new List<SessionLineItemOptions>();
            var currency = currencyInfoProvider.Get(order.OrderCurrencyID);
            if (order != null && currency != null)
            {
                //Only use one line - separate lines requires calculation to happen on Stripe side, which would negate Kentico calculation pipeline.
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.CurrencyCode,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = localizationService.GetString("custom.stripe.checkout.paymentname"),
                            Description = CreateDescription(order.OrderID)
                        },

                        //Stripe uses cents or analogous units of whichever currency is being used.
                        UnitAmountDecimal = order.OrderGrandTotal * 100
                    },
                    Quantity = 1
                });
            }
            return lineItems;
        }
    }
}
