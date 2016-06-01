

using EntityExtensionForORM.Auxiliary;
using SQLite.Net.Platform.Win32;


namespace EntityExtensionForORM.Tests
{
   public class UTDbContext : DbContext
    {

        public UTDbContext (DbConnect connect) : base(connect) {

            connect.TraceListener = new DebugTraceListener_OutputWindow();

            connect.CreateTable<User>();
            connect.CreateTable<UserType>();
            connect.CreateTable<UserRole>();

            CreateSchema();
        }
    }
}
