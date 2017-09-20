using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DM.SyncModule.Services;
using SqlBulkUpsert;

namespace DM.SyncModule.Implementations.DataSave
{
    public class SqlServerBulkSave : IDataSaveService
    {
        public void SaveDataOnTarget(string targetDbConnection, params DataTable[] dataToUpsert)
        {
            using (var conection = new SqlConnection(targetDbConnection))
            {
                conection.Open();
                var upserter = new DataTableUpserter();

                var syncData = dataToUpsert.Select(dt => 
                    new DataTableMetadata(dt, "Id", dt.TableName)).ToArray();

                upserter.Upsert(conection, syncData);

            }
        }
    }
}
