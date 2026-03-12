using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Controllers
{
    public class SmsTemplatesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private string TmZn = "";


        public SmsTemplatesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;

            TmZn = _configuration["AppSettings:TmZn"];
        }

        private DateTime GetTime()
        {
            //TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TmZn);
            DateTime usEastCoastNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);

            return usEastCoastNow;
        }

        public string GetTemplateImage(int TemplateId)
        {
            var tmplate = _context.SmsTemplates.Where(r => r.Id == TemplateId).First();

            if (tmplate.imgPath != null)
            {
                return tmplate.imgPath;
            }

            return string.Empty;
        }

        public IActionResult GetTemplates()
        {
            var urs = _context.SmsTemplates.OrderBy(r => r.name).ToList();
            return Ok(urs);

        }

        // GET: SmsTemplates
        public async Task<IActionResult> Index()
        {
              return _context.SmsTemplates != null ? 
                          View(await _context.SmsTemplates.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.SmsTemplate'  is null.");
        }

        // GET: SmsTemplates/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.SmsTemplates == null)
            {
                return NotFound();
            }

            var smsTemplate = await _context.SmsTemplates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (smsTemplate == null)
            {
                return NotFound();
            }

            return View(smsTemplate);
        }

        // GET: SmsTemplates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SmsTemplates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,name,templateBody,createdOn,imgPath")] SmsTemplate smsTemplate, IFormFile ImgFile)
        {
            smsTemplate.createdOn = GetTime();
            smsTemplate.imgPath = GetImagePath(ImgFile);
            _context.Add(smsTemplate);
            await _context.SaveChangesAsync();
            TempData["StMsg"] = "Template has been added successfully!";
            return RedirectToAction(nameof(Index));
            //if (ModelState.IsValid)
            //{
            //    _context.Add(smsTemplate);
            //    await _context.SaveChangesAsync();
            //    return RedirectToAction(nameof(Index));
            //}
            //return View(smsTemplate);
        }

        private string GetImagePath(IFormFile imgFile)
        {
            if (imgFile != null)
            {
                string fileName = "IMG" + Path.GetExtension(imgFile.FileName);

                string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "tempimages");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + fileName.Trim();

                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    imgFile.CopyTo(stream);
                }

                return "/tempimages/" + fileName;
            }

            return "";
        }


        // GET: SmsTemplates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.SmsTemplates == null)
            {
                return NotFound();
            }

            var smsTemplate = await _context.SmsTemplates.FindAsync(id);
            if (smsTemplate == null)
            {
                return NotFound();
            }
            return View(smsTemplate);
        }

        // POST: SmsTemplates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,name,templateBody,createdOn,imgPath")] SmsTemplate smsTemplate, IFormFile ImgFile)
        {
            if (id != smsTemplate.Id)
            {
                return NotFound();
            }

            try
            {
                smsTemplate.createdOn = GetTime();
                smsTemplate.imgPath = GetImagePath(ImgFile);
                _context.Update(smsTemplate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SmsTemplateExists(smsTemplate.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
            //if (ModelState.IsValid)
            //{
            //    try
            //    {
            //        _context.Update(smsTemplate);
            //        await _context.SaveChangesAsync();
            //    }
            //    catch (DbUpdateConcurrencyException)
            //    {
            //        if (!SmsTemplateExists(smsTemplate.Id))
            //        {
            //            return NotFound();
            //        }
            //        else
            //        {
            //            throw;
            //        }
            //    }
            //    return RedirectToAction(nameof(Index));
            //}
            //return View(smsTemplate);
        }

        // GET: SmsTemplates/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.SmsTemplates == null)
            {
                return NotFound();
            }

            var smsTemplate = await _context.SmsTemplates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (smsTemplate == null)
            {
                return NotFound();
            }

            return View(smsTemplate);
        }

        // POST: SmsTemplates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.SmsTemplates == null)
            {
                return Problem("Entity set 'ApplicationDbContext.SmsTemplate'  is null.");
            }
            var smsTemplate = await _context.SmsTemplates.FindAsync(id);
            if (smsTemplate != null)
            {
                _context.SmsTemplates.Remove(smsTemplate);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SmsTemplateExists(int id)
        {
          return (_context.SmsTemplates?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
