using SendSmsCallAlerts.Services;

namespace SendSmsCallAlerts.Models
{
    public class SmsDetailsVm
    {
        public int Id { get; set; }
        public string fromNum { get; set; }
        public string toNum { get; set; }
        public string smsBody { get; set; }
        public string smsDirection { get; set; }
        public string? IsReadYesOrNo { get; set; }
        public string? dt { get; set; }
        public DateTime createdOn { get; set; }
        public string? imgPath { get; set; }

        public List<string> links { get; set; }
        public int attchmentsCount { get; set; }

        public string name { get; set; }
        public string address { get; set; }
        // NEW: Split address fields
        public string? streetAddress { get; set; }
        public string? cityState { get; set; }
        public string? zipCode { get; set; }
        public string email { get; set; }
        public string category { get; set; }
        public string scheduler { get; set; }
        public DateTime? JobBookDate { get; set; }
        public int SchedulerId { get; set; }
        public string? jobBookDatesList { get; set; }
        public string jobBookTimesList { get; set; }

        // Task 4: Response time tracking
        public DateTime? RespondedOn { get; set; }

        /// <summary>
        /// True when a real outgoing reply has been sent for this incoming message.
        /// </summary>
        public bool IsResponded => RespondedOn.HasValue;

        /// <summary>
        /// Elapsed minutes from incoming to the actual reply (set only when IsResponded is true).
        /// Used for the Waiting Time display column and for admin locked colour.
        /// </summary>
        public double? WaitingMinutes { get; set; }

        /// <summary>
        /// The colour bucket locked at the moment the message was responded to,
        /// based on WaitingMinutes (actual reply elapsed time).
        /// Used by admin to permanently show the colour the message had when staff replied.
        /// Returns "" when not yet responded or when the reply came in under 10 min (no threshold crossed).
        /// </summary>
        public string RespondedColor =>
            smsDirection != "Incoming" || !IsResponded
                ? ""
                : ResponseRules.ColorFor(WaitingMinutes);

        /// <summary>
        /// Staff-view colour:
        ///   - Pending → based on (UtcNow – createdOn), grows live.
        ///   - Responded → always "" (default, no colour).
        /// </summary>
        public string WaitingColor
        {
            get
            {
                if (smsDirection != "Incoming") return "";
                if (IsResponded) return "";   // staff: responded = no colour
                var elapsedMinutes = (DateTime.UtcNow - createdOn).TotalMinutes;
                return ResponseRules.ColorFor(elapsedMinutes);
            }
        }

        /// <summary>
        /// Admin-view colour:
        ///   - Pending → same as WaitingColor (live elapsed time).
        ///   - Responded → locked to RespondedColor (the colour the message held at reply time).
        ///     If staff replied before any threshold (under 10 min) this returns "" — same as staff.
        /// </summary>
        public string AdminWaitingColor
        {
            get
            {
                if (smsDirection != "Incoming") return "";
                if (IsResponded) return RespondedColor;  // lock in the colour from reply time
                var elapsedMinutes = (DateTime.UtcNow - createdOn).TotalMinutes;
                return ResponseRules.ColorFor(elapsedMinutes);
            }
        }
    }
}