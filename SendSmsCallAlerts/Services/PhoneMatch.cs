namespace SendSmsCallAlerts.Services
{
    /// <summary>
    /// Helper to match phone numbers across different storage formats.
    /// Staff messages are identified by matching either fromNum or toNum against the Staff user's Twilio#.
    /// Since numbers may be stored in various formats (with/without +1, digits only, etc.),
    /// this service generates all reasonable variants and checks against them in EF queries.
    /// </summary>
    public static class PhoneMatch
    {
        /// <summary>
        /// Generate all reasonable variants of a phone number for matching purposes.
        /// Returns: original, digits-only, +1+digits, +digits, last-10-digits with +1 and without.
        /// Removes null/empty and duplicates.
        /// </summary>
        public static IReadOnlyList<string> Variants(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return new List<string>();

            var results = new HashSet<string>();
            phone = phone.Trim();

            // Original format
            if (!string.IsNullOrEmpty(phone))
                results.Add(phone);

            // Digits only
            string digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digitsOnly))
                results.Add(digitsOnly);

            // Last 10 digits (for cases like +1 is stored vs not stored)
            if (digitsOnly.Length >= 10)
            {
                string last10 = digitsOnly.Substring(digitsOnly.Length - 10);
                results.Add(last10);
                results.Add("+1" + last10);
            }

            // E.164 formats
            if (digitsOnly.Length == 10)
                results.Add("+1" + digitsOnly);
            if (digitsOnly.Length == 11 && digitsOnly.StartsWith("1"))
                results.Add("+" + digitsOnly);

            // Remove empty strings
            results.RemoveWhere(string.IsNullOrEmpty);

            return results.ToList().AsReadOnly();
        }
    }
}
