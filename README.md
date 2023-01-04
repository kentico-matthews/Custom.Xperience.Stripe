# Xperience 13 Stripe Integration

## Summary
This community integration connects Kentico Xperience 13 with [Stripe](https://stripe.com/) for processing payments in your E-commerce store.

It uses [Stripe Checkout](https://stripe.com/payments/checkout), where the customer enters their payment data into a form hosted by Stripe, meaning your servers do not need to touch their card information.

With this library, you'll be able to accept payments both through direct capture, and delayed capture of payment within the standard (typically 7-day) authorization window.

The Orders will be updated based on their payment status in Stripe, through an endpoint in the admin application.

## Setup
---
### **Stripe Dashboard**
1. Log into the [Stripe Dashboard](https://dashboard.stripe.com/) and set up your business.
1. Click on **Developers** and go to the **Webhooks** tab.
1. Under **Hosted Endpiont**, click **Add Endpoint**
1. Set the **Endpoint URL** to *[YOUR ADMIN DOMIAN]/stripe_admin/xperience-stripe/updateorder*
1. Under the **Select events to listen to** label, click the **Select events** button and choose the following:
   * Checkout
     * checkout.session.completed
     * checkout.session.expired
     * checkout.session.async_payment_failed
     * checkout.session.async_payment_succeeded
   * Payment
     * payment_intent.payment_failed
     * payment_intent.succeeded
1. Click the **Add enpoint** button
1. In the properties of your newly cretaed webhook, click *Reveal* under **Signing secret**.
1. Add your this value to the **appSettings** section of your **web.config** file with the key *CustomStripeSecretKey*.
1. In the Stripe dashboard, go to **Developers > API Keys** and reveal and copy the **Secret key**.
1. Add your secret key from stripe under the **appSettings** section of your **web.config** file, and to your live site's **appconfig.json** (or other custom configuration) file with the key *CustomStripeSecretKey*.

### **Kentico Xperience Admin**
1. Install the [ADMINPACKAGENAME] package and build your solution
1. Open the **Store configuration** or **Multistore Configuration** app in Xperience 13, whichever you're using for your shop.
1. Go to the **Payment Methods** Tab.
1. Click the button to create a **New payment method**.
1. Set the **Display name** and **Code name** to *Stripe*.
   * The display name can be different if you want, but some of the code relies on the code name being *Stripe*.
1. Designate order statuses for each of the following:
   * Order status if payment succeeds
   * Order status if payment is authorized
   * Order status if payment fails
1. Download the import package **Custom.Xperience.Stripe.zip** from the **Import Package** folder in this repository and copy it into the **~/CMSSiteUtils/Import** folder of your admin site.
1. Open the **Sites** application, and click the button to **Import site or objects**.
1. Choose the import package and pre-select all objects, then complete the import.
1. If you plan on using delayed capture for payments, go to **Settings > Integration > Stripe**, and set an order status that will trigger the capture of an order.

### **Live site**
1. Install the [LIVESITEPACKAGENAME] package and build your solution
1. (Optional) Register XperienceStripeService as the implementation of IXperienceStripeService with your IoC container so that you can inject it
1. During your checkout, check whether the payment method of the current shopping cart is set to the one you created for Stripe
1. If so, once shoppingService.CreateOrder() is called, you can pass the resulting order object to the GetDirectOptions or GetDelayedOptions method of the XperienceStripeService, depending on whether you want to use direct or delayed capture
1. You can then use these options to create a Stripe Checkout Session, as shown in the below example

---

## Examples
The following example demonstrates the use of the stripe integration in a checkout controller. (It utilizes both direct capture and delayed capture, choosing which to use based on a boolean. This is not necessary, and whichever option you prefer can be used on its own.)

```c#
private readonly IShoppingService shoppingService;
//...
private readonly IXperienceStripeService xperienceStripeService;

public CheckoutController(IShoppingService shoppingService, /*...,*/ IXperienceStripeService xperienceStripeService)
{
	this.shoppingService = shoppingService;
	//...
	this.xperienceStripeService = xperienceStripeService;
}

[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Pay(PayViewModel model)
{
	var cart = shoppingService.GetCurrentShoppingCart();

	//...

	//Check if the shopping cart is set to use the Stripe payment option.
	var paymentOption = PaymentOptionInfo.Provider.Get().WhereEquals("PaymentOptionName", "Stripe").First();
	if (cart.ShoppingCartPaymentOptionID == paymentOption.PaymentOptionID)
	{
		try{
			//Convert the ShoppingCartInfo object to an OrderInfo object.
			var order = shoppingService.CreateOrder();

			//Create stripe checkout options based on whether or not we want delayed capture.
			SessionCreateOptions options;
			if (useDelayedCapture)
			{
				//Creates options for an asynchronous checkout session, where payment is not captured immediately at checkout (delayed capture).
				options = xperienceStripeService.getDelayedOptions(order, Url.Action(action: "ThankYou", controller: "Checkout"), Url.Action(action: "Login", controller: "Account"));
			}
			else
			{
				//Creates options for a standard checkout session, where paymnet is captured when the cusotmer completes the checkout (direct capture).
				options = xperienceStripeService.getDirectOptions(order, Url.Action(action: "ThankYou", controller: "Checkout"), Url.Action(action: "Login", controller: "Account"));
			}
			
			//Create Stripe checkout session.
			var service = new SessionService();
			Session session = service.Create(options);

			//Redirect to Stripe checkout.
			Response.Headers.Add("Location", session.Url);
			return new StatusCodeResult(303);
		}
		catch(Exception ex)
		{
			//...
			return new StatusCodeResult(500);
		}
	}
}
```
The above example requires XperienceStripeService to be registered with your IoC container as an implementation of IXperienceStripeService.

If you are not using Dependency Injection, you can simply instantiate an instance of XperienceStripeService.

```c#
var xperienceStripeService = new XperienceStripeService();

var options = xperienceStripeService.getDirectOptions(order, Url.Action(action: "ThankYou", controller: "Checkout"), Url.Action(action: "Login", controller: "Account"));
```

