# Xperience 13 Stripe Integration

## Summary
This community integration connects Kentico Xperience 13 with [Stripe](https://stripe.com/) for processing payments in your E-commerce store. (Please note that it is not an official integration, and has not been reviewed or tested by the Kentico development and QA staff. If you run into any problems, please create an issue in this repository rather than contacting Kentico support.)

This repository uses [Stripe Checkout](https://stripe.com/payments/checkout), where the customer enters their payment data into a form hosted by Stripe, meaning your servers do not need to touch their card information.

This Xperience Stripe library allows you to accept payments both through direct capture, and delayed capture of payment within the standard (typically 7-day) authorization window.

The Orders will be updated based on their payment status in Stripe, through an endpoint in the admin application.

If the **Order status for capture** setting under **Settings > Integration > Stripe** is configured, the capture of approved delayed capture payments can be triggered by setting orders to the specified status.

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
1. In the properties of your newly created webhook, click *Reveal* under **Signing secret**.
1. Add your this value to the **appSettings** section of your **web.config** file with the key *CustomStripeWebhookSecret*.
1. In the Stripe dashboard, go to **Developers > API Keys** and reveal and copy the **Secret key**.
1. Add your secret key from stripe under the **appSettings** section of your **web.config** file, and to your live site's **appconfig.json** (or other custom configuration) file with the key *CustomStripeSecretKey*.

### **Kentico Xperience Admin**
1. Install the [kentico-matthews.Custom.Xperience.Stripe.Admin](https://www.nuget.org/packages/kentico-matthews.Custom.Xperience.Stripe.Admin/) NuGet package and build your solution
1. Open the **Store configuration** or **Multistore Configuration** app in Xperience 13, whichever you're using for your shop.
1. Go to the **Payment Methods** Tab.
1. Click the button to create a **New payment method**.
1. Set the **Display name** and **Code name** to *Stripe*.
   * The display name can be different if you want, these projects rely on the code name being *Stripe*.
1. Designate order statuses for each of the following:
   * Order status if payment succeeds
   * Order status if payment is authorized
   * Order status if payment fails
1. Download the import package **Custom.Xperience.Stripe.zip** from the **Admin\Import Package** folder in this repository and copy it into the **~/CMSSiteUtils/Import** folder of your admin site.
1. Open the **Sites** application, and click the button to **Import site or objects**.
1. Choose the import package and pre-select all objects, then complete the import.
1. If you plan on using delayed capture for payments, go to **Settings > Integration > Stripe**, and set an order status that will trigger the capture of an order.

### **Live Site**
1. Install the [kentico-matthews.Custom.Xperience.Stripe.LiveSite](https://www.nuget.org/packages/kentico-matthews.Custom.Xperience.Stripe.LiveSite/) NuGet package and build your solution.
1. (Optional) Register **XperienceStripeService** as the implementation of **IXperienceStripeService** with your IoC container so that you can inject it.
1. During your checkout, make sure that the payment method of the current shopping cart is set to the one you created for Stripe.
1. If so, once **shoppingService.CreateOrder()** is called, you can pass the resulting order object to the **GetDirectOptions** or **GetDelayedOptions** method of the **XperienceStripeService**, depending on whether you want to use direct or delayed capture.
1. You can then use these options to create a Stripe Checkout Session, as shown in the below example.

---

## Examples
The following example demonstrates the use of the Stripe integration in a checkout controller on the live site. (It utilizes both direct capture and delayed capture, with a bool variable to determine which. This is not necessary, and whichever option you prefer can be used on its own.)

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

If you are not using Dependency Injection, instantiate an instance of XperienceStripeService.

```c#
var xperienceStripeService = new XperienceStripeService();

var options = xperienceStripeService.getDirectOptions(order, Url.Action(action: "ThankYou", controller: "Checkout"), Url.Action(action: "Login", controller: "Account"));
```

If you prefer to manually capture an order's payment, rather than using the Order-Status-based setting, you can use code similar to the following example. (For instance, you could do this in a custom scheduled task.)

```c#
//Get the order
var order = OrderInfo.Provider.Get(100)

//Get the Stripe payment intent ID from the order's custom data.
var paymentIntentID = (string)order.OrderCustomData.GetValue("StripePaymentIntentID");

if(!String.IsNullOrEmpty(paymentIntentID))
{
	try
	{
		//Capture the payment.
		CaptureHelper.CapturePayment(paymentIntentID);
	}
	catch(StripeException ex)
	{
		//Capture failed, handle the exception.
		//...
	}
}
else
{
	//No payment intent reference found, handle the exception.
	//...
}
```

---
## Development
If this repository doesn't meet your requirements, there are two main approaches to customize it.
### **Extending the classes**
Many of the classes in this repository contain virtual members which can be overridden in child classes. If your requiremetns differ only sligtly form the existing implementation, it may be easiest to install the NuGet packages and override any necessary methods, substituting your own business logic.
### **Forking the repository**
If your use case requires more in-depth or fundamental changes, you can fork (or just download and modify) the repository, and make whatever structural changes you need. In this case, you'll need to add the live site and admin class library projects from the repository to the appropriate solutions in Visual Studio. Do the following for each of the two projects:
1. Right-click the solution and choose **Add > Existing Project**.
1. Expand the main project of the solution (either the live site or admin WebApp). Here, you should either see a node for **Dependencies** or **References**, depending on the type of your project.
1. Right click this node, and choose either **Add Reference...** or **Add Project Reference...**
1. On the **Projects** tab of the resulting modal window, make sure that the checkbox next to the class library project is selected.
1. Click **Ok** to close the modal.