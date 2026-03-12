using System.ComponentModel.DataAnnotations.Schema;

namespace SendSmsCallAlerts.Models
{
    public class RunFrom
    {
        public int Id { get; set; }

        public string RunFromName { get; set; }
    }
}
