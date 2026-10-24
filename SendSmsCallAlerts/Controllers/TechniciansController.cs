using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class TechniciansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleDistanceService _distanceService;
        private readonly TechnicianAvailabilityService _availabilityService;

        public TechniciansController(
            ApplicationDbContext context,
            GoogleDistanceService distanceService,
            TechnicianAvailabilityService availabilityService)
        {
            _context = context;
            _distanceService = distanceService;
            _availabilityService = availabilityService;
        }

        // GET: Technicians
        public async Task<IActionResult> Index()
        {
            try
            {
                var technicians = await _context.Technicians
                    .Include(t => t.Availabilities)
                    .OrderBy(t => t.FirstName)
                    .ThenBy(t => t.LastName)
                    .ToListAsync();

                var categories = await _context.IvrOption.ToListAsync();

                var technicianViewModels = new List<dynamic>();
                var summaries = new Dictionary<int, string>();
                foreach (var t in technicians)
                {
                    technicianViewModels.Add(new
                    {
                        Technician = t,
                        CategoryNames = GetCategoryNames(t.SelectedCategoryIds, categories)
                    });
                    summaries[t.Id] = TechnicianAvailabilityService.BuildSummary(t.Availabilities);
                }

                ViewBag.Categories = categories;
                ViewBag.TechnicianViewModels = technicianViewModels;
                ViewBag.TechnicianCount = technicians.Count;
                ViewBag.AvailabilitySummaries = summaries;   // use in Index view: summaries[item.Id]

                return View(technicians);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading technicians: " + ex.Message;
                return View(new List<Technician>());
            }
        }

        // GET: Technicians/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Availability = TechnicianAvailabilityService.DefaultWeek();
            return View();
        }

        // POST: Technicians/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Technician technician, string[] selectedCategories,
            List<TechnicianAvailability> availability)
        {
            var week = TechnicianAvailabilityService.Normalize(availability);
            var availabilityError = TechnicianAvailabilityService.Validate(week);
            if (availabilityError != null)
                ModelState.AddModelError(string.Empty, availabilityError);

            if (ModelState.IsValid)
            {
                technician.SelectedCategoryIds = selectedCategories != null && selectedCategories.Length > 0
                    ? string.Join(",", selectedCategories)
                    : null;

                technician.Availabilities = week;   // inserted together with the technician (one SaveChanges)

                _context.Add(technician);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Technician created successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Availability = week;
            ViewBag.SelectedCategoryIds = selectedCategories ?? Array.Empty<string>();
            return View(technician);
        }

        // GET: Technicians/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var technician = await _context.Technicians.FindAsync(id);
            if (technician == null) return NotFound();

            ViewBag.Categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.SelectedCategoryIds = technician.SelectedCategoryIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            var rows = await _context.TechnicianAvailabilities
                .AsNoTracking()
                .Where(a => a.TechnicianId == id)
                .ToListAsync();
            ViewBag.Availability = rows.Count > 0
                ? TechnicianAvailabilityService.Normalize(rows)
                : TechnicianAvailabilityService.DefaultWeek();

            return View(technician);
        }

        // POST: Technicians/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Technician technician, string[] selectedCategories,
            List<TechnicianAvailability> availability)
        {
            if (id != technician.Id) return NotFound();

            var week = TechnicianAvailabilityService.Normalize(availability);
            var availabilityError = TechnicianAvailabilityService.Validate(week);
            if (availabilityError != null)
                ModelState.AddModelError(string.Empty, availabilityError);

            if (ModelState.IsValid)
            {
                try
                {
                    technician.SelectedCategoryIds = selectedCategories != null && selectedCategories.Length > 0
                        ? string.Join(",", selectedCategories)
                        : null;

                    technician.Availabilities = null; // handled separately below
                    _context.Update(technician);

                    // Update availability in place (unique index on TechnicianId + DayOfWeek)
                    var existing = await _context.TechnicianAvailabilities
                        .Where(a => a.TechnicianId == id)
                        .ToListAsync();

                    foreach (var w in week)
                    {
                        var row = existing.FirstOrDefault(e => e.DayOfWeek == w.DayOfWeek);
                        if (row == null)
                        {
                            _context.TechnicianAvailabilities.Add(new TechnicianAvailability
                            {
                                TechnicianId = id,
                                DayOfWeek = w.DayOfWeek,
                                IsAvailable = w.IsAvailable,
                                StartTime = w.StartTime,
                                EndTime = w.EndTime
                            });
                        }
                        else
                        {
                            row.IsAvailable = w.IsAvailable;
                            row.StartTime = w.StartTime;
                            row.EndTime = w.EndTime;
                        }
                    }

                    await _context.SaveChangesAsync();   // single transaction

                    TempData["SuccessMessage"] = "Technician updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TechnicianExists(technician.Id)) return NotFound();
                    throw;
                }
            }

            ViewBag.Categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.SelectedCategoryIds = selectedCategories ?? Array.Empty<string>();
            ViewBag.Availability = week;
            return View(technician);
        }

        // POST: Technicians/Delete/5   (availability rows are removed by cascade)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var technician = await _context.Technicians.FindAsync(id);
                if (technician == null)
                    return Json(new { success = false, message = "Technician not found." });

                var jobCount = await _context.Jobs.CountAsync(j => j.TechnicianId == id);
                if (jobCount > 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete technician. They are assigned to {jobCount} job(s)."
                    });
                }

                _context.Technicians.Remove(technician);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Technician deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting technician: " + ex.Message });
            }
        }

        // GET /Technicians/GetAvailability?technicianId=5
        [HttpGet]
        public async Task<JsonResult> GetAvailability(int technicianId)
        {
            var rows = await _context.TechnicianAvailabilities
                .AsNoTracking()
                .Where(a => a.TechnicianId == technicianId)
                .ToListAsync();

            return Json(new
            {
                success = true,
                summary = TechnicianAvailabilityService.BuildSummary(rows),
                days = TechnicianAvailabilityService.DisplayOrder.Select(d =>
                {
                    var r = rows.FirstOrDefault(x => x.DayOfWeek == d);
                    return new
                    {
                        dayOfWeek = d,
                        dayName = TechnicianAvailabilityService.DayNames[d],
                        isAvailable = r?.IsAvailable ?? false,
                        startTime = r?.StartTime?.ToString(@"hh\:mm"),
                        endTime = r?.EndTime?.ToString(@"hh\:mm")
                    };
                })
            });
        }

        // GET /Technicians/CheckAvailability?technicianId=5&jobDateTime=2026-10-23T10:00
        [HttpGet]
        public async Task<JsonResult> CheckAvailability(int technicianId, DateTime jobDateTime)
        {
            var result = await _availabilityService.CheckAsync(technicianId, jobDateTime);
            return Json(new
            {
                success = true,
                allowed = result.Allowed,
                configured = result.Configured,
                message = result.Message
            });
        }

        // GET /Technicians/GetFilteredTechnicians?customerZip=10001&categoryId=3&jobDateTime=2026-10-23T10:00
        // GET /Technicians/GetFilteredTechnicians?customerZip=10001&categoryId=3&jobStartAt=2026-10-27T10:00&jobEndAt=2026-10-27T12:00&excludeJobId=5
        [HttpGet]
        public async Task<JsonResult> GetFilteredTechnicians(string customerZip, int? categoryId,
            DateTime? jobStartAt, DateTime? jobEndAt, int? excludeJobId)
        {
            try
            {
                var query = _context.Technicians
                    .AsNoTracking()
                    .Include(t => t.Availabilities)
                    .AsQueryable();

                // Exact token match on the CSV ("1" must not match "11")
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    var token = "," + categoryId.Value + ",";
                    query = query.Where(t => t.SelectedCategoryIds != null &&
                        ("," + t.SelectedCategoryIds.Replace(" ", "") + ",").Contains(token));
                }

                var technicians = await query.ToListAsync();

                // availability for all technicians at once (working hours + overlap with other jobs)
                Dictionary<int, AvailabilityCheckResult> checks = null;
                if (jobStartAt.HasValue)
                    checks = await _availabilityService.CheckManyAsync(technicians, jobStartAt.Value, jobEndAt, excludeJobId);

                // Distance lookups in parallel (max 5 at once); results are cached in the service
                using var gate = new SemaphoreSlim(5);
                var tasks = technicians.Select(async tech =>
                {
                    double? miles = null;
                    if (!string.IsNullOrWhiteSpace(customerZip) && !string.IsNullOrWhiteSpace(tech.ZipCode))
                    {
                        await gate.WaitAsync();
                        try { miles = await _distanceService.GetDistanceInMilesOrNull(customerZip, tech.ZipCode); }
                        finally { gate.Release(); }
                    }

                    AvailabilityCheckResult check = null;
                    if (checks != null) checks.TryGetValue(tech.Id, out check);

                    return new TechnicianOption
                    {
                        Id = tech.Id,
                        Name = tech.FullName,
                        Miles = miles ?? 0,
                        DistanceAvailable = miles.HasValue,
                        Label = miles.HasValue
                            ? $"{tech.FullName} ({miles.Value:F1} miles)"
                            : $"{tech.FullName} (N/A miles)",
                        AvailabilitySummary = TechnicianAvailabilityService.BuildSummary(tech.Availabilities),
                        IsAvailable = check?.Allowed ?? true,
                        AvailabilityMessage = check?.Message
                    };
                });

                var results = (await Task.WhenAll(tasks))
                    .OrderBy(r => r.DistanceAvailable ? 0 : 1)   // unknown distance last
                    .ThenBy(r => r.Miles)
                    .ThenBy(r => r.Name)
                    .ToList();

                return Json(results);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private sealed class TechnicianOption
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public double Miles { get; set; }
            public bool DistanceAvailable { get; set; }
            public string Label { get; set; }
            public string AvailabilitySummary { get; set; }
            public bool IsAvailable { get; set; }
            public string AvailabilityMessage { get; set; }
        }

        private bool TechnicianExists(int id) => _context.Technicians.Any(e => e.Id == id);

        private List<string> GetCategoryNames(string categoryIds, List<IvrOptions> allCategories)
        {
            if (string.IsNullOrWhiteSpace(categoryIds)) return new List<string>();

            var ids = categoryIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(id => int.TryParse(id, out int parsed) ? parsed : 0)
                                 .Where(id => id > 0)
                                 .ToList();

            return allCategories.Where(c => ids.Contains(c.Id)).Select(c => c.keyword).ToList();
        }
    }
}