using System.Data;
using Dapper;

namespace SpaceWay.Core.Data;

/// <summary>
/// Dapper type handlers.
/// </summary>
internal static class TypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        SqlMapper.AddTypeHandler(new GuidHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());

        _registered = true;
    }

    private sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value) => value switch
        {
            string s => Guid.Parse(s),
            Guid g => g,
            _ => throw new DataException($"Cannot read Guid from{value?.GetType().Name ?? "NULL"}"),
        };

        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString();
        }
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) => value switch
        {
            string s => DateTimeOffset.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTimeOffset d => d,
            DateTime dt => new DateTimeOffset(dt),
            _ => throw new DataException($"Cannot read date from{value?.GetType().Name ?? "NULL"}"),
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("O");
        }
    }
}
