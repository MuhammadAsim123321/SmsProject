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
using static System.Net.Mime.MediaTypeNames;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class IntroSmsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public IntroSmsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: IntroSms
        public async Task<IActionResult> Index()
        {
            var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;
            return _context.IntroSms != null ? 
                          View(await _context.IntroSms.Where(r=>r.TwNum==twNo).ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.IntroSms'  is null.");
        }

        // GET: IntroSms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.IntroSms == null)
            {
                return NotFound();
            }

            var introSms = await _context.IntroSms
                .FirstOrDefaultAsync(m => m.Id == id);
            if (introSms == null)
            {
                return NotFound();
            }

            return View(introSms);
        }

        // GET: IntroSms/Create
        public IActionResult Create()
        {
            var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;
            if(_context.IntroSms.Any(r=>r.TwNum == twNo))
            {
                return RedirectToAction("Edit", new { id = _context.IntroSms.Where(r => r.TwNum == twNo).First().Id });
            }

            ViewBag.TwilioNum = twNo;
            return View();
        }

        // POST: IntroSms/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IntroSms introSms)
        {
            var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;
            if(introSms.TwNum == null || introSms.TwNum.Length != 12)
            {
                ViewBag.ErMsg = "Twilio Number does not exist!";
                return View(introSms);
            }

            //if (ModelState.IsValid)
            //{
                _context.Add(introSms);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
           // }
            //return View(introSms);
        }

        // GET: IntroSms/Edit/5
        public IActionResult Edit(int? id=1)
        {
            if (id == null || _context.IntroSms == null)
            {
                return NotFound();
            }

            var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;

           // var introSms = await _context.IntroSms.FindAsync(id);
            var introSms = _context.IntroSms.Where(r=>r.TwNum==twNo).FirstOrDefault();
            if (introSms == null)
            {
                return NotFound();
            }
            return View(introSms);
        }

        // POST: IntroSms/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IntroSms introSms)
        {
            if (id != introSms.Id)
            {
                return NotFound();
            }

            //if (ModelState.IsValid)
            //{
                try
                {
                var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
                var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;
                introSms.TwNum = twNo;

                _context.Update(introSms);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IntroSmsExists(introSms.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
               // return RedirectToAction(nameof(Index));
                return RedirectToAction("IncomingLogs", "Sms");
            //}
            //return View(introSms);
        }

        // GET: IntroSms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.IntroSms == null)
            {
                return NotFound();
            }

            var introSms = await _context.IntroSms
                .FirstOrDefaultAsync(m => m.Id == id);
            if (introSms == null)
            {
                return NotFound();
            }

            return View(introSms);
        }

        // POST: IntroSms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.IntroSms == null)
            {
                return Problem("Entity set 'ApplicationDbContext.IntroSms'  is null.");
            }
            var introSms = await _context.IntroSms.FindAsync(id);
            if (introSms != null)
            {
                _context.IntroSms.Remove(introSms);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool IntroSmsExists(int id)
        {
          return (_context.IntroSms?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
