using System;
using System.Collections.Generic;
using System.Linq;
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

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private IActionResult FormFailure(User user)
        {
            if (!IsAjax) return View(user);
            ViewBag.IsModal = true;
            return PartialView("_UserForm", user);
        }

        private IActionResult FormSuccess(string message) => IsAjax
            ? Json(new { success = true, message })
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
            user.Username = user.Username?.Trim() ?? "";

            if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password))
            {
                ViewBag.ErMsg = "Please enter a username and password!";
                return FormFailure(user);
            }

            if (IsHidden(user))
            {
                ViewBag.ErMsg = "This username is reserved. Please choose another one.";
                return FormFailure(user);
            }

            if (user.Password.Length < MinPasswordLength)
            {
                ViewBag.ErMsg = $"Password must be at least {MinPasswordLength} characters.";
                return FormFailure(user);
            }

            if (_context.User.Any(u => u.Username.ToLower() == user.Username.ToLower()))
            {
                ViewBag.ErMsg = "A user with this username already exists!";
                return FormFailure(user);
            }

            if (!AppRoles.IsValid(user.Role))
            {
                ViewBag.ErMsg = "Please select a valid role!";
                return FormFailure(user);
            }

            if (!TryNormalizePhone(user))
            {
                ViewBag.ErMsg = "Please enter a valid Twilio number!";
                return FormFailure(user);
            }

            if (ModelState.IsValid)
            {
                var entity = new User
                {
                    Username = user.Username,
                    Name = user.Name?.Trim(),
                    Email = user.Email?.Trim(),
                    Phone = user.Phone,
                    Role = AppRoles.Normalize(user.Role)
                };
                entity.Password = _passwords.Hash(entity, user.Password);

                _context.Add(entity);
                await _context.SaveChangesAsync();
                TempData["StMsg"] = "User created successfully!";
                return FormSuccess("User created successfully!");
            }

            ViewBag.ErMsg = "Please try again with all necessary details!";
            return FormFailure(user);
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

            if (string.IsNullOrEmpty(user.Username))
            {
                ViewBag.ErMsg = "Please enter a username!";
                return FormFailure(user);
            }

            if (IsHidden(user))
            {
                ViewBag.ErMsg = "This username is reserved. Please choose another one.";
                return FormFailure(user);
            }

            if (_context.User.Any(u => u.Id != id && u.Username.ToLower() == user.Username.ToLower()))
            {
                ViewBag.ErMsg = "A user with this username already exists!";
                return FormFailure(user);
            }

            if (!AppRoles.IsValid(user.Role))
            {
                ViewBag.ErMsg = "Please select a valid role!";
                return FormFailure(user);
            }

            var newRole = AppRoles.Normalize(user.Role);
            var oldRole = AppRoles.Normalize(existing.Role);
            if (newRole != oldRole)
            {
                if (isSelf)
                {
                    ViewBag.ErMsg = "You cannot change your own role.";
                    return FormFailure(user);
                }
                if (oldRole == AppRoles.Admin && !IsOtherAdminLeft(id))
                {
                    ViewBag.ErMsg = "At least one Admin must remain.";
                    return FormFailure(user);
                }
            }

            if (!string.IsNullOrEmpty(user.Password) && user.Password.Length < MinPasswordLength)
            {
                ViewBag.ErMsg = $"Password must be at least {MinPasswordLength} characters.";
                return FormFailure(user);
            }

            if (!TryNormalizePhone(user))
            {
                ViewBag.ErMsg = "Please enter a valid Twilio number!";
                return FormFailure(user);
            }

            if (ModelState.IsValid)
            {
                existing.Username = user.Username;
                existing.Name = user.Name?.Trim();
                existing.Email = user.Email?.Trim();
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
            return FormFailure(user);
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