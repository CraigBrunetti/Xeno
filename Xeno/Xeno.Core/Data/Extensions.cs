using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Xeno.Data
{
    public static class Extensions
    {
        public static void AddParameter(this IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }


        public static bool SafeGetBoolean(this DbDataReader reader, string columnName, bool defaultValue)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetBoolean(ordinal);
        }


        public static DateTime? GetDateTimeNullable(this DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;
            else
                return reader.GetDateTime(ordinal);
        }


        public static int SafeGetInt(this DbDataReader reader, string columnName, int defaultValue)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetInt32(ordinal);
        }


        public static int? GetIntNullable(this DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;
            else
                return reader.GetInt32(ordinal);
        }


        public static long SafeGetLong(this DbDataReader reader, string columnName, long defaultValue)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetInt64(ordinal);
        }


        public static long? GetLongNullable(this DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;
            else
                return reader.GetInt64(ordinal);
        }


        public static decimal SafeGetDecimal(this DbDataReader reader, string columnName, decimal defaultValue = 0)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetDecimal(ordinal);
        }


        public static decimal? GetDecimalNullable(this DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;
            else
                return reader.GetDecimal(ordinal);
        }


        public static string SafeGetString(this DbDataReader reader, string columnName, string defaultValue = null)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetString(ordinal);
        }


        public static Guid? SafeGetGuid(this DbDataReader reader, string columnName, Guid? defaultValue = null)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return defaultValue;
            else
                return reader.GetGuid(ordinal);
        }


        public static Guid? GetGuidNullable(this DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;
            else
                return reader.GetGuid(ordinal);
        }


        public static async Task<DbDataReader> ExecuteStoredProcedureReaderAsync(this DbCommand command, string procedureName, DbParameter[] parameters)
        {
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddRange(parameters);

            return await command.ExecuteReaderAsync();
        }

    }
}
