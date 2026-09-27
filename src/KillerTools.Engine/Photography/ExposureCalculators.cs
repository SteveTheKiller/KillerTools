namespace KillerTools.Engine.Photography;

public readonly record struct DepthOfFieldResult(double HyperfocalMeters, double NearMeters, double? FarMeters, double? DepthOfFieldMeters);

public static class ExposureCalculators
{
    public static double WithNeutralDensity(double baseSeconds, double stops) => baseSeconds * System.Math.Pow(2, stops);

    public static double EquivalentShutter(double shutterSeconds, double originalAperture, double targetAperture) =>
        shutterSeconds * System.Math.Pow(targetAperture, 2) / System.Math.Pow(originalAperture, 2);

    public static DepthOfFieldResult DepthOfField(double focalLengthMm, double aperture, double focusDistance, string focusUnit, double circleOfConfusionMm)
    {
        var distance = focusDistance * (focusUnit == "ft" ? 0.3048 : 1) * 1000;
        if (distance <= focalLengthMm) throw new ArgumentOutOfRangeException(nameof(focusDistance));
        var hyperfocal = System.Math.Pow(focalLengthMm, 2) / (aperture * circleOfConfusionMm) + focalLengthMm;
        var near = distance * (hyperfocal - focalLengthMm) / (hyperfocal + distance - 2 * focalLengthMm);
        double? far = distance >= hyperfocal ? null : distance * (hyperfocal - focalLengthMm) / (hyperfocal - distance);
        return new(hyperfocal / 1000, near / 1000, far / 1000, far is null ? null : (far - near) / 1000);
    }
}
