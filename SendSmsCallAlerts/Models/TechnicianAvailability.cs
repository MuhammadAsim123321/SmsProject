using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SendSmsCallAlerts.Models
{
    /// <summary>
    /// One row per technician per weekday (7 rows per technician).
    /// DayOfWeek: 0 = Sunday ... 6 = Saturday (same as System.DayOfWeek).
    /// </summary>
    public class TechnicianAvailability
    {
        public int Id { get; set; }

        public int TechnicianId { get; set; }

        [Range(0, 6)]
        public int DayOfWeek { get; set; }

        public bool IsAvailable { get; set; }

        public TimeSpan? StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }

        [ForeignKey(nameof(TechnicianId))]
        [ValidateNever]
        public Technician Technician { get; set; }
    }
}

/* ------------------------------------------------------------------
   ADD THIS PROPERTY TO YOUR EXISTING Models/Technician.cs:

   [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
   public List<TechnicianAvailability> Availabilities { get; set; } = new();
   ------------------------------------------------------------------ */