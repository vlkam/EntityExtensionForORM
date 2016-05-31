
using SQLite.Net;
using SQLite.Net.Attributes;
using SQLite.Net.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EntityExtensionForORM
{
   public class DbContext 
    {
        protected SQLiteConnection connect;
        public SQLiteConnection GetConnectionForTestOnly() { return connect; }
        
        public DBschema DBschema;
        
        public Dictionary<UUID, Entity> entities = new Dictionary<UUID, Entity>();
        public Guid ContextId = Guid.NewGuid();

        public DbContext(ISQLitePlatform platform,string path)
        {
            connect = new SQLiteConnection(platform, path);
            connect.ExtraTypeMappings.Add(typeof(UUID), "blob");
        }

        public void RegisterChange<T>(UUID id,T obj) where T : Base
        {
            Entity entity;
            if (!entities.TryGetValue(id,out entity))
            {
                throw new Exception("RegisterChange: Entity<"+obj+"> isn't attached to DbContext");
            }

            switch (entity.State)
            {
                case Entity.EntityState.Unchanged:
                    entity.State = Entity.EntityState.Modified;
                    break;
                case Entity.EntityState.Added:
                    break;
                case Entity.EntityState.Modified:
                    break;
                default:
                    throw new Exception("RegisterChange<T> : Entity status is invalid");
            }
        }

        public void CreateSchema()
        {
            DBschema = new DBschema();
            foreach(var tableMap in connect.TableMappings)
            {
                TableInfo table = new TableInfo { SqlName = tableMap.TableName,Type = tableMap.MappedType,TypeInfo = tableMap.MappedType.GetTypeInfo()};
                DBschema.Tables.Add(tableMap.MappedType,table);
                foreach(var columnMap in tableMap.Columns)
                {
                    ColumnInfo ci = new ColumnInfo();
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
                    ColumnInfo ci;
                    if(!table.Columns.TryGetValue(propname, out ci))
                    {
                        ci = new ColumnInfo();
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
                foreach (ColumnInfo ci in table.Columns.Values)
                {
                    if (ci.InversePropertyAttribute)
                    {
                        ci.GenericType = ci.Type.GenericTypeArguments[0];
                        //ci.GenericTypeInfo = ci.GenericType.GetTypeInfo();
                    }
                }
            }
        }

        public void ClearChanges()
        {
            entities.Clear();
        }

        public void Close()
        {
            foreach(var elm in entities)
            {
                elm.Value.Obj.DBContext = null;
            }
            entities.Clear();
            connect.Close();
        }

        public void SaveChanges()
        {
            foreach(var keyval in entities)
            {
                var Value = keyval.Value;
                switch (Value.State)
                {
                    case Entity.EntityState.Added:
                    case Entity.EntityState.Modified:
                        connect.InsertOrReplace(Value.Obj, Value.Type);
                        keyval.Value.State = Entity.EntityState.Unchanged;
                        break;
                    case Entity.EntityState.Deleted:
                        connect.Delete(Value.Obj);
                        break;
                    case Entity.EntityState.Unchanged:
                        break;
                    default:
                        throw new Exception("Uknown operation type "+Value.State);
                }
            }
        }

        /*
        public void AddNewItemToDBContext<T>(T obj) where T : Base
        {
            AttachToDBContext(obj,typeof(T), Entity.EntityState.Added);
        }
        */
        public void AddNewItemToDBContext<T>(T obj) where T : Base => AttachToDBContext(obj, typeof(T), Entity.EntityState.Added);

        public void AddNewItemToDBContext(Base obj, Type type) => AttachToDBContext(obj, type, Entity.EntityState.Added);

        public void AttachToDBContext<T>(T obj, Entity.EntityState state) where T : Base => AttachToDBContext(obj,typeof(T),state);

        public void AttachToDBContext(Base obj,Type type,Entity.EntityState state)
        {
            if (obj == null) throw new Exception("Cannot add to DB null object");

            if (obj.DBContext != null)
            {
                if (obj.DBContext.ContextId != ContextId)
                {
                    throw new Exception("DbContext cannot be changed for object <" + this + "> because another context set already");
                }
                else
                {
                    //throw new Exception("Object in context allready");
                    return;
                }
            }
            obj.DBContext = this;

            UUID id = obj.id;
            if (entities.ContainsKey(id)) throw new Exception("Cannot add entity <"+obj+"> to DbContext because it already over there");

            Entity entity = new Entity
            {
                Obj = obj,
                Type = type,
                Order = entities.Count,
                State = state
            };
            entities.Add(id, entity);
        }

        public void Insert<T>(T obj) where T: Base
        {
            connect.Insert(obj);
            Entity entity;
            if (!entities.TryGetValue(obj.id, out entity)) AttachToDBContext(obj,Entity.EntityState.Unchanged);
        }

        public T FirstOrDefault<T>(Func<T,bool> predicate,bool attachToContext = true) where T : Base
        {
            T obj = connect.Table<T>().FirstOrDefault(predicate);
            if (obj != null && attachToContext) AttachToDBContext(obj, Entity.EntityState.Unchanged);
            return obj;
        }

        public List<T> Set<T>(bool attachToContext = false) where T : Base
        {
            List<T> lst = connect.Table<T>().ToList();
            if (attachToContext)
            {
                foreach(var elm in lst)
                {
                    AttachToDBContext(elm, Entity.EntityState.Unchanged);
                }
            }
            return lst;
        }

        public T Get<T>(UUID id) where T : Base
        {
            Entity entity;
            
            // tries to get from cache
            if (entities.TryGetValue(id, out entity)) return (T)entity.Obj;

            // tries to get from db
            T obj = connect.Find<T>(id);
            if(obj != null)
            {
                AttachToDBContext(obj,Entity.EntityState.Unchanged);
            }
            return obj;
        }

        public T First<T>() where T : Base
        {
            T obj = connect.Table<T>().FirstOrDefault();
            Entity entity;
            if (entities.TryGetValue(obj.id, out entity)) return (T)entity.Obj;
            AttachToDBContext(obj,Entity.EntityState.Unchanged);
            return obj;
        }

        public T FirstOrDefault<T>() where T : Base
        {
            return First<T>();
        }

        public bool ObjectExists<T>(T obj) where T : Base
        {
            return connect.Find<T>(obj.id) != null;
        }

        public TableQuery<T> Table<T>() where T : Base
        {
            return connect.Table<T>();
        }

        public void Delete<T>(T obj) where T : Base
        {
            Delete(obj, typeof(T));
        }

        public void Delete(Base obj,Type type)
        {
            Entity entity;
            if (!entities.TryGetValue(obj.id, out entity)) throw new Exception("Delete : Can't find an object in the cache");
            entity.State = Entity.EntityState.Deleted;

            TableInfo ti = DBschema.GetTable(type);
            foreach(ColumnInfo ci in ti.Columns.Where(x => x.Value.CascadeDeleteAttribute).Select(x => x.Value).ToList())
            {

                // Collection
                if (ci.InversePropertyAttribute) {
                    IEnumerable coll = (IEnumerable)ci.Property.GetValue(obj);
                    foreach(Base elm in coll)
                    {
                        if(elm != null) Delete(elm,ci.GenericType);
                    }
                }
                // Property
                else
                {
                    Base tobj = (Base)ci.Property.GetValue(obj);
                    if(tobj != null) Delete(tobj,ci.Type);
                }

            }
        }

        public void DeleteRange<T>(IEnumerable<T> lst) where T : Base
        {
            Type type = typeof(T);
            foreach (T elm in lst)
            {
                Delete(elm,type);
            }
        }
    }
}
