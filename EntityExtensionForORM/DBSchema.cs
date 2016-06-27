
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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

        public TableInfo GetTable<T>()
        {
            TableInfo tbl;
            bool res = Tables.TryGetValue(typeof(T), out tbl);
            return res ? tbl : null;
        }

    }

    public class TableInfo
    {

        public string SqlName;
        public Type Type;
        public TypeInfo TypeInfo;

        public Dictionary<string, ColumnInfo> Columns;

        public string SQLColumnsAsString(bool includePrivateData) {
            if (includePrivateData)
            {
                if (_SQLColumnsAsString == null) _SQLColumnsAsString = SQLColumnsAsString_(includePrivateData);
                return _SQLColumnsAsString;
            } else
            {
                if(_SQLColumnsAsStringWithoutPrivateData == null) _SQLColumnsAsStringWithoutPrivateData = SQLColumnsAsString_(includePrivateData);
                return _SQLColumnsAsStringWithoutPrivateData;
            }
        }
        string SQLColumnsAsString_(bool includePrivateData) {
            StringBuilder sb = new StringBuilder(); ;
            bool firststep = true;
            foreach(ColumnInfo column in Columns.Values)
            {
                if (column.IgnoreAttribute || column.NotMapped) continue;
                if (!includePrivateData && column.PrivateDataAttribute) continue;
                sb.Append(firststep ? "" : ",");
                sb.Append(column.SqlName);
                firststep = false;
            }
            return sb.ToString();
        }
        string _SQLColumnsAsString;
        string _SQLColumnsAsStringWithoutPrivateData;

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
        public string ClrName;
        public string SqlName;

        public TableInfo Table;

        public Type Type;
        public TypeInfo TypeInfo;
        public PropertyInfo Property;

        public Type GenericType;
        //public TypeInfo GenericTypeInfo;

        public bool CascadeDeleteAttribute = false;
        public bool NotMapped = false;
        public bool IgnoreAttribute = false;
        public bool PrivateDataAttribute = false;

        public bool InversePropertyAttribute = false;
        public string InversePropertyName;

        public bool IsNullable;

        public bool ForeignKeyAttribute;
        public string ForeignKeyName;

        public ColumnInfo()
        {
        }

        public override string ToString()
        {
            return "["+ClrName+"   ("+Type+")]";
        }
    }
}
