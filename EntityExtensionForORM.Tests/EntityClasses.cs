
using SQLite.Net.Attributes;
using System;
using System.Collections.ObjectModel;

namespace EntityExtensionForORM.Tests
{
    [Table(TableName)]
    public class User : Base
    {
        public const string TableName = "Users";

        // Property
        public string Name { get { return Get(ref Name_); } set { Set(ref Name_, value); } } private string Name_;
        
        // Reference property
        public UUID UserType_id { get { return GetEntityId(ref UserType_id_); } set { SetEntityId(ref UserType_id_, value); }} private UUID UserType_id_;
        [Ignore][CascadeDelete]
        public UserType UserType { get { return GetEntity(ref UserType_,ref UserType_id_); } set { SetEntity(ref UserType_,ref UserType_id_,value); }} private UserType UserType_;
        
        // Navigation property
        [Ignore][InverseProperty("User")][CascadeDelete]
        public ObservableCollection<UserRole> UserRoles { get { return GetCollection(ref UserRoles_); } set { SetCollection(ref UserRoles_, value); } } private ObservableCollection<UserRole> UserRoles_;

        [PrivateData]
        public string Statistics { get { return Get(ref Statistics_); } set { Set(ref Statistics_, value); } }
        private string Statistics_;

        public User() : base() {
            UserRoles = new ObservableCollection<UserRole>();
        }

    }

    [Table(TableName)]
    public class UserType : Base
    {

        public const string TableName = "UserTypes";
        public string Type { get; set; }

        public UserType() : base() { }
    }

    [Table(TableName)]
    public class UserRole : Base
    {

        public const string TableName = "UserRoles";
        public string Name { get; set; }

        public UUID User_id { get; set; }
        [ForeignKey("User_id")][Ignore]
        public User User { get; set; }

        [Ignore]
        public Guid internal_id { get; set; }

        public UserRole() : base() { }
    }

}
