using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using SendSmsCallAlerts.Filters;

namespace SendSmsCallAlerts.Controllers
{
    public class RunNowVm
    {
        public string SettingName { get; set; } = "";
        public string PreviewDescription { get; set; } = "";
        public List<AutomatedSmsItem> Items { get; set; } = new();
    }

    [SessionChecking]
    public class AutomatedNotificationsController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly AutomatedSmsService svc;

        public AutomatedNotificationsController(ApplicationDbContext db, AutomatedSmsService svc)
        {
            this.db = db; this.svc = svc;
        }

        public async Task<IActionResult> Index()
        {
            await svc.EnsureNextOccurrencesAsync();
            var settings = await db.AutomatedSmsSettings.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            var next = await db.AutomatedSmsOccurrences.AsNoTracking()
                .Where(o => o.Status == AutomatedSmsOccurrenceStatus.Pending ||
                            o.Status == AutomatedSmsOccurrenceStatus.Running)
                .GroupBy(o => o.SettingId)
                .Select(g => g.OrderBy(o => o.ScheduledForUtc).First())
                .ToDictionaryAsync(o => o.SettingId, o => svc.ToBusiness(o.ScheduledForUtc));
            ViewBag.NextOccurrences = next;
            return View(settings);
        }

        public IActionResult Create() =>
            View(new AutomatedSmsSetting { Frequency = SmsFrequencies.Daily, SendTime = new TimeSpan(17, 0, 0), IsActive = true });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,NotificationType,Frequency,DayOfWeek,SendTime,TemplateBody,IsActive")] AutomatedSmsSetting model)
        {
            await ValidateAsync(model, 0);
            if (!ModelState.IsValid) return View(model);
            model.CreatedOn = DateTime.UtcNow;
            db.AutomatedSmsSettings.Add(model);
            await db.SaveChangesAsync();
            await svc.EnsureNextOccurrenceAsync(model);
            TempData["Success"] = "Setting created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            return s == null ? NotFound() : View(s);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Name,NotificationType,Frequency,DayOfWeek,SendTime,TemplateBody,IsActive")] AutomatedSmsSetting model)
        {
            if (id != model.Id) return BadRequest();
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            if (s == null) return NotFound();

            await ValidateAsync(model, id);
            if (!ModelState.IsValid) return View(model);

            s.Name = model.Name;
            s.NotificationType = model.NotificationType;
            s.RecipientType = model.RecipientType;
            s.Frequency = model.Frequency;
            s.DayOfWeek = model.DayOfWeek;
            s.SendTime = model.SendTime;
            s.TemplateBody = model.TemplateBody;
            s.IsActive = model.IsActive;
            s.UpdatedOn = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await svc.CancelPendingOccurrencesAsync(s.Id, "Setting schedule changed");
            if (s.IsActive) await svc.EnsureNextOccurrenceAsync(s);
            TempData["Success"] = "Setting saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            if (s == null) return NotFound();
            s.IsActive = !s.IsActive;
            s.UpdatedOn = DateTime.UtcNow;
            await db.SaveChangesAsync();
            if (s.IsActive) await svc.EnsureNextOccurrenceAsync(s);
            else await svc.CancelPendingOccurrencesAsync(s.Id, "Setting deactivated");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            if (s != null)
            {
                await svc.CancelPendingOccurrencesAsync(s.Id, "Setting deactivated");
                db.AutomatedSmsSettings.Remove(s);
                await db.SaveChangesAsync();
            }
            TempData["Success"] = "Setting deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Dry run: builds the real messages for "tomorrow" but sends nothing.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RunNow(int id)
        {
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            if (s == null) return NotFound();
            var key = "TEST-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var items = await svc.RunAsync(s, key, true, svc.BusinessNow());
            var previewDescription = s.NotificationType switch
            {
                NotificationTypes.TechnicianAvailabilityRequest => "Technician weekly availability request",
                NotificationTypes.TechnicianDailyJobSummary => "Technician daily job summary for the next day",
                NotificationTypes.CustomerJobReminder => "Customer next-day job reminder",
                _ => "Configured notification"
            };
            return View("RunResult", new RunNowVm
            {
                SettingName = s.Name,
                PreviewDescription = previewDescription,
                Items = items
            });
        }

        private async Task ValidateAsync(AutomatedSmsSetting m, int currentId)
        {
            var validTypes = new[] { NotificationTypes.TechnicianAvailabilityRequest,
                                     NotificationTypes.TechnicianDailyJobSummary,
                                     NotificationTypes.CustomerJobReminder };
            if (!validTypes.Contains(m.NotificationType))
                ModelState.AddModelError(nameof(m.NotificationType), "Choose a valid notification type.");

            m.RecipientType = m.NotificationType == NotificationTypes.CustomerJobReminder
                ? RecipientTypes.Customer : RecipientTypes.Technician;
            ModelState.Remove(nameof(m.RecipientType));

            if (m.Frequency != SmsFrequencies.Daily && m.Frequency != SmsFrequencies.Weekly)
                ModelState.AddModelError(nameof(m.Frequency), "Choose Daily or Weekly.");
            if (m.Frequency == SmsFrequencies.Weekly && m.DayOfWeek == null)
                ModelState.AddModelError(nameof(m.DayOfWeek), "Pick the day of the week.");
            if (m.Frequency == SmsFrequencies.Daily) m.DayOfWeek = null;

            if (!string.IsNullOrWhiteSpace(m.Name) &&
                await db.AutomatedSmsSettings.AnyAsync(x => x.Name == m.Name && x.Id != currentId))
                ModelState.AddModelError(nameof(m.Name), "A setting with this name already exists.");
        }
    }
}