using System.Drawing;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SysMath = System.Math;

namespace KillerTools.Engine.Web;

public sealed record ColorConversion(
    [property: JsonPropertyName("hex")] string Hex,
    [property: JsonPropertyName("rgb")] string Rgb,
    [property: JsonPropertyName("hsl")] string Hsl,
    [property: JsonPropertyName("hwb")] string Hwb,
    [property: JsonPropertyName("lch")] string Lch,
    [property: JsonPropertyName("cmyk")] string Cmyk,
    [property: JsonPropertyName("name")] string Name);

public static partial class ColorConverter
{
    private readonly record struct Rgba(double R, double G, double B, double A = 1);
    private static readonly (string Name, Rgba Value)[] Names = Enum.GetValues<KnownColor>()
        .Select(value => (value.ToString().ToLowerInvariant(), FromColor(Color.FromKnownColor(value))))
        .Where(value => value.Item2.A > 0)
        .ToArray();

    public static ColorConversion Convert(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var color = Parse(input.Trim()) ?? throw new FormatException("Invalid color");
        var rounded = new Rgba(Round(color.R), Round(color.G), Round(color.B), Round(color.A, 3));
        var hsl = ToHsl(color);
        var hue = Round(hsl.H) % 360;
        var white = Round(SysMath.Min(color.R, SysMath.Min(color.G, color.B)) / 255 * 100);
        var black = Round(100 - SysMath.Max(color.R, SysMath.Max(color.G, color.B)) / 255 * 100);
        var cmyk = ToCmyk(color);
        var lch = ToLch(color);
        return new ColorConversion(
            Hex(rounded),
            rounded.A < 1 ? $"rgba({F(rounded.R)}, {F(rounded.G)}, {F(rounded.B)}, {F(rounded.A)})" : $"rgb({F(rounded.R)}, {F(rounded.G)}, {F(rounded.B)})",
            rounded.A < 1 ? $"hsla({F(hue)}, {F(Round(hsl.S))}%, {F(Round(hsl.L))}%, {F(rounded.A)})" : $"hsl({F(hue)}, {F(Round(hsl.S))}%, {F(Round(hsl.L))}%)",
            rounded.A < 1 ? $"hwb({F(hue)} {F(white)}% {F(black)}% / {F(rounded.A)})" : $"hwb({F(hue)} {F(white)}% {F(black)}%)",
            rounded.A < 1 ? $"lch({F(Round(lch.L, 2))}% {F(Round(lch.C, 2))} {F(Round(lch.H, 2) % 360)} / {F(rounded.A)})" : $"lch({F(Round(lch.L, 2))}% {F(Round(lch.C, 2))} {F(Round(lch.H, 2) % 360)})",
            rounded.A < 1 ? $"device-cmyk({F(cmyk.C)}% {F(cmyk.M)}% {F(cmyk.Y)}% {F(cmyk.K)}% / {F(rounded.A)})" : $"device-cmyk({F(cmyk.C)}% {F(cmyk.M)}% {F(cmyk.Y)}% {F(cmyk.K)}%)",
            ClosestName(rounded));
    }

    private static Rgba? Parse(string value)
    {
        if (HexRegex().Match(value) is { Success: true } hex) return ParseHex(hex.Groups[1].Value);
        if (RgbRegex().Match(value) is { Success: true } rgb) return new Rgba(Channel(rgb, 1, 2), Channel(rgb, 3, 4), Channel(rgb, 5, 6), Alpha(rgb, 7, 8));
        if (HslRegex().Match(value) is { Success: true } hsl) return FromHsl(Angle(hsl, 1, 2), Number(hsl, 3), Number(hsl, 4), Alpha(hsl, 5, 6));
        if (HwbRegex().Match(value) is { Success: true } hwb) return FromHwb(Angle(hwb, 1, 2), Number(hwb, 3), Number(hwb, 4), Alpha(hwb, 5, 6));
        if (LchRegex().Match(value) is { Success: true } lch) return FromLch(Number(lch, 1), Number(lch, 2), Angle(lch, 3, 4), Alpha(lch, 5, 6));
        if (CmykRegex().Match(value) is { Success: true } cmyk) return FromCmyk(Percent(cmyk, 1, 2), Percent(cmyk, 3, 4), Percent(cmyk, 5, 6), Percent(cmyk, 7, 8), Alpha(cmyk, 9, 10));
        var named = Color.FromName(value);
        return named.IsKnownColor || value.Equals("transparent", StringComparison.OrdinalIgnoreCase) ? FromColor(named) : null;
    }

    private static Rgba ParseHex(string value)
    {
        if (value.Length is 3 or 4) value = string.Concat(value.Select(c => $"{c}{c}"));
        var r = System.Convert.ToInt32(value[..2], 16); var g = System.Convert.ToInt32(value[2..4], 16); var b = System.Convert.ToInt32(value[4..6], 16);
        return new(r, g, b, value.Length == 8 ? Round(System.Convert.ToInt32(value[6..], 16) / 255d, 2) : 1);
    }

    private static Rgba FromHsl(double h, double s, double l, double a)
    {
        h = Wrap(h) / 360; s = Clamp(s) / 100; l = Clamp(l) / 100;
        double ChannelValue(double n) { var k = (n + h * 12) % 12; return l - s * SysMath.Min(l, 1 - l) * SysMath.Max(-1, SysMath.Min(k - 3, SysMath.Min(9 - k, 1))); }
        return new(255 * ChannelValue(0), 255 * ChannelValue(8), 255 * ChannelValue(4), Clamp(a, 0, 1));
    }

    private static Rgba FromHwb(double h, double w, double b, double a)
    {
        w = Clamp(w); b = Clamp(b);
        if (w + b >= 100) { var gray = w / (w + b) * 255; return new(gray, gray, gray, a); }
        var pure = FromHsl(h, 100, 50, a); var factor = (100 - w - b) / 100;
        return new(pure.R * factor + 255 * w / 100, pure.G * factor + 255 * w / 100, pure.B * factor + 255 * w / 100, a);
    }

    private static Rgba FromCmyk(double c, double m, double y, double k, double a) => new(Round(255 * (1 - c / 100) * (1 - k / 100)), Round(255 * (1 - m / 100) * (1 - k / 100)), Round(255 * (1 - y / 100) * (1 - k / 100)), a);
    private static Rgba FromLch(double l, double c, double h, double alpha) { l=Clamp(l);h=Wrap(h);var aa=c*SysMath.Cos(h*SysMath.PI/180);var bb=c*SysMath.Sin(h*SysMath.PI/180);var fy=(l+16)/116;var fx=aa/500+fy;var fz=fy-bb/200;const double d=216d/24389;const double f=24389d/27;double Cube(double v)=>SysMath.Pow(v,3)>d?SysMath.Pow(v,3):(116*v-16)/f;var x=Cube(fx)*96.422;var y=(l>8?SysMath.Pow(fy,3):l/f)*100;var z=Cube(fz)*82.521;var xx=.9555766*x-.0230393*y+.0631636*z;var yy=-.0282895*x+1.0099416*y+.0210077*z;var zz=.0122982*x-.020483*y+1.3299098*z;double Gamma(double v)=>255*(v>.0031308?1.055*SysMath.Pow(v,1/2.4)-.055:12.92*v);return new(Clamp(Gamma(.032404542*xx-.015371385*yy-.004985314*zz),0,255),Clamp(Gamma(-.00969266*xx+.018760108*yy+.00041556*zz),0,255),Clamp(Gamma(.000556434*xx-.002040259*yy+.010572252*zz),0,255),alpha); }
    private static (double H, double S, double L) ToHsl(Rgba c) { var r=c.R/255; var g=c.G/255; var b=c.B/255; var max=SysMath.Max(r,SysMath.Max(g,b)); var min=SysMath.Min(r,SysMath.Min(g,b)); var d=max-min; var l=(max+min)/2; var h=d==0?0:max==r?60*((g-b)/d%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4); if(h<0)h+=360; return(h,d==0?0:d/(1-SysMath.Abs(2*l-1))*100,l*100); }
    private static (double C,double M,double Y,double K) ToCmyk(Rgba c) { var k=1-SysMath.Max(c.R/255,SysMath.Max(c.G/255,c.B/255)); return (Round(double.IsNaN((1-c.R/255-k)/(1-k))?0:100*(1-c.R/255-k)/(1-k)),Round(double.IsNaN((1-c.G/255-k)/(1-k))?0:100*(1-c.G/255-k)/(1-k)),Round(double.IsNaN((1-c.B/255-k)/(1-k))?0:100*(1-c.B/255-k)/(1-k)),Round(100*k)); }
    private static (double L,double C,double H) ToLch(Rgba c) { double Linear(double v){v/=255;return v<.04045?v/12.92:SysMath.Pow((v+.055)/1.055,2.4);} var r=Linear(c.R);var g=Linear(c.G);var b=Linear(c.B);var x=100*(.4124564*r+.3575761*g+.1804375*b);var y=100*(.2126729*r+.7151522*g+.072175*b);var z=100*(.0193339*r+.119192*g+.9503041*b);var xx=1.0478112*x+.0228866*y-.050127*z;var yy=.0295424*x+.9904844*y-.0170491*z;var zz=-.0092345*x+.0150436*y+.7521316*z;double Lab(double v)=>v>216d/24389?SysMath.Cbrt(v):(24389d/27*v+16)/116;var fx=Lab(Clamp(xx,0,96.422)/96.422);var fy=Lab(Clamp(yy,0,100)/100);var fz=Lab(Clamp(zz,0,82.521)/82.521);var l=116*fy-16;var aa=Round(500*(fx-fy),3);var bb=Round(200*(fy-fz),3);var h=SysMath.Atan2(bb,aa)/SysMath.PI*180;if(h<0)h+=360;return(l,SysMath.Sqrt(aa*aa+bb*bb),h); }

    private static string ClosestName(Rgba c) { if(c.A==0&&c.R==0&&c.G==0&&c.B==0)return "transparent"; var exact=Names.LastOrDefault(x=>x.Value.R==c.R&&x.Value.G==c.G&&x.Value.B==c.B); if(exact.Name is not null)return exact.Name; return Names.MinBy(x=>SysMath.Pow(c.R-x.Value.R,2)+SysMath.Pow(c.G-x.Value.G,2)+SysMath.Pow(c.B-x.Value.B,2)).Name; }
    private static Rgba FromColor(Color c)=>new(c.R,c.G,c.B,c.A/255d);
    private static string Hex(Rgba c)=>$"#{(int)c.R:x2}{(int)c.G:x2}{(int)c.B:x2}"+(c.A<1?$"{(int)Round(255*c.A):x2}":"");
    private static double Channel(Match m,int n,int percent)=>Clamp(Number(m,n)/(m.Groups[percent].Success?100d/255:1),0,255);
    private static double Alpha(Match m,int n,int percent)=>m.Groups[n].Success?Clamp(Number(m,n)/(m.Groups[percent].Success?100:1),0,1):1;
    private static double Percent(Match m,int n,int percent)=>Clamp(Number(m,n)*(m.Groups[percent].Success?1:100));
    private static double Angle(Match m,int n,int unit)=>Number(m,n)*(m.Groups[unit].Value.ToLowerInvariant() switch{"grad"=>.9,"turn"=>360,"rad"=>360/(2*SysMath.PI),_=>1});
    private static double Number(Match m,int n)=>double.Parse(m.Groups[n].Value,CultureInfo.InvariantCulture);
    private static double Round(double v,int digits=0)=>SysMath.Round(v,digits,MidpointRounding.AwayFromZero)+0;
    private static double Clamp(double v,double min=0,double max=100)=>SysMath.Max(min,SysMath.Min(max,v));
    private static double Wrap(double v)=>(v%360+360)%360;
    private static string F(double v)=>v.ToString("0.###",CultureInfo.InvariantCulture);
    private const string Num=@"([+-]?(?:\d*\.\d+|\d+))";
    [GeneratedRegex(@"^#([0-9a-f]{3,8})$",RegexOptions.IgnoreCase)] private static partial Regex HexRegex();
    [GeneratedRegex(@"^rgba?\(\s*"+Num+@"(%)?[, ]+"+Num+@"(%)?[, ]+"+Num+@"(%)?(?:\s*[,/]\s*"+Num+@"(%)?)?\s*\)$",RegexOptions.IgnoreCase)] private static partial Regex RgbRegex();
    [GeneratedRegex(@"^hsla?\(\s*"+Num+@"(deg|rad|grad|turn)?[, ]+"+Num+@"%[, ]+"+Num+@"%(?:\s*[,/]\s*"+Num+@"(%)?)?\s*\)$",RegexOptions.IgnoreCase)] private static partial Regex HslRegex();
    [GeneratedRegex(@"^hwb\(\s*"+Num+@"(deg|rad|grad|turn)?\s+"+Num+@"%\s+"+Num+@"%(?:\s*/\s*"+Num+@"(%)?)?\s*\)$",RegexOptions.IgnoreCase)] private static partial Regex HwbRegex();
    [GeneratedRegex(@"^lch\(\s*"+Num+@"%\s+"+Num+@"\s+"+Num+@"(deg|rad|grad|turn)?(?:\s*/\s*"+Num+@"(%)?)?\s*\)$",RegexOptions.IgnoreCase)] private static partial Regex LchRegex();
    [GeneratedRegex(@"^device-cmyk\(\s*"+Num+@"(%)?\s+"+Num+@"(%)?\s+"+Num+@"(%)?\s+"+Num+@"(%)?(?:\s*/\s*"+Num+@"(%)?)?\s*\)$",RegexOptions.IgnoreCase)] private static partial Regex CmykRegex();
}
