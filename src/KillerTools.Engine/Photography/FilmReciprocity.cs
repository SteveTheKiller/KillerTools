using System.Reflection;
using System.Text.Json;

namespace KillerTools.Engine.Photography;

public sealed record FilmStock(string Id, string Name, string Manufacturer, string Type, int Iso, double Exponent, double? NoFailureUpTo, bool ColorShift, string SourceReliability, string? Notes);
public readonly record struct FilmStockSummary(string Id, string Name, string Manufacturer, int Iso, string Type);
public readonly record struct ReciprocityResult(double AdjustedSeconds, double ExtraStops, bool NoFailure);

public static class FilmReciprocity
{
    private static readonly Lazy<FilmStock[]> Stocks = new(Load);

    public static IReadOnlyList<FilmStockSummary> List() =>
        Stocks.Value.Select(stock => new FilmStockSummary(stock.Id, stock.Name, stock.Manufacturer, stock.Iso, stock.Type)).ToArray();

    public static ReciprocityResult Calculate(string filmStockId, double meteredSeconds)
    {
        var stock = Stocks.Value.FirstOrDefault(item => item.Id == filmStockId) ?? throw new KeyNotFoundException("Unknown film stock");
        var noFailure = stock.NoFailureUpTo is not null && meteredSeconds <= stock.NoFailureUpTo;
        var adjustedSeconds = noFailure ? meteredSeconds : System.Math.Pow(meteredSeconds, stock.Exponent);
        var extraStops = noFailure ? 0 : System.Math.Log2(adjustedSeconds / meteredSeconds);
        return new(adjustedSeconds, extraStops, noFailure);
    }

    private static FilmStock[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KillerTools.Engine.Data.film-stocks.json")
            ?? throw new InvalidOperationException("The film stock catalog is missing.");
        return JsonSerializer.Deserialize<FilmStock[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The film stock catalog is invalid.");
    }
}
