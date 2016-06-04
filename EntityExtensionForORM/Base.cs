
using SQLite.Net.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EntityExtensionForORM
{
    public class Base : INotifyPropertyChanged
    {
        
        [PrimaryKey]        
        public UUID id { get { return Get(ref id_); } set { Set(ref id_, value); } }
        private UUID id_;

        public DbContext DBContext;

        bool isSynchronization = false;

        List<CollectionInfo> Collections = new List<CollectionInfo>();

        public event PropertyChangedEventHandler PropertyChanged;

        public Base()
        {
            id_ = new UUID();
        }

        public void SynchronizeWith<T>(T obj) where T : Base => SynchronizeWith(obj, typeof(T));

        public void SynchronizeWith(Base obj,Type type)
        {
            isSynchronization = true;
            TableInfo ti;
            if (!DBContext.DBschema.Tables.TryGetValue(type, out ti)) throw new Exception("Table for type " + type + " not found");
            foreach(ColumnInfo ci in ti.Columns.Values)
            {
                if (ci.IgnoreAttibute) continue;
                ci.Property.SetValue(this,ci.Property.GetValue(obj));
            }
            isSynchronization = false;
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void OnPropertyChanged<T>(Expression<Func<T>> propertyExpression)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                var propertyName = GetPropertyName(propertyExpression);
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
        {
            if (propertyExpression == null)
            {
                throw new ArgumentNullException("propertyExpression");
            }

            var body = propertyExpression.Body as MemberExpression;

            if (body == null)
            {
                throw new ArgumentException("Invalid argument", "propertyExpression");
            }

            var property = body.Member as PropertyInfo;

            if (property == null)
            {
                throw new ArgumentException("Argument is not a property", "propertyExpression");
            }

            return property.Name;
        }

        private void Modified()
        {
            if (DBContext != null && !isSynchronization)
            {
                DBContext.RegisterChange(id, this);
            }
        }

#region Base type properties Get/Set (string, bool, int etc...)

        protected bool Set<T>(ref T field, T value,[CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            Modified();
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected T Get<T>(ref T field,[CallerMemberName] string propertyName = null)
        {
            return field;
        }
        
        /*
        private void AddedRef<T>(Guid id,T obj) where T : Base
        {
            db.InsertOrReplace(id, obj);
            //Guid id = ChangeTracker.connect.
            //bool t = ChangeTracker.connect.  .Find<T>(id); 
        }
        
             */
#endregion 

#region Reference Get/Set
        protected bool SetEntityId<T>(ref T idField_, T newid,[CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(idField_,newid)) return false;
            Modified();
            idField_ = newid;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(propertyName.Replace("_id",""));

            // ? Try to get from DB 
            return true;
        }

        protected T GetEntityId<T>(ref T idField_,[CallerMemberName] string propertyName = null) {
            return idField_;
        }

        protected bool SetEntity<T>(ref T refField_,ref UUID idField_, T newRef,[CallerMemberName] string propertyName = null) where T : Base
        {
            if (EqualityComparer<T>.Default.Equals(refField_, newRef)) return false;
            if(newRef != null)
            {
                Modified();
                idField_ = newRef.id;
            } else
            {
                idField_ = null;
            }
            refField_ = newRef;

            // Is it new object
            if (newRef != null && newRef.DBContext == null) DBContext.AddNewItemToDBContext(newRef);

            OnPropertyChanged(propertyName);
            OnPropertyChanged(propertyName + "_id");
            return true;
        }

        protected T GetEntity<T>(ref T Entity,ref UUID id,[CallerMemberName] string propertyName = null) where T : Base
        {
            if (DBContext == null) return Entity;

            //if(id == Guid.Empty || id == null)
            if(id == null)
            {
                if(Entity != null)
                {
                    throw new Exception("GetEntity : Value of entity isn't null but Id field of entity is null. Entity<"+this+">");
                } else
                {
                    return null;
                }
            }

            if (Entity != null)
            {
                if (Entity.id != id)
                {
                    throw new Exception("GetEntity : mismatched id");
                } else
                {
                    return Entity;
                }
            }

            // Context lazy loading
            T obj = DBContext.Find<T>(id);
            return obj;
        }
#endregion

#region Collections
        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DBContext != null)
            {
                CollectionInfo collection_info = Collections.FirstOrDefault(x => x.Collection == sender);
                if (collection_info == null) throw new Exception("Collection not initialized");
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach(Base obj in e.NewItems)
                        {
                            if (obj.DBContext == null) DBContext.AddNewItemToDBContext(obj,collection_info.DependentTableInfo.Type);
                            PropertyInfo prop = collection_info.KeyIdProperytInfo.Property;//collection_info.CollectionItemTypeInfo.GetDeclaredProperty(collection_info.InversePropertyId);
                            UUID old_id = (UUID)prop.GetValue(obj);
                            if(old_id != this.id)
                            {
                                obj.Modified();
                                //obj.id = this.id;
                                prop.SetValue(obj, this.id);
                            }
                        }
                        break;
                    
                        
                    case NotifyCollectionChangedAction.Remove:
                        foreach(Base obj in e.OldItems)
                        {
                            if (obj.DBContext == null) throw new Exception ("A collection has an item which hasn't the DBContext");
                            if (collection_info.KeyIdProperytInfo.IsNullable)
                            {
                                collection_info.KeyIdProperytInfo.Property.SetValue(obj, null);
                            }

                            if (collection_info.InversePropertyInfo.CascadeDeleteAttribute)
                            {
                                DBContext.Delete(obj, collection_info.InversePropertyInfo.Type);
                            }
                        }
                        break;
                        
                    case NotifyCollectionChangedAction.Move:
                        break;
                    default:
                        throw new Exception("CollectionChanged: Operation "+e.Action+" not permitted");
                }
            }
        }

        private CollectionInfo InitiliazeCollection<T>(T collection,string propertyName) where T : INotifyCollectionChanged
        {

            if (DBContext == null) throw new Exception("DbContext for "+this+" is null, collection can't be initialized");
            if (collection == null) throw new Exception("Collection is null");

            CollectionInfo ci = new CollectionInfo();
            ci.Collection = collection;
            ci.isLoadedFromDB = false;

            // A table and a property of current object
            Type this_type = this.GetType();
            ci.MasterTableInfo = DBContext.DBschema.GetTable(this_type);
            if (ci.MasterTableInfo == null) throw new Exception("Can't initialize collection. Master table info not found");
            ci.InversePropertyInfo = ci.MasterTableInfo.GetColumnInfo(propertyName);
            if (ci.InversePropertyInfo == null) throw new Exception("Can't initialize collection. Inverse property info not found");

            // The dependent table 
            Type typeofcollection = collection.GetType();
            Type generic_type_of_collection = typeofcollection.GenericTypeArguments[0];
            ci.DependentTableInfo = DBContext.DBschema.GetTable(generic_type_of_collection);
            if (ci.DependentTableInfo == null) throw new Exception("Can't initialize collection. Dependent table info not found");

            // The key property
            TypeInfo this_type_info = this_type.GetTypeInfo();
            PropertyInfo this_prop = this_type_info.GetDeclaredProperty(propertyName);
            InversePropertyAttribute inverse_property = this_prop.GetCustomAttribute<InversePropertyAttribute>();
            ci.KeyPropertyInfo = ci.DependentTableInfo.GetColumnInfo(inverse_property.PropertyName);
            if (ci.KeyPropertyInfo == null) throw new Exception("Can't initialize collection. Key property info not found");

            // The key id property
            TypeInfo generic_type_of_collection_type_info = generic_type_of_collection.GetTypeInfo();
            PropertyInfo generic_type_property = generic_type_of_collection_type_info.GetDeclaredProperty(ci.KeyPropertyInfo.ClrName);
            ForeignKeyAttribute foreignKeyAttribute = generic_type_property.GetCustomAttribute<ForeignKeyAttribute>();
            ci.KeyIdProperytInfo = ci.DependentTableInfo.GetColumnInfo(foreignKeyAttribute.ForeignKeyName);
            if (ci.KeyIdProperytInfo == null) throw new Exception("Can't initialize collection. Key id property info not found");

            collection.CollectionChanged += CollectionChanged;
            Collections.Add(ci);

            return ci;
        }

        public bool SetCollection<T>(ref T CollectionField_, T value, [CallerMemberName] string propertyName = null) where T : INotifyCollectionChanged
        {
            if(DBContext != null)
            {
                if (CollectionField_ != null)
                {
                    object OldCollection = (object)CollectionField_;
                    CollectionInfo item = Collections.FirstOrDefault(x=>x.Collection == OldCollection);
                    if (item != null)
                    {
                        CollectionField_.CollectionChanged -= CollectionChanged;
                        Collections.Remove(item);
                    }
                }
                CollectionField_ = value;
                if (CollectionField_ != null)
                {
                    InitiliazeCollection(CollectionField_, propertyName);
                }
            } else
            {
                if (EqualityComparer<T>.Default.Equals(CollectionField_, value)) return false;
                CollectionField_ = value;
            }

            OnPropertyChanged(propertyName);
            return true;
        }

        public T GetCollection<T>(ref T CollectionField_,[CallerMemberName] string propertyName = null) where T : INotifyCollectionChanged,IList, new()
        {
            if (DBContext != null)
            {
                object objCollection = (object)CollectionField_;
                CollectionInfo item = Collections.FirstOrDefault(x=>x.Collection == objCollection);
                if(item == null){
                    item = InitiliazeCollection(CollectionField_, propertyName);
                }
                if (item == null) throw new Exception(propertyName + " collection isn't initialized for lazy loading.");
                if(!item.isLoadedFromDB)
                {
                    SQLite.Net.TableMapping tablemapping = new SQLite.Net.TableMapping(item.DependentTableInfo.Type,item.DependentTableInfo.Type.GetRuntimeProperties());
                    string sql = "select * from " + item.DependentTableInfo.SqlName + " where "+item.KeyIdProperytInfo.SqlName + " = ?";

                    List<object> items = DBContext.GetConnectionForTestOnly().Query(tablemapping,sql,this.id);
                    foreach(Base coll_item in items)
                    {
                        DBContext.AttachToDBContext(coll_item, item.DependentTableInfo.Type,Entity.EntityState.Unchanged);
                        CollectionField_.Add(Convert.ChangeType(coll_item,item.DependentTableInfo.Type));
                        //DBContext.AttachToDBContext(Convert.ChangeType(coll_item, item.CollectionItemType),Entity.EntityState.Unchanged);
                    }

                    item.isLoadedFromDB = true;
                }
            }
            return CollectionField_;
        }

#endregion

        protected T Get<T>(ref T field,T value,[CallerMemberName] string propertyName = null)
        {
            
            if(propertyName == "UserType" && value == null)
            {
                value = Activator.CreateInstance<T>();
            }
            
            return value;
        }


        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (!(obj is Base)) return false;
            return ((Base)obj).id == this.id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

    }
}
