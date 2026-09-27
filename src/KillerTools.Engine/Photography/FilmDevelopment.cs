using KillerTools.Engine.Reference;

namespace KillerTools.Engine.Photography;

public sealed record FilmDeveloperDilution(string Label, int DevParts, int WaterParts);
public sealed record FilmDeveloper(string Id, string Name, FilmDeveloperDilution[] Dilutions, int DefaultDilution);
public sealed record DevelopmentFilm(string Name, int IsoBase, Dictionary<string, double?> Times, double? BaseTemp, string? Process);
internal sealed record FilmDevelopmentCatalog(FilmDeveloper[] Developers, DevelopmentFilm[] FilmStocks);
public readonly record struct FilmDevelopmentResult(double BaseSeconds, double TemperatureAdjustedSeconds, double FinalSeconds, string DisplayTime, string Dilution, double DevMl, double WaterMl, string Process);

public static class FilmDevelopment
{
    private static readonly Lazy<FilmDevelopmentCatalog> Options = new(() =>
        EmbeddedCatalog.DeserializeBrotliBase64<FilmDevelopmentCatalog>("KillerTools.Engine.Data.film-development.br64"));

    public static IReadOnlyList<FilmDeveloper> Developers => Options.Value.Developers;
    public static IReadOnlyList<DevelopmentFilm> Films => Options.Value.FilmStocks;

    public static FilmDevelopmentResult Calculate(string filmName, string developerId, int? dilutionIndex = null, double? baseSeconds = null, double? temperatureC = null, double pushPullStops = 0, double tankMl = 500)
    {
        var film = Films.FirstOrDefault(item => item.Name == filmName) ?? throw new KeyNotFoundException("Unknown film or developer");
        var developer = Developers.FirstOrDefault(item => item.Id == developerId) ?? throw new KeyNotFoundException("Unknown film or developer");
        var index = dilutionIndex ?? developer.DefaultDilution;
        if (index < 0 || index >= developer.Dilutions.Length) throw new ArgumentOutOfRangeException(nameof(dilutionIndex), "Invalid dilution index");
        var dilution = developer.Dilutions[index];
        var baseTime = baseSeconds ?? film.Times.GetValueOrDefault(developerId) ?? throw new InvalidOperationException("A base development time is required for this combination");
        var baseTemperature = film.BaseTemp ?? 20;
        var temperatureAdjusted = baseTime * System.Math.Pow(2, (baseTemperature - (temperatureC ?? baseTemperature)) / 10);
        var final = temperatureAdjusted * System.Math.Pow(1.4, pushPullStops);
        var developerMl = dilution.WaterParts == 0
            ? tankMl
            : JsRound(tankMl * dilution.DevParts / (dilution.DevParts + dilution.WaterParts));
        return new(baseTime, temperatureAdjusted, final, FormatTime(final), dilution.Label, developerMl, dilution.WaterParts == 0 ? 0 : tankMl - developerMl, film.Process ?? "bw");
    }

    private static string FormatTime(double seconds)
    {
        var minutes = (int)System.Math.Floor(seconds / 60);
        var remaining = JsRound(seconds % 60);
        if (minutes == 0) return $"{remaining}s";
        return remaining == 0 ? $"{minutes}m" : $"{minutes}m {remaining}s";
    }

    private static int JsRound(double value) => (int)System.Math.Floor(value + 0.5);
}
