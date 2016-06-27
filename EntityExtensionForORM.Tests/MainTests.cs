

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SQLite.Net;
using SQLite.Net.Platform.Win32;
using System;
using System.IO;
using System.Linq;

namespace EntityExtensionForORM.Tests
{
    [TestClass]
    public class MainTests
    {

        private string PathToDb(string basename)
        {
            return Path.Combine(Environment.CurrentDirectory, basename);
        }

        private DbContext ConnectToDb(string basename) {
            UTDbConnect con = new  UTDbConnect(new SQLitePlatformWin32(), PathToDb(basename));
            return new DbContext(con);
        }
        

        private DbContext RecreateDB(string basename) {
            string path = PathToDb(basename);
            if (File.Exists(path)) File.Delete(path);
            return ConnectToDb(basename);
        }


        [TestMethod]
        public void DBConnectTestMethod()
        {
            DbContext db = RecreateDB("DBConnectTest.db");

            SQLiteConnection connect = db.GetConnectionForTestOnly();
            connect.Insert(new User { Name = "Test user" });

            // add a user 
            User us = connect.Table<User>().FirstOrDefault();
            Assert.IsTrue(us.Name == "Test user");

            // delete a user
            connect.Delete(us);
            us = connect.Table<User>().FirstOrDefault();
            Assert.IsNull(us);

            db.Close();
        }

        [TestMethod]
        public void AddToContext()
        {
            DbContext db = RecreateDB("AddToContext.db");

            UUID user_id = new UUID();

            // Add new user to context
            User us = new User { id = user_id,Name = "Really cool user" };
            //us.AddToDBContext(db);
            db.AddNewItemToDBContext(us);

            Entity entity;

            // Is user added to context ? 
            entity = null;
            db.Entities.TryGetValue(user_id, out entity);
            Assert.IsTrue(entity != null);
            Assert.IsTrue(entity.State == Entity.EntityState.Added);

            // Makes change in user
            UUID usertype_id = new UUID();
            UserType type = new UserType { id = usertype_id};
            us.UserType = type;

            // Is usertype added to context ?
            entity = null;
            db.Entities.TryGetValue(usertype_id, out entity);
            Assert.IsTrue(entity != null);
            Assert.IsTrue(entity.State == Entity.EntityState.Added);

            // Is user modified ?
            //entity = null;
            //db.entities.TryGetValue(user_id, out entity);
            //Assert.IsTrue(entity != null);
            //Assert.IsTrue(entity.State == Entity.EntityState.Modified);

            db.Close();
        }

        [TestMethod]
        public void NotifyPropertyChangedTest()
        {
            DbContext db = RecreateDB("NotifyPropertyChanged.db");

            User user = new User();
            db.AddNewItemToDBContext(user);
 
            bool isPropertyChanged = false;

            user.PropertyChanged += (x,y) => isPropertyChanged = true;
            user.Name = "Hello";

            db.Close();
            Assert.IsTrue(isPropertyChanged);
        }

        [TestMethod]
        public void SaveChanges_and_LazyLoading()
        {

            DbContext db;
            db = RecreateDB("SaveChanges_and_LazyLoading.db");

            User user = new User { Name = "Peter" };
            db.AddNewItemToDBContext(user);

            user.UserType = new UserType { Type = "Cool" };

            db.SaveChanges();
            db.Close();

            user.UserType = null;
            user = null;

            db = ConnectToDb("SaveChanges_and_LazyLoading.db");
            user = db.First<User>();

            Assert.IsTrue(user.UserType.Type == "Cool");

        }

        [TestMethod]
        public void CollectionTest()
        {
            // Add to DB
            DbContext db;
            db = RecreateDB("CollectionTest.db");

            User user = new User { Name = "Alex" };
            db.AddNewItemToDBContext(user);
            user.UserType = new UserType {Type = "Advanced"};

            bool isCollectionChanged = false;
            user.UserRoles.CollectionChanged += (x, y) => isCollectionChanged = true;
            user.UserRoles.Add(new UserRole {Name = "Admin" });
            user.UserRoles.Add(new UserRole { Name = "Customer" });

            Assert.IsTrue(isCollectionChanged);
            Assert.IsTrue(db.Entities.Count == 4);
            Assert.IsTrue(db.Entities[user.id].State == Entity.EntityState.Added);
            Assert.IsTrue(db.Entities[user.UserRoles[0].id].State == Entity.EntityState.Added);
            Assert.IsTrue(!db.Entities.Any(x => x.Value.State != Entity.EntityState.Added));

            db.SaveChanges();
            Assert.IsTrue(db.Entities[user.id].State == Entity.EntityState.Unchanged);
            Assert.IsTrue(db.Entities[user.UserRoles[0].id].State == Entity.EntityState.Unchanged);

            db.Close();

            // Lazy loading
            UUID userid = user.id;
            user = null;
            db = ConnectToDb("CollectionTest.db");

            user = db.Find<User>(userid);
            Assert.IsTrue(user.UserRoles.Count == 2);
            Assert.IsTrue(user.UserType.id != null);
            Assert.IsFalse(db.Entities.Any(x=>x.Value.State != Entity.EntityState.Unchanged));
            db.Close();

            // Delete
            user = null;
            db = ConnectToDb("CollectionTest.db");
            user = db.Find<User>(userid);

            db.Delete(user);
            
            Assert.IsTrue(!db.Entities.Any(x=>x.Value.State != Entity.EntityState.Deleted));
            db.SaveChanges();
            db.Close();

            // try to get data from database
            user = null;
            db = ConnectToDb("CollectionTest.db");

            user = db.Find<User>(userid);
            Assert.IsNull(user);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<User>().Count() == 0);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<UserRole>().Count() == 0);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<UserType>().Count() == 0);
        }

        [TestMethod]
        public void SyncronizationContextsTest()
        {
            string path = PathToDb("SyncronizationContextsTest.db");
            if (File.Exists(path)) File.Delete(path);

            UTDbConnect con = new  UTDbConnect(new SQLitePlatformWin32(), path);

            DbContext ctx1 = new DbContext(con);
            User usr1 = new User { Name = "Alex" };
            ctx1.AddNewItemToDBContext(usr1);
            ctx1.SaveChanges();

            DbContext ctx2 = new DbContext(con);
            User usr2 = ctx2.FirstOrDefault<User>(x => x.Name == "Alex");
            Assert.IsNotNull(usr2);
            Assert.IsTrue(usr2.Name == "Alex");
            Assert.IsTrue(usr1.id == usr2.id);

            bool isChanged = false;
            usr1.PropertyChanged += (x, e) => { isChanged = true; };

            usr2.Name = "Alex!";

            UserRole ur = new UserRole { Name = "Admin" };
            var coll = usr1.UserRoles.ToList();
            usr2.UserRoles.Add(ur);
            bool isCollectionChanged = false;
            usr1.UserRoles.CollectionChanged += (x, e) => { isCollectionChanged = true; };
            Assert.IsTrue(ctx2.Entities.Count == 2);
            
            ctx2.SaveChanges(true);

            //usr1.SynchronizeWith(usr2);
            Assert.IsTrue(usr1.Name == "Alex!");
            Assert.IsTrue(isChanged);
            Assert.IsTrue(isCollectionChanged);

            Entity ent = ctx1.FindEntityInCache(usr1.id);
            Assert.IsTrue(ent.State == Entity.EntityState.Unchanged);
            Assert.IsTrue(usr1.UserRoles[0].Name == "Admin");


        }

        [TestMethod]
        public void ColumnsAsString()
        {
            DbContext db = RecreateDB("ColumnsAsString.db");

            Assert.IsTrue(db.DBschema.GetTable<User>().SQLColumnsAsString(true) == "Name,UserType_id,Statistics,id");
            Assert.IsTrue(db.DBschema.GetTable<User>().SQLColumnsAsString(false) == "Name,UserType_id,id");


            

            db.Close();

        }

    }
}
