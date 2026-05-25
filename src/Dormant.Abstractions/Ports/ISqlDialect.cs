namespace Dormant.Abstractions.Ports;

/// <summary>Provider SQL dialect: identifier quoting, placeholders, and capability checks.</summary>
public interface ISqlDialect
{
    /// <summary>Quotes an identifier (table/column) for this provider.</summary>
    /// <param name="name">The unquoted identifier.</param>
    /// <returns>The quoted identifier.</returns>
    string QuoteIdentifier(string name);

    /// <summary>Renders the positional placeholder for the given one-based index (e.g. <c>$1</c>).</summary>
    /// <param name="index">One-based parameter index.</param>
    /// <returns>The placeholder text.</returns>
    string Placeholder(int index);

    /// <summary>Returns whether this provider supports the given native provider scope (spec FR-042).</summary>
    /// <param name="providerScope">The provider scope name (e.g. <c>postgres</c>).</param>
    /// <returns><see langword="true"/> if supported.</returns>
    bool Supports(string providerScope);
}