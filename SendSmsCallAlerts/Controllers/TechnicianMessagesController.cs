using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    [RoleAuthorize]
    public class TechnicianMessagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppClock _clock;

        public TechnicianMessagesController(ApplicationDbContext context, IAppClock clock)
        {
            _context = context;
            _clock = clock;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = AppRoles.Normalize(HttpContext.Session.GetString("UserRole"));
            var isAdmin = string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
            string? staffPhone = null;

            if (!isAdmin)
            {
                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var userId))
                    return RedirectToAction("LogIn", "Account");

                staffPhone = await _context.User.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.Phone)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(staffPhone))
                {
                    ViewBag.EmptyMessage = "No Twilio number is assigned to your account. Ask an administrator to set your Twilio number.";
                    return View(new List<TechnicianMessageVm>());
                }
            }

            var technicians = await _context.Technicians.AsNoTracking()
                .OrderBy(t => t.FirstName)
                .ThenBy(t => t.LastName)
                .ToListAsync();

            if (technicians.Count == 0)
            {
                ViewBag.EmptyMessage = "No technicians have been configured yet.";
                return View(new List<TechnicianMessageVm>());
            }

            var technicianVariants = technicians
                .SelectMany(t => PhoneMatch.Variants(t.ContactNumber))
                .Distinct()
                .ToList();

            if (technicianVariants.Count == 0)
            {
                ViewBag.EmptyMessage = "No valid technician phone numbers are configured.";
                return View(new List<TechnicianMessageVm>());
            }

            IQueryable<SmsDetail> query = _context.SmsDetails.AsNoTracking()
                .Where(s => s.TechnicianId.HasValue
                    || (s.smsDirection == "Incoming" && technicianVariants.Contains(s.fromNum))
                    || (s.smsDirection != "Incoming" && s.smsDirection.StartsWith("Outgoing")
                        && technicianVariants.Contains(s.toNum)));

            // Staff may only see messages associated with their assigned Twilio number.
            if (!isAdmin)
            {
                var staffVariants = PhoneMatch.Variants(staffPhone);
                if (staffVariants.Count == 0)
                {
                    ViewBag.EmptyMessage = "Your assigned Twilio number could not be matched to SMS records.";
                    return View(new List<TechnicianMessageVm>());
                }

                query = query.Where(s => staffVariants.Contains(s.fromNum) || staffVariants.Contains(s.toNum));
            }

            // Keep the newest 1,000 matching rows. The filter is applied in SQL before the limit.
            var smsRows = await query
                .OrderByDescending(s => s.createdOn)
                .Take(1000)
                .ToListAsync();

            var techniciansById = technicians.ToDictionary(t => t.Id);
            var mapped = new List<(SmsDetail Sms, TechnicianMessageVm Vm)>();

            foreach (var sms in smsRows)
            {
                Technician? technician = null;
                var mappingStatus = "Phone match (legacy)";

                // Prefer the persisted FK. This remains correct even if the technician later changes phone number.
                if (sms.TechnicianId.HasValue && techniciansById.TryGetValue(sms.TechnicianId.Value, out var linkedTech))
                {
                    technician = linkedTech;
                    mappingStatus = "Linked to technician record";
                }
                else
                {
                    // Legacy/outgoing rows can be resolved from the participant's phone number.
                    var participantNumber = string.Equals(sms.smsDirection, "Incoming", StringComparison.OrdinalIgnoreCase)
                        ? sms.fromNum
                        : sms.toNum;
                    var participantVariants = PhoneMatch.Variants(participantNumber);
                    var matches = technicians.Where(t => PhoneMatch.Variants(t.ContactNumber)
                        .Intersect(participantVariants, StringComparer.Ordinal).Any()).ToList();

                    if (matches.Count == 1)
                    {
                        technician = matches[0];
                    }
                    else if (matches.Count > 1)
                    {
                        mapped.Add((sms, new TechnicianMessageVm
                        {
                            SmsId = sms.Id,
                            TechnicianName = "Ambiguous technician number: " + string.Join(", ", matches.Select(t => t.FullName)),
                            TechnicianPhone = participantNumber ?? "",
                            MappingStatus = "Needs review — duplicate technician phone",
                            FromNumber = sms.fromNum ?? "",
                            ToNumber = sms.toNum ?? "",
                            Direction = sms.smsDirection ?? "",
                            MessageBody = sms.smsBody ?? "",
                            CreatedOnBusiness = _clock.ToBusiness(sms.createdOn)
                        }));
                        continue;
                    }
                }

                // Only include rows that can be tied to a technician record (or flagged as ambiguous above).
                if (technician == null) continue;

                mapped.Add((sms, new TechnicianMessageVm
                {
                    SmsId = sms.Id,
                    TechnicianId = technician.Id,
                    TechnicianName = technician.FullName,
                    TechnicianPhone = technician.ContactNumber,
                    MappingStatus = mappingStatus,
                    FromNumber = sms.fromNum ?? "",
                    ToNumber = sms.toNum ?? "",
                    Direction = sms.smsDirection ?? "",
                    MessageBody = sms.smsBody ?? "",
                    CreatedOnBusiness = _clock.ToBusiness(sms.createdOn)
                }));
            }

            var messageIds = mapped.Select(x => x.Sms.Id).Distinct().ToList();
            var attachmentCounts = messageIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.MmsLinkss.AsNoTracking()
                    .Where(m => messageIds.Contains(m.SmsId))
                    .GroupBy(m => m.SmsId)
                    .Select(g => new { SmsId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.SmsId, x => x.Count);

            var result = mapped.Select(x =>
            {
                x.Vm.AttachmentCount = attachmentCounts.TryGetValue(x.Sms.Id, out var count) ? count : 0;
                return x.Vm;
            }).OrderByDescending(x => x.CreatedOnBusiness).ToList();

            ViewBag.TotalMessages = result.Count;
            ViewBag.EmptyMessage = "No messages from registered technicians were found.";
            return View(result);
        }
    }
}
