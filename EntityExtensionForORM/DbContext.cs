
using SQLite.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EntityExtensionForORM
{

    public  class  DbContext 
    {
        DbConnect DbConnect;
        public DBschema DBschema; 
        public SQLiteConnection GetConnectionForTestOnly() { return DbConnect; }
        
        public Dictionary<UUID,Entity> Entities = new Dictionary<UUID, Entity>();
        public Guid Id = Guid.NewGuid();

        //List<Entity> Changes => entities.Values.Where(x => x.State != Entity.EntityState.Unchanged).ToList();
        // For debug purpose
        List<Entity> ModifiedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Modified).Select(x=>x.Value).ToList(); } }
        List<Entity> AddedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Added).Select(x=>x.Value).ToList(); } }
        List<Entity> DeletedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Deleted).Select(x=>x.Value).ToList(); } }
        //List<Entity> ChangedEntities { get { return Entities.Where(x => x.Value.State == Entity.EntityState.Deleted || x.Value.State == Entity.EntityState.Added || x.Value.State == Entity.EntityState.Detached || x.Value.State == Entity.EntityState.Modified).Select(x=>x.Value).ToList(); } }

        public DbContext(DbConnect DbConnect_)
        {
            DbConnect = DbConnect_;
            DBschema = DbConnect.DBschema;
            DbConnect.Contexts.Add(new WeakReference<DbContext>(this));
        }

        public T FindObjectInCache<T>(UUID id) where T : Base
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

        public void SynchronizeContexts()
        {
            foreach(WeakReference<DbContext> refctx in DbConnect.Contexts)
            {
                DbContext ctx;
                if (!refctx.TryGetTarget(out ctx)) continue;
                if (ctx.Id == this.Id) continue;

                foreach(Entity en in Entities.Values.Where(
                    x=>x.PreviousState == Entity.EntityState.Modified || x.PreviousState ==Entity.EntityState.Added || x.PreviousState == Entity.EntityState.Deleted))
                {
                    Base source_obj;
                    if (!en.Obj.TryGetTarget(out source_obj)) throw new Exception("Can't get a weak reference for synchronize contexts");

                    if(en.PreviousState == Entity.EntityState.Modified)
                    {
                        Entity dest_ent;
                        if (!ctx.Entities.TryGetValue(source_obj.id, out dest_ent)) continue;
                        Base dest_obj;
                        if (!dest_ent.Obj.TryGetTarget(out dest_obj)) continue;

                        dest_obj.SynchronizeWith(source_obj,en.Type);
                    }

                    if(en.PreviousState == Entity.EntityState.Added || en.PreviousState == Entity.EntityState.Deleted)
                    {
                        TableInfo ti = DbConnect.DBschema.GetTable(en.Type);
                        foreach (ColumnInfo ci in ti.Columns.Values)
                        {
                            if (!ci.ForeignKeyAttribute) continue;

                            // Owner's column and table
                            TableInfo owner_ti = DBschema.GetTable(ci.Type);
                            ColumnInfo owner_column = owner_ti.Columns.Values.FirstOrDefault(x=>x.InversePropertyName == ci.ClrName);
                            if (owner_column == null) continue;

                            // Owner's UUID
                            PropertyInfo pi = ti.TypeInfo.GetDeclaredProperty(ci.ClrName);
                            ColumnInfo fk_ci = ti.GetColumnInfo(ci.ForeignKeyName);
                            UUID owner_id = (UUID)fk_ci.Property.GetValue(source_obj);

                            // Owner's object
                            Entity dest_ent = ctx.FindEntityInCache(owner_id);
                            Base owner;
                            if (!dest_ent.Obj.TryGetTarget(out owner)) continue;

                            // Owner's collection
                            IList owner_collection = ((IList)owner_column.Property.GetValue(owner));
                            var owner_collection_typed = owner_collection.Cast<Base>();
                            if (owner_collection == null) continue;

                            switch (en.PreviousState)
                            {
                                case Entity.EntityState.Added:
                                    if(!owner_collection_typed.Any(x=>x.id == source_obj.id))
                                    {
                                        // Creates a new copy of source object for add to another context
                                        Base new_obj = (Base)Activator.CreateInstance(ti.Type);
                                        foreach(ColumnInfo tcli in ti.Columns.Values) {
                                            if (tcli.IgnoreAttribute) continue;
                                            tcli.Property.SetValue(new_obj,tcli.Property.GetValue(source_obj));
                                        }
                                        owner_collection.Add(new_obj);
                                    }
                                    break;

                                case Entity.EntityState.Deleted:
                                    Base old_obj = owner_collection_typed.FirstOrDefault(x => x.id == source_obj.id);
                                    if (old_obj != null)
                                    {
                                        owner_collection.Remove(old_obj);
                                    }
                                    break;
                            }

                            //if(en.PreviousState == Entity)
                            //if(cc.)


                            //IEnumerable collection =

                            Base ForeignKeyValue = (Base)pi.GetValue(source_obj);
                            //int i = 5;
                            source_obj.GetType().GetRuntimeProperty("User");
                            
                        }
                        
                    }
                }
            }
        }

        public void SaveChanges(bool synchronazeContexts = false)
        {
            foreach(var keyval in Entities)
            {
                Entity entity = keyval.Value;
                if (entity.State == Entity.EntityState.Unchanged) continue;
                /*
                WeakReference<Base> refer = entity.Obj;
                Base obj;
                if(!refer.TryGetTarget(out obj))
                {
                    throw new Exception("Weakreference " + refer + " cannot be resolve. Order: " + entity.Order + " Type : " + entity.Type);
                }
                */
                //Base obj = entity.HardReference;
                switch (entity.State)
                {
                    case Entity.EntityState.Added:
                    case Entity.EntityState.Modified:
                        DbConnect.InsertOrReplace(entity.HardReference, entity.Type);
                        entity.PreviousState = entity.State;
                        entity.IsNeedSynchronize = true;
                        entity.State = Entity.EntityState.Unchanged;
                        entity.HardReference = null;
                        break;
                    case Entity.EntityState.Deleted:
                        DbConnect.Delete(entity.HardReference);
                        entity.PreviousState = entity.State;
                        entity.IsNeedSynchronize = true;
                        entity.HardReference = null;
                        break;
                    default:
                        throw new Exception("Unknown operation type "+entity.State);
                }
            }
            if (synchronazeContexts) SynchronizeContexts();
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

            // Debug
            if(type.ToString() == "OpenLearningPlayer.Shared.Domain.LearnedWord")
            {
                int dfd = 9;
            }

            if (obj.DBContext != null)
            {
                if (obj.DBContext.Id != Id)
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

            //if(id.guid.ToString() == "")
            //{
            //    int i = 8;
            //}

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
            if (entity.State == Entity.EntityState.Deleted) return;

            entity.State = Entity.EntityState.Deleted;
            entity.HardReference = obj;

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
