
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EntityExtensionForORM
{

    public class DBschema
    {
        public Dictionary<Type,TableInfo> Tables {get; set;}

        public DBschema() {
            Tables = new Dictionary<Type, TableInfo>();
        }
           
        public TableInfo GetTable(Type type)
        {
            TableInfo tbl;
            bool res = Tables.TryGetValue(type, out tbl);
            return res ? tbl : null;
        }

    }

    public class TableInfo
    {

        public string SqlName;
        public Type Type;
        public TypeInfo TypeInfo;

        public Dictionary<string, ColumnInfo> Columns;

        public TableInfo()
        {
            Columns = new Dictionary<string,ColumnInfo>();
        }

        public override string ToString()
        {
            return "["+SqlName+"("+Type+")]";
        }

        public ColumnInfo GetColumnInfo(string clrname)
        {
            ColumnInfo fld;
            bool res = Columns.TryGetValue(clrname, out fld);
            return res ? fld : null;
        }

    }

    public class ColumnInfo
    {
        public string ClrName {get; set;}
        public string SqlName;

        public TableInfo Table;

        public Type Type;
        public TypeInfo TypeInfo;
        public PropertyInfo Property;

        public Type GenericType;
        //public TypeInfo GenericTypeInfo;

        public bool CascadeDeleteAttribute = false;
        public bool NotMapped = false;
        public bool IgnoreAttibute = false;
        public bool InversePropertyAttribute = false;

        public bool IsNullable;

        public ColumnInfo()
        {
        }

        public override string ToString()
        {
            return "["+ClrName+"("+Type+")]";
        }
    }
}
