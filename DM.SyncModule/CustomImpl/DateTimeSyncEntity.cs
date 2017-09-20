using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Metadata.Edm;
using Neolant.MBM.Database;
using Neolant.MBM.SyncModule;
using Neolant.MBM.SyncDescriptor.ExcludeElementsInfo;
using Neolant.MBM.SyncDescriptor.DbTypes;

namespace Neolant.MBM.ControlPanel.CustomImplementation
{

    /// <summary>
    /// Класс сущностей для синхронизации
    /// содержит специфичную для проекта логику синхронизации  
    /// </summary>
    public class DateTimeSyncEntity : BaseSyncEntity
    {
        /// <summary>
        /// Формат даты для запросов
        /// </summary>
        private const string cSqlDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        /// <summary>
        /// Интервал выгрузки: дата начала
        /// </summary>
        private DateTime? mFrom;
        /// <summary>
        /// Интервал выгрузки: дата конца
        /// </summary>
        private DateTime? mTo;
        /// <summary>
        /// Тип сущности EF
        /// </summary>
        private readonly EntityType mEntity;
        /// <summary>
        /// t = цбд/f = абд
        /// </summary>
        private bool mIsCentralDb;
        /// <summary>
        /// Правила выгрузки данных
        /// </summary>
        private List<ColumnExcludeInfo> mSyncRulesColumns;


        /// <summary>
        /// ctor 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public DateTimeSyncEntity(EntityType entity, DateTime? from, DateTime? to) :
            base(entity)
        {
            mFrom = from;
            mTo = to;
            mEntity = entity;
        }

        /// <summary>
        /// Ctor: load data on init  
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="syncContext"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public DateTimeSyncEntity(EntityType entity, IContext syncContext, DateTime? from, DateTime? to) :
            base(entity, syncContext)
        {
            mFrom = from;
            mTo = to;
            mEntity = entity;
        }

        /// <summary>
        /// Ctor: filtering columns
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="isCentralDb"></param>
        /// <param name="syncRulesColumns"></param>
        public DateTimeSyncEntity(EntityType entity, DateTime? from, DateTime? to, bool isCentralDb, List<ColumnExcludeInfo> syncRulesColumns)
            : base(entity)
        {
            mFrom = from;
            mTo = to;
            mEntity = entity;

            mIsCentralDb = isCentralDb;
            mSyncRulesColumns = syncRulesColumns;
        }

        /// <summary>
        /// Здесь специфичная для проекта логика
        /// фильтраци данных, а именно: синхронизация от даты изменения
        /// выгрузка из цбд текущих назначений и вакантных назначений
        /// </summary>
        /// <returns></returns>
        protected override string GetCondition()
        {
            var dateCondition = mTo == null ? " UpdateDate >= CAST('{0}' AS DATETIME2) " : 
                " UpdateDate >= CAST('{0}' AS DATETIME2) And UpdateDate <=  CAST('{1}' AS DATETIME2)";

            string resultCondition = null;

            if ((mFrom == null && mTo == null) || !mEntity.Properties.Contains("UpdateDate"))
            {
                resultCondition = null;
            }
            else
            {
                if (resultCondition != null && mFrom == null)
                    dateCondition = "UpdateDate <= CAST('{1}' AS DATETIME2)";

                resultCondition = string.Format(dateCondition, mFrom == null ? string.Empty : mFrom.Value.ToString(cSqlDateTimeFormat),
                    mTo == null ? string.Empty : mTo.Value.ToString(cSqlDateTimeFormat));

            }



            if (string.Equals(mEntity.Name, "personposition", StringComparison.InvariantCultureIgnoreCase) && mIsCentralDb)
                resultCondition = string.Format(string.Format("{0} ( IsActual = 'true' Or PersonId IS NULL )",
                    string.IsNullOrEmpty(resultCondition) ? "" : "{0} and"), resultCondition);


            return resultCondition;
        }

        /// <summary>
        /// Врозращает таблицу с данными, которую 
        /// отфильтровали в сооветствии с правилами синхронизации  
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="allData"></param>
        /// <returns></returns>
        public override DataTable GetSyncData(IContext dbContext, bool allData = false)
        {
            var datatable = base.GetSyncData(dbContext, allData);

            ExcludeColumns(datatable);

            var datetimeCols = new List<DataColumn>();

            if (allData) return datatable;

            foreach (DataColumn col in datatable.Columns)
            {
                if (col.DataType != typeof(DateTime)) continue;

                datetimeCols.Add(col);
            }

            foreach (DataRow row in datatable.Rows)
            {
                foreach (var col in datetimeCols)
                {
                    if (row[col.ColumnName] == DBNull.Value) continue;

                    var date = Convert.ToDateTime(row[col.ColumnName]);

                    row[col.ColumnName] = date.ToUniversalTime();
                }
            }
            return datatable;
        }

        /// <summary>
        /// Исключает столбцы в соответствии с правилами
        /// </summary>
        /// <param name="dataTable"></param>
        private void ExcludeColumns(DataTable dataTable)
        {
            var columnsSyncRules = mSyncRulesColumns;
            if (columnsSyncRules == null)
                return;

            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                var tableName = dataTable.TableName.Replace("[", "").Replace("]", "").Replace(".", "").Replace("dbo", "");

                var column = dataTable.Columns[i];

                foreach (var columnSync in columnsSyncRules)
                {

                    if (!tableName.Equals(columnSync.NameTable, StringComparison.InvariantCultureIgnoreCase) ||
                        !column.ColumnName.Equals(columnSync.Name, StringComparison.InvariantCultureIgnoreCase)) continue;


                    //если правило действует для любого типа сегмента
                    if (columnSync.DbType == SyncDbType.Any || columnSync.DbType == SyncDbType.AnyWithLoad)
                    {
                        ExcludeChooseColumn(column, dataTable);
                        continue;
                    }
                    //если тип правила совпадает с типом сегмента
                    if ((mIsCentralDb && columnSync.DbType == SyncDbType.CentralDb) ||
                        (!mIsCentralDb && columnSync.DbType == SyncDbType.AbonentDb))
                    {
                        ExcludeChooseColumn(column, dataTable);
                    }

                }
            }

        }

        /// <summary>
        /// Исключение выбранного столбца из синхронизации
        /// </summary>
        /// <param name="column"></param>
        /// <param name="dataTable"></param>
        private void ExcludeChooseColumn(DataColumn column, DataTable dataTable)
        {
            dataTable.Columns.Remove(column);
        }
    }
}
