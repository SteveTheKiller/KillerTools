using System.Text;
using HashLib4CSharp.Base;
using HashLib4CSharp.Interfaces;

namespace KillerTools.Engine.Security;

public static class TextHashes
{
    public static string Hash(string value, string algorithm, string encoding = "Hex") => Encode(Compute(Create(algorithm), Encoding.UTF8.GetBytes(value)), encoding);

    public static string Hmac(string value, string secret, string algorithm, string encoding = "Hex")
    {
        var hmac = HashFactory.HMAC.CreateHMAC(Create(algorithm), Encoding.UTF8.GetBytes(secret));
        return Encode(Compute(hmac, Encoding.UTF8.GetBytes(value)), encoding);
    }

    private static IHash Create(string algorithm) => algorithm switch
    {
        "MD5" => HashFactory.Crypto.CreateMD5(),
        "RIPEMD160" => HashFactory.Crypto.CreateRIPEMD160(),
        "SHA1" => HashFactory.Crypto.CreateSHA1(),
        "SHA3" => HashFactory.Crypto.CreateKeccak_512(),
        "SHA224" => HashFactory.Crypto.CreateSHA2_224(),
        "SHA256" => HashFactory.Crypto.CreateSHA2_256(),
        "SHA384" => HashFactory.Crypto.CreateSHA2_384(),
        "SHA512" => HashFactory.Crypto.CreateSHA2_512(),
        _ => throw new ArgumentException("Unknown hash algorithm.", nameof(algorithm)),
    };

    private static byte[] Compute(IHash hash, byte[] input)
    {
        hash.Initialize();
        hash.TransformByteSpan(input);
        return hash.TransformFinal().GetBytes();
    }

    private static string Encode(byte[] value, string encoding) => encoding switch
    {
        "Bin" => string.Concat(value.Select(item => Convert.ToString(item, 2).PadLeft(8, '0'))),
        "Hex" => Convert.ToHexStringLower(value),
        "Base64" => Convert.ToBase64String(value),
        "Base64url" => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        _ => throw new ArgumentException("Unknown hash encoding.", nameof(encoding)),
    };
}
