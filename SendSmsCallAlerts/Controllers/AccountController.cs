using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AccountController(ApplicationDbContext dbContext)
        {
            _context = dbContext;
        }

        public IActionResult LogIn(int id = 0)
        {
            if (TempData["returnUrl"] != null)
            {
                ViewBag.ReturnUrl = TempData["returnUrl"];
            }

            if (id == -2)
            {
                ViewBag.ErrMsg = "Your session has been expired. Please login again!";
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(LogInViewModel model, string storedurl)
        {
            var returnUrl = storedurl;

                var usr = _context.User.Where(r => r.Username == model.Username && r.Password == model.Password).FirstOrDefault();
                if (usr != null)
                {
                    //if (usr.Username == "Snowbro")
                    //{
                        HttpContext.Session.SetString("UserRole", usr.Role.ToString());
                        HttpContext.Session.SetString("UserName", usr.Username);
                        HttpContext.Session.SetString("UserId", usr.Id.ToString());

                        return RedirectToAction("IncomingLogs", "Sms");
                    //}
                
                    //if (!string.IsNullOrEmpty(returnUrl))
                    //{
                    //    HttpContext.Session.Remove("ReturnUrl"); // Remove it from session after use
                    //    return Redirect(returnUrl);
                    //}
                    //else
                    //{
                    //    return RedirectToAction("Index", "Statistics");
                    //}

                }
                else
                {
                    ViewBag.ErrMsg = "Invalid Username Or Password!";
                }

            return View();
        }

        public IActionResult Logout(int id = 0, string returnUrl = "")
        {
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("UserName");
            HttpContext.Session.Remove("UserId");

            if (id == -2)
            {
                TempData["returnUrl"] = returnUrl;
                return RedirectToAction("LogIn", new { id = -2, returnUrl = returnUrl });
            }

            else
                return RedirectToAction("LogIn");
        }


    }
}
