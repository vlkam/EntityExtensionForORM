
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace EntityExtensionForORM
{

    public  class  DbContext 
    {
        public DbConnect DbConnect;
        public DBschema DBschema; 
        
        public Dictionary<UUID,Entity> Entities = new Dictionary<UUID, Entity>();
        public Guid Id = Guid.NewGuid();

        //List<Entity> Changes => entities.Values.Where(x => x.State != Entity.EntityState.Unchanged).ToList();
        public List<Entity> ModifiedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Modified).Select(x=>x.Value).ToList(); } }
        public List<Entity> AddedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Added).Select(x=>x.Value).ToList(); } }
        public List<Entity> DeletedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Deleted).Select(x=>x.Value).ToList(); } }
        public List<Entity> ChangedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Deleted || x.Value.State == Entity.EntityState.Added || x.Value.State == Entity.EntityState.Detached || x.Value.State == Entity.EntityState.Modified).Select(x=>x.Value).ToList(); } }

        public DbContext(DbConnect DbConnect_)
        {
            DbConnect = DbConnect_;
            DBschema = DbConnect.DBschema;
        }

        public Base FindObjectInCache(Type type,UUID id)
        {
            Entity entity;
            if (!Entities.TryGetValue(id,out entity)) return null;

            Base obj;
            if(!entity.Obj.TryGetTarget(out obj))
            {
                //throw new Exception("FindObjectInCache : Cannot resolve a weakreference");
                Entities.Remove(id);
                return null;
            }
            return obj;
        }

        public T FindObjectInCache<T>(UUID id) where T : Base => (T)FindObjectInCache(typeof(T), id);

        public Entity FindEntityInCache(UUID id)
        {
            Entity entity;
            if (!Entities.TryGetValue(id,out entity)) return null;
            return entity;
        }

        public void RegisterChange<T>(UUID id,T obj) where T : Base
        {
            Entity entity = FindEntityInCache(id);
            if (entity == null)
            {
                throw new Exception("RegisterChange: Entity<"+obj+"> isn't attached to DbContext.");
            }

            switch (entity.State)
            {
                case Entity.EntityState.Unchanged:
                    entity.State = Entity.EntityState.Modified;
                    entity.HardReference = obj;
                    break;
                case Entity.EntityState.Added:
                    break;
                case Entity.EntityState.Modified:
                    break;
                default:
                    throw new Exception("RegisterChange<T> : Entity status is invalid");
            }
        }

        public void ClearChanges()
        {
            //Entities.Clear();
            foreach(var entity in ChangedEntities)
            {
                switch (entity.State)
                {
                    case Entity.EntityState.Unchanged:
                        break;
                    case Entity.EntityState.Added:
                        Entities.Remove(entity.HardReference.id);
                        entity.HardReference = null;
                        break;
                    case Entity.EntityState.Modified:
                        entity.State = Entity.EntityState.Unchanged;
                        entity.HardReference = null;
                        break;
                    case Entity.EntityState.Deleted:
                        entity.State = Entity.EntityState.Unchanged;
                        entity.HardReference = null;
                        break;
                    default:
                        throw new Exception("RegisterChange<T> : Entity status is invalid");
                }

            }
        }

        public bool HasChanges()
        {
            return Entities.Values.Any(x=>x.State != Entity.EntityState.Unchanged);
        }

        public void Close()
        {
            foreach(var elm in Entities)
            {
                Base obj;
                if(elm.Value.Obj.TryGetTarget(out obj))
                {
                    obj.DBContext = null;
                }
            }
            ClearChanges();
        }

        public void SaveChanges()
        {
            DbConnect.BeginTransaction();
            try
            {
                foreach (var keyval in Entities)
                {
                    Entity entity = keyval.Value;
                    if (entity.State == Entity.EntityState.Unchanged) continue;
                    switch (entity.State)
                    {
                        case Entity.EntityState.Added:
                        case Entity.EntityState.Modified:
                            DbConnect.InsertOrReplace(entity.HardReference, entity.Type);
                            entity.State = Entity.EntityState.Unchanged;
                            entity.HardReference.IsModified = false;
                            entity.HardReference = null;
                            break;
                        case Entity.EntityState.Deleted:
                            DbConnect.Delete(entity.HardReference);
                            entity.HardReference = null;
                            break;
                        default:
                            throw new Exception("Unknown operation type "+entity.State);
                    }
                }

                // Removes references to deleted object
                foreach (UUID id in Entities.Where(x => x.Value.State == Entity.EntityState.Deleted).Select(x=>x.Key).ToList()) Entities.Remove(id);

                DbConnect.Commit();
            }
            catch(Exception ex)
            {
                DbConnect.Rollback();
                throw ex;
            }
        }

        public void AddNewItemToDBContext<T>(T obj) where T : Base => AddNewItemToDBContext(obj, typeof(T));

        public void AddNewItemToDBContext(Base obj, Type type)
        {

            if (obj.IsAttachToContext()) {
                if (obj.DBContext.Id != Id)
                {
                    throw new Exception("DbContext cannot be changed for object <" + this + "> because another context set already");
                }
                else
                {
                    return;
                }
            }

            AttachToDBContextInternal(obj, type, Entity.EntityState.Added);

            // add children elements to context
            TableInfo ti = DBschema.GetTable(type);
            
            foreach(ColumnInfo ci in ti.Columns.Values)
            {
                if (!ci.IgnoreAttribute) continue;
                if (ci.InversePropertyAttribute)
                {
                    //collection
                    IEnumerable coll = (IEnumerable)ci.Property.GetValue(obj);
                    if (coll == null) continue;

                    obj.InitiliazeCollection((INotifyCollectionChanged)ci.Property.GetValue(obj), ci.ClrName);

                    TableInfo foreignKeyTable = DBschema.GetTable(ci.GenericType);
                    ColumnInfo foreignKeyColumn = foreignKeyTable.GetColumnInfo(ci.InversePropertyName);
                    ColumnInfo foreignKeyIdColumn = foreignKeyTable.GetColumnInfo(foreignKeyColumn.ForeignKeyName);
                    foreach (Base collobj in coll)
                    {
                        if (collobj != null)
                        {
                            AddNewItemToDBContext(collobj, ci.GenericType);
                            // Setting a foreign key for the new object. It's a parent object UUID
                            foreignKeyIdColumn.Property.SetValue(collobj, obj.id);
                        }
                    }
                    continue;  
                }
                
                if (ci.TypeInfo.IsSubclassOf(typeof(Base)))
                {
                    // property
                    Base value = (Base)ci.Property.GetValue(obj);
                    if(value != null) AddNewItemToDBContext(value, ci.Type);
                    continue;
                }

            }
        }

 
        // Internal
        public void AttachToDBContextInternal<T>(T obj, Entity.EntityState state) where T : Base => AttachToDBContextInternal(obj,typeof(T),state);

        // Internal
        public void AttachToDBContextInternal(Base obj,Type type,Entity.EntityState state)
        {
            if (obj == null) throw new Exception("Cannot add to DB null object");

            if (obj.DBContext != null)
            {
                if (obj.DBContext.Id != Id)
                {
                    throw new Exception("DbContext cannot be changed for object <" + this + "> because another context set already");
                }
                else
                {
                    throw new Exception("Object in context already");
                }
            }
            obj.DBContext = this;

            UUID id = obj.id;
            if (Entities.ContainsKey(id)) throw new Exception("Cannot add entity <"+obj+"> to DbContext because it already over there");

            Entity entity = new Entity
            {
                Obj = new WeakReference<Base>(obj),
                Type = type,
                Order = Entities.Count,
                State = state
            };
            if(state == Entity.EntityState.Added || state == Entity.EntityState.Deleted || state == Entity.EntityState.Modified)
            {
                entity.HardReference = obj;
            }
            Entities.Add(id, entity);
        }

        // for example condititon  "code = 'RUS'"
        public T FirstOrDefault<T>(string condition = null) where T : Base
        {
            string sql = "SELECT id FROM " + DBschema.GetTable<T>().SqlName;
            if (!string.IsNullOrEmpty(condition)) sql += " WHERE " + condition;
            sql += " LIMIT 1";
            
            UUID id = DbConnect.ExecuteScalar<UUID>(sql);
            if (id == null) return null;
            return Find<T>(id);
        }


        public T Find<T>(UUID id) where T : Base
        {
            T obj = FindObjectInCache<T>(id);
            if(obj == null)
            {
               // try to get it from db
                obj = DbConnect.Find<T>(id);
                if(obj != null) AttachToDBContextInternal(obj,Entity.EntityState.Unchanged);
            }
            return obj;
        }

        public int Count<T>(string condition = null) where T : Base
        {
            QueryBuilder qb = new QueryBuilder(DbConnect);
            qb.AddField("Count(*)");
            qb.AddFrom<T>();
            qb.AddWhere(condition);
            return DbConnect.ExecuteScalar<int>(qb.Sql());
        }

        public List<T> Query<T>(QueryBuilder qb) where T : Base
        {
            qb.Select = "id";
            qb.AddFrom<T>();
            List<UUID> idlst = DbConnect.Query<UUID>(qb.Sql());
            List<T> lst = new List<T>();
            foreach (UUID id in idlst)
            {
                lst.Add(Find<T>(id));
            }
            return lst;
        }

        public void Delete<T>(T obj) where T : Base => Delete(obj, typeof(T));

        public void Delete(Base obj,Type type)
        {
            Entity entity;
            if (!Entities.TryGetValue(obj.id, out entity)) throw new Exception("Delete : Can't find an object in the cache");
            if (entity.State == Entity.EntityState.Deleted) return;

            entity.State = Entity.EntityState.Deleted;
            entity.HardReference = obj;

            TableInfo ti = DBschema.GetTable(type);

            // Updates owners's collection
            foreach (ColumnInfo ci in ti.Columns.Where(x => x.Value.ForeignKeyAttribute).Select(x => x.Value).ToList())
            {
                ci.Property.SetValue(obj,null);
            }

            // Cascade delete
            foreach (ColumnInfo ci in ti.Columns.Where(x => x.Value.CascadeDeleteAttribute).Select(x => x.Value).ToList())
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
