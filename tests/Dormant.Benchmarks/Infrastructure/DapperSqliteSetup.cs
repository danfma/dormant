using System.Data;
using System.Globalization;
using Dapper;

namespace Dormant.Benchmarks.Infrastructure;

/// <summary>
/// Dapper has no built-in conversion for the way SQLite stores <see cref="Guid"/> and <see cref="decimal"/>
/// (both as TEXT — see Dormant's generated DDL: <c>"id" TEXT</c>, <c>"price" TEXT</c>). Dapper surfaces the
/// raw string and then fails the direct cast, so the canonical SQLite+Dapper fix is to register type
/// handlers. EF Core and Insight.Database perform this conversion internally; for Dapper it is explicit.
/// Registered once, globally, before any Dapper call.
/// </summary>
internal static class DapperSqliteSetup
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));
            SqlMapper.RemoveTypeMap(typeof(decimal));
            SqlMapper.RemoveTypeMap(typeof(decimal?));
            SqlMapper.AddTypeHandler(new GuidAsTextHandler());
            SqlMapper.AddTypeHandler(new DecimalAsTextHandler());
            _registered = true;
        }
    }

    private sealed class GuidAsTextHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value) => Guid.Parse((string)value);

        // Pass the Guid through so Microsoft.Data.Sqlite serializes it exactly as it stored it.
        public override void SetValue(IDbDataParameter parameter, Guid value) =>
            parameter.Value = value;
    }

    private sealed class DecimalAsTextHandler : SqlMapper.TypeHandler<decimal>
    {
        public override decimal Parse(object value) =>
            Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        public override void SetValue(IDbDataParameter parameter, decimal value) =>
            parameter.Value = value;
    }
}
