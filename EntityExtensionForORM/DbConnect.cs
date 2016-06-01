
using SQLite.Net;
using SQLite.Net.Interop;

namespace EntityExtensionForORM
{
    public class DbConnect : SQLiteConnection
    {
        public DbConnect(ISQLitePlatform platform,string path) : base (platform,path)
        {
            ExtraTypeMappings.Add(typeof(UUID), "blob");
        }
    }
}
