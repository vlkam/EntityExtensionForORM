﻿
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
        enum CollectionOperations { Add, Remove }

        [PrimaryKey]        
        public UUID id {
            get { return Get(ref id_); }
            set { Set(ref id_, value); } }
        private UUID id_;

        public DbContext DBContext;

        protected List<CollectionInfo> Collections = new List<CollectionInfo>();

        public Base()
        {
            id_ = new UUID();
        }

        // INotifyPropertyChanged
        readonly WeakEventSource<PropertyChangedEventArgs> _propertyChangedSource = new WeakEventSource<PropertyChangedEventArgs>();
        public event PropertyChangedEventHandler PropertyChanged
        {
            add { _propertyChangedSource.Subscribe(value); }
            remove { _propertyChangedSource.Unsubscribe(value); }
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            _propertyChangedSource.Raise(this,new PropertyChangedEventArgs(propertyName));
        }

        // ReSharper disable once ExplicitCallerInfoArgument
        public virtual void OnPropertyChanged<T>(Expression<Func<T>> propertyExpression) => OnPropertyChanged(GetPropertyName(propertyExpression));

        public bool IsAttachToContext() => DBContext != null;

        public void RefreshFromDB() 
        {
            if (!IsAttachToContext()) throw new Exception("Object "+this+" isn't attached to context");

            TableInfo ti = DBContext.DBschema.GetTable(this.GetType());
            Base obj_fromDB = (Base)DBContext.DbConnect.Find(id,ti.TableMapping);
            
            foreach(var pair in ti.Columns)
            {
                ColumnInfo fi = pair.Value;
                if (fi.NotMapped || fi.IsPrimaryKey) continue;
                fi.Property.SetValue(this, fi.Property.GetValue(obj_fromDB));
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

        [Ignore]
        public bool IsModified {
            get { return _IsModified; }
            set {
                if(_IsModified != value)
                {
                    _IsModified = value;
                    OnPropertyChanged("IsModified");
                }
            }
        }
        bool _IsModified;

        private void Modified()
        {
            if (DBContext != null)
            {
                IsModified = true;
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
        
#endregion 

#region Enumeration properties Get/Set 

        protected bool SetEnumeration<T>(ref int? field, T value,[CallerMemberName] string propertyName = null) where T : Enumeration
        {
            if (value == null && field == null) return false;

            int? newcode = value?.Value;

            if (field == newcode) return false;
            Modified();
            field = newcode;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected T GetEnumeration<T>(ref int? field,[CallerMemberName] string propertyName = null) where T : Enumeration,new()
        {
            return field == null ? null : Enumeration.FromValue<T>((int)field);
        }

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

            // Entity hasn't been loaded. Load the entity
            if (idField_ != null && refField_ == null)
            {
                refField_ = DBContext.Find<T>(idField_);
            }

            if (EqualityComparer<T>.Default.Equals(refField_, newRef)) return false;

            T oldref = refField_;

            // sets new values
            idField_ = newRef?.id;
            refField_ = newRef;
            if (DBContext != null) Modified();

            // Removes previous object from an owner's collection
            if (DBContext != null && oldref != null) UpdateCollections(propertyName, oldref, CollectionOperations.Remove);

            // Is it a new object
            if (DBContext != null && newRef != null && newRef.DBContext == null) DBContext.AddNewItemToDBContext(newRef);

            // Adds this object to new owner's collection
            if(DBContext != null && newRef != null) UpdateCollections(propertyName,refField_,CollectionOperations.Add);

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
            //T obj = DBContext.Find<T>(id);
            //return obj;
            Entity = DBContext.Find<T>(id);
            return Entity;
        }

        void UpdateCollections<T>(string propertyName,T owner,CollectionOperations operation) where T : Base
        {
            TableInfo thisTi = DBContext.DBschema.GetTable(this.GetType());
            ColumnInfo thisCi = thisTi.GetColumnInfo(propertyName);
            if (thisCi.ForeignKeyAttribute)
            {
                //TableInfo owner_ti = DBContext.DBschema.GetTable<T>();
                //ColumnInfo owner_ci = owner_ti.Columns.Select(x=>x.Value).FirstOrDefault(x => x.InversePropertyName == propertyName);
                //if (owner_ci == null) return;

                CollectionInfo colinf = owner.Collections.FirstOrDefault(x=>x.KeyPropertyInfo == thisCi);
                if(colinf != null && colinf.isLoadedFromDB)
                {
                    IList ilist = (IList)colinf.Collection;
                    switch (operation)
                    {
                        case CollectionOperations.Add:
                            if (!ilist.Contains(this))
                            {
                                ilist.Add(this);
                            }
                            break;
                        case CollectionOperations.Remove:
                            if (ilist.Contains(this))
                            {
                                ilist.Remove(this);
                            }
                            break;
                    }
                }
                   
            }
        }
#endregion

#region Collections
        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DBContext != null)
            {
                CollectionInfo collectionInfo = Collections.FirstOrDefault(x => x.Collection == sender);
                if (collectionInfo == null) throw new Exception("Collection not initialized");

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach(Base obj in e.NewItems)
                        {
                            if (obj.DBContext == null) DBContext.AddNewItemToDBContext(obj,collectionInfo.DependentTableInfo.Type);
                            PropertyInfo prop = collectionInfo.KeyIdProperytInfo.Property;
                            UUID old_id = (UUID)prop.GetValue(obj);
                            if(old_id != this.id)
                            {
                                obj.Modified();
                                prop.SetValue(obj, this.id);
                            }
                        }
                        break;
                    
                        
                    case NotifyCollectionChangedAction.Remove:
                        foreach(Base obj in e.OldItems)
                        {
                            if (obj.DBContext == null) throw new Exception ("A collection has an item which hasn't the DBContext");
                            if (collectionInfo.KeyIdProperytInfo.IsNullable)
                            {
                                collectionInfo.KeyIdProperytInfo.Property.SetValue(obj, null);
                            }

                            if (collectionInfo.InversePropertyInfo.CascadeDeleteAttribute)
                            {
                                DBContext.Delete(obj, collectionInfo.InversePropertyInfo.GenericType);
                            }
                        }
                        break;
                        
                    case NotifyCollectionChangedAction.Move:
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        break;

                    default:
                        throw new Exception("CollectionChanged: Operation "+e.Action+" not permitted");
                }
            }
        }

        public CollectionInfo InitiliazeCollection(INotifyCollectionChanged collection,string propertyName)
        {

            if (DBContext == null) throw new Exception("DbContext for "+this+" is null, collection can't be initialized");
            if (collection == null) throw new Exception("Collection is null");

            CollectionInfo ci;

            ci = Collections.FirstOrDefault(x=>x.Collection == collection);
            if (ci != null) return ci;

            ci = new CollectionInfo();
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

                    List<object> items = DBContext.DbConnect.Query(tablemapping,sql,this.id);
                    foreach(Base coll_item in items)
                    {
                        Base objfromcache = DBContext.FindObjectInCache(item.DependentTableInfo.Type,coll_item.id);
                        if(objfromcache == null)
                        {
                            DBContext.AttachToDBContextInternal(coll_item, item.DependentTableInfo.Type,Entity.EntityState.Unchanged);
                            CollectionField_.Add(Convert.ChangeType(coll_item,item.DependentTableInfo.Type));
                        }
                        else
                        {
                            CollectionField_.Add(Convert.ChangeType(objfromcache,item.DependentTableInfo.Type));
                        }
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
            Base tobj = obj as Base;
            if (tobj == null) return false;
            return tobj.id == this.id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

    }
}
