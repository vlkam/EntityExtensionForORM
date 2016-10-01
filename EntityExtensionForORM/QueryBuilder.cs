
using System.Text;
using System.Reflection;

namespace EntityExtensionForORM
{
    public class QueryBuilder<T> where T : Base
    {
        private DbConnect DbConnect;

        public string Select;
        public string From;
        public string Where;
        public string Order;
        public int Limit;

        public QueryBuilder(DbContext dbcontext) : this(dbcontext.DbConnect) { }

        public QueryBuilder(DbConnect dbconnect)
        {
            DbConnect = dbconnect;
            if (typeof(T).GetTypeInfo().IsSubclassOf(typeof(Base)))
            {
                From = DbConnect.DBschema.GetTable<T>().SqlName;
            }
        }

        public void AddField(string fields) => Select = string.IsNullOrEmpty(Select) ? "" : "," + fields;
        
        public void AddWhere(string where) => Where = string.IsNullOrEmpty(Where) ? "" : " AND " + where;

        public void AddWhere(string where, string param) => AddWhere($"{where} = '{param}'");

        public string Sql()
        {
            StringBuilder sql = new StringBuilder(1000);
            sql.Append("SELECT ");
            sql.Append(string.IsNullOrEmpty(Select) ? "*": Select);
            sql.Append(" FROM ");
            sql.Append(From);
            if (!string.IsNullOrEmpty(Where))
            {
                sql.Append(" WHERE ");
                sql.Append(Where);
            }
            if (!string.IsNullOrEmpty(Order))
            {
                sql.Append(" ORDER BY ");
                sql.Append(Order);
            }
            if (Limit > 0 )
            {
                sql.Append(" LIMIT ");
                sql.Append(Limit.ToString());
            }
            return sql.ToString();
        }
    }
}
