using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    [RoleAuthorize(AppRoles.Admin)]
    public class UsersController : Controller
    {
        // Hidden built-in account: not listed, cannot be created, edited or deleted from the UI.
        private const string HiddenSuperUser = "Snowbro";
        private const int MinPasswordLength = 8;

        private readonly ApplicationDbContext _context;
        private readonly IPasswordService _passwords;

        public UsersController(ApplicationDbContext context, IPasswordService passwords)
        {
            _context = context;
            _passwords = passwords;
        }

        // ---------------- helpers ----------------
        private int CurrentUserId =>
            int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : 0;

        private void LoadRoles(string? selected = null)
        {
            var items = new[]
            {
                new { Value = AppRoles.Admin, Text = "Admin" },
                new { Value = AppRoles.Staff, Text = "Staff" }
            };
            ViewBag.Roles = new SelectList(items, "Value", "Text", selected == null ? null : AppRoles.Normalize(selected));
        }

        // true when another visible Admin (not the hidden account) would still exist
        private bool IsOtherAdminLeft(int excludingUserId) =>
            _context.User.Any(u => u.Id != excludingUserId
                                   && u.Username != HiddenSuperUser
                                   && u.Role != null && u.Role.ToUpper() == AppRoles.Admin);

        private static bool IsHidden(User u) =>
            string.Equals(u.Username, HiddenSuperUser, StringComparison.OrdinalIgnoreCase);

        private static bool TryNormalizePhone(User user)
        {
            var phone = user.Phone;
            var valid = PhoneNumberService.TryNormalize(ref phone);
            user.Phone = phone;
            return valid;
        }

        private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();

        private static bool TryNormalizeUsersPhone(ref string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return true;
            var value = phone.Trim();
            if (!value.StartsWith("+", StringComparison.Ordinal)) return false;
            var digits = value.Substring(1).Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            if (digits.Length < 8 || digits.Length > 15 || digits[0] == '0' || digits.Any(c => c < '0' || c > '9'))
                return false;
            phone = "+" + digits;
            return true;
        }

        private static bool TryNormalizeUsersPhoneValue(string? phone, out string? normalized)
        {
            normalized = phone;
            return TryNormalizeUsersPhone(ref normalized);
        }

        private Dictionary<string, string> UserErrors() =>
            ModelState.Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value!.Errors[0].ErrorMessage);

        private IActionResult UserValidationFailure(User user, string generalMessage)
        {
            if (IsAjax)
                return StatusCode(400, new { success = false, errors = UserErrors(), message = generalMessage });
            ViewBag.ErMsg = generalMessage;
            return FormFailure(user);
        }

        private void ValidateCreateUser(User user)
        {
            user.Username = user.Username?.Trim() ?? "";
            user.Name = user.Name?.Trim();
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim();
            user.Role = user.Role?.Trim();
            ModelState.Clear();

            if (!Regex.IsMatch(user.Username, "^[A-Za-z0-9_.]{3,30}$"))
                ModelState.AddModelError(nameof(user.Username), "Username must be 3 to 30 characters and contain only letters, digits, underscores, and dots.");
            if (string.IsNullOrWhiteSpace(user.Name) || user.Name.Length < 2 || user.Name.Length > 100)
                ModelState.AddModelError(nameof(user.Name), "Name must be between 2 and 100 characters.");
            else if (new EmailAddressAttribute().IsValid(user.Name))
                ModelState.AddModelError(nameof(user.Name), "Enter the person's name, not an email address.");
            if (!string.IsNullOrWhiteSpace(user.Email) && !new EmailAddressAttribute().IsValid(user.Email))
                ModelState.AddModelError(nameof(user.Email), "Enter a valid email address.");
            if (!AppRoles.IsValid(user.Role))
                ModelState.AddModelError(nameof(user.Role), "Role must be Staff or Admin.");
            if (string.IsNullOrEmpty(user.Password))
                ModelState.AddModelError(nameof(user.Password), "Password is required.");
            else if (user.Password.Length < MinPasswordLength || !user.Password.Any(char.IsLetter) || !user.Password.Any(char.IsDigit))
                ModelState.AddModelError(nameof(user.Password), "Password must be at least 8 characters and contain a letter and a digit.");
            if (AppRoles.Normalize(user.Role) == AppRoles.Staff)
            {
                if (string.IsNullOrWhiteSpace(user.Phone))
                    ModelState.AddModelError(nameof(user.Phone), "Twilio number is required for Staff users.");
                else if (!TryNormalizeUsersPhoneValue(user.Phone, out var staffPhone))
                    ModelState.AddModelError(nameof(user.Phone), "Enter a valid phone number in international format, such as +923456543210.");
                else
                    user.Phone = staffPhone;
            }
            else if (!string.IsNullOrWhiteSpace(user.Phone))
            {
                if (!TryNormalizeUsersPhoneValue(user.Phone, out var adminPhone))
                    ModelState.AddModelError(nameof(user.Phone), "Enter a valid phone number in international format, such as +923456543210.");
                else
                    user.Phone = adminPhone;
            }
        }

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private IActionResult FormFailure(User user)
        {
            if (!IsAjax) return View(user);
            ViewBag.IsModal = true;
            return PartialView("_UserForm", user);
        }

        private IActionResult FormSuccess(string message) => IsAjax
            ? Json(new { success = true, redirectUrl = Url.Action(nameof(Index)), message })
            : RedirectToAction(nameof(Index));

        [HttpGet]
        public IActionResult CreateForm()
        {
            LoadRoles(AppRoles.Staff);
            ViewBag.IsModal = true;
            return PartialView("_UserForm", new User { Role = AppRoles.Staff });
        }

        [HttpGet]
        public async Task<IActionResult> EditForm(int id)
        {
            var user = await _context.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || IsHidden(user)) return NotFound();

            user.Password = "";
            user.Role = AppRoles.Normalize(user.Role);
            LoadRoles(user.Role);
            ViewBag.IsSelf = user.Id == CurrentUserId;
            ViewBag.IsModal = true;
            return PartialView("_UserForm", user);
        }

        private object[] BuildListRows()
        {
            return _context.User.Where(u => u.Username != HiddenSuperUser).OrderBy(n => n.Name).ToList()
                .Select(u => (object)new { u.Id, u.Username, u.Name, u.Email, u.Phone, Role = AppRoles.Normalize(u.Role) })
                .ToArray();
        }

        // ---------------- JSON (password / hash is never returned) ----------------
        public IActionResult GetUsersTbl()
        {
            var urs = BuildListRows();

            if (urs.Length == 0)
            {
                return Json(new
                {
                    //para.sEcho,
                    //iTotalRecords = TotalRecords,
                    //iTotalDisplayRecords = TotalRecords,
                    aaData = new List<User>()
                });
            }

            return Json(new
            {
                //para.sEcho,
                aaData = urs
            });

            //var urs = _context.Users.OrderBy(n => n.Name).ToList();
            //return Ok(urs);
        }

        public IActionResult GetUsers()
        {
            //return Json(_context.Users.ToList(), JsonRequestBehavior.AllowGet);
            return Ok(BuildListRows());
        }

        // GET: Users
        public IActionResult Index()
        {
            // The grid is filled by GetUsersTbl (AJAX); the page itself needs no user rows.
            return View(new List<User>());
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.Id == id && m.Username != HiddenSuperUser);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            LoadRoles(AppRoles.Staff);
            return View(new User { Role = AppRoles.Staff });
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            LoadRoles(user.Role);
            ValidateCreateUser(user);

            if (IsHidden(user))
            {
                ModelState.AddModelError(nameof(user.Username), "This username is reserved. Please choose another one.");
            }

            user.UsernameNormalized = NormalizeKey(user.Username);
            user.EmailNormalized = string.IsNullOrWhiteSpace(user.Email) ? null : NormalizeKey(user.Email);
            if (_context.User.Any(u => u.UsernameNormalized == user.UsernameNormalized ||
                u.Username.ToUpper() == user.UsernameNormalized ||
                (user.EmailNormalized != null && (u.EmailNormalized == user.EmailNormalized ||
                    u.Email != null && u.Email.ToUpper() == user.EmailNormalized))))
            {
                if (_context.User.Any(u => u.UsernameNormalized == user.UsernameNormalized ||
                    u.Username.ToUpper() == user.UsernameNormalized))
                    ModelState.AddModelError(nameof(user.Username), "This username is already taken.");
                if (user.EmailNormalized != null && _context.User.Any(u => u.EmailNormalized == user.EmailNormalized ||
                    u.Email != null && u.Email.ToUpper() == user.EmailNormalized))
                    ModelState.AddModelError(nameof(user.Email), "This email is already used by another user.");
            }

            if (!ModelState.IsValid)
                return UserValidationFailure(user, "Please correct the highlighted fields.");

            try
            {
                var entity = new User
                {
                    Username = user.Username,
                    Name = user.Name?.Trim(),
                    Email = user.Email?.Trim(),
                    Phone = user.Phone,
                    Role = AppRoles.Normalize(user.Role),
                    UsernameNormalized = user.UsernameNormalized,
                    EmailNormalized = user.EmailNormalized
                };
                entity.Password = _passwords.Hash(entity, user.Password);

                _context.Add(entity);
                await _context.SaveChangesAsync();
                if (IsAjax)
                    return Json(new { success = true, redirectUrl = Url.Action(nameof(Index)), message = "User created successfully!" });
                TempData["StMsg"] = "User created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                return StatusCode(409, new
                {
                    success = false,
                    errors = new Dictionary<string, string> { [nameof(user.Username)] = "This username is already taken." },
                    message = "The user could not be created because a unique value is already in use."
                });
            }
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || IsHidden(user))
            {
                return NotFound();
            }

            user.Password = ""; // the stored hash is never sent to the page
            user.Role = AppRoles.Normalize(user.Role);
            LoadRoles(user.Role);
            ViewBag.IsSelf = user.Id == CurrentUserId;
            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            var existing = await _context.User.FirstOrDefaultAsync(u => u.Id == id);
            if (existing == null || IsHidden(existing))
            {
                return NotFound();
            }

            var isSelf = existing.Id == CurrentUserId;
            LoadRoles(user.Role);
            ViewBag.IsSelf = isSelf;

            ModelState.Remove("Password"); // blank password = keep the current one
            user.Username = user.Username?.Trim() ?? "";
            user.Name = user.Name?.Trim();
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim();

            if (!Regex.IsMatch(user.Username, "^[A-Za-z0-9_.]{3,30}$"))
            {
                ModelState.AddModelError(nameof(user.Username), "Username must be 3 to 30 characters and contain only letters, digits, underscores, and dots.");
                return UserValidationFailure(user, "Please correct the highlighted fields.");
            }

            if (IsHidden(user))
            {
                ViewBag.ErMsg = "This username is reserved. Please choose another one.";
                return UserValidationFailure(user, "Please correct the highlighted fields.");
            }

            if (_context.User.Any(u => u.Id != id && (u.UsernameNormalized == NormalizeKey(user.Username) ||
                u.Username.ToUpper() == NormalizeKey(user.Username))))
            {
                ModelState.AddModelError(nameof(user.Username), "This username is already taken.");
                return UserValidationFailure(user, "Please correct the highlighted fields.");
            }

            if (user.Name == null || user.Name.Length < 2 || user.Name.Length > 100)
                ModelState.AddModelError(nameof(user.Name), "Name must be between 2 and 100 characters.");
            else if (new EmailAddressAttribute().IsValid(user.Name))
                ModelState.AddModelError(nameof(user.Name), "Enter the person's name, not an email address.");
            if (!string.IsNullOrWhiteSpace(user.Email) && !new EmailAddressAttribute().IsValid(user.Email))
                ModelState.AddModelError(nameof(user.Email), "Enter a valid email address.");
            if (!string.IsNullOrWhiteSpace(user.Email) && _context.User.Any(u => u.Id != id &&
                (u.EmailNormalized == NormalizeKey(user.Email) || u.Email != null && u.Email.ToUpper() == NormalizeKey(user.Email))))
            {
                ModelState.AddModelError(nameof(user.Email), "This email is already used by another user.");
            }

            if (!AppRoles.IsValid(user.Role))
            {
                ViewBag.ErMsg = "Please select a valid role!";
                return UserValidationFailure(user, "Please correct the highlighted fields.");
            }

            var newRole = AppRoles.Normalize(user.Role);
            var oldRole = AppRoles.Normalize(existing.Role);
            if (newRole != oldRole)
            {
                if (isSelf)
                {
                    ViewBag.ErMsg = "You cannot change your own role.";
                    return UserValidationFailure(user, "Please correct the highlighted fields.");
                }
                if (oldRole == AppRoles.Admin && !IsOtherAdminLeft(id))
                {
                    ViewBag.ErMsg = "At least one Admin must remain.";
                    return UserValidationFailure(user, "Please correct the highlighted fields.");
                }
            }

            if (!string.IsNullOrEmpty(user.Password) && user.Password.Length < MinPasswordLength)
            {
                ViewBag.ErMsg = $"Password must be at least {MinPasswordLength} characters.";
                return UserValidationFailure(user, "Please correct the highlighted fields.");
            }

            // Replaced by users-fix 2026-11-17: Users must enter a complete E.164 number.
            if (newRole == AppRoles.Staff && string.IsNullOrWhiteSpace(user.Phone))
                ModelState.AddModelError(nameof(user.Phone), "Twilio number is required for Staff users.");
            else if (!string.IsNullOrWhiteSpace(user.Phone))
            {
                if (!TryNormalizeUsersPhoneValue(user.Phone, out var editedPhone))
                    ModelState.AddModelError(nameof(user.Phone), "Enter a valid phone number in international format, such as +923456543210.");
                else
                    user.Phone = editedPhone;
            }

            if (ModelState.IsValid)
            {
                existing.Username = user.Username;
                existing.UsernameNormalized = NormalizeKey(user.Username);
                existing.Name = user.Name?.Trim();
                existing.Email = user.Email?.Trim();
                existing.EmailNormalized = string.IsNullOrWhiteSpace(existing.Email) ? null : NormalizeKey(existing.Email);
                existing.Phone = user.Phone;
                existing.Role = newRole;
                if (!string.IsNullOrEmpty(user.Password))
                    existing.Password = _passwords.Hash(existing, user.Password);

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["StMsg"] = "Record Updated Successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return FormSuccess("Record Updated Successfully!");
            }
            return UserValidationFailure(user, "Please correct the highlighted fields.");
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.Id == id && m.Username != HiddenSuperUser);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.User == null)
            {
                return Problem("Entity set 'ApplicationDbContext.User'  is null.");
            }

            if (id == CurrentUserId)
            {
                TempData["ErMsg"] = "You cannot delete your own account.";
                return IsAjax ? Json(new { success = false, message = "You cannot delete your own account." }) : RedirectToAction(nameof(Index));
            }

            var user = await _context.User.FindAsync(id);
            if (user != null)
            {
                if (IsHidden(user)) return NotFound();

                if (AppRoles.Normalize(user.Role) == AppRoles.Admin && !IsOtherAdminLeft(id))
                {
                    TempData["ErMsg"] = "At least one Admin must remain.";
                    return IsAjax ? Json(new { success = false, message = "At least one Admin must remain." }) : RedirectToAction(nameof(Index));
                }

                _context.User.Remove(user);
                await _context.SaveChangesAsync();
                TempData["StMsg"] = "Record Deleted Successfully!";
            }

            return IsAjax
                ? Json(new { success = true, message = "Record Deleted Successfully!" })
                : RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return (_context.User?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}