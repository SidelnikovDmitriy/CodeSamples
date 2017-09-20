using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using DM.Database;
using DM.Infrastructure.Remote;
using DM.SyncModule.Services;

namespace DM.SyncModule
{
    /// <summary>
    /// Сервис синхронизации баз данных
    /// </summary>
    public class SimpleSyncService : BaseSyncService
    {
        #region Private
        /// <summary>
        /// Датасет для синхронизации
        /// </summary>
        private DataSet mSyncDataSet;

        #endregion

        #region Init
        /// <summary>
        ///  Ctor
        /// </summary>
        /// <param name="syncContext"></param>
        /// <param name="syncSettings"></param>
        /// <param name="entitiesToSync"></param>
        public SimpleSyncService(IContext syncContext, ISyncSettings syncSettings, IEnumerable<ISyncEntity<DataTable>> entitiesToSync = null)
            : base(syncContext, syncSettings, entitiesToSync)
        {
            Init();
        }
        /// <summary>
        /// инициализация
        /// </summary>
        private void Init()
        {
            Output = string.Empty;
            mSyncDataSet = new DataSet("SyncDataSet") { Namespace = "Neolant.Sync" };
        }

        /// <summary>
        /// Заполнение датасета для выгрузки
        /// </summary>
        /// <param name="dataSetToFil"></param>
        /// <param name="alldata"></param>
        /// <param name="onImport"></param>
        /// <returns></returns>
        private DataSet FillSyncDataSet(DataSet dataSetToFil, bool alldata = false)
        {

            foreach (var syncData in EntitiesToSync.Select(i => i.GetSyncData(SyncContext, alldata)))
            {
                if (syncData == null) continue;

                dataSetToFil.Tables.Add(syncData);

                Debug.Print(syncData.TableName);
            }

            dataSetToFil.AcceptChanges();

            return dataSetToFil;
        }
        #endregion

        #region Main sync methods

        /// <summary>
        /// Выгрузка
        /// </summary>
        /// <param name="filePath"></param>
        public override void SyncOut(string filePath = null)
        {
            Init();
            if (!CreateDumpFile(FillSyncDataSet(mSyncDataSet), SyncSettings.DataArchiver, filePath))
                throw new Exception("Возникли ошибки при формировании файла выгрузки");

            //todo: отправка
        }

        /// <summary>
        /// Запуск отслеживания входящих данных
        /// </summary>
        /// <param name="onUpdate"></param>
        public override void StartUpdateTracking(Action<ISyncService<DataTable>, IRemoteData> onUpdate)
        {
            throw new NotImplementedException("Данная реализация сервиса синхронизации не поддерживает проверку входящих данных. Используйте RemoteSyncService.");
        }

        public override void CheckUpdate(Action<ISyncService<DataTable>, IRemoteData> onUpdate)
        {
            throw new NotImplementedException("Данная реализация сервиса синхронизации не поддерживает проверку входящих данных. Используйте RemoteSyncService.");
        }


        public override bool SyncOut(IRemoteData sendData, AccountInfo reciver)
        {
            throw new NotImplementedException("Данная реализация сервиса синхронизации не поддерживает отправку данных. Используйте RemoteSyncService.");
        }

        /// <summary>
        /// Загрузка
        /// </summary>
        /// <param name="filepath"></param>
        public override void SyncIn(string filepath = null)
        {
            Init();
            var primarySyncDataSet = LoadDumpFile(SyncSettings.DataArchiver, filepath); //десериализованный датасет

            if (primarySyncDataSet == null)
                throw new NullReferenceException("Не удалось получить данные для синхронизации...");

            var contextDataset = FillSyncDataSet(mSyncDataSet, true);

            var syncTables = primarySyncDataSet.Tables.Cast<DataTable>().
                                Where(source => source.Columns.Count > 0).ToDictionary(source => source.TableName);

            //типы колонок могут отличаться в целевой и исходной например Guid -> String 
            //тут происходит корректирока типов для таблиц дессериализованного датасета и отсев пустых таблиц
            var normalizedSyncData =
                contextDataset.Tables.Cast<DataTable>()
                    .Where(model => syncTables.ContainsKey(model.TableName))
                    .Select(model => FixTableDataTypes(model, syncTables[model.TableName], filepath));

            try
            {
                //тут десериализованный данные записываются в БД
                SyncSettings.DataSaveMethod.SaveDataOnTarget(SyncContext.GetConnectionString(), normalizedSyncData.ToArray());
            }
            catch (Exception ex)
            {
                SyncContext.Logger.Error(ex);
                Out(ex.Message);
                //todo : SyncErrors
            }


        }

        #endregion

    }
}
