using System.Globalization;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Network;

public sealed record EmailField(string Label, string Value);
public sealed record EmailHop(string From, string By, string Ip, string Timestamp, string Delay);
public sealed record EmailAuthResult(string Protocol, string Result, string Detail);
public sealed record ParsedEmailHeaders(IReadOnlyList<EmailField> Fields, IReadOnlyList<EmailHop> Hops, IReadOnlyList<EmailAuthResult> Auth, string? SpamScore, string? SpamStatus, string? Scl, string SclLabel, bool SenderMismatch, string SendingService);

public static partial class EmailHeaderParser
{
    public static ParsedEmailHeaders Parse(string rawHeaders)
    {
        var unfolded = FoldedLine().Replace(rawHeaders.Trim(), " ");
        var lines = unfolded.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Where(line => line.Contains(':')).ToArray();
        var from = Get(lines, "From");
        var sender = Get(lines, "Sender");
        var fromDomain = Domain(Email(from));
        var senderDomain = Domain(Email(sender));
        var service = DetectService(lines);
        var scl = EmptyToNull(Get(lines, "X-MS-Exchange-Organization-SCL"));

        var fields = new[]
        {
            new EmailField("From", from), new EmailField("Sender", sender), new EmailField("To", Get(lines, "To")),
            new EmailField("Delivered-To", Get(lines, "Delivered-To") is { Length: > 0 } delivered ? delivered : Get(lines, "X-Forwarded-To")),
            new EmailField("Reply-To", Get(lines, "Reply-To")), new EmailField("Return-Path", Get(lines, "Return-Path")),
            new EmailField("Subject", Get(lines, "Subject")), new EmailField("Date", Get(lines, "Date")),
            new EmailField("Message-ID", Get(lines, "Message-ID")), new EmailField("Sending Service", service),
            new EmailField("X-Originating-IP", Get(lines, "X-Originating-IP") is { Length: > 0 } origin ? origin : Get(lines, "X-Sender-IP")),
            new EmailField("X-Mailer", Get(lines, "X-Mailer")), new EmailField("MIME-Version", Get(lines, "MIME-Version")),
        }.Where(field => field.Value.Length > 0).ToArray();

        return new ParsedEmailHeaders(fields, Hops(All(lines, "Received")), Auth(lines),
            EmptyToNull(Get(lines, "X-Spam-Score") is { Length: > 0 } score ? score : Get(lines, "X-Spam-Level")),
            EmptyToNull(Get(lines, "X-Spam-Status")), scl, scl is null ? string.Empty : SclLabel(scl),
            sender.Length > 0 && fromDomain.Length > 0 && senderDomain.Length > 0 && fromDomain != senderDomain, service);
    }

    private static string Get(string[] lines, string name) => lines.FirstOrDefault(line => line.StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase)) is { } found ? found[(name.Length + 1)..].Trim() : string.Empty;
    private static string[] All(string[] lines, string name) => lines.Where(line => line.StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase)).Select(line => line[(name.Length + 1)..].Trim()).ToArray();
    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
    private static string Email(string value) => AngleEmail().Match(value) is { Success: true } match ? match.Groups[1].Value.Trim() : value.Trim();
    private static string Domain(string value) => value.LastIndexOf('@') is var index && index >= 0 ? value[(index + 1)..].ToLowerInvariant() : string.Empty;
    private static string Ip(string value) => BracketIp().Match(value) is { Success: true } bracket ? bracket.Groups[1].Value : BareIp().Match(value) is { Success: true } bare ? bare.Groups[1].Value : string.Empty;

    private static IReadOnlyList<EmailHop> Hops(string[] received)
    {
        var rows = new List<(string From, string By, string Ip, string Timestamp, DateTimeOffset? Date)>();
        foreach (var raw in received)
        {
            var dateMatch = ReceivedDate().Match(raw);
            var timestamp = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : string.Empty;
            var parseableTimestamp = timestamp.Length > 4 && timestamp[3] == ',' ? timestamp[4..].TrimStart() : timestamp;
            DateTimeOffset? date = DateTimeOffset.TryParse(parseableTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed) ? parsed : null;
            var from = ReceivedFrom().Match(raw);
            var by = ReceivedBy().Match(raw);
            rows.Add((from.Success ? from.Groups[1].Value : string.Empty, by.Success ? by.Groups[1].Value : string.Empty, Ip(raw), timestamp, date));
        }
        return rows.Select((row, index) => new EmailHop(row.From, row.By, row.Ip, row.Timestamp,
            index < rows.Count - 1 ? Delay(row.Date, rows[index + 1].Date) : string.Empty)).ToArray();
    }

    private static string Delay(DateTimeOffset? current, DateTimeOffset? next)
    {
        if (current is not { } currentValue || next is not { } nextValue) return string.Empty;
        var seconds = System.Math.Round((currentValue - nextValue).TotalSeconds, MidpointRounding.AwayFromZero);
        return $"{(seconds >= 0 ? "+" : string.Empty)}{seconds}s";
    }

    private static IReadOnlyList<EmailAuthResult> Auth(string[] lines)
    {
        var results = new List<EmailAuthResult>();
        foreach (var header in All(lines, "Authentication-Results"))
        foreach (var protocol in new[] { "spf", "dkim", "dmarc", "arc" })
        foreach (Match match in Regex.Matches(header, $"{protocol}=([a-z]+)([^;]*)", RegexOptions.IgnoreCase))
            results.Add(new EmailAuthResult(protocol.ToUpperInvariant(), match.Groups[1].Value.ToLowerInvariant(), Whitespace().Replace(match.Groups[2].Value.Trim(), " ")));
        return results;
    }

    private static string DetectService(string[] lines)
    {
        var received = string.Join(' ', All(lines, "Received")).ToLowerInvariant();
        var feedback = Get(lines, "Feedback-ID").ToLowerInvariant();
        var mailer = Get(lines, "X-Mailer").ToLowerInvariant();
        if (Get(lines, "X-SES-Outgoing").Length > 0 || received.Contains("amazonses.com")) return "Amazon SES";
        if (Get(lines, "X-SG-ID").Length > 0 || Get(lines, "X-Sendgrid-ID").Length > 0 || received.Contains("sendgrid.net")) return "SendGrid";
        if (feedback.Contains("mailchimp") || received.Contains("mailchimp.com")) return "Mailchimp";
        if (feedback.Contains("salesforce") || received.Contains("exacttarget.com") || received.Contains("salesforce.com")) return "Salesforce Marketing Cloud";
        if (received.Contains("sendinblue.com") || received.Contains("brevo.com")) return "Brevo (Sendinblue)";
        if (received.Contains("constantcontact.com")) return "Constant Contact";
        if (received.Contains("hubspot.com")) return "HubSpot";
        if (received.Contains("smtp.gmail.com") || received.Contains("google.com")) return "Google Workspace / Gmail";
        if (Get(lines, "X-MS-Exchange-Organization-SCL").Length > 0 || received.Contains("protection.outlook.com") || received.Contains("outlook.com")) return "Microsoft 365 / Exchange";
        if (received.Contains("zoho.com")) return "Zoho Mail";
        if (received.Contains("protonmail.ch") || received.Contains("proton.me")) return "Proton Mail";
        return mailer.Contains("apple mail") ? "Apple Mail" : string.Empty;
    }

    private static string SclLabel(string value) => int.TryParse(value, out var number) ? number switch { -1 => "Trusted / Bypass", <= 1 => "Not Spam", <= 4 => "Low Suspicion", <= 6 => "Spam", _ => "High Confidence Spam" } : string.Empty;

    [GeneratedRegex("\\r?\\n[ \\t]+")] private static partial Regex FoldedLine();
    [GeneratedRegex("<([^>]+)>")] private static partial Regex AngleEmail();
    [GeneratedRegex("\\[(\\d{1,3}(?:\\.\\d{1,3}){3})\\]")] private static partial Regex BracketIp();
    [GeneratedRegex("\\b(\\d{1,3}(?:\\.\\d{1,3}){3})\\b")] private static partial Regex BareIp();
    [GeneratedRegex(";(.+)$")] private static partial Regex ReceivedDate();
    [GeneratedRegex("from\\s+([^\\s(]+)", RegexOptions.IgnoreCase)] private static partial Regex ReceivedFrom();
    [GeneratedRegex("by\\s+([^\\s(]+)", RegexOptions.IgnoreCase)] private static partial Regex ReceivedBy();
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();
}
