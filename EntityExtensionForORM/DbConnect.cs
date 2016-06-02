
using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Interop;
using System.Reflection;

namespace EntityExtensionForORM
{
    public class DbConnect : SQLiteConnection
    {
        public DBschema DBschema;

        public DbConnect(ISQLitePlatform platform,string path) : base (platform,path)
        {
            ExtraTypeMappings.Add(typeof(UUID), "blob");
        }

        public void CreateSchema()
        {
            DBschema = new DBschema();
            foreach(var tableMap in TableMappings)
            {
                TableInfo table = new TableInfo { SqlName = tableMap.TableName,Type = tableMap.MappedType,TypeInfo = tableMap.MappedType.GetTypeInfo()};
                DBschema.Tables.Add(tableMap.MappedType,table);
                foreach(var columnMap in tableMap.Columns)
                {
                    EntityExtensionForORM.ColumnInfo ci = new EntityExtensionForORM.ColumnInfo();
                    ci.ClrName = columnMap.PropertyName;
                    ci.Type = columnMap.ColumnType;
                    ci.TypeInfo = ci.Type.GetTypeInfo();
                    ci.Property = table.Type.GetRuntimeProperty(columnMap.PropertyName);
                    ci.Table = table;
                    ci.IsNullable = columnMap.IsNullable;
                    ci.SqlName = columnMap.Name;

                    table.Columns.Add(ci.ClrName,ci);
                }

                // scans all property
                foreach (var property in tableMap.MappedType.GetRuntimeProperties())
                {
                    string propname = property.Name;
                    EntityExtensionForORM.ColumnInfo ci;
                    if(!table.Columns.TryGetValue(propname, out ci))
                    {
                        ci = new EntityExtensionForORM.ColumnInfo();
                        ci.ClrName = propname;
                        ci.Type = property.PropertyType;
                        ci.TypeInfo = property.PropertyType.GetTypeInfo();
                        ci.Property = property;
                        ci.NotMapped = true;
                        ci.Table = table;
                        table.Columns.Add(propname,ci);
                    }
                    foreach (var customattr in property.CustomAttributes)
                    {
                        if (customattr.AttributeType == typeof(IgnoreAttribute))
                        {
                            ci.IgnoreAttibute = true;
                            continue;
                        }
                        if (customattr.AttributeType == typeof(CascadeDeleteAttribute))
                        {
                            ci.CascadeDeleteAttribute = true;
                            continue;
                        }
                        if (customattr.AttributeType == typeof(InversePropertyAttribute))
                        {
                            ci.InversePropertyAttribute = true;
                            continue;
                        }
                    }
                }

                // post processing
                foreach (EntityExtensionForORM.ColumnInfo ci in table.Columns.Values)
                {
                    if (ci.InversePropertyAttribute)
                    {
                        ci.GenericType = ci.Type.GenericTypeArguments[0];
                        //ci.GenericTypeInfo = ci.GenericType.GetTypeInfo();
                    }
                }
            }
        }

    }
}
