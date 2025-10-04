using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReflectiveSql.Core
{
    using SQLitePCL;

    public static class SqliteProviderInitializer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new();

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
                Batteries_V2.Init();
                _initialized = true;
            }
        }
    }
    
}
