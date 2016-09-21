
using SQLite.Net.Attributes;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing;

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

        // Enumeration
        public EmployeeType EmployeeType { get { return GetEnumeration<EmployeeType>(ref EmployeeType_code_); } set { SetEnumeration(ref EmployeeType_code_,value); } }  private int? EmployeeType_code_;

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

        public string Type { get { return Get(ref Type_); } set { Set(ref Type_, value); } }  string Type_;

        public UserType() : base() { }
    }

    [Table(TableName)]
    public class UserRole : Base
    {
        public const string TableName = "UserRoles";

        public string Name { get { return Get(ref Name_); } set { Set(ref Name_, value); } }  string Name_;

        // User User 
        public UUID User_id { get { return GetEntityId(ref User_id_); } set { SetEntityId(ref User_id_, value); } }  UUID User_id_;
        [Ignore]
        [ForeignKey("User_id")]
        public User User { get { return GetEntity(ref User_, ref User_id_); } set { SetEntity(ref User_, ref User_id_, value); } } User User_;

        // for test only
        [Ignore]
        public Guid internal_id { get; set; }

    }

    public class EmployeeType : Enumeration
    {
        public static readonly EmployeeType Manager = new EmployeeType(0, "Manager");
        public static readonly EmployeeType Servant = new EmployeeType(1, "Servant");
        public static readonly EmployeeType AssistantToTheRegionalManager = new EmployeeType(2, "Assistant to the Regional Manager");

        public  EmployeeType(int value) : base(value) { }
        public  EmployeeType() { }
        private EmployeeType(int value, string displayName) : base(value, displayName) { }
    }

}
