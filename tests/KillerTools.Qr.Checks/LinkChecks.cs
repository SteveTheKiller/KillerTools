using KillerTools.Qr;

internal static class LinkChecks
{
    public static void Run()
    {
        var result = QrLink.Check("https://killertools.net/qr");
        Program.Equal(QrLinkDisposition.Allowed, result.Disposition, "an ordinary HTTPS link is allowed");
        Program.Equal("https://killertools.net/qr", result.Url?.AbsoluteUri, "the checked destination is retained");
        Warns("http://example.com/path", "PlainHttp");
        Warns("https://example.com:8443/path", "UnusualPort");
        Warns("https://192.0.2.12/path", "RawIp");
        Warns("https://[2001:db8::1]/path", "RawIp");
        Warns("https://trusted.example@evil.example/path", "UserInfo");
        Warns("https://xn--bcher-kva.example/", "InternationalHost");
        Warns("https://pаypal.example/", "MixedScripts"); // Cyrillic а among Latin letters.
        Warns("https://a.b.c.example.com/path", "DeepSubdomains");
        Warns("https://bit.ly/abc", "Shortener");
        Warns("https://tinyurl.com/abc", "Shortener");
        Warns("https://example.com/setup.EXE?download=1", "DirectDownload");
        Warns("https://example.com/package%2Emsi", "DirectDownload");
        Program.Equal(QrLinkDisposition.Allowed, QrLink.Check("https://bit.ly.example.com/page").Disposition, "a shortener lookalike is checked by its actual host");
        Program.Equal(QrLinkDisposition.Blocked, QrLink.Check("https://example.com/hidden\nline").Disposition, "control characters cannot disguise a web destination");
        Program.Equal(QrLinkDisposition.Blocked, QrLink.Check("https://[broken/").Disposition, "malformed web links cannot open");
        foreach (string payload in new[] { "javascript:alert(1)", "data:text/html,test", "file:///C:/Windows/notepad.exe", "ms-settings:privacy", "ftp://example.com/file", "custom-app:open" })
            Program.Equal(QrLinkDisposition.Blocked, QrLink.Check(payload).Disposition, payload + " cannot launch a handler");
        foreach (string payload in new[] { "A note to copy", "WIFI:S:Shop;T:WPA;P:secret;;", "mailto:help@example.com", "tel:+15551234567", "sms:+15551234567", "bitcoin:address", "BEGIN:VCARD\nVERSION:3.0\nEND:VCARD" })
            Program.Equal(QrLinkDisposition.DisplayOnly, QrLink.Check(payload).Disposition, payload + " is display and copy only");
    }

    private static void Warns(string url, string warning)
    {
        var result = QrLink.Check(url);
        Program.Equal(QrLinkDisposition.Warning, result.Disposition, url + " requires confirmation");
        Program.Equal(true, result.Warnings.Contains(warning), url + " explains " + warning);
    }
}
