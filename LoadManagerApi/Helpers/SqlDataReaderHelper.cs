using Microsoft.Data.SqlClient;

namespace LoadManagerApi.Helpers;

public static class SqlDataReaderHelper
{
    public static int GetFirstInt(SqlDataReader reader, params string[] names)
    {
        var value = GetFirstValue(reader, names);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    public static string GetFirstString(SqlDataReader reader, params string[] names)
    {
        var value = GetFirstValue(reader, names);
        return value is null ? string.Empty : Convert.ToString(value) ?? string.Empty;
    }

    public static decimal GetFirstDecimal(SqlDataReader reader, params string[] names)
    {
        var value = GetFirstValue(reader, names);
        return value is null ? 0m : Convert.ToDecimal(value);
    }

    public static bool GetFirstBool(SqlDataReader reader, params string[] names)
    {
        var value = GetFirstValue(reader, names);
        return value is not null && Convert.ToBoolean(value);
    }

    public static DateTime? GetFirstDateTime(SqlDataReader reader, params string[] names)
    {
        var value = GetFirstValue(reader, names);
        return value is null ? null : Convert.ToDateTime(value);
    }

    private static object? GetFirstValue(SqlDataReader reader, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return reader[name];
            }
        }

        return null;
    }

    private static bool HasColumn(SqlDataReader reader, string name)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
