
using SQLite.Net.Attributes;
using System;
using System.Collections.ObjectModel;

namespace EntityExtensionForORM.Tests
{
    [Table(TableName)]
    public class User : Base
    {
        public const string TableName = "Users";

        // properties
        public string Name { get { return Name_; } set { Set(ref Name_, value); } } private string Name_;
        //public string Name { get; set; }
        
        
        // reference properties
        public Guid? UserType_id {
            get { return UserType_id_; }
            set { SetEntityGuid(ref UserType_id_, value); }}
            private Guid? UserType_id_;
        [Ignore][CascadeDelete]
        public UserType UserType {
            get { return GetEntity(ref UserType_,ref UserType_id_); }
            set { SetEntity(ref UserType_,ref UserType_id_,value); }}
        private UserType UserType_;
        
        [Ignore][InverseProperty("User")][CascadeDelete]
        public ObservableCollection<UserRole> UserRoles {
            get { return GetCollection(ref UserRoles_); }
            set { SetCollection(ref UserRoles_, value); } }
        private ObservableCollection<UserRole> UserRoles_;

        public User() : base() {
            UserRoles = new ObservableCollection<UserRole>();
            //UserRoles.CollectionChanged += UserRoles_CollectionChanged;
        }

        //Add	An item was added to the collection.
        //Move An item was moved within the collection.
        //Remove	An item was removed from the collection.
        //Replace	An item was replaced in the collection.
        //Reset	The content of the collection was cleared.
        private void UserRoles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //throw new NotImplementedException();
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

        public Guid User_id { get; set; }
        [ForeignKey("User_id")][Ignore]
        public User User { get; set; }

        public UserRole() : base() { }
    }

}
