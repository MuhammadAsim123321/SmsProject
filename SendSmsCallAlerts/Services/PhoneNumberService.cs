namespace SendSmsCallAlerts.Services
{
    public static class PhoneNumberService
    {
        public static bool TryNormalize(ref string? phone)
        {
            // Users-specific E.164 validation is handled in UsersController.
            // Replaced by users-fix 2026-11-17:
            // if (string.IsNullOrWhiteSpace(phone)) return true;
            // phone = phone.Trim();
            // if (!phone.StartsWith("+", StringComparison.Ordinal)) return false;
            // var digits = phone.Substring(1).Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            // if (digits.Length < 8 || digits.Length > 15 || digits.Length > 0 && digits[0] == '0') return false;
            // if (digits.Any(ch => ch < '0' || ch > '9')) return false;
            // phone = "+" + digits;
            // return true;
            if (string.IsNullOrEmpty(phone)) return true;
            phone = phone.Trim();
            if (phone.Length == 10) phone = "+1" + phone;
            else if (phone.Length == 11) phone = "+" + phone;
            else if (phone.Length < 10 || phone.Length > 12) return false;
            return true;
        }
    }
}