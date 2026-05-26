using System.Globalization;
using Dormant.Abstractions.Providers;

namespace Dormant.Provider.Sqlite;

/// <summary>SQLite <see cref="ISqlDialect"/>: double-quoted identifiers, named <c>@pN</c> placeholders.</summary>
internal sealed class SqliteDialect : ISqlDialect
{
    public static readonly SqliteDialect Instance = new();

    public DialectId Id => DialectId.Sqlite;

    public string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    public string Placeholder(int index) => "@p" + index.ToString(CultureInfo.InvariantCulture);

    public bool Supports(string providerScope) =>
        string.Equals(providerScope, "sqlite", System.StringComparison.Ordinal);
}
