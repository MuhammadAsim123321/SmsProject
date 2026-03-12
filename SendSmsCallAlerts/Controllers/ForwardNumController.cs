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
    public class ForwardNumController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ForwardNumController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ForwardNum
        public async Task<IActionResult> Index()
        {
              return _context.ForwardNum != null ? 
                          View(await _context.ForwardNum.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.ForwardNum'  is null.");
        }

        // GET: ForwardNum/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.ForwardNum == null)
            {
                return NotFound();
            }

            var forwardNum = await _context.ForwardNum
                .FirstOrDefaultAsync(m => m.Id == id);
            if (forwardNum == null)
            {
                return NotFound();
            }

            return View(forwardNum);
        }

        // GET: ForwardNum/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ForwardNum/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Number,Name,createdOn")] ForwardNum forwardNum)
        {
            //if (ModelState.IsValid)
            //{
            if(forwardNum.Number == null || forwardNum.Number.Length != 10)
            {
                ViewBag.ErMsg = "Please enter a 10 digit number";
                return View(forwardNum);
            }
            forwardNum.createdOn = DateTime.UtcNow;
            forwardNum.Number = "+1" +forwardNum.Number;
                _context.Add(forwardNum);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            //}
         //   return View(forwardNum);
        }

        // GET: ForwardNum/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.ForwardNum == null)
            {
                return NotFound();
            }

            var forwardNum = await _context.ForwardNum.FindAsync(id);
            if (forwardNum == null)
            {
                return NotFound();
            }
            return View(forwardNum);
        }

        // POST: ForwardNum/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Number,Name,createdOn")] ForwardNum forwardNum)
        {
            if (id != forwardNum.Id)
            {
                return NotFound();
            }

            if (forwardNum.Number == null || forwardNum.Number.Length != 10)
            {
                ViewBag.ErMsg = "Please enter a 10 digit number";
                return View(forwardNum);
            }

            //if (ModelState.IsValid)
            //{
            try
                {
                forwardNum.Number = "+1" + forwardNum.Number;
                _context.Update(forwardNum);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ForwardNumExists(forwardNum.Id))
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
           // return View(forwardNum);
        }

        // GET: ForwardNum/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.ForwardNum == null)
            {
                return NotFound();
            }

            var forwardNum = await _context.ForwardNum
                .FirstOrDefaultAsync(m => m.Id == id);
            if (forwardNum == null)
            {
                return NotFound();
            }

            return View(forwardNum);
        }

        // POST: ForwardNum/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.ForwardNum == null)
            {
                return Problem("Entity set 'ApplicationDbContext.ForwardNum'  is null.");
            }
            var forwardNum = await _context.ForwardNum.FindAsync(id);
            if (forwardNum != null)
            {
                _context.ForwardNum.Remove(forwardNum);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ForwardNumExists(int id)
        {
          return (_context.ForwardNum?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
