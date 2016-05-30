﻿

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

        private string PathToDb()
        {
            return Path.Combine(Environment.CurrentDirectory, "test.db");
        }

        private UTDbContext ConnectToDb() => new UTDbContext(PathToDb());

        private UTDbContext RecreateDB() {
            string path = PathToDb();
            if (File.Exists(path)) File.Delete(path);
            return new UTDbContext(path);
        }
       
        [TestMethod]
        public void DBConnectTestMethod()
        {
            DbContext db = RecreateDB();

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
            DbContext db = RecreateDB();

            Guid user_id = Guid.NewGuid();

            // Add new user to context
            User us = new User { id = user_id,Name = "Really cool user" };
            //us.AddToDBContext(db);
            db.AddNewItemToDBContext(us);

            Entity entity;

            // Is user added to context ? 
            entity = null;
            db.entities.TryGetValue(user_id, out entity);
            Assert.IsTrue(entity != null);
            Assert.IsTrue(entity.State == Entity.EntityState.Added);

            // Makes change in user
            Guid usertype_id = Guid.NewGuid();
            UserType type = new UserType { id = usertype_id};
            us.UserType = type;

            // Is usertype added to context ?
            entity = null;
            db.entities.TryGetValue(usertype_id, out entity);
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
            DbContext db = RecreateDB();

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
            db = RecreateDB();

            User user = new User { Name = "Peter" };
            db.AddNewItemToDBContext(user);

            user.UserType = new UserType { Type = "Cool" };

            db.SaveChanges();
            db.Close();

            user.UserType = null;
            user = null;

            db = ConnectToDb();
            user = db.First<User>();

            Assert.IsTrue(user.UserType.Type == "Cool");

        }

        [TestMethod]
        public void CollectionTest()
        {
            // Add to DB
            DbContext db;
            db = RecreateDB();

            User user = new User { Name = "Alex" };
            db.AddNewItemToDBContext(user);
            user.UserType = new UserType {Type = "Advanced"};

            bool isCollectionChanged = false;
            user.UserRoles.CollectionChanged += (x, y) => isCollectionChanged = true;
            user.UserRoles.Add(new UserRole {Name = "Admin" });
            user.UserRoles.Add(new UserRole { Name = "Customer" });

            Assert.IsTrue(isCollectionChanged);
            Assert.IsTrue(db.entities.Count == 4);
            Assert.IsTrue(db.entities[user.id].State == Entity.EntityState.Added);
            Assert.IsTrue(db.entities[user.UserRoles[0].id].State == Entity.EntityState.Added);
            Assert.IsTrue(!db.entities.Any(x => x.Value.State != Entity.EntityState.Added));

            db.SaveChanges();
            Assert.IsTrue(db.entities[user.id].State == Entity.EntityState.Unchanged);
            Assert.IsTrue(db.entities[user.UserRoles[0].id].State == Entity.EntityState.Unchanged);

            db.Close();

            // Lazy loading
            Guid userid = user.id;
            user = null;
            db = ConnectToDb();

            user = db.Get<User>(userid);
            Assert.IsTrue(user.UserRoles.Count == 2);
            Assert.IsTrue(user.UserType.id != null);
            Assert.IsFalse(db.entities.Any(x=>x.Value.State != Entity.EntityState.Unchanged));
            db.Close();

            // Delete
            user = null;
            db = ConnectToDb();
            user = db.Get<User>(userid);

            db.Delete(user);
            
            Assert.IsTrue(!db.entities.Any(x=>x.Value.State != Entity.EntityState.Deleted));
            db.SaveChanges();
            db.Close();

            // try to get data from database
            user = null;
            db = ConnectToDb();

            user = db.Get<User>(userid);
            Assert.IsNull(user);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<User>().Count() == 0);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<UserRole>().Count() == 0);
            Assert.IsTrue(db.GetConnectionForTestOnly().Table<UserType>().Count() == 0);
        }

    }
}