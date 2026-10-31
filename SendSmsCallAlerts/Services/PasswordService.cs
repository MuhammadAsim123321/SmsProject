using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using SendSmsCallAlerts.Data;
using SendSmsCallAlerts.Models;

namespace SendSmsCallAlerts.Services
{
    public enum PasswordCheck { Failed, Success, SuccessRehashNeeded }

    public interface IPasswordService
    {
        string Hash(User user, string password);
        PasswordCheck Verify(User user, string providedPassword);
        void SimulateWork();
    }

    public class PasswordService : IPasswordService
    {
        // Marks a stored value as a hash. Anything without it is legacy plaintext.
        public const string Prefix = "pbkdf2$";

        private readonly PasswordHasher<User> _hasher = new PasswordHasher<User>();
        private static readonly User _dummyUser = new User { Username = "dummy", Password = "" };

        public string Hash(User user, string password) =>
            Prefix + _hasher.HashPassword(user, password);

        public PasswordCheck Verify(User user, string providedPassword)
        {
            if (string.IsNullOrEmpty(providedPassword)) return PasswordCheck.Failed;

            var stored = user.Password ?? "";

            // Legacy plaintext row (not converted yet): compare in constant time, then ask caller to re-hash.
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                var a = Encoding.UTF8.GetBytes(stored);
                var b = Encoding.UTF8.GetBytes(providedPassword);
                return CryptographicOperations.FixedTimeEquals(a, b)
                    ? PasswordCheck.SuccessRehashNeeded
                    : PasswordCheck.Failed;
            }

            var result = _hasher.VerifyHashedPassword(user, stored.Substring(Prefix.Length), providedPassword);
            return result switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed
            };
        }

        // Called when the username does not exist, so "unknown user" and "wrong password" take similar time.
        public void SimulateWork() => _hasher.HashPassword(_dummyUser, "simulate-work");
    }

    public static class LegacyPasswordMigration
    {
        /// Hashes every plaintext password. Safe to run on every start: already-hashed rows are skipped.
        public static int HashPlaintextPasswords(ApplicationDbContext db, IPasswordService passwords)
        {
            var legacy = db.User.Where(u => !u.Password.StartsWith(PasswordService.Prefix)).ToList();
            foreach (var u in legacy)
                u.Password = passwords.Hash(u, u.Password ?? "");

            if (legacy.Count > 0) db.SaveChanges();
            return legacy.Count;
        }
    }
}