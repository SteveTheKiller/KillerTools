using CronExpressionDescriptor;

namespace KillerTools.Engine.SystemTools;

public static class CronDescriptions
{
    public static string Describe(string expression, bool use24HourTimeFormat = true, bool dayOfWeekStartIndexZero = true)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 128) throw new FormatException("Invalid cron expression.");
        try
        {
            return ExpressionDescriptor.GetDescription(expression.Trim(), new Options
            {
                Verbose = true,
                DayOfWeekStartIndexZero = dayOfWeekStartIndexZero,
                Use24HourTimeFormat = use24HourTimeFormat,
                ThrowExceptionOnParseError = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new FormatException("Invalid cron expression.", exception);
        }
    }
}
