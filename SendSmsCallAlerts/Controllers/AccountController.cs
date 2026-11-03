using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    public class AccountController : Controller
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(10);

        private readonly ApplicationDbContext _context;
        private readonly IPasswordService _passwords;
        private readonly IMemoryCache _cache;
        private readonly ActivityTracker _activity;

        public AccountController(ApplicationDbContext dbContext, IPasswordService passwords, IMemoryCache cache, ActivityTracker activity)
        {
            _context = dbContext;
            _passwords = passwords;
            _cache = cache;
            _activity = activity;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LogInViewModel model, string storedurl)
        {
            var returnUrl = storedurl;
            ViewBag.ReturnUrl = string.IsNullOrEmpty(returnUrl) ? null : returnUrl;

            var username = model?.Username?.Trim() ?? "";
            var password = model?.Password ?? "";

            if (username.Length == 0 || password.Length == 0)
            {
                ViewBag.ErrMsg = "Invalid Username Or Password!";
                return View("LogIn", model);
            }

            // simple brute-force protection: 5 failures per username+IP, locked for 10 minutes
            var throttleKey = $"login-fail:{username.ToLowerInvariant()}|{HttpContext.Connection.RemoteIpAddress}";
            _cache.TryGetValue(throttleKey, out int failedAttempts);
            if (failedAttempts >= MaxFailedAttempts)
            {
                ViewBag.ErrMsg = "Too many failed attempts. Please try again in 10 minutes.";
                return View("LogIn", model);
            }

            var candidates = _context.User.Where(r => r.Username == username).ToList();
            SendSmsCallAlerts.Models.User? usr = null;

            if (candidates.Count == 0)
            {
                _passwords.SimulateWork();
            }
            foreach (var candidate in candidates)
            {
                var check = _passwords.Verify(candidate, password);
                if (check == PasswordCheck.Failed) continue;

                usr = candidate;
                if (check == PasswordCheck.SuccessRehashNeeded)
                {
                    usr.Password = _passwords.Hash(usr, password); // upgrades legacy plaintext / old hash settings
                    _context.SaveChanges();
                }
                break;
            }

            if (usr != null)
            {
                _cache.Remove(throttleKey);

                //if (usr.Username == "Snowbro")
                //{
                // Just for check that it work good 
                HttpContext.Session.Clear(); // never carry an old session into a new login
                HttpContext.Session.SetString("UserRole", AppRoles.Normalize(usr.Role));
                HttpContext.Session.SetString("UserName", usr.Username);
                HttpContext.Session.SetString("UserId", usr.Id.ToString());

                if (ActivityTracker.IsStaff(usr.Role)) await _activity.StartSessionAsync(HttpContext, usr.Id);   // STAFF only

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

            _cache.Set(throttleKey, failedAttempts + 1, LockoutWindow);
            await _activity.LoginFailedAsync(HttpContext, username);
            ViewBag.ErrMsg = "Invalid Username Or Password!";
            return View("LogIn", model);
        }

        public async Task<IActionResult> Logout(int id = 0, string returnUrl = "")
        {
            if (ActivityTracker.IsStaff(HttpContext.Session.GetString("UserRole"))
                && int.TryParse(HttpContext.Session.GetString("UserId"), out var trackedUserId))
                await _activity.EndSessionAsync(HttpContext, trackedUserId, id == -2 ? LogoutTypes.System : LogoutTypes.Manual);

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

        // Logged-in user opened a page their role cannot access
        public IActionResult AccessDenied()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return RedirectToAction("LogIn");

            Response.StatusCode = 403;
            return View();
        }
    }
}