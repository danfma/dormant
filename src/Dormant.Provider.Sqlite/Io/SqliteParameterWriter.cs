using System;
using System.Globalization;
using Dormant.Abstractions.Querying;
using Microsoft.Data.Sqlite;

namespace Dormant.Provider.Sqlite.Io;

/// <summary>
/// An <see cref="IParameterWriter"/> over a <see cref="SqliteParameterCollection"/>. Binds named
/// <c>@pN</c> parameters (matching <see cref="SqliteDialect"/>'s placeholders) so binding is order-independent.
/// Microsoft.Data.Sqlite maps Guid/DateTime to TEXT and <c>byte[]</c> to BLOB; the value is boxed (an inherent
/// cost of the SQLite parameter API, isolated to this adapter — not the core hot path).
/// </summary>
internal sealed class SqliteParameterWriter(SqliteParameterCollection parameters) : IParameterWriter
{
    public void Write<T>(int index, T value) =>
        parameters.AddWithValue(
            "@p" + index.ToString(CultureInfo.InvariantCulture),
            (object?)value ?? DBNull.Value
        );
}
