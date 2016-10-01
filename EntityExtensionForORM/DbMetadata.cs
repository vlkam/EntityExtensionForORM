
using SQLite.Net.Attributes;

namespace EntityExtensionForORM
{

    [Table(nameof(DbMetadata))]
    public class DbMetadata
    {
        public const string TableName = "DbMetadata";

        public int? Version { get; set; }
    }
}
