using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace DM.SyncModule.Extenssions
{
    public static class DatatableExtenssions
    {
        /// <summary>
        /// BulkSave for SerializedDataTable
        /// </summary>
        /// <param name="table"></param>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        public static void BulkSaveDataTable(this DataTable table, string connectionString, string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var bulkCopy =
                    new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.UseInternalTransaction, null)
                    {
                        DestinationTableName = tableName,
                        BatchSize = 1000

                    };

                connection.Open();

                bulkCopy.WriteToServer(table);

                connection.Close();
            }
        }
    }
}
