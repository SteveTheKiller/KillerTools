namespace KillerTools.Engine.Math;

public static class Temperatures
{
    private static readonly string[] Scales = ["kelvin", "celsius", "fahrenheit", "rankine", "delisle", "newton", "reaumur", "romer"];

    public static IReadOnlyDictionary<string, double> Convert(double value, string scale)
    {
        var kelvin = ToKelvin(value, scale);
        return Scales.ToDictionary(target => target, target => target == scale ? value : System.Math.Floor(FromKelvin(kelvin, target) * 100) / 100);
    }

    private static double ToKelvin(double value, string scale) => scale switch
    {
        "kelvin" => value, "celsius" => value + 273.15, "fahrenheit" => (value + 459.67) * (5d / 9),
        "rankine" => value * (5d / 9), "delisle" => 373.15 - (2d / 3) * value,
        "newton" => value * (100d / 33) + 273.15, "reaumur" => value * (5d / 4) + 273.15,
        "romer" => (value - 7.5) * (40d / 21) + 273.15, _ => throw new ArgumentOutOfRangeException(nameof(scale)),
    };

    private static double FromKelvin(double value, string scale) => scale switch
    {
        "kelvin" => value, "celsius" => value - 273.15, "fahrenheit" => value * (9d / 5) - 459.67,
        "rankine" => value * (9d / 5), "delisle" => (3d / 2) * (373.15 - value),
        "newton" => (value - 273.15) * (33d / 100), "reaumur" => (value - 273.15) * (4d / 5),
        "romer" => (value - 273.15) * (21d / 40) + 7.5, _ => throw new ArgumentOutOfRangeException(nameof(scale)),
    };
}
