using Dormant.Abstractions.Providers;

namespace Dormant.Provider.PostgreSql;

/// <summary>PostgreSQL <see cref="ISqlDialect"/>: double-quoted identifiers, positional <c>$n</c> placeholders.</summary>
internal sealed class PostgreSqlDialect : ISqlDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    public string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    public string Placeholder(int index) =>
        "$" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool Supports(string providerScope) =>
        string.Equals(providerScope, "postgres", System.StringComparison.Ordinal);
}
