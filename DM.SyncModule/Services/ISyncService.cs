using System;
using System.Collections.Generic;
using DM.Database;
using DM.Infrastructure;
using DM.Infrastructure.Archiver;
using DM.Infrastructure.Remote;
using DM.SyncModule.Services;

namespace DM.SyncModule
{
    /// <summary>
    /// Интерфейс сервиса синхронизации баз данных
    /// </summary>
    public interface ISyncService<out T> : IBusyService where T  : class 
    {
        /// <summary>
        /// Сущности для синхронизации
        /// </summary>
        IEnumerable<ISyncEntity<T>> EntitiesToSync { get; }

        /// <summary>
        /// Дата-контекст для синхронизации
        /// </summary>
        IContext SyncContext { get; }

        /// <summary>
        /// Настройки синхронизации
        /// </summary>
        ISyncSettings SyncSettings { get; }

        /// <summary>
        /// Возвращает метаданные для файлаю
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="archiver"></param>
        /// <returns></returns>
        string GetFileMetadata(string filepath, IArchiver archiver = null);

        /// <summary>
        /// Загрузка
        /// </summary>
        /// <param name="filePath">произвольное расположение файла загрузки</param>
        void SyncIn(string filePath = null);

        /// <summary>
        /// Выгрузка
        /// </summary>
        /// <param name="filePath">произвольное расположение файла выгрузки</param>
        void SyncOut(string filePath = null);

        /// <summary>
        /// Отслеживание обновлений входящих данных
        /// </summary>
        /// <param name="onUpdate"></param>
        void StartUpdateTracking(Action<ISyncService<T>, IRemoteData> onUpdate);
        /// <summary>
        /// Разоваая проверка обновлений входящих данных
        /// </summary>
        /// <param name="onUpdate"></param>
        void CheckUpdate(Action<ISyncService<T>, IRemoteData> onUpdate);
        /// <summary>
        /// Выгрузка с отправкой
        /// через клиент синхронизации
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="reciver"></param>
        bool SyncOut(IRemoteData sendData, AccountInfo reciver);
    }
}
