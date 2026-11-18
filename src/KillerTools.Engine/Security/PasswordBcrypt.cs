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
        if (hash is not { Length: 60 } || hash[0] != '$' || hash[1] != '2' || hash[2] is not ('a' or 'b' or 'x' or 'y')
            || hash[3] != '$' || hash[6] != '$' || !char.IsAsciiDigit(hash[4]) || !char.IsAsciiDigit(hash[5]))
            throw new FormatException("Invalid bcrypt hash.");
        int cost = (hash[4] - '0') * 10 + hash[5] - '0';
        if (cost is < 4 or > 14) throw new FormatException("Bcrypt cost must be between 4 and 14.");
        return BCrypt.Net.BCrypt.Verify(value, hash);
    }
}
