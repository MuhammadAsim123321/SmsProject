using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class TimeToRun
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int HourCount { get; set; }
    }
}
