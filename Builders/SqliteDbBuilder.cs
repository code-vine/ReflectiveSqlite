using Microsoft.Data.Sqlite;
using ReflectiveSql.Attributes;
using ReflectiveSql.Core;
using ReflectiveSql.Mappers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReflectiveSql;

public class SqliteDbBuilder
{
    private string? _filePath;
    private bool _useMemory;
    private Assembly? _schemaAssembly;
    private Action<SqliteConnection>? _seedAction;
    private bool _enableForeignKeys = true;

    public SqliteDbBuilder WithForeignKeysEnabled(bool enabled = true)
    {
        _enableForeignKeys = enabled;
        return this;
    }

    public SqliteDbBuilder WithFile(string filePath)
    {
        _filePath = filePath;
        _useMemory = false;
        return this;
    }

    public SqliteDbBuilder UseInMemory()
    {
        _useMemory = true;
        return this;
    }

    public SqliteDbBuilder WithSchemaFromAssembly(Assembly assembly)
    {
        _schemaAssembly = assembly;
        return this;
    }

    public SqliteDbBuilder WithSeed<T>(IEnumerable<T> items) where T : class
    {
        _seedAction = conn => {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null) throw new InvalidOperationException($"Missing [Table] attribute on {type.Name}");

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM {tableAttr.Name};";
            var count = Convert.ToInt32(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                foreach (var item in items)
                {
                    SqlMapper.Insert(conn, item);
                }
            }
        };
        return this;
    }

    public SqliteConnection Build()
    {
        SqliteProviderInitializer.EnsureInitialized();

        var connectionString = _useMemory ? "Data Source=:memory:" : $"Data Source={_filePath}";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        if (_enableForeignKeys)
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        if (_schemaAssembly != null)
        {
            foreach (var type in _schemaAssembly.GetTypes())
            {
                var sql = GenerateCreateTableSQL(type);
                if (sql != null)
                {
                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        _seedAction?.Invoke(connection);
        return connection;
    }

    private static string? GenerateCreateTableSQL(Type type)
    {
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr == null) return null;

        var columns = new List<string>();
        var foreignKeys = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;

            string columnDef = $"{colAttr.Name} {colAttr.Type}";
            if (!colAttr.IsNullable) columnDef += " NOT NULL";
            if (colAttr.IsPrimaryKey) columnDef += " PRIMARY KEY";
            if (colAttr.IsAutoIncrement)
            {
                if (!colAttr.IsPrimaryKey || colAttr.Type.ToUpper() != "INTEGER")
                    throw new InvalidOperationException($"AUTOINCREMENT requires INTEGER PRIMARY KEY on {prop.Name}");
                columnDef += " AUTOINCREMENT";
            }

            columns.Add(columnDef);

            var fkAttr = prop.GetCustomAttribute<ForeignKeyAttribute>();
            if (fkAttr != null)
            {
                foreignKeys.Add($"FOREIGN KEY({colAttr.Name}) REFERENCES {fkAttr.ReferencedTable}({fkAttr.ReferencedColumn})");
            }
        }

        string columnsSql = string.Join(", ", columns.Concat(foreignKeys));
        return $"CREATE TABLE IF NOT EXISTS {tableAttr.Name} ({columnsSql});";
    }
}