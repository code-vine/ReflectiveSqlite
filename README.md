# 🧱 SQLite Reflection ORM for .NET

A lightweight, attribute-driven SQLite ORM built on `Microsoft.Data.Sqlite`.

---

## 🚀 Getting Started

**Step 1: Build the Library**

- Compile the source code into `ReflectiveSql.dll`
- Reference it in your project

**Step 2: Install Required Packages**

Add these to your `.csproj` file:

```xml
<PackageReference Include="Microsoft.Data.Sqlite.Core" Version="9.0.9" />
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.2" />
```

Or via CLI:
```bash
dotnet add package Microsoft.Data.Sqlite.Core --version 9.0.9
dotnet add package SQLitePCLRaw.bundle_e_sqlite3 --version 3.0.2
```

**Step 3: 🧩 Define Your Data Models**

Use ReflectiveSql.Attributes to describe your schema:
```c#
[Table("People")]
public class Person {
    [Column("Id", "INTEGER", IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [Column("FirstName", "TEXT")]
    public string FirstName { get; set; }

    [Column("LastName", "TEXT")]
    public string LastName { get; set; }

    [Column("Gender", "INTEGER")]
    public Gender Gender { get; set; }
}

public enum Gender {
    Unknown = 0,
    Male = 1,
    Female = 2
}
```

**Step 4: 🛠️ Build and Initialize the Database**
```c#
var builder = new SqliteDbBuilder()
    .WithFile("Data Source=game.db")
    .WithSchemaFromAssembly(Assembly.GetExecutingAssembly())
    .WithSeed(new[] {
        new Person { FirstName = "Milo", LastName = "Muggins", Gender = Gender.Male }
    });

var connection = builder.Build();
```

**🧪 Core Operations:**

🔍 Query All
```c#
var people = SqlMapper.QueryAll<Person>(connection);
```

🔍 Query by ID
```c#
var person = SqlMapper.QueryById<Person>(connection, 1);
```

🔍 Query with Filters
```c#
var results = SqlMapper.QueryWhere<Person>(connection, new Dictionary<string, object> {
    { "Gender", Gender.Female }
});
```

➕ Insert
```c#
var newPerson = new Person {
    FirstName = "Luna",
    LastName = "Lark",
    Gender = Gender.Female
};

SqlMapper.Insert(connection, newPerson);
```

❌ Delete
```c#
SqlMapper.Delete<Person>(connection, newPerson);
//or
SqlMapper.DeleteById<Person>(connection, newPerson.Id);
```


🧠 Features:
- ✅ Auto-incremented primary keys with ID binding
- ✅ Enum and nullable enum conversion
- ✅ Foreign key support with enforcement
- ✅ Attribute-driven schema generation
- ✅ Conditional seeding
- ✅ Modular builder pattern




