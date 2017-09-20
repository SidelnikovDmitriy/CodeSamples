using DM.Infrastructure;
using DM.Infrastructure.Archiver;
using DM.Infrastructure.Database;
using DM.Infrastructure.Email;
using DM.Infrastructure.Logger;

namespace DM.SyncModule.Services
{
    public interface ISyncSettings
    {
        /// <summary>
        /// Имя выгруженного файлу
        /// синхронизации
        /// </summary>
        string ExportFileName { get; }
        /// <summary>
        /// Имя  директории, где формируется 
        /// файл
        /// </summary>
        string TempFolderName { get; }
        /// <summary>
        /// Название файла метаданных
        /// </summary>
        string MetadataFile { get; }
        /// <summary>
        /// Сериалайзер
        /// </summary>
        IDataSerializer DataSerializer { get; }
        /// <summary>
        /// Логгер
        /// </summary>
        ILogger Logger { get; }
        /// <summary>
        /// Клиент для отправки/получения данных  
        /// </summary>
        IRemoteClient Client { get; set; }
        /// <summary>
        /// Используемый архиватор
        /// </summary>
        IArchiver DataArchiver { get; set; }
        /// <summary>
        /// Сервис сохранения данных в целевой БД 
        /// </summary>
        IDataSaveService DataSaveMethod { get; set; }
        /// <summary>
        /// Резервное копирование файла сингхронизации
        /// </summary>
        IBackupService DumpFileBackup { get; set; }
        /// <summary>
        /// Резервное копирование БД 
        /// </summary>
        IBackupService DataBaseBackup{ get; set; }
        /// <summary>
        /// Время проверки входящих данных
        /// в минутах
        /// </summary>
        int SyncUptime { get;  }
    }
}
