using Dormant.Abstractions.Querying;
using Npgsql;

namespace Dormant.Provider.PostgreSql.Io;

/// <summary>
/// No-boxing <see cref="IParameterWriter"/> that appends generic <see cref="NpgsqlParameter{T}"/>
/// values (via <c>TypedValue</c>) to a command's collection. Positional placeholders (<c>$1</c>, <c>$2</c>,
/// …) bind by add order, so callers write parameters in ascending index order (research §2).
/// </summary>
internal sealed class PostgreSqlParameterWriter(NpgsqlParameterCollection parameters)
    : IParameterWriter
{
    public void Write<T>(int index, T value) =>
        parameters.Add(new NpgsqlParameter<T> { TypedValue = value });
}
