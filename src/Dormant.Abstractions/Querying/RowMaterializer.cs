namespace Dormant.Abstractions.Querying;

/// <summary>Materializes one result row from an <see cref="IFieldReader"/>; emitted by the generator.</summary>
/// <typeparam name="TRow">The row/result type.</typeparam>
/// <param name="reader">The current-row reader.</param>
/// <returns>The materialized row.</returns>
public delegate TRow RowMaterializer<out TRow>(IFieldReader reader);