//using Hangfire.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using Job = SendSmsCallAlerts.Models.Job;

namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// All business rules for: contact + job (appointment) + message plan.
    /// Controllers stay thin and call this service.
    /// </summary>
    public class JobService
    {
        public const int RunFromCustomDateId = 2;      // RunFrom: "Custom Date"
        public const int TimeToRunCustomMarker = -1;   // TimeToRun.HourCount = -1: "Enter Custom Days and Hours"

        private static readonly Regex ZipRegex = new(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);

        private readonly ApplicationDbContext _db;
        private readonly TechnicianAvailabilityService _availability;
        private readonly GoogleDistanceService _distance;
        private readonly IAppClock _clock;
        private readonly ILogger<JobService> _logger;

        public JobService(ApplicationDbContext db, TechnicianAvailabilityService availability,
            GoogleDistanceService distance, IAppClock clock, ILogger<JobService> logger)
        {
            _db = db;
            _availability = availability;
            _distance = distance;
            _clock = clock;
            _logger = logger;
        }

        // =====================================================================
        // READ: contact + its jobs (modal)
        // =====================================================================

        /// <summary>
        /// Load the contact view keyed by an incoming SMS id.
        /// - LinkedJob  : the single job whose SourceSmsId == smsId (null if none).
        /// - AllJobs    : every job for the contact, newest-first (used by the history popup).
        /// </summary>
        public async Task<ContactView> GetContactViewBySmsAsync(int smsId)
        {
            // resolve the message and its sender's phone number
            var sms = await _db.SmsDetails.AsNoTracking().FirstOrDefaultAsync(s => s.Id == smsId);
            if (sms == null) return new ContactView { Found = false };

            var phone = sms.fromNum ?? "";
            var contact = await _db.ContactDetail.AsNoTracking().FirstOrDefaultAsync(c => c.PhoneNumber == phone);
            if (contact == null) return new ContactView { Found = false };

            // all jobs for the customer, newest first
            var allJobs = await _db.Jobs.AsNoTracking()
                .Include(j => j.Category)
                .Include(j => j.Technician)
                .Include(j => j.ScheduledJobs).ThenInclude(p => p.Scheduler)
                .Where(j => j.ContactDetailId == contact.Id)
                .OrderByDescending(j => j.StartAt)
                .ToListAsync();

            var now = _clock.BusinessNow;

            JobView? MapJob(Job j) => new JobView
            {
                Id = j.Id,
                CategoryId = j.CategoryId,
                CategoryName = j.Category?.keyword ?? "",
                TechnicianId = j.TechnicianId,
                TechnicianName = j.Technician?.FullName ?? "",
                StartAt = j.StartAt.ToString("yyyy-MM-dd'T'HH:mm"),
                EndAt = j.EndAt.ToString("yyyy-MM-dd'T'HH:mm"),
                DistanceMiles = j.DistanceMiles,
                Status = j.Status,
                EffectiveStatus = j.GetEffectiveStatus(now),
                Plans = j.ScheduledJobs.OrderBy(p => p.Id).Select(p => new PlanView
                {
                    Id = p.Id,
                    SchedulerId = p.SchedulerId,
                    SchedulerName = p.Scheduler?.name ?? "",
                    CustomDate = p.CustomDate?.ToString("yyyy-MM-dd"),
                    CustomHours = p.CustomHours,
                    IsPaused = p.IsPaused,
                    LastExecutedAt = p.LastExecutedAt
                }).ToList()
            };

            // the job directly linked to this message (may be null)
            var linkedJobEntity = allJobs.FirstOrDefault(j => j.SourceSmsId == smsId);

            return new ContactView
            {
                Found = true,
                Name = contact.Name ?? "",
                Email = contact.Email ?? "",
                StreetAddress = contact.StreetAddress ?? "",
                CityState = contact.CityState ?? "",
                ZipCode = contact.ZipCode ?? "",
                LinkedJob = linkedJobEntity != null ? MapJob(linkedJobEntity) : null,
                Jobs = allJobs.Select(MapJob).ToList()!
            };
        }

        public async Task<ContactView> GetContactViewAsync(string phone)
        {
            phone = (phone ?? "").Trim();
            var contact = await _db.ContactDetail.AsNoTracking().FirstOrDefaultAsync(c => c.PhoneNumber == phone);
            if (contact == null) return new ContactView { Found = false };

            var jobs = await _db.Jobs.AsNoTracking()
                .Include(j => j.Category)
                .Include(j => j.Technician)
                .Include(j => j.ScheduledJobs).ThenInclude(p => p.Scheduler)
                .Where(j => j.ContactDetailId == contact.Id)
                .OrderByDescending(j => j.StartAt)
                .ToListAsync();

            var now = _clock.BusinessNow;
            return new ContactView
            {
                Found = true,
                Name = contact.Name ?? "",
                Email = contact.Email ?? "",
                StreetAddress = contact.StreetAddress ?? "",
                CityState = contact.CityState ?? "",
                ZipCode = contact.ZipCode ?? "",
                Jobs = jobs.Select(j => new JobView
                {
                    Id = j.Id,
                    CategoryId = j.CategoryId,
                    CategoryName = j.Category?.keyword ?? "",
                    TechnicianId = j.TechnicianId,
                    TechnicianName = j.Technician?.FullName ?? "",
                    StartAt = j.StartAt.ToString("yyyy-MM-dd'T'HH:mm"),
                    EndAt = j.EndAt.ToString("yyyy-MM-dd'T'HH:mm"),
                    DistanceMiles = j.DistanceMiles,
                    Status = j.Status,
                    EffectiveStatus = j.GetEffectiveStatus(now),
                    Plans = j.ScheduledJobs.OrderBy(p => p.Id).Select(p => new PlanView
                    {
                        Id = p.Id,
                        SchedulerId = p.SchedulerId,
                        SchedulerName = p.Scheduler?.name ?? "",
                        CustomDate = p.CustomDate?.ToString("yyyy-MM-dd"),
                        CustomHours = p.CustomHours,
                        IsPaused = p.IsPaused,
                        LastExecutedAt = p.LastExecutedAt
                    }).ToList()
                }).ToList()
            };
        }

        /// <summary>Jobs (with category, technician, plans) for many contacts. Used by the grid and the CSV export.</summary>
        public async Task<List<Job>> LoadJobsForContactsAsync(List<int> contactIds)
        {
            if (contactIds == null || contactIds.Count == 0) return new List<Job>();
            return await _db.Jobs.AsNoTracking()
                .Include(j => j.Category)
                .Include(j => j.Technician)
                .Include(j => j.ScheduledJobs).ThenInclude(p => p.Scheduler)
                .Where(j => contactIds.Contains(j.ContactDetailId))
                .OrderBy(j => j.StartAt)
                .ToListAsync();
        }

        // =====================================================================
        // WRITE: one Update click = contact + job + plan, all or nothing
        // =====================================================================
        public async Task<SaveResult> SaveAsync(SaveContactJobRequest req)
        {
            var result = new SaveResult();
            var errors = result.Errors;

            var phone = (req.PhoneNumber ?? "").Trim();
            if (phone.Length == 0) { result.Message = "Phone number is required."; return result; }

            // ---- 1) contact rules -------------------------------------------------
            var c = req.Contact ?? new ContactSection();
            var name = (c.Name ?? "").Trim();
            var email = (c.Email ?? "").Trim();
            var zip = (c.ZipCode ?? "").Trim();

            if (name.Length == 0) errors["contactName"] = "Customer name is required.";
            else if (name.Length > 100) errors["contactName"] = "Name cannot be longer than 100 characters.";
            if (email.Length > 0 && !new EmailAddressAttribute().IsValid(email)) errors["contactEmail"] = "Enter a valid email address.";
            if (zip.Length > 0 && !ZipRegex.IsMatch(zip)) errors["contactZip"] = "Enter a valid ZIP code (12345 or 12345-6789).";

            // ---- 2) what was submitted -------------------------------------------
            bool isOutgoing = (req.SmsDirection ?? "").StartsWith("Outgoing", StringComparison.OrdinalIgnoreCase);
            var j = isOutgoing ? null : req.Job;
            var sc = isOutgoing ? null : req.Scheduler;
            int schedulerId = sc?.SchedulerId ?? 0;
            bool hasJobInput = j != null && (j.JobId.HasValue || (j.CategoryId ?? 0) > 0 || (j.TechnicianId ?? 0) > 0
                                             || j.StartAt.HasValue || j.EndAt.HasValue);
            bool needsJob = hasJobInput || schedulerId > 0;

            var contact = await _db.ContactDetail.FirstOrDefaultAsync(x => x.PhoneNumber == phone);

            Job? job = null;
            if (needsJob && j?.JobId is int existingId && existingId > 0)
            {
                job = contact == null ? null : await _db.Jobs.Include(x => x.ScheduledJobs)
                    .FirstOrDefaultAsync(x => x.Id == existingId && x.ContactDetailId == contact.Id);
                if (job == null)
                {
                    result.Message = "The selected job was not found for this customer.";
                    return result;
                }
            }

            // ---- 3) job + scheduler rules ----------------------------------------
            Technician? tech = null;
            DateTime? start = null, end = null;
            string status = JobStatus.Scheduled;
            var now = _clock.BusinessNow;

            if (needsJob)
            {
                var jj = j ?? new JobSection();

                if ((jj.CategoryId ?? 0) <= 0) errors["jobCategory"] = "Select a service category.";
                else if (!await _db.IvrOption.AnyAsync(x => x.Id == jj.CategoryId!.Value))
                    errors["jobCategory"] = "The selected category no longer exists.";

                if ((jj.TechnicianId ?? 0) <= 0) errors["jobTechnician"] = "Select a technician.";
                else
                {
                    tech = await _db.Technicians.AsNoTracking().FirstOrDefaultAsync(t => t.Id == jj.TechnicianId!.Value);
                    if (tech == null) errors["jobTechnician"] = "The selected technician was not found.";
                    else if (!errors.ContainsKey("jobCategory") && !TechnicianServesCategory(tech, jj.CategoryId!.Value))
                        errors["jobTechnician"] = "This technician does not serve the selected category.";
                }

                if (!jj.StartAt.HasValue) errors["jobStartAt"] = "Select the job start date and time.";
                if (!jj.EndAt.HasValue) errors["jobEndAt"] = "Select the job end date and time.";
                if (jj.StartAt.HasValue && jj.EndAt.HasValue) { start = jj.StartAt.Value; end = jj.EndAt.Value; }

                var requested = (jj.Status ?? "").Trim();
                if (requested.Length > 0 && !JobStatus.IsEditable(requested)) errors["jobStatus"] = "Invalid status.";
                else status = requested.Length > 0 ? requested : (job?.Status ?? JobStatus.Scheduled);

                if (start.HasValue && end.HasValue)
                {
                    if (end <= start) errors["jobEndAt"] = "End time must be after the start time.";
                    else if (start.Value.Date != end.Value.Date) errors["jobEndAt"] = "Job must start and end on the same day.";

                    bool startChanged = job == null || job.StartAt != start.Value;
                    if (startChanged && status != JobStatus.Cancelled && start.Value < TruncateToMinute(now) && !errors.ContainsKey("jobStartAt"))
                        errors["jobStartAt"] = "Start time cannot be in the past.";
                }

                if (tech != null && start.HasValue && end.HasValue && status != JobStatus.Cancelled
                    && !errors.ContainsKey("jobStartAt") && !errors.ContainsKey("jobEndAt"))
                {
                    var slot = await _availability.CheckJobSlotAsync(tech.Id, start.Value, end.Value, job?.Id);
                    if (!slot.Allowed) errors[slot.Field ?? "jobStartAt"] = slot.Message;
                }

                if (schedulerId > 0)
                {
                    var scheduler = await _db.Schedulers.Include(s => s.TimeToRun).FirstOrDefaultAsync(s => s.Id == schedulerId);
                    if (scheduler == null) errors["schedulerTemplate"] = "The selected scheduler template was not found.";
                    else
                    {
                        if (scheduler.RunFromId == RunFromCustomDateId && !(sc!.CustomDate.HasValue))
                            errors["schedulerCustomDate"] = "Select the custom date.";
                        if (scheduler.TimeToRun?.HourCount == TimeToRunCustomMarker && (sc!.CustomHours ?? 0) <= 0)
                            errors["schedulerCustomHours"] = "Enter custom days and/or hours (at least 1 hour).";
                    }
                }
            }

            if (errors.Count > 0)
            {
                result.Message = "Please fix the highlighted fields.";
                return result;
            }

            // ---- 4) distance (outside the transaction: external call) -------------
            double? distance = null;
            if (needsJob && tech != null)
                distance = await _distance.GetDistanceInMilesOrNull(zip, tech.ZipCode ?? "");

            // ---- 5) save everything in ONE transaction ---------------------------
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (contact == null)
                {
                    contact = new ContactDetail { PhoneNumber = phone };
                    _db.ContactDetail.Add(contact);
                }
                contact.Name = name;
                contact.Email = email.Length > 0 ? email : null;
                contact.StreetAddress = Clean(c.StreetAddress);
                contact.CityState = Clean(c.CityState);
                contact.ZipCode = zip.Length > 0 ? zip : null;
                await _db.SaveChangesAsync();

                // link every unlinked message of this phone number to the contact
                int contactId = contact.Id;
                await _db.SmsDetails
                    .Where(s => s.ContactDetailId == null && (s.fromNum == phone || s.toNum == phone))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ContactDetailId, contactId));

                if (needsJob)
                {
                    var jj = j!;
                    if (job == null)
                    {
                        job = new Job { ContactDetailId = contact.Id, CreatedOn = _clock.UtcNow };
                        if (req.SourceSmsId is int srcId && srcId > 0 && await _db.SmsDetails.AnyAsync(s => s.Id == srcId))
                            job.SourceSmsId = srcId;
                        _db.Jobs.Add(job);
                    }
                    else
                    {
                        // a confirmed job whose time/technician changed must be confirmed again
                        bool changed = job.StartAt != start!.Value || job.EndAt != end!.Value || job.TechnicianId != tech!.Id;
                        if (job.Status == JobStatus.Confirmed && status == JobStatus.Confirmed && changed)
                            status = JobStatus.Scheduled;
                    }

                    job.CategoryId = jj.CategoryId;
                    job.TechnicianId = tech!.Id;
                    job.StartAt = start!.Value;
                    job.EndAt = end!.Value;
                    job.DistanceMiles = distance;
                    job.Status = status;
                    job.UpdatedOn = _clock.UtcNow;
                    await _db.SaveChangesAsync();   // job.Id is needed for the plan

                    SyncPlan(job, schedulerId, sc);
                    await _db.SaveChangesAsync();
                    result.JobId = job.Id;
                }

                await tx.CommitAsync();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveContactJob failed for {Phone}", phone);
                result.Success = false;
                result.Message = "The changes could not be saved. Please try again.";
                return result;
            }
        }

        /// <summary>One message plan per job for now. No scheduler selected = the plan is removed.</summary>
        private void SyncPlan(Job job, int schedulerId, SchedulerSection? sc)
        {
            var plans = job.ScheduledJobs.OrderBy(p => p.Id).ToList();

            if (schedulerId <= 0)
            {
                foreach (var p in plans) _db.AllScheduledJobs.Remove(p);
                return;
            }

            var current = plans.FirstOrDefault();
            if (current != null && current.SchedulerId == schedulerId)
            {
                current.CustomDate = sc?.CustomDate;
                current.CustomHours = sc?.CustomHours;
                return;
            }

            foreach (var p in plans) _db.AllScheduledJobs.Remove(p);
            _db.AllScheduledJobs.Add(new AllScheduledJob
            {
                JobId = job.Id,
                SchedulerId = schedulerId,
                AssignedDate = _clock.BusinessNow,
                CustomDate = sc?.CustomDate,
                CustomHours = sc?.CustomHours
            });
        }

        // =====================================================================
        // Customer replies (webhook): act on the customer's NEXT upcoming job only
        // =====================================================================
        public Task<Job?> CancelNextUpcomingAsync(string phone) =>
            ChangeNextUpcomingAsync(phone, new[] { JobStatus.Scheduled, JobStatus.Confirmed }, JobStatus.Cancelled);

        public Task<Job?> ConfirmNextUpcomingAsync(string phone) =>
            ChangeNextUpcomingAsync(phone, new[] { JobStatus.Scheduled }, JobStatus.Confirmed);

        private async Task<Job?> ChangeNextUpcomingAsync(string phone, string[] fromStatuses, string toStatus)
        {
            var contact = await _db.ContactDetail.AsNoTracking().FirstOrDefaultAsync(c => c.PhoneNumber == phone);
            if (contact == null) return null;

            var now = _clock.BusinessNow;
            var job = await _db.Jobs.Include(j => j.Category)
                .Where(j => j.ContactDetailId == contact.Id && j.StartAt >= now && fromStatuses.Contains(j.Status))
                .OrderBy(j => j.StartAt)
                .FirstOrDefaultAsync();
            if (job == null) return null;

            job.Status = toStatus;
            job.UpdatedOn = _clock.UtcNow;
            await _db.SaveChangesAsync();
            return job;
        }

        // =====================================================================
        // helpers
        // =====================================================================
        /// <summary>Technician.SelectedCategoryIds is a CSV ("1,3,5"). Exact token match.</summary>
        private static bool TechnicianServesCategory(Technician t, int categoryId)
        {
            return (t.SelectedCategoryIds ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => int.TryParse(x, out var id) && id == categoryId);
        }

        private static string? Clean(string? s)
        {
            s = (s ?? "").Trim();
            return s.Length == 0 ? null : s;
        }

        private static DateTime TruncateToMinute(DateTime d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);
    }
}