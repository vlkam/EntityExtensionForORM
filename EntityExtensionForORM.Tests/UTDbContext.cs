

using EntityExtensionForORM.Auxiliary;
using SQLite.Net.Interop;


namespace EntityExtensionForORM.Tests
{

    public class UTDbConnect : DbConnect
    {
        public UTDbConnect(ISQLitePlatform platform, string path) : base(platform, path)
        {
            TraceListener = new DebugTraceListener_OutputWindow();

            CreateTable<User>();
            CreateTable<UserType>();
            CreateTable<UserRole>();

            CreateSchema();
        }
    }
}
