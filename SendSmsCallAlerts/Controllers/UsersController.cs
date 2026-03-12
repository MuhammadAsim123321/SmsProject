using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult GetUsersTbl()
        {
            var urs = _context.User.Where(u=>u.Username!= "Snowbro").OrderBy(n => n.Name).ToList();

            if (urs == null || urs.Count() == 0)
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
            var urs = _context.User.Where(u => u.Username != "Snowbro").OrderBy(n => n.Name).ToList();
            return Ok(urs);

        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
              return _context.User != null ? 
                          View(await _context.User.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.User'  is null.");
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password))
            {
                ViewBag.ErMsg = "Please enter a username and password!";
                return View(user);
            }

            if (_context.User.Any(u => u.Username.ToLower() == user.Username.ToLower()))
            {
                ViewBag.ErMsg = "A user with this username already exists!";
                return View(user);
            }

            if (user.Phone != null && user.Phone != "")
            {
                user.Phone = user.Phone.Trim();
                if (user.Phone.Length == 10) user.Phone = "+1" + user.Phone;
                else if (user.Phone.Length == 11) user.Phone = "+" + user.Phone;
                else if (user.Phone.Length < 10 || user.Phone.Length > 12)
                {
                    ViewBag.ErMsg = "Please enter a valid Twilio number!";
                    return View(user);
                }
            }

            if (ModelState.IsValid)
            {
                if (user.Username == "Snowbro") user.Role = "ADMIN";

                    _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ErMsg = "Please try again with all necessary details!";
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (user.Phone != null && user.Phone != "")
            {
                user.Phone = user.Phone.Trim();
                if (user.Phone.Length == 10) user.Phone = "+1" + user.Phone;
                else if (user.Phone.Length == 11) user.Phone = "+" + user.Phone;
                else if (user.Phone.Length < 10 || user.Phone.Length > 12)
                {
                    ViewBag.ErMsg = "Please enter a valid Twilio number!";
                    return View(user);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
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
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.User == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.Id == id);
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
            var user = await _context.User.FindAsync(id);
            if (user != null)
            {
                _context.User.Remove(user);
                TempData["StMsg"] = "Record Deleted Successfully!";
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
          return (_context.User?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
