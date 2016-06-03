
using SQLite.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EntityExtensionForORM
{

    public  class  DbContext 
    {
        DbConnect DbConnect;
        public DBschema DBschema; 
        public SQLiteConnection GetConnectionForTestOnly() { return DbConnect; }
        
        public Dictionary<UUID,Entity> Entities = new Dictionary<UUID, Entity>();
        public Guid ContextId = Guid.NewGuid();

        //List<Entity> Changes => entities.Values.Where(x => x.State != Entity.EntityState.Unchanged).ToList();

        public DbContext(DbConnect DbConnect_)
        {
            DbConnect = DbConnect_;
            DBschema = DbConnect.DBschema;
        }

        public T FindObjectInCache<T>(UUID id) where T : Base
        {
            Entity entity;
            if (!Entities.TryGetValue(id,out entity)) return null;

            Base obj;
            if(!entity.Obj.TryGetTarget(out obj))
            {
                throw new Exception("FindObjectInCache : Cannot resolve a weakreference");
            }
            return (T)obj;
        }


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
            Entities.Clear();
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
            Entities.Clear();
        }

        public void SaveChanges()
        {
            foreach(var keyval in Entities)
            {
                var value = keyval.Value;
                WeakReference<Base> refer = value.Obj;
                Base obj;
                if(!refer.TryGetTarget(out obj))
                {
                    throw new Exception("Weakreference " + refer + " cannot be resolve. Order: " + value.Order + " Type : " + value.Type);
                }

                switch (value.State)
                {
                    case Entity.EntityState.Added:
                    case Entity.EntityState.Modified:
                        DbConnect.InsertOrReplace(obj, value.Type);
                        keyval.Value.State = Entity.EntityState.Unchanged;
                        break;
                    case Entity.EntityState.Deleted:
                        DbConnect.Delete(obj);
                        break;
                    case Entity.EntityState.Unchanged:
                        break;
                    default:
                        throw new Exception("Uknown operation type "+value.State);
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
            if (Entities.ContainsKey(id)) throw new Exception("Cannot add entity <"+obj+"> to DbContext because it already over there");

            Entity entity = new Entity
            {
                Obj = new WeakReference<Base>(obj),
                Type = type,
                Order = Entities.Count,
                State = state
            };
            Entities.Add(id, entity);
        }

        public void Insert<T>(T obj) where T: Base
        {
            DbConnect.Insert(obj);
            Entity entity;
            if (!Entities.TryGetValue(obj.id, out entity)) AttachToDBContext(obj,Entity.EntityState.Unchanged);
        }

        public T FirstOrDefault<T>(Func<T,bool> predicate,bool attachToContext = true) where T : Base
        {
            T obj = DbConnect.Table<T>().FirstOrDefault(predicate);
            if (obj != null && attachToContext) AttachToDBContext(obj, Entity.EntityState.Unchanged);
            return obj;
        }

        public List<T> Set<T>(bool attachToContext = false) where T : Base
        {
            List<T> lst = DbConnect.Table<T>().ToList();
            if (attachToContext)
            {
                foreach(var elm in lst)
                {
                    AttachToDBContext(elm, Entity.EntityState.Unchanged);
                }
            }
            return lst;
        }

        public T Find<T>(UUID id,bool AttachToContext = true) where T : Base
        {
            Entity entity = FindEntityInCache(id);

            T obj = FindObjectInCache<T>(id);
            if(obj == null)
            {
               // try to get it from db
                obj = DbConnect.Find<T>(id);
            }

            if(AttachToContext && obj != null)
            {
                AttachToDBContext(obj,Entity.EntityState.Unchanged);
            }
            return obj;
        }

        public T First<T>() where T : Base
        {
            T obj = DbConnect.Table<T>().FirstOrDefault();
            if (obj == null) return null;

            T objc = FindObjectInCache<T>(obj.id);
            if (objc != null) return objc;

            AttachToDBContext(obj,Entity.EntityState.Unchanged);
            return obj;
        }

        public T FirstOrDefault<T>() where T : Base
        {
            return First<T>();
        }

        public bool ObjectExists<T>(T obj) where T : Base
        {
            return DbConnect.Find<T>(obj.id) != null;
        }

        public TableQuery<T> Table<T>() where T : Base
        {
            return DbConnect.Table<T>();
        }

        public void Delete<T>(T obj) where T : Base
        {
            Delete(obj, typeof(T));
        }

        public void Delete(Base obj,Type type)
        {
            Entity entity;
            if (!Entities.TryGetValue(obj.id, out entity)) throw new Exception("Delete : Can't find an object in the cache");
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
