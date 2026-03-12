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
    public class IvrOptionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public IvrOptionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: IvrOptions
        public async Task<IActionResult> Index()
        {
            int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            string uRole = HttpContext.Session.GetString("UserRole");
            if (uRole == "ADMIN") uId = 0;

            return _context.IvrOption != null ? 
                          View(await _context.IvrOption.Where(r=>uId==0 || r.UserId==uId).OrderBy(r=>r.Num).ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.IvrOption'  is null.");
        }

        // GET: IvrOptions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.IvrOption == null)
            {
                return NotFound();
            }

            var ivrOptions = await _context.IvrOption
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ivrOptions == null)
            {
                return NotFound();
            }

            return View(ivrOptions);
        }

        // GET: IvrOptions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: IvrOptions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Num,keyword")] IvrOptions ivrOptions)
        {
            ivrOptions.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var usrDetails = _context.User.Where(r => r.Id == ivrOptions.UserId).FirstOrDefault();
            if(usrDetails== null)
            {
                ViewBag.ErMsg = "User does not exist!";
                return View(ivrOptions);
            }

            if(usrDetails.Phone == null || usrDetails.Phone.Length != 12)
            {
                ViewBag.ErMsg = "Your Twillio number does not exist. Please request the admin to add the Twilio number, then proceed here.";
                return View(ivrOptions);
            }

            ivrOptions.TwilioNum = usrDetails.Phone;

            //if (ModelState.IsValid)
            //{
                
                _context.Add(ivrOptions);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            //}
            //return View(ivrOptions);
        }

        // GET: IvrOptions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.IvrOption == null)
            {
                return NotFound();
            }

            var ivrOptions = await _context.IvrOption.FindAsync(id);
            if (ivrOptions == null)
            {
                return NotFound();
            }
            return View(ivrOptions);
        }

        // POST: IvrOptions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Num,keyword")] IvrOptions ivrOptions)
        {
            if (id != ivrOptions.Id)
            {
                return NotFound();
            }

            ivrOptions.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var usrDetails = _context.User.Where(r => r.Id == ivrOptions.UserId).FirstOrDefault();
            if (usrDetails == null)
            {
                ViewBag.ErMsg = "User does not exist!";
                return View(ivrOptions);
            }

            if (usrDetails.Phone == null || usrDetails.Phone.Length != 12)
            {
                ViewBag.ErMsg = "Your Twillio number does not exist. Please request the admin to add the Twilio number, then proceed here.";
                return View(ivrOptions);
            }

            ivrOptions.TwilioNum = usrDetails.Phone;

            //if (ModelState.IsValid)
            //{
                try
                {
                    _context.Update(ivrOptions);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IvrOptionsExists(ivrOptions.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            //}
            //return View(ivrOptions);
        }

        // GET: IvrOptions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.IvrOption == null)
            {
                return NotFound();
            }

            var ivrOptions = await _context.IvrOption
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ivrOptions == null)
            {
                return NotFound();
            }

            return View(ivrOptions);
        }

        // POST: IvrOptions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.IvrOption == null)
            {
                return Problem("Entity set 'ApplicationDbContext.IvrOption'  is null.");
            }
            var ivrOptions = await _context.IvrOption.FindAsync(id);
            if (ivrOptions != null)
            {
                _context.IvrOption.Remove(ivrOptions);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool IvrOptionsExists(int id)
        {
          return (_context.IvrOption?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
