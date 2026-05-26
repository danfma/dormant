using Dormant.Abstractions.Querying;
using Microsoft.Data.Sqlite;

namespace Dormant.Provider.Sqlite.Io;

/// <summary>An <see cref="IFieldReader"/> over a <see cref="SqliteDataReader"/>. Typed reads dispatch to the
/// driver's <see cref="SqliteDataReader.GetFieldValue{T}(int)"/> (TEXT→Guid/DateTime affinity reads included).</summary>
internal sealed class SqliteFieldReader(SqliteDataReader reader) : IFieldReader
{
    public bool IsNull(int ordinal) => reader.IsDBNull(ordinal);

    public T GetValue<T>(int ordinal) => reader.GetFieldValue<T>(ordinal);
}
