using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Timers;
using DM.Database;
using DM.Infrastructure.Database;
using DM.Infrastructure.Email;
using DM.Infrastructure.Remote;
using DM.SyncModule.Implementations.RemoteData;
using DM.SyncModule.Services;

namespace DM.SyncModule.Implementations
{
    /// <summary>
    /// Сервис синхронизации работающий с клиентом синхронизации
    /// </summary>
    public class RemoteSyncService : SimpleSyncService
    {
        #region Private fields
        /// <summary>
        /// Таймер выполняющий проверку обновления
        /// </summary>
        private Timer mInboxTrackingTimer;
        /// <summary>
        /// Почтовый клиент
        /// </summary>
        private IRemoteClient<BaseSyncRemoteData> mSyncClient;
        /// <summary>
        /// Выполнеине фоновых операций
        /// </summary>
        private BackgroundWorker mBgWorker;
        #endregion

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="syncContext"></param>
        /// <param name="syncSettings"></param>
        /// <param name="entitiesToSync"></param>
        public RemoteSyncService(IContext syncContext, ISyncSettings syncSettings, IEnumerable<ISyncEntity<DataTable>> entitiesToSync = null, bool initClient=true) :
            base(syncContext, syncSettings, entitiesToSync)
        {
            if(initClient)
                       Init();
        }
        /// <summary>
        /// Инициализация
        /// </summary>
        private void Init()
        {
            //перевожу uptime в миллисекунды
            mSyncClient = SyncSettings.Client as IRemoteClient<BaseSyncRemoteData>;

            if (mSyncClient == null)
                throw new Exception("Некорректно задан клиент для синхронизации");

            IsBusy = false;
         

            mSyncClient.Init();
        }

        /// <summary>
        /// Запуск отслеживания 
        /// входящих данных
        /// </summary>
        /// <param name="onUpdate"></param>
        public override void StartUpdateTracking(Action<ISyncService<DataTable>, IRemoteData> onUpdate)
        {
            //CheckUpdate(onUpdate);
            
            mInboxTrackingTimer = new Timer
            {
                Interval = SyncSettings.SyncUptime * 60000
            };

            mInboxTrackingTimer.Elapsed 
                += (a, e) =>
            {
                if (IsBusy) return;
                CheckUpdate(onUpdate);

            };
            mInboxTrackingTimer.Start();
        }
        /// <summary>
        /// Проверка входящих данных
        /// </summary>
        /// <param name="onUpdate"></param>
        public override void CheckUpdate(Action<ISyncService<DataTable>, IRemoteData> onUpdate)
        {
            mBgWorker = new BackgroundWorker();

            mBgWorker.DoWork += (p, b) =>
            {
                IsBusy = true;

                if (!mSyncClient.Init()) return;

                if (!mSyncClient.CheckUpdate()) return;

                //если есть новые входящие данные  
                while (mSyncClient.New.Any())
                {
                    var data = mSyncClient.New.Pop();

                    data.State = RemoteState.ToRecive;

                    onUpdate(this, data); //обновляю их
                }
            };

            mBgWorker.RunWorkerCompleted +=
                    (sender, obj) => IsBusy = false;


            mBgWorker.RunWorkerAsync();
            
        }

        /// <summary>
        /// Отправка данных
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="reciver"></param>
        public override bool SyncOut(IRemoteData sendData, AccountInfo reciver)
        {
            var dataToSend = sendData as BaseSyncRemoteData;

            if (dataToSend == null) return false;

            foreach (var file in dataToSend.Attachments.ToList())
            {
                var dir = new DirectoryInfo(file.DirectoryName);

                if (!dir.Exists)
                    dir.Create();
                
                SyncOut(file.FullName);

                var filename = Path.GetFileNameWithoutExtension(file.FullName);
                var dataDir = Path.GetDirectoryName(file.FullName);

                if (dataDir != null)
                {
                    var chunks = Directory.GetFiles(dataDir, string.Format("{0}.z*", filename));

                    foreach (var chunk in chunks)
                    {
                        dataToSend.Attachments.Add(new FileInfo(chunk));
                    }

                }
            }
            
            var result = mSyncClient.SendData(dataToSend, reciver.Username);

           
            return result;
        }


    }
}
