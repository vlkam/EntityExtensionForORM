
using System.Text;

namespace EntityExtensionForORM
{
    public class QueryBuilder
    {
        DbConnect DbConnect;
         
        //public StringBuilder sql;
        public string Select;
        public string From;
        public string Where;
        public string Order;
        public string Limit;

        public QueryBuilder(DbConnect dbconnect)
        {
            DbConnect = dbconnect;
        }

        public QueryBuilder(DbContext dbcontext)
        {
            DbConnect = dbcontext.DbConnect;
        }


        public void AddField(string fields) => Select = Select == null ? "" : "," + fields;
        
        public void AddFrom<T>() where T : Base =>  From = DbConnect.DBschema.GetTable<T>().SqlName;

        public void AddWhere(string where) {
            if (where == null) return;
            Where = Where == null ? "" : " AND " + where;
        }

        public void AddWhere(string where, string param) => AddWhere(where + " = '"+param+"'");

        public void AddOrder(string order) => Order = order;

        public void AddLimit(string limit) => Limit = limit;

        public string Sql()
        {
            StringBuilder sql = new StringBuilder(1000);
            sql.Append("SELECT ");
            sql.Append(Select);
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
            if (!string.IsNullOrEmpty(Limit))
            {
                sql.Append(Limit);
            }
            return sql.ToString();
        }
    }
}
