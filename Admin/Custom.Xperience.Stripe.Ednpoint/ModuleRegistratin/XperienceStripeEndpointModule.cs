using CMS;
using CMS.Core;
using CMS.DataEngine;
using Custom.Xperience.Stripe.Endpoint;
using System.Web.Http;

[assembly: RegisterModule(typeof(XperienceStripeEndpointModule))]
namespace Custom.Xperience.Stripe.Endpoint
{
    public class XperienceStripeEndpointModule : Module
    {
        public XperienceStripeEndpointModule() : base("XperienceStripeEndpoint")
        {
        }

        protected override void OnInit()
        {
            base.OnInit();

            GlobalConfiguration.Configuration.Routes.MapHttpRoute(
                "xperience-stripe",
                "xperience-stripe/updateorder",
                defaults: new { controller = "Stripe", action = "Update" }
            );
        }
    }
}
