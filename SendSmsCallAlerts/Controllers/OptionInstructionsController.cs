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
    public class OptionInstructionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OptionInstructionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: OptionInstructions
        //public async Task<IActionResult> Index()
        public IActionResult Index()
        {
            int uId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            string uRole = HttpContext.Session.GetString("UserRole");
            if (uRole == "ADMIN") uId = 0;

            List<OptInstVM> res = (from ivrOption in _context.IvrOption.Where(u=>uId==0 || u.UserId==uId)
                        join optionInstruction in _context.OptionInstruction.Where(oi => uId == 0 || oi.UserId == uId)
                        on ivrOption.Id equals optionInstruction.IvrId
                        select new OptInstVM
                        {
                            Id = optionInstruction.Id,
                            OptionNumber = ivrOption.Num+"- "+ivrOption.keyword,
                            InstructionOrder = optionInstruction.InstructionOrder,
                            Instruction = optionInstruction.Instruction,
                            TwilioNumber = optionInstruction.TwilioNum
                        }).ToList();

            return View(res);

            //return _context.OptionInstruction != null ? 
            //              View(await _context.OptionInstruction.ToListAsync()) :
            //              Problem("Entity set 'ApplicationDbContext.OptionInstruction'  is null.");
        }

        // GET: OptionInstructions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.OptionInstruction == null)
            {
                return NotFound();
            }

            var optionInstructions = await _context.OptionInstruction
                .FirstOrDefaultAsync(m => m.Id == id);
            if (optionInstructions == null)
            {
                return NotFound();
            }

            return View(optionInstructions);
        }

        // GET: OptionInstructions/Create
        public IActionResult Create()
        {
            var UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var twNo = _context.User.Where(r => r.Id == UserId).First().Phone;
            
            ViewBag.TwilioNumber=twNo;

            var ivrOptList = _context.IvrOption.Where(r=>r.TwilioNum==twNo).OrderBy(r=>r.Num).ToList();

            // Create a list of SelectListItem objects
            var selectListItems = ivrOptList.Select(opt => new SelectListItem
            {
                Value = opt.Id.ToString(), // Set the value of each option to the Id
                Text = opt.Num+"- "+ opt.keyword // Set the text of each option to the keyword
            }).ToList();

            // Pass the selectListItems to the view
            ViewBag.IvrOptList = selectListItems;


            return View();
        }

        // POST: OptionInstructions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OptionInstructions optionInstructions)
        {

            optionInstructions.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId")); 
            var usrDetails = _context.User.Where(r => r.Id == optionInstructions.UserId).FirstOrDefault();
            if (usrDetails == null)
            {
                ViewBag.ErMsg = "User does not exist!";
                return View(optionInstructions);
            }

            if (usrDetails.Phone == null || usrDetails.Phone.Length != 12)
            {
                ViewBag.ErMsg = "Your Twillio number does not exist. Please request the admin to add the Twilio number, then proceed here.";
                return View(optionInstructions);
            }

            optionInstructions.TwilioNum = usrDetails.Phone;


            //if (ModelState.IsValid)
            //{
                
                _context.Add(optionInstructions);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            //}
            //return View(optionInstructions);
        }

        // GET: OptionInstructions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.OptionInstruction == null)
            {
                return NotFound();
            }

            var optionInstructions = await _context.OptionInstruction.FindAsync(id);
            ViewBag.IvrOptList = GetIvrOptList(optionInstructions.IvrId);
            if (optionInstructions == null)
            {
                return NotFound();
            }
            return View(optionInstructions);
        }

        private List<SelectListItem> GetIvrOptList(int selectedOptionId)
        {
            var ivrOptList = _context.IvrOption.OrderBy(r => r.Num).ToList();

            // Create a list of SelectListItem objects
            var selectListItems = ivrOptList.Select(opt => new SelectListItem
            {
                Value = opt.Id.ToString(), // Set the value of each option to the Id
                Text = opt.Num + "- " + opt.keyword, // Set the text of each option to the keyword
                Selected = (opt.Id == selectedOptionId)
            }).ToList();

            return selectListItems;
        }

        // POST: OptionInstructions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IvrId,InstructionOrder,Instruction")] OptionInstructions optionInstructions)
        {
            if (id != optionInstructions.Id)
            {
                return NotFound();
            }

            optionInstructions.UserId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var usrDetails = _context.User.Where(r => r.Id == optionInstructions.UserId).FirstOrDefault();
            if (usrDetails == null)
            {
                ViewBag.ErMsg = "User does not exist!";
                return View(optionInstructions);
            }

            if (usrDetails.Phone == null || usrDetails.Phone.Length != 12)
            {
                ViewBag.ErMsg = "Your Twillio number does not exist. Please request the admin to add the Twilio number, then proceed here.";
                return View(optionInstructions);
            }

            optionInstructions.TwilioNum = usrDetails.Phone;

            //if (ModelState.IsValid)
            //{
                try
                {
                    _context.Update(optionInstructions);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OptionInstructionsExists(optionInstructions.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
           // }
            //return View(optionInstructions);
        }

        // GET: OptionInstructions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.OptionInstruction == null)
            {
                return NotFound();
            }

            var optionInstructions = await _context.OptionInstruction
                .FirstOrDefaultAsync(m => m.Id == id);
            if (optionInstructions == null)
            {
                return NotFound();
            }

            return View(optionInstructions);
        }

        // POST: OptionInstructions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.OptionInstruction == null)
            {
                return Problem("Entity set 'ApplicationDbContext.OptionInstruction'  is null.");
            }
            var optionInstructions = await _context.OptionInstruction.FindAsync(id);
            if (optionInstructions != null)
            {
                _context.OptionInstruction.Remove(optionInstructions);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool OptionInstructionsExists(int id)
        {
          return (_context.OptionInstruction?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
