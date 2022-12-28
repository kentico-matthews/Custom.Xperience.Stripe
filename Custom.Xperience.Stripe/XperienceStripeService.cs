using System.Linq;
using System.Collections.Generic;
using Stripe.Checkout;
using CMS.Ecommerce;
using CMS.Helpers;
using System;
using Stripe;

namespace Custom.Xperience.Stripe
{
    public class XperienceStripeService : IXperienceStripeService
    {
        /// <summary>
        /// Prepares session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        public virtual SessionCreateOptions getDirectOptions(OrderInfo order, string successUrl, string cancelUrl)
        {
            var lineItems = GetLineItems(order);
            
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


        /// <summary>
        /// Prepares async session options for a Stripe checkout session
        /// </summary>
        /// <param name="order">The kentico OrderInfo object for which payment is required</param>
        /// <param name="successUrl">The Url that the cusotmer will be directed to after successful payment</param>
        /// <param name="cancelUrl">The Url that the cusotmer will be directed to after failed payment</param>
        /// <returns>Session options for creating a Stripe Checkout session.</returns>
        public virtual SessionCreateOptions getDelayedOptions(OrderInfo order, string successUrl, string cancelUrl)
        {
            var lineItems = GetLineItems(order);

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


        //Lists items for the checkout description, as kentico-side calculation requires a single line item in stripe
        protected virtual string CreateDescription(int orderId)
        {
            string description = String.Empty;
            var orderItemsQuery = OrderItemInfo.Provider.Get().WhereEquals("OrderItemOrderID", orderId);

            //execute the query so that multiple database calls don't happen when we filter the data
            var orderItems = orderItemsQuery.GetEnumerableTypedResult().ToArray();

            for(int i = 0; i < orderItems.Length; i++)
            {
                //if the order item is not a product option
                if (orderItems[i].OrderItemParentGUID == System.Guid.Empty)
                {
                    //add the product name
                    string name = String.Empty;
                    name += orderItems[i].OrderItemSKUName;
                    
                    //find and list any "child" product options for the item
                    var options = orderItems.Where(x => x.OrderItemParentGUID.Equals(orderItems[i].OrderItemGUID));
                    foreach (OrderItemInfo option in options)
                    {
                        name += $" ({option.OrderItemSKUName})";
                    }
                    //if the previous stuff resulted in any text, and we're not on the last line add a delimiter (line breaks don't work)
                    name += name.Equals(String.Empty) || (i == orderItems.Length - 1) ? String.Empty : ", ";
                    description += name;
                }
            }
            return description.Equals(String.Empty) ? ResHelper.GetString("custom.stripe.checkout.defaultdescription") : description;
        }


        //Create line item set for the stripe checkout
        protected virtual List<SessionLineItemOptions> GetLineItems(OrderInfo order)
        {
            //Since calculation of discounts, taxes, etc. is handled on kentico side, we must submit the whole order as a signleline item
            var lineItems = new List<SessionLineItemOptions>();
            lineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = CurrencyInfo.Provider.Get(order.OrderCurrencyID).CurrencyCode,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = ResHelper.GetString("custom.stripe.checkout.payment"),
                        Description = CreateDescription(order.OrderID)
                    },

                    //stripe uses cents or analogous units of whichever currency is being used
                    UnitAmountDecimal = order.OrderGrandTotal * 100
                },
                Quantity = 1
            });
            return lineItems;
        }
    }
}
