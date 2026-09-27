using Jint;
using KillerTools.Engine.Reference;
using JavaScriptEngine = Jint.Engine;

namespace KillerTools.Engine.Text;

public static class SqlFormatter
{
    private static readonly string[] Languages = ["bigquery", "db2", "hive", "mariadb", "mysql", "n1ql", "plsql", "postgresql", "redshift", "spark", "sql", "sqlite", "tsql"];
    private static readonly string[] KeywordCases = ["upper", "lower", "preserve"];
    private static readonly string[] IndentStyles = ["standard", "tabularLeft", "tabularRight"];
    private static readonly Lazy<FormatterRuntime> Runtime = new(() => new());

    public static string Format(string sql, string language = "sql", string keywordCase = "upper", string indentStyle = "standard", bool useTabs = false)
    {
        if (sql.Length > 4096) throw new ArgumentOutOfRangeException(nameof(sql));
        if (!Languages.Contains(language, StringComparer.Ordinal)) throw new ArgumentException("Unsupported SQL dialect.", nameof(language));
        if (!KeywordCases.Contains(keywordCase, StringComparer.Ordinal)) throw new ArgumentException("Unsupported keyword case.", nameof(keywordCase));
        if (!IndentStyles.Contains(indentStyle, StringComparer.Ordinal)) throw new ArgumentException("Unsupported indent style.", nameof(indentStyle));

        var output = Runtime.Value.Format(sql, language, keywordCase, indentStyle, useTabs);
        if (output.Length > 16_384) throw new InvalidDataException("Output is too large.");
        return output;
    }

    private sealed class FormatterRuntime
    {
        private readonly JavaScriptEngine _engine;
        private readonly object _sync = new();

        public FormatterRuntime()
        {
            using var stream = EmbeddedCatalog.OpenBrotliBase64("KillerTools.Engine.Data.sql-formatter-15.9.0.br64");
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            _engine = new JavaScriptEngine(options => options.TimeoutInterval(TimeSpan.FromSeconds(3)).LimitMemory(64_000_000));
            _engine.Execute(source);
        }

        public string Format(string sql, string language, string keywordCase, string indentStyle, bool useTabs)
        {
            lock (_sync)
            {
                _engine.SetValue("__sql", sql);
                _engine.SetValue("__language", language);
                _engine.SetValue("__keywordCase", keywordCase);
                _engine.SetValue("__indentStyle", indentStyle);
                _engine.SetValue("__useTabs", useTabs);
                return _engine.Evaluate("sqlFormatter.format(__sql,{language:__language,keywordCase:__keywordCase,indentStyle:__indentStyle,useTabs:__useTabs})").AsString();
            }
        }
    }
}
