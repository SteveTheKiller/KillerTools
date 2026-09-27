namespace KillerTools.Engine.Security;

public static class PasswordBcrypt
{
    public static string Hash(string value, int rounds = 10)
    {
        if (rounds is < 4 or > 14) throw new ArgumentOutOfRangeException(nameof(rounds));
        return BCrypt.Net.BCrypt.HashPassword(value, rounds);
    }

    public static bool Verify(string value, string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length > 128) throw new FormatException("Invalid bcrypt hash.");
        return BCrypt.Net.BCrypt.Verify(value, hash);
    }
}
