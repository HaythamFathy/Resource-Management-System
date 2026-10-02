using BCrypt.Net;

namespace Resource_Management_System.Common
{
    /// <summary>
    /// Wraps BCrypt so password hashing/verification logic lives in one place.
    /// Never store or compare plain-text passwords directly - always go through here.
    /// </summary>
    public static class PasswordHelper
    {
        public static string Hash(string plainTextPassword)
        {
            return BCrypt.Net.BCrypt.HashPassword(plainTextPassword);
        }

        public static bool Verify(string plainTextPassword, string storedHash)
        {
            return BCrypt.Net.BCrypt.Verify(plainTextPassword, storedHash);
        }
    }
}