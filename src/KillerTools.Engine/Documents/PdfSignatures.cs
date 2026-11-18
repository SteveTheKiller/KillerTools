using System.Formats.Asn1;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KillerTools.Engine.Checks")]

namespace KillerTools.Engine.Documents;

public static partial class PdfSignatures
{
    private const long MaximumFileSize = 8_000_000;

    public static PdfSignatureResult Inspect(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumFileSize) throw new InvalidDataException("PDF exceeds 8 MB limit");
        var pdf = File.ReadAllBytes(path);
        if (pdf.Length > MaximumFileSize) throw new InvalidDataException("PDF exceeds 8 MB limit");
        return Inspect(pdf);
    }

    public static PdfSignatureResult Inspect(byte[] pdf) => Inspect(pdf, null);

    internal static PdfSignatureResult Inspect(byte[] pdf, X509Certificate2Collection? trustRoots)
    {
        if (pdf.Length > MaximumFileSize) throw new InvalidDataException("PDF exceeds 8 MB limit");
        var text = Encoding.Latin1.GetString(pdf);
        var subFilter = SubFilterRegex().Match(text);
        if (!subFilter.Success || (subFilter.Groups[1].Value.ToLowerInvariant() is not "adbe.pkcs7.detached" and not "etsi.cades.detached"))
        {
            throw new InvalidDataException("PDF signature format is missing or unsupported");
        }

        var ranges = ByteRangeRegex().Matches(text);
        if (ranges.Count > 32) throw new InvalidDataException("PDF exceeds 32 signature limit");
        if (ranges.Count == 0) throw new InvalidDataException("PDF signature byte range is missing");
        var signatures = new List<PdfSignatureInfo>(ranges.Count);
        var verifiedRanges = new Dictionary<(int Offset, int Length, int SecondOffset, int SecondLength), PdfSignatureInfo>();
        var lastSignedEnd = 0;
        foreach (Match match in ranges)
        {
            var firstOffset = ParseRange(match.Groups[1]);
            var firstLength = ParseRange(match.Groups[2]);
            var secondOffset = ParseRange(match.Groups[3]);
            var secondLength = ParseRange(match.Groups[4]);
            ValidateRange(pdf.Length, firstOffset, firstLength, secondOffset, secondLength);
            var range = (firstOffset, firstLength, secondOffset, secondLength);
            if (verifiedRanges.TryGetValue(range, out var previous))
            {
                signatures.Add(previous);
                continue;
            }

            var signedData = new byte[firstLength + secondLength];
            Buffer.BlockCopy(pdf, firstOffset, signedData, 0, firstLength);
            Buffer.BlockCopy(pdf, secondOffset, signedData, firstLength, secondLength);

            var contents = Encoding.ASCII.GetString(pdf, firstOffset + firstLength + 1, secondOffset - firstOffset - firstLength - 2);
            var hex = string.Concat(contents.Where(Uri.IsHexDigit));
            if (hex.Length == 0 || hex.Length % 2 != 0) throw new InvalidDataException("PDF signature contents are invalid");
            var encoded = Convert.FromHexString(hex);
            AsnDecoder.ReadEncodedValue(encoded, AsnEncodingRules.BER, out _, out _, out var consumed);

            var coversWholeDocument = firstOffset == 0 && (long)secondOffset + secondLength == pdf.Length;
            var signature = Verify(encoded.AsSpan(0, consumed).ToArray(), signedData, SignatureMetadata(signedData), trustRoots);
            signature = signature with { CoversWholeDocument = coversWholeDocument, Verified = signature.Verified && coversWholeDocument };
            verifiedRanges.Add(range, signature);
            signatures.Add(signature);
            lastSignedEnd = System.Math.Max(lastSignedEnd, secondOffset + secondLength);
        }
        return new(signatures) { ModifiedAfterLastSignature = lastSignedEnd != pdf.Length };
    }

    private static PdfSignatureInfo Verify(byte[] encoded, byte[] signedData, PdfSignatureMetadata metadata, X509Certificate2Collection? trustRoots)
    {
        var cms = new SignedCms(new ContentInfo(signedData), true);
        cms.Decode(encoded);
        var integrity = true;
        try { cms.CheckSignature(true); }
        catch { integrity = false; }

        if (cms.SignerInfos.Count == 0) throw new InvalidDataException("PDF signature has no signer");
        var signer = cms.SignerInfos[0].Certificate ?? throw new InvalidDataException("PDF signer certificate is missing");
        using var chain = CreateLocalChain(trustRoots);
        chain.ChainPolicy.ExtraStore.AddRange(cms.Certificates);
        bool verifiedChain = chain.Build(signer);
        var flags = chain.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, status) => all | status.Status);
        const X509ChainStatusFlags unavailable = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
        bool expired = flags.HasFlag(X509ChainStatusFlags.NotTimeValid);
        bool authenticity = (flags & ~(unavailable | X509ChainStatusFlags.NotTimeValid)) == 0;
        string revocation = flags.HasFlag(X509ChainStatusFlags.Revoked) ? "revoked"
            : (flags & unavailable) != 0 || !verifiedChain || chain.ChainElements.Count <= 1 ? "unknown" : "good";
        var details = chain.ChainElements.Select((element, index) => CertificateDetails(element.Certificate, index == 0)).ToArray();
        return new(integrity && verifiedChain && revocation == "good", authenticity, integrity, expired, new(details, metadata))
        {
            RevocationStatus = revocation,
        };
    }

    internal static X509Chain CreateLocalChain(X509Certificate2Collection? trustRoots)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
        if (trustRoots is not null)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(trustRoots);
        }
        return chain;
    }

    private static PdfCertificateDetails CertificateDetails(X509Certificate2 certificate, bool clientCertificate) => new(
        clientCertificate ? true : null,
        DistinguishedName(certificate.IssuerName),
        DistinguishedName(certificate.SubjectName),
        new(certificate.NotBefore.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"), certificate.NotAfter.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")),
        certificate.ExportCertificatePem().Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n");

    private static PdfCertificateParty DistinguishedName(X500DistinguishedName name)
    {
        var values = name.Decode(X500DistinguishedNameFlags.UseNewLines)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split('=', 2))
            .Where(value => value.Length == 2)
            .GroupBy(value => value[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(value => value[1].Trim())), StringComparer.OrdinalIgnoreCase);
        string? Value(string key) => values.TryGetValue(key, out var value) ? value : null;
        return new(Value("CN") ?? "", Value("OU"), Value("O"), Value("C"), Value("L"), Value("S") ?? Value("ST"), Value("SERIALNUMBER"));
    }

    private static PdfSignatureMetadata SignatureMetadata(byte[] signedData)
    {
        var text = Encoding.Latin1.GetString(signedData);
        return new(Metadata(text, "Reason"), Metadata(text, "ContactInfo"), Metadata(text, "Location"), Metadata(text, "Name"));
    }

    private static string? Metadata(string text, string key)
    {
        var matches = Regex.Matches(text, $@"/{key}\s*\(([\w.\s@,]*)", RegexOptions.CultureInvariant);
        return matches.Count == 0 ? null : matches[^1].Groups[1].Value;
    }

    private static int ParseRange(Group group) => int.TryParse(group.Value, out var value) ? value : throw new InvalidDataException("PDF signature byte range is invalid");

    private static void ValidateRange(int length, int firstOffset, int firstLength, int secondOffset, int secondLength)
    {
        if (firstOffset != 0 || firstLength < 0 || secondOffset < 0 || secondLength < 0 || (long)firstOffset + firstLength >= secondOffset || (long)secondOffset + secondLength > length)
        {
            throw new InvalidDataException("PDF signature byte range is invalid");
        }
    }

    [GeneratedRegex(@"/SubFilter\s*/([\w.]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubFilterRegex();

    [GeneratedRegex(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.CultureInvariant)]
    private static partial Regex ByteRangeRegex();
}

public sealed record PdfSignatureResult(IReadOnlyList<PdfSignatureInfo> Signatures)
{
    public bool ModifiedAfterLastSignature { get; init; }
}
public sealed record PdfSignatureInfo(bool Verified, bool Authenticity, bool Integrity, bool Expired, PdfSignatureMeta Meta)
{
    public bool CoversWholeDocument { get; init; }
    public string RevocationStatus { get; init; } = "unknown";
}
public sealed record PdfSignatureMeta(IReadOnlyList<PdfCertificateDetails> Certs, PdfSignatureMetadata SignatureMeta);
public sealed record PdfSignatureMetadata(string? Reason, string? ContactInfo, string? Location, string? Name);
public sealed record PdfCertificateDetails([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ClientCertificate, PdfCertificateParty IssuedBy, PdfCertificateParty IssuedTo, PdfCertificateValidity ValidityPeriod, string PemCertificate);
public sealed record PdfCertificateParty(string CommonName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OrganizationalUnitName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OrganizationName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CountryName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LocalityName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StateOrProvinceName, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SerialNumber);
public sealed record PdfCertificateValidity(string NotBefore, string NotAfter);
