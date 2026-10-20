using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Filters;
using SendSmsCallAlerts.Models;
using SendSmsCallAlerts.Services;
using System.Text.Json;

namespace SendSmsCallAlerts.Controllers
{
    [SessionChecking]
    public class TechniciansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleDistanceService _distanceService;

        public TechniciansController(
            ApplicationDbContext context,
            GoogleDistanceService distanceService)
        {
            _context = context;
            _distanceService = distanceService;
        }

        // GET: Technicians
        public async Task<IActionResult> Index()
        {
            try
            {
                var technicians = await _context.Technicians
                    .OrderBy(t => t.FirstName)
                    .ThenBy(t => t.LastName)
                    .ToListAsync();

                // Load categories for display
                var categories = await _context.IvrOption.ToListAsync();
                
                // Prepare view model with category names
                var technicianViewModels = new List<dynamic>();
                foreach (var t in technicians)
                {
                    var categoryNames = GetCategoryNames(t.SelectedCategoryIds, categories);
                    technicianViewModels.Add(new
                    {
                        Technician = t,
                        CategoryNames = categoryNames
                    });
                }

                ViewBag.Categories = categories;
                ViewBag.TechnicianViewModels = technicianViewModels;
                ViewBag.TechnicianCount = technicians.Count;

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
            var categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Categories = categories;
            return View();
        }

        // POST: Technicians/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Technician technician, string[] selectedCategories)
        {
            if (ModelState.IsValid)
            {
                // Store selected category IDs as comma-separated string
                technician.SelectedCategoryIds = selectedCategories != null && selectedCategories.Length > 0
                    ? string.Join(",", selectedCategories)
                    : null;

                _context.Add(technician);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Technician created successfully.";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Categories = categories;
            return View(technician);
        }

        // GET: Technicians/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var technician = await _context.Technicians.FindAsync(id);
            if (technician == null)
            {
                return NotFound();
            }

            var categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Categories = categories;
            
            // Parse selected category IDs for pre-selection in the form
            ViewBag.SelectedCategoryIds = technician.SelectedCategoryIds?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            
            return View(technician);
        }

        // POST: Technicians/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Technician technician, string[] selectedCategories)
        {
            if (id != technician.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Store selected category IDs as comma-separated string
                    technician.SelectedCategoryIds = selectedCategories != null && selectedCategories.Length > 0
                        ? string.Join(",", selectedCategories)
                        : null;

                    _context.Update(technician);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Technician updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TechnicianExists(technician.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var categories = await _context.IvrOption.OrderBy(c => c.keyword).ToListAsync();
            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryIds = selectedCategories ?? Array.Empty<string>();
            
            return View(technician);
        }

        // POST: Technicians/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var technician = await _context.Technicians.FindAsync(id);
                if (technician == null)
                {
                    return Json(new { success = false, message = "Technician not found." });
                }

                // Check if technician is assigned to any contacts
                var assignedContactsCount = await _context.ContactDetail
                    .Where(c => c.AssignedTechnicianId == id)
                    .CountAsync();

                if (assignedContactsCount > 0)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Cannot delete technician. They are currently assigned to {assignedContactsCount} contact(s)." 
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

        // API: GET /Technicians/GetFilteredTechnicians?customerZip={zip}&categoryId={catId}
        [HttpGet]
        public async Task<JsonResult> GetFilteredTechnicians(string customerZip, int? categoryId)
        {
            try
            {
                // Get all technicians
                var techniciansQuery = _context.Technicians.AsQueryable();

                // Filter by category if specified
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    techniciansQuery = techniciansQuery.Where(t => 
                        t.SelectedCategoryIds != null && 
                        t.SelectedCategoryIds.Contains(categoryId.Value.ToString()));
                }

                var technicians = await techniciansQuery.ToListAsync();

                // Calculate distances
                var results = new List<object>();

                foreach (var tech in technicians)
                {
                    double miles = 0;
                    
                    if (!string.IsNullOrWhiteSpace(customerZip) && !string.IsNullOrWhiteSpace(tech.ZipCode))
                    {
                        miles = await _distanceService.GetDistanceInMiles(customerZip, tech.ZipCode);
                    }

                    results.Add(new
                    {
                        id = tech.Id,
                        name = tech.FullName,
                        miles = miles,
                        label = $"{tech.FullName} ({miles:F1} miles)"
                    });
                }

                // Sort by distance
                results = results.OrderBy(r => ((dynamic)r).miles).ToList();

                return Json(results);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private bool TechnicianExists(int id)
        {
            return _context.Technicians.Any(e => e.Id == id);
        }

        private List<string> GetCategoryNames(string categoryIds, List<IvrOptions> allCategories)
        {
            if (string.IsNullOrWhiteSpace(categoryIds))
                return new List<string>();

            var ids = categoryIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(id => int.TryParse(id, out int parsed) ? parsed : 0)
                                 .Where(id => id > 0)
                                 .ToList();

            return allCategories
                .Where(c => ids.Contains(c.Id))
                .Select(c => c.keyword)
                .ToList();
        }
    }
}
