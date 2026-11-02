namespace SendSmsCallAlerts.Services
{
    public static class PhoneNumberService
    {
        public static bool TryNormalize(ref string? phone)
        {
            if (string.IsNullOrEmpty(phone)) return true;

            phone = phone.Trim();
            if (phone.Length == 10) phone = "+1" + phone;
            else if (phone.Length == 11) phone = "+" + phone;
            else if (phone.Length < 10 || phone.Length > 12) return false;
            return true;
        }
    }
}