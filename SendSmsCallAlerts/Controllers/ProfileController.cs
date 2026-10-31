using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    [RoleAuthorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordService _passwords;
        private readonly IMemoryCache _cache;

        public ProfileController(ApplicationDbContext context, IPasswordService passwords, IMemoryCache cache)
        {
            _context = context;
            _passwords = passwords;
            _cache = cache;
        }

        private int CurrentUserId =>
            int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : 0;

        private async Task<User?> CurrentUser() =>
            await _context.User.FirstOrDefaultAsync(user => user.Id == CurrentUserId);

        private static ProfilePageViewModel BuildModel(User user, ProfileDetailsViewModel? profile = null,
            ChangePasswordViewModel? password = null, bool passwordOpen = false) => new()
        {
            Username = user.Username,
            Role = AppRoles.Normalize(user.Role),
            Phone = user.Phone,
            Profile = profile ?? new ProfileDetailsViewModel
            {
                Name = user.Name,
                Email = user.Email,
            },
            Password = password ?? new ChangePasswordViewModel(),
            PasswordOpen = passwordOpen
        };

        public async Task<IActionResult> Index()
        {
            var user = await CurrentUser();
            return user == null ? RedirectToAction("LogIn", "Account") : View(BuildModel(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileDetailsViewModel model)
        {
            var user = await CurrentUser();
            if (user == null) return RedirectToAction("LogIn", "Account");

            if (ModelState.IsValid)
            {
                user.Name = model.Name?.Trim();
                user.Email = model.Email?.Trim();
                await _context.SaveChangesAsync();
                TempData["ProfileMessage"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), BuildModel(user, model, passwordOpen: false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel model)
        {
            var user = await CurrentUser();
            if (user == null) return RedirectToAction("LogIn", "Account");

            var throttleKey = $"profile-password-fail:{CurrentUserId}|{HttpContext.Connection.RemoteIpAddress}";
            _cache.TryGetValue(throttleKey, out int failedAttempts);
            if (failedAttempts >= 5)
                ModelState.AddModelError(nameof(model.CurrentPassword), "Too many failed attempts. Please try again in 10 minutes.");

            var check = _passwords.Verify(user, model.CurrentPassword);
            if (check == PasswordCheck.Failed)
            {
                _cache.Set(throttleKey, failedAttempts + 1, TimeSpan.FromMinutes(10));
                ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
            }
            else if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "The new password must differ from the current password.");
            }

            if (model.NewPassword != model.ConfirmPassword)
                ModelState.AddModelError(nameof(model.ConfirmPassword), "The passwords do not match.");

            if (ModelState.IsValid)
            {
                user.Password = _passwords.Hash(user, model.NewPassword);
                await _context.SaveChangesAsync();
                _cache.Remove(throttleKey);
                TempData["PasswordMessage"] = "Password changed successfully.";
                return RedirectToAction(nameof(Index));
            }

            var page = BuildModel(user);
            return View(nameof(Index), BuildModel(user, password: model, passwordOpen: true));
        }
    }
}