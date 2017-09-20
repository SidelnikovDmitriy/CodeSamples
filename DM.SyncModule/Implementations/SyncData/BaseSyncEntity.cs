using System;
using System.Data;
using System.Data.Entity.Core.Metadata.Edm;
using DM.Database;

namespace DM.SyncModule
{
    public class BaseSyncEntity : ISyncEntity<DataTable>
    {
        /// <summary>
        /// Метаданные сущности
        /// </summary>
        private readonly EntityType mEntityMetadata;
        /// <summary>
        /// Дата по которой формируется выборка 
        /// </summary>
        public DateTime SyncDate { get; set; }

        private DataTable mSyncData;
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="entity"></param>
        public BaseSyncEntity(EntityType entity)
        {
            mEntityMetadata = entity;
        }
        /// <summary>
        /// Ctor: Выполняет загрузку данных
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="syncContext"></param>
        public BaseSyncEntity(EntityType entity, IContext syncContext)
            : this(entity)
        {
            InitSyncData(syncContext);
        }
        /// <summary>
        /// Инициализация дынных для синхронизации
        /// </summary>
        /// <param name="syncContext"></param>
        public void InitSyncData(IContext syncContext)
        {
            GetSyncData(syncContext);
        }
        /// <summary>
        /// Возвращает дататэйбл
        /// </summary>
        /// <returns></returns>
        public virtual DataTable GetSyncData(IContext dbContext, bool allData = false )
        {
            var name = mEntityMetadata.MetadataProperties.Contains("TableName")
                        ?
                        mEntityMetadata.MetadataProperties["TableName"].Value.ToString() : mEntityMetadata.Name;

            name = name.Replace(".", "].[");

            mSyncData = dbContext.GetDataTable(name, allData ? null : GetCondition());

            if (mSyncData == null)
            {
                return null;
            }

            mSyncData.Namespace = mEntityMetadata.Name;
            
            return mSyncData;
        }

        protected virtual string GetCondition()
        {
            return null;
        }
    }
}
