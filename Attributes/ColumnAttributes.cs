using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReflectiveSql.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public string Type { get; }
        public bool IsNullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; } = false;
        public bool IsAutoIncrement { get; set; } = false;

        public ColumnAttribute(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }
}
