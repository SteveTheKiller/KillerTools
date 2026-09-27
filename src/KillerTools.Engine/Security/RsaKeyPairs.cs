using System.Security.Cryptography;

namespace KillerTools.Engine.Security;

public readonly record struct RsaKeyPair(string PublicKeyPem, string PrivateKeyPem);

public static class RsaKeyPairs
{
    public static RsaKeyPair Generate(int bits)
    {
        if (bits is not (2048 or 3072 or 4096))
        {
            throw new ArgumentOutOfRangeException(nameof(bits));
        }
        using var rsa = RSA.Create(bits);
        return new(rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportRSAPrivateKeyPem());
    }
}
