using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Web;

namespace SendSmsCallAlerts.Filters
{
    public class SessionCheckingAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ActionDescriptor.EndpointMetadata.Any(em => em.GetType() == typeof(AllowAnonymousAttribute))
                && !context.ActionDescriptor.EndpointMetadata.Any(em => em.GetType() == typeof(AllowAnonymousAttribute)))
            {
                if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    if (context.HttpContext.Session.GetString("UserId") == null)
                    {
                        context.HttpContext.Response.StatusCode = 403;
                        context.Result = new JsonResult("LogOut");
                    }
                }
                else if (context.HttpContext.Session.GetString("UserId") == null)
                {
                    var returnUrlWithQueryString = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                    var redirectUrl = $"/Account/Logout?id=-2&returnUrl={HttpUtility.UrlEncode(returnUrlWithQueryString)}";
                    context.Result = new RedirectResult(redirectUrl);
                }

            }
        }

    }
}
