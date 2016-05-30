

using EntityExtensionForORM.Auxiliary;
using SQLite.Net.Platform.Win32;


namespace EntityExtensionForORM.Tests
{
   public class UTDbContext : DbContext
    {

        public UTDbContext (string path) : base(new SQLitePlatformWin32(),path) {

            //connect = GetConnectionForTestOnly();
            connect.TraceListener = new DebugTraceListener_OutputWindow();
            connect.CreateTable<User>();
            connect.CreateTable<UserType>();
            connect.CreateTable<UserRole>();

            CreateSchema();
        }
    }
}
