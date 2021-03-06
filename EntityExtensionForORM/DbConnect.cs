﻿
using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EntityExtensionForORM
{

    class column_info_from_sqlite
    {
        public byte cid { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int notnull { get; set; }
        public string dflt_value { get; set; }
        public int pk { get; set; }
    }

    public class DbConnect : SQLiteConnection
    {
        public DBschema DBschema;

        public DbConnect(ISQLitePlatform platform,string path) : base (platform,path)
        {
            ExtraTypeMappings.Add(typeof(UUID), "blob");
            CreateTable<DbMetadata>();
        }

        public void CreateSchema()
        {

            DBschema = new DBschema();
            foreach(var tableMap in TableMappings)
            {

                if (!tableMap.MappedType.GetTypeInfo().IsSubclassOf(typeof(Base))) continue;

                SQLiteCommand cmd =  CreateCommand("PRAGMA table_info('"+tableMap.TableName+"');");
                List<column_info_from_sqlite> sql_columns = cmd.ExecuteDeferredQuery<column_info_from_sqlite>().ToList();
                
                TableInfo table = new TableInfo {
                    SqlName = tableMap.TableName,
                    Type = tableMap.MappedType,
                    TypeInfo = tableMap.MappedType.GetTypeInfo(),
                    TableMapping = tableMap
                };
                DBschema.Tables.Add(tableMap.MappedType,table);
                foreach (var columnMap in tableMap.Columns)
                {
                    EntityExtensionForORM.ColumnInfo ci = new EntityExtensionForORM.ColumnInfo();
                    ci.ClrName = columnMap.PropertyName;
                    ci.Type = columnMap.ColumnType;
                    ci.TypeInfo = ci.Type.GetTypeInfo();
                    ci.Property = table.Type.GetRuntimeProperty(columnMap.PropertyName);
                    ci.Table = table;
                    ci.IsNullable = columnMap.IsNullable;
                    ci.SqlName = columnMap.Name;
                    ci.IsPrimaryKey = ci.ClrName == "id";

                    column_info_from_sqlite cifs = sql_columns.Find(x => x.name == ci.SqlName);
                    ci.Index = cifs.cid;

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
                            ci.IgnoreAttribute = true;
                            continue;
                        }
                        if(customattr.AttributeType == typeof(PrivateData))
                        {
                            ci.PrivateDataAttribute = true;
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
                            ci.InversePropertyName = (string)customattr.ConstructorArguments[0].Value;
                            continue;
                        }
                        if(customattr.AttributeType == typeof(ForeignKeyAttribute))
                        {
                            ci.ForeignKeyAttribute = true;
                            ci.ForeignKeyName = (string)customattr.ConstructorArguments[0].Value;
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

        public void RemoveOldColumns()
        {
            if (DBschema == null) throw new Exception("Dbschema not created");

            bool tablesChanged = false;

            foreach (TableMapping tableMap in TableMappings)
            {

                if (!tableMap.MappedType.GetTypeInfo().IsSubclassOf(typeof (Base))) continue;

                TableInfo tableInfo = DBschema.Tables.Values.First(x=>x.SqlName == tableMap.TableName);

                SQLiteCommand cmd = CreateCommand("PRAGMA table_info('" + tableMap.TableName + "');");
                List<column_info_from_sqlite> sql_columns = cmd.ExecuteDeferredQuery<column_info_from_sqlite>().ToList();

                List<column_info_from_sqlite> columnToDelete = new List<column_info_from_sqlite>();
                foreach (column_info_from_sqlite column in sql_columns)
                {
                    if (!tableInfo.Columns.Values.Any(x => x.SqlName == column.name)) columnToDelete.Add(column);
                }

                if (columnToDelete.Count > 0)
                {

                    tablesChanged = true;

                    string oldTable = tableInfo.SqlName + "_old;";
                    string SQLcolumns = tableInfo.SQLColumnsAsString(true);

                    cmd = CreateCommand("ALTER TABLE "+tableInfo.SqlName+" RENAME TO "+oldTable+";");
                    cmd.ExecuteNonQuery();

                    CreateTable(tableInfo.Type);

                    cmd = CreateCommand(
                        "INSERT INTO "+tableInfo.SqlName+" ("+SQLcolumns+") SELECT "+ SQLcolumns+" FROM " + oldTable+Environment.NewLine+
                        "DROP TABLE " + tableInfo.SqlName +"tmp;"
                        );

                        //DROP TABLE " + tableInfo.SqlName +@"tmp;
                        //COMMIT;
                        //PRAGMA foreign_keys=ON;");
                    cmd.ExecuteNonQuery();

                }
            }

            if (tablesChanged)
            {
                CreateSchema();
            }
        }

    }
}
