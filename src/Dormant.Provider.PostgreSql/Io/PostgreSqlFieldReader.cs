using Dormant.Abstractions.Querying;
using Npgsql;

namespace Dormant.Provider.PostgreSql.Io;

/// <summary>No-boxing <see cref="IFieldReader"/> over an <see cref="NpgsqlDataReader"/> (research §2).</summary>
internal sealed class PostgreSqlFieldReader(NpgsqlDataReader reader) : IFieldReader
{
    public bool IsNull(int ordinal) => reader.IsDBNull(ordinal);

    public T GetValue<T>(int ordinal) => reader.GetFieldValue<T>(ordinal);
}
