using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using SendSmsCallAlerts.Filters;

namespace SendSmsCallAlerts.Controllers
{
    public class AutomatedLogRow
    {
        public string SettingName { get; set; } = "";
        public string RecipientType { get; set; } = "";
        public int RecipientId { get; set; }
        public string RecipientName { get; set; } = "";
        public string? Phone { get; set; }
        public string Status { get; set; } = "";
        public string? Error { get; set; }
        public string When { get; set; } = "";
    }

    public class RunNowVm
    {
        public string SettingName { get; set; } = "";
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
            var logs = await db.AutomatedSmsLogs.AsNoTracking().Include(l => l.Setting)
                .OrderByDescending(l => l.CreatedOn).Take(50).ToListAsync();

            var techIds = logs.Where(l => l.RecipientType == RecipientTypes.Technician).Select(l => l.RecipientId).Distinct().ToList();
            var custIds = logs.Where(l => l.RecipientType == RecipientTypes.Customer).Select(l => l.RecipientId).Distinct().ToList();

            var techs = await db.Technicians.AsNoTracking().Where(t => techIds.Contains(t.Id)).ToListAsync();
            var custs = await db.Set<ContactDetail>().AsNoTracking().Where(c => custIds.Contains(c.Id)).ToListAsync();

            var techNames = techs.ToDictionary(t => t.Id, t => AutomatedSmsService.TechFullName(t));
            var custNames = custs.ToDictionary(c => c.Id, c => AutomatedSmsService.CustomerFullName(c));

            ViewBag.RecentLogs = logs.Select(l => new AutomatedLogRow
            {
                SettingName = l.Setting.Name,
                RecipientType = l.RecipientType,
                RecipientId = l.RecipientId,
                RecipientName = (l.RecipientType == RecipientTypes.Technician ? techNames : custNames)
                                    .TryGetValue(l.RecipientId, out var n) ? n : "",
                Phone = l.PhoneNumber,
                Status = l.Status,
                Error = l.ErrorMessage,
                When = svc.ToBusiness(l.CreatedOn).ToString("dd-MMM-yy HH:mm:ss")
            }).ToList();

            return View(await db.AutomatedSmsSettings.AsNoTracking().OrderBy(s => s.Id).ToListAsync());
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
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await db.AutomatedSmsSettings.FindAsync(id);
            if (s != null) { db.AutomatedSmsSettings.Remove(s); await db.SaveChangesAsync(); }
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
            return View("RunResult", new RunNowVm { SettingName = s.Name, Items = items });
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