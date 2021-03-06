﻿
using System;

namespace EntityExtensionForORM
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CascadeDeleteAttribute : Attribute 
    {
        public CascadeDeleteAttribute(){
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class InversePropertyAttribute : Attribute 
    {
        public string PropertyName { get; set; }

        public InversePropertyAttribute(string PropertyName_){
            PropertyName = PropertyName_;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute 
    {
        public string ForeignKeyName { get; set; }

        public ForeignKeyAttribute(string ForeignKeyName_){
            ForeignKeyName = ForeignKeyName_;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DataTypeAttribute : Attribute 
    {
        public DataTypeAttribute(){}
        public DataTypeAttribute(object dt) { }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class PrivateData : Attribute 
    {
        public PrivateData(){}
    }

}
