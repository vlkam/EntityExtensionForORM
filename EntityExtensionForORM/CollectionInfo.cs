
namespace EntityExtensionForORM
{
    public class CollectionInfo {

        public object Collection;
        public bool isLoadedFromDB;

        public TableInfo    MasterTableInfo;
        public TableInfo    DependentTableInfo;

        public ColumnInfo   InversePropertyInfo;
        public ColumnInfo   KeyPropertyInfo;
        public ColumnInfo   KeyIdProperytInfo;

    }
}
