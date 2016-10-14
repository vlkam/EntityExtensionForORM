

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SQLite.Net;
using SQLite.Net.Platform.Win32;
using System;
using System.Collections.Generic;
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
        public void EnumerationTest()
        {

            DbContext db;
            db = RecreateDB("EnumerationTest.db"); 

            User user = new User { Name = "Peter" };
            user.EmployeeType = EmployeeType.Servant;
            db.AddNewItemToDBContext(user);

            db.SaveChanges();
            db.Close();

            db = ConnectToDb("EnumerationTest.db");
            user = db.FirstOrDefault<User>();

            Assert.IsTrue(user.EmployeeType == EmployeeType.Servant);

            bool isCheck = false;
            user.PropertyChanged += (sender, args) => isCheck = true;
            user.EmployeeType = EmployeeType.AssistantToTheRegionalManager;

            Assert.IsTrue(isCheck);
        }

        [TestMethod]
        public void UpdateCollectionTest()
        {
            DbContext db;
            db = RecreateDB("UpdateCollectionTest.db");

            User user = new User { Name = "Alex" };
            db.AddNewItemToDBContext(user);
            int i = user.UserRoles.Count();

            bool isCollectionChanged = false;
            user.UserRoles.CollectionChanged += (sender,e) => isCollectionChanged = true;

            UserRole ur1 = new UserRole();
            db.AddNewItemToDBContext(ur1);
            ur1.User = user;

            // remove from collection on change owner test
            Assert.IsTrue(user.UserRoles.Contains(ur1));
            Assert.IsTrue(isCollectionChanged);

            isCollectionChanged = false;

            ur1.User = null;
            Assert.IsTrue(!user.UserRoles.Contains(ur1));
            Assert.IsTrue(isCollectionChanged);

            // Delete test
            ur1 = new UserRole();
            db.AddNewItemToDBContext(ur1);
            ur1.User = user;

            Assert.IsTrue(user.UserRoles.Contains(ur1));

            db.Delete(ur1);
            Assert.IsTrue(!user.UserRoles.Contains(ur1));

            //db.Delete(ur1);

            db.Close();

        }

        [TestMethod]
        public void CacheCollectionTest()
        {
            DbContext db;
            db = RecreateDB("CacheCollectionTest.db");

            User user = new User { Name = "Alex" };
            user.UserType = new UserType {Type = "Advanced"};

            UserRole adm = new UserRole { Name = "Admin" };
            user.UserRoles.Add(adm);
            UserRole usr = new UserRole { Name = "User" };
            user.UserRoles.Add(usr);
            UserRole pub = new UserRole { Name = "Publisher" };
            user.UserRoles.Add(pub);

            db.AddNewItemToDBContext(user);
            db.SaveChanges();
            db.Close();

            db = ConnectToDb("CacheCollectionTest.db");
            User user1 = db.FirstOrDefault<User>();
            UserRole ur1 = db.FirstOrDefault<UserRole>($"{nameof(UserRole.Name)} = 'User'");
            ur1.internal_id = new Guid();
            Assert.IsTrue(user1.UserRoles.FirstOrDefault(x=>x.Name == "User").internal_id == ur1.internal_id);
        }

        [TestMethod]
        public void FirstOrDefaultTest()
        {
            DbContext db;
            db = RecreateDB("FirstOrDefaultTest.db");

            User user = new User { Name = "Alex" };
            user.UserType = new UserType {Type = "Advanced"};

            user.UserRoles.Add(new UserRole { Name = "Admin"});
            user.UserRoles.Add(new UserRole { Name = "User"});
            user.UserRoles.Add(new UserRole { Name = "Publisher"});

            db.AddNewItemToDBContext(user);
            db.SaveChanges();

            UserRole ur = db.FirstOrDefault<UserRole>($"{nameof(UserRole.Name)} = 'User'");
            Assert.IsTrue(ur.Name == "User");
        }

        [TestMethod]
        public void DBConnectTestMethod()
        {
            DbContext db = RecreateDB("DBConnectTest.db");

            SQLiteConnection connect = db.DbConnect;
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
            user = db.FirstOrDefault<User>();

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
            Assert.IsTrue(db.Entities.All(x => x.Value.State == Entity.EntityState.Added));

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
            
            Assert.IsTrue(db.Entities.All(x => x.Value.State == Entity.EntityState.Deleted));
            db.SaveChanges();
            db.Close();

            // try to get data from database
            user = null;
            db = ConnectToDb("CollectionTest.db");

            user = db.Find<User>(userid);
            Assert.IsNull(user);
            Assert.IsTrue(!db.DbConnect.Table<User>().Any());
            Assert.IsTrue(!db.DbConnect.Table<UserRole>().Any());
            Assert.IsTrue(!db.DbConnect.Table<UserType>().Any());
        }

        [TestMethod]
        public void ColumnsAsString()
        {
            DbContext db = RecreateDB("ColumnsAsString.db");
            Assert.IsTrue(db.DBschema.GetTable<User>().SQLColumnsAsString(true) == "Name,UserType_id,EmployeeType,Statistics,id");
            db.Close();

        }

        [TestMethod]
        public void AttachNewParentWithChildrenTest()
        {
            // Add to DB
            DbContext db;
            db = RecreateDB("AttachNewParentWithChildrenTest.db");

            User user = new User { Name = "Alex" };
            user.UserType = new UserType {Type = "Advanced"};

            user.UserRoles.Add(new UserRole { Name = "Admin"});
            user.UserRoles.Add(new UserRole { Name = "User"});
            user.UserRoles.Add(new UserRole { Name = "Publisher"});

            db.AddNewItemToDBContext(user);

            Assert.IsTrue(db.Entities.Where(x => x.Value.State == Entity.EntityState.Added).Count() == 5);
            foreach(UserRole role in user.UserRoles)
            {
                Assert.IsTrue(role.User_id == user.id);
            }
        }

        [TestMethod]
        public void RefreshFromDBTest()
        {
            // Add to DB
            DbContext db;
            db = RecreateDB("RefreshFromDBTest.db");

            User user = new User { Name = "Alex",Statistics = "101" };
            user.UserType = new UserType {Type = "Advanced"};

            db.AddNewItemToDBContext(user);
            db.SaveChanges();

            User us1 = db.DbConnect.Find<User>(user.id);
            us1.Name = "Victor";
            us1.Statistics = "202";
            db.DbConnect.InsertOrReplace(us1);

            user.RefreshFromDB();

            Assert.IsTrue(user.Name == us1.Name);
            Assert.IsTrue(user.Statistics == us1.Statistics);

        }

        [TestMethod]
        public void UUIDtest()
        {
            UUID testUUID = new UUID("22F4916A-430D-45C3-9FB4-4958E7C5216C");

            DbContext db;
            db = RecreateDB("UUIDtest.db");

            User user = new User { Name = "Peter" };
            db.AddNewItemToDBContext(user);
            user.id = testUUID;

            db.SaveChanges();
            db.Close();

            user = null;
            db = ConnectToDb("UUIDtest.db");
            user = db.FirstOrDefault<User>();

            Assert.IsTrue(user.id == testUUID);

            List<UUID> userid = db.DbConnect.Query<UUID>("SELECT id FROM " + User.TableName);
            Assert.IsTrue(userid[0] == testUUID,"UUIDs aren't equivalent");

            UUID id = db.DbConnect.ExecuteScalar<UUID>("SELECT id FROM " + User.TableName + " LIMIT 1");
            Assert.IsTrue(id == testUUID,"UUIDs aren't equivalent");
        }

        [TestMethod]
        public void CollectionItemDeleteTest()
        {
            DbContext db;
            db = RecreateDB("CollectionItemDeleteTest.db");

            User user = new User { Name = "Alex" };
            user.UserType = new UserType {Type = "Advanced"};

            user.UserRoles.Add(new UserRole { Name = "Admin"});
            user.UserRoles.Add(new UserRole { Name = "User"});
            user.UserRoles.Add(new UserRole { Name = "Publisher"});

            db.AddNewItemToDBContext(user);

            UserRole r = user.UserRoles.First(x => x.Name == "User");
            string username = r.User.Name;
            user.UserRoles.Remove(r);

            Assert.IsTrue(db.Entities.Count(x => x.Value.State == Entity.EntityState.Deleted) == 1);
        }

    }
}
