using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Migrations;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using Twilio.TwiML.Voice;

namespace SendSmsCallAlerts.Controllers
{
    public class SchedulersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private string TmZn = "";
        private readonly IUploadedMediaStore _uploadedMediaStore;

        public SchedulersController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IUploadedMediaStore uploadedMediaStore)
        {
            _context = context;
            _configuration = configuration;
            _uploadedMediaStore = uploadedMediaStore;

            TmZn = _configuration["AppSettings:TmZn"];
        }


        private DateTime GetTime()
        {
            //TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TmZn);
            DateTime usEastCoastNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);

            return usEastCoastNow;
        }

        public async Task<IActionResult> Index()
        {
            var schedulers = await _context.Schedulers.ToListAsync();
            List<SchedulerViewModel> model = new List<SchedulerViewModel>();

            foreach (var rec in schedulers)
            {
                SchedulerViewModel schedulerViewModel = new SchedulerViewModel();
                schedulerViewModel.Id = rec.Id;
                schedulerViewModel.name = rec.name;
                schedulerViewModel.schedulerFor = rec.schedulerFor;
                schedulerViewModel.templateOrAudioId = rec.templateOrAudioId;
                schedulerViewModel.onceOrRepeat = rec.onceOrRepeat;
                schedulerViewModel.executionDateAndTime = rec.executionDateAndTime;
                schedulerViewModel.executionDateAndTimeSt = rec.executionDateAndTime.ToString("MM/dd/yy HH:mm");
                schedulerViewModel.createdOn = rec.createdOn;
                schedulerViewModel.status = rec.status;
                schedulerViewModel.executionTime = rec.executionTime;
                schedulerViewModel.executionTimeSt = rec.executionTime.ToString("HH:mm");
                schedulerViewModel.fromSms = rec.fromNum;
                schedulerViewModel.toSms = rec.toNum;
                schedulerViewModel.smsBody = rec.smsBody;
                schedulerViewModel.JobBookDate = rec.JobBookDate;
                schedulerViewModel.IsPaused = rec.IsPaused;

                model.Add(schedulerViewModel);
            }

            return _context.Schedulers != null ?
                        View(model) :
                        Problem("Entity set 'ApplicationDbContext.Schedulers'  is null.");
        }

        // GET: Schedulers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Schedulers == null)
            {
                return NotFound();
            }

            var scheduler = await _context.Schedulers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (scheduler == null)
            {
                return NotFound();
            }

            return View(scheduler);
        }

        // GET: Schedulers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Schedulers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        // public async Task<IActionResult> Create([Bind("Id,name,schedulerFor,templateOrAudioId,onceOrRepeat,executionDateAndTime,createdOn,status,executionTime")] Scheduler scheduler)
        public async Task<IActionResult> Create(Scheduler scheduler, List<IFormFile> file)
        {
            //if (scheduler.onceOrRepeat == "Once")
            //{
            //    scheduler.createdOn = GetTime();
            //    scheduler.executionTime = GetTime();
            //    scheduler.JobBookDate = null;
            //}
            //else if (scheduler.onceOrRepeat == "Repeat")
            //{
            //    scheduler.createdOn = GetTime();
            //    scheduler.executionDateAndTime = GetTime();
            //    if (!scheduler.JobBookDate.HasValue)
            //    {
            //        ViewBag.ErMsg = "Job Book Date is required when execution is Repeat.";
            //        return View(scheduler);
            //    }
            //}
            //else
            //{
            //    ViewBag.ErMsg = "Please select a scheduler Execution!";
            //    return View(scheduler);
            //}

            scheduler.fromNum = "";
            scheduler.toNum = "";

            scheduler.schedulerFor = "";
            scheduler.templateOrAudioId = 0;
            scheduler.IsPaused = false;

            // FIX: createdOn / executionDateAndTime / executionTime ke form fields
            // Create.cshtml me comment hain aur yahan bhi set nahi ho rahe thay,
            // is liye DB me default 0001-01-01 save ho raha tha aur Schedulers list
            // me "01/01/01 00:00" dikh raha tha. Ab asli creation time record hota hai.
            DateTime createdNow = GetTime();
            scheduler.createdOn = createdNow;
            scheduler.executionDateAndTime = createdNow;
            scheduler.executionTime = createdNow;

            _context.Add(scheduler);
            await _context.SaveChangesAsync();

            var media = await _uploadedMediaStore.SaveAsync(file);
            if (media.Count > 0)
            {
                await SaveImagUrls(scheduler.Id, media);
            }

            return RedirectToAction(nameof(Index));
        }


        private async Task<bool> SaveImagUrls(int schId, List<StoredMedia> media)
        {
            if (media.Count > 0)
            {
                _context.MmsLinkss.AddRange(media.Select(item => new MmsLinks
                {
                    FileName = item.FileName,
                    Location = item.Location.ToString(),
                    SchedulerId = schId,
                    SmsId = 0
                }));
                await _context.SaveChangesAsync();
            }

            return true;
        }

        private async Task<bool> DeleteAndSaveImagUrls(int schId, List<StoredMedia> media)
        {
            var oldLinks = await _context.MmsLinkss.Where(r => r.SchedulerId == schId && r.SmsId == 0).ToListAsync();
            _context.MmsLinkss.RemoveRange(oldLinks);
            _context.MmsLinkss.AddRange(media.Select(item => new MmsLinks
            {
                FileName = item.FileName,
                Location = item.Location.ToString(),
                SchedulerId = schId,
                SmsId = 0
            }));
            await _context.SaveChangesAsync();

            return true;
        }



        // GET: Schedulers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Schedulers == null)
            {
                return NotFound();
            }

            var scheduler = await _context.Schedulers.FindAsync(id);
            if (scheduler == null)
            {
                return NotFound();
            }

            return View(scheduler);
        }



        // POST: Schedulers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Scheduler scheduler, List<IFormFile> file)
        {
            if (id != scheduler.Id)
            {
                return NotFound();
            }

            // REMOVED: JobBookDate validation block (Once/Repeat date check) 
            // because those fields no longer exist in the form

            // Clear ModelState errors for fields removed from the form
            ModelState.Remove("fromNum");
            ModelState.Remove("toNum");
            ModelState.Remove("schedulerFor");
            ModelState.Remove("templateOrAudioId");
            ModelState.Remove("JobBookDate");

            try
            {
                // ADDED: Fetch existing record from DB to avoid overwriting removed fields
                var existingScheduler = await _context.Schedulers.FindAsync(id);
                if (existingScheduler == null)
                {
                    return NotFound();
                }

                // ADDED: Only update fields that are still present in the form
                existingScheduler.name = scheduler.name;
                //existingScheduler.fromNum = scheduler.fromNum;
                //existingScheduler.toNum = scheduler.toNum;
                existingScheduler.smsBody = scheduler.smsBody;
                if (!string.IsNullOrWhiteSpace(scheduler.onceOrRepeat))
                    existingScheduler.onceOrRepeat = scheduler.onceOrRepeat;
                existingScheduler.status = scheduler.status;
                existingScheduler.TimeToRunId = scheduler.TimeToRunId;
                existingScheduler.RunFromId = scheduler.RunFromId;

                // FIX: purane records jinki createdOn / execution date-time kabhi save hi nahi hui
                // (un me DateTime ka default 0001-01-01 para hai) unhe Edit par repair kar dete hain,
                // warna list me "01/01/01 00:00" hi dikhta rehta hai.
                if (existingScheduler.createdOn == DateTime.MinValue) existingScheduler.createdOn = GetTime();
                if (existingScheduler.executionDateAndTime == DateTime.MinValue) existingScheduler.executionDateAndTime = GetTime();
                if (existingScheduler.executionTime == DateTime.MinValue) existingScheduler.executionTime = GetTime();

                _context.Update(existingScheduler);
                await _context.SaveChangesAsync();

                // No new upload means existing scheduler image links remain unchanged.
                if (file != null && file.Any(upload => upload != null && upload.Length > 0))
                {
                    var media = await _uploadedMediaStore.SaveAsync(file);
                    if (media.Count > 0)
                        await DeleteAndSaveImagUrls(existingScheduler.Id, media);
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErMsg = "Could not save the scheduler: " + ex.Message;
                ViewBag.TimeToRunList = await _context.TimeToRun.ToListAsync();
                ViewBag.RunFromList = await _context.RunFrom.ToListAsync();
                return View(scheduler);
            }

            return RedirectToAction(nameof(Index));
        }














        // GET: Schedulers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Schedulers == null)
            {
                return NotFound();
            }

            var scheduler = await _context.Schedulers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (scheduler == null)
            {
                return NotFound();
            }

            return View(scheduler);
        }

        // POST: Schedulers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            //if (_context.Schedulers == null)
            //{
            //    return Problem("Entity set 'ApplicationDbContext.Scheduler'  is null.");
            //}
            //var scheduler = await _context.Schedulers.FindAsync(id);
            //if (scheduler != null)
            //{
            //    _context.Schedulers.Remove(scheduler);
            //}

            //await _context.SaveChangesAsync();
            //return RedirectToAction(nameof(Index));
            var linkedJobs = await _context.AllScheduledJobs.AnyAsync(r => r.SchedulerId == id);
            if (linkedJobs)
            {
                TempData["ErrMsg"] = "This scheduler cannot be deleted because there are jobs linked to it. First, delete those jobs from the 'All Scheduled Jobs' page.";
                return RedirectToAction(nameof(Index));
            }
            var scheduler = await _context.Schedulers.FindAsync(id);
            _context.Schedulers.Remove(scheduler);
            await _context.SaveChangesAsync();
            TempData["SucMsg"] = "Scheduler deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TogglePause(int id)
        {
            var scheduler = await _context.Schedulers.FindAsync(id);
            if (scheduler == null)
            {
                return NotFound();
            }

            scheduler.IsPaused = scheduler.IsPaused == true ? false : true;
            _context.Update(scheduler);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool SchedulerExists(int id)
        {
            return (_context.Schedulers?.Any(e => e.Id == id)).GetValueOrDefault();
        }
        [HttpGet]
        public async Task<IActionResult> GetTimeToRunData()
        {
            var data = await _context.TimeToRun
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetRunFromData()
        {
            var data = await _context.RunFrom
                .Select(x => new { x.Id, x.RunFromName })
                .ToListAsync();
            return Json(data);
        }
    }
}