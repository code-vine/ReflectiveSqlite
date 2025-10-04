using Microsoft.Data.Sqlite;
using ReflectiveSql.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace ReflectiveSql.Mappers
{
    public static class SqlMapper
    {
        public static void Insert<T>(SqliteConnection connection, T obj) where T : class
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException("Missing [Table] attribute");

            var props = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToList();

            var insertable = props
                .Where(x => !(x.Attr.IsPrimaryKey && x.Attr.IsAutoIncrement))
                .ToList();

            var columnNames = insertable.Select(x => x.Attr.Name).ToList();
            var paramNames = columnNames.Select(n => $"@{n}").ToList();

            string sql = $"INSERT INTO {tableAttr.Name} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)});";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            foreach (var x in insertable)
            {
                var value = x.Prop.GetValue(obj) ?? DBNull.Value;
                cmd.Parameters.AddWithValue($"@{x.Attr.Name}", value);
            }

            cmd.ExecuteNonQuery();

            // Bind last inserted ID back to object
            var pk = props.FirstOrDefault(x => x.Attr.IsPrimaryKey && x.Attr.IsAutoIncrement);
            if (pk != null && pk.Prop.CanWrite)
            {
                cmd.CommandText = "SELECT last_insert_rowid();";
                var lastId = cmd.ExecuteScalar();
                pk.Prop.SetValue(obj, Convert.ChangeType(lastId, pk.Prop.PropertyType));
            }
        }
        private static object? ConvertValue(object value, Type targetType)
        {
            if (value == DBNull.Value) return null;

            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, value);
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null && nullableType.IsEnum)
            {
                return Enum.ToObject(nullableType, value);
            }

            return Convert.ChangeType(value, targetType);
        }

        public static T? QueryById<T>(SqliteConnection connection, object id) where T : class, new()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new InvalidOperationException($"Missing [Table] attribute on {type.Name}");

            var props = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToList();

            var pkProp = props.FirstOrDefault(x => x.Attr.IsPrimaryKey);
            if (pkProp == null)
                throw new InvalidOperationException($"No primary key defined on {type.Name}");

            var columnMap = props.ToDictionary(x => x.Attr.Name, x => x.Prop);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {tableAttr.Name} WHERE {pkProp.Attr.Name} = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var obj = new T();
            foreach (var col in columnMap.Keys)
            {
                var prop = columnMap[col];
                var value = reader[col];
                if (value != DBNull.Value)
                {
                    object converted;
                    var targetType = prop.PropertyType;

                    if (targetType.IsEnum)
                    {
                        converted = Enum.ToObject(targetType, value);
                    }
                    else if (Nullable.GetUnderlyingType(targetType)?.IsEnum == true)
                    {
                        var enumType = Nullable.GetUnderlyingType(targetType)!;
                        converted = Enum.ToObject(enumType, value);
                    }
                    else
                    {
                        converted = Convert.ChangeType(value, targetType);
                    }

                    prop.SetValue(obj, converted);
                }
            }

            return obj;
        }

        public static List<T> QueryAll<T>(SqliteConnection connection) where T : class, new()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException("Missing [Table] attribute");

            var props = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToList();

            string sql = $"SELECT {string.Join(", ", props.Select(x => x.Attr.Name))} FROM {tableAttr.Name};";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            var results = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var instance = new T();
                for (int i = 0; i < props.Count; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    var converted = ConvertValue(value!, props[i].Prop.PropertyType);
                    props[i].Prop.SetValue(instance, converted);
                }
                results.Add(instance);
            }

            return results;
        }

        public static List<T> QueryWhere<T>(SqliteConnection connection, Dictionary<string, object> filters) where T : class, new()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException($"Missing [Table] attribute on {type.Name}");

            var props = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToList();

            var columnMap = props.ToDictionary(x => x.Attr.Name, x => x.Prop);

            var whereClauses = filters.Keys.Select(k => $"{k} = @{k}").ToList();
            var sql = $"SELECT * FROM {tableAttr.Name} WHERE {string.Join(" AND ", whereClauses)};";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            foreach (var kvp in filters)
            {
                cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
            }

            using var reader = cmd.ExecuteReader();
            var results = new List<T>();

            while (reader.Read())
            {
                var obj = new T();
                foreach (var col in columnMap.Keys)
                {
                    var prop = columnMap[col];
                    var value = reader[col];
                    var converted = ConvertValue(value, prop.PropertyType);
                    prop.SetValue(obj, converted);
                }
                results.Add(obj);
            }

            return results;
        }

        public static void Update<T>(SqliteConnection connection, T obj) where T : class
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException("Missing [Table] attribute");

            var props = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .Where(x => x.Attr != null)
                .ToList();

            var pk = props.FirstOrDefault(x => x.Attr.IsPrimaryKey);
            if (pk == null) throw new InvalidOperationException("No primary key defined");

            var setClauses = props
                .Where(x => !x.Attr.IsPrimaryKey)
                .Select(x => $"{x.Attr.Name} = @{x.Attr.Name}")
                .ToList();

            string sql = $"UPDATE {tableAttr.Name} SET {string.Join(", ", setClauses)} WHERE {pk.Attr.Name} = @{pk.Attr.Name};";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            foreach (var x in props)
            {
                var value = x.Prop.GetValue(obj) ?? DBNull.Value;
                cmd.Parameters.AddWithValue($"@{x.Attr.Name}", value);
            }

            cmd.ExecuteNonQuery();
        }

        public static void Delete<T>(SqliteConnection connection, T obj) where T : class
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException($"Missing [Table] attribute on {type.Name}");

            var pkProp = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .FirstOrDefault(x => x.Attr != null && x.Attr.IsPrimaryKey);

            if (pkProp == null) throw new InvalidOperationException($"No primary key defined on {type.Name}");
            var pkValue = pkProp.Prop.GetValue(obj);
            if (pkValue == null) throw new InvalidOperationException($"Primary key value is null for {type.Name}");

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {tableAttr.Name} WHERE {pkProp.Attr.Name} = @pk;";
            cmd.Parameters.AddWithValue("@pk", pkValue);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteById<T>(SqliteConnection connection, object id) where T : class
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new InvalidOperationException($"Missing [Table] attribute on {type.Name}");

            var pkProp = type.GetProperties()
                .Select(p => new {
                    Prop = p,
                    Attr = p.GetCustomAttribute<ColumnAttribute>()
                })
                .FirstOrDefault(x => x.Attr != null && x.Attr.IsPrimaryKey);

            if (pkProp == null)
                throw new InvalidOperationException($"No primary key defined on {type.Name}");

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {tableAttr.Name} WHERE {pkProp.Attr.Name} = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
