using Microsoft.Data.SqlClient;
using System.Data;

namespace Resource_Management_System.Data
{
    /// <summary>
    /// Single point of contact between the app and SQL Server.
    /// All database access goes through this class — controllers never
    /// open a SqlConnection directly.
    /// </summary>
    public class DbHelper
    {
        private readonly string _connectionString;

        public DbHelper(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("PaperDb")
                ?? throw new InvalidOperationException("Connection string 'PaperDb' not found in appsettings.json");
        }

        /// <summary>
        /// Runs a stored procedure or SQL query that returns a single DataTable.
        /// </summary>
        public DataTable ExecuteQuery(string procName, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(procName, conn) { CommandType = GetCommandType(procName) };
            if (parameters != null && parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            var table = new DataTable();
            conn.Open();
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// Runs a stored procedure or SQL query that returns multiple DataTables (DataSet).
        /// </summary>
        public DataSet ExecuteDataSet(string procName, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(procName, conn) { CommandType = GetCommandType(procName) };
            if (parameters != null && parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            var ds = new DataSet();
            conn.Open();
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(ds);
            return ds;
        }

        /// <summary>
        /// Runs a stored procedure or SQL query that performs an action (INSERT/UPDATE)
        /// and doesn't return rows, e.g. sp_ApproveRequest.
        /// </summary>
        public void ExecuteNonQuery(string procName, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(procName, conn) { CommandType = GetCommandType(procName) };
            if (parameters != null && parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        private static CommandType GetCommandType(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return CommandType.StoredProcedure;

            var trimmed = commandText.Trim();
            if (trimmed.Contains(' ') || trimmed.Contains('\r') || trimmed.Contains('\n') || trimmed.Contains('\t'))
                return CommandType.Text;

            return CommandType.StoredProcedure;
        }
    }
}