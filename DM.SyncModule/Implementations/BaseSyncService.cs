using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using DM.Database;
using DM.Infrastructure;
using DM.Infrastructure.Archiver;
using DM.Infrastructure.Remote;
using DM.SyncModule.Annotations;
using DM.SyncModule.Services;
using DM.SyncModule.Properties;

namespace DM.SyncModule
{
    /// <summary>
    /// Сервис синхронизации использующий
    /// для сериализацию прослойку в виде DataSet/DataTable
    /// </summary>
    public abstract class BaseSyncService : ISyncService<DataTable>, INotifyPropertyChanged
    {
        /// <summary>
        /// Общее кол-во строк
        /// </summary>
        public long RowsToUpdateCount { get; set; }
        /// <summary>
        /// Набор сущностей для синхронизации
        /// </summary>
        public IEnumerable<ISyncEntity<DataTable>> EntitiesToSync { get; set; }
        /// <summary>
        /// Контекст для синхронизации
        /// </summary>
        public IContext SyncContext { get; set; }

        /// <summary>
        /// Настройки синхронизации
        /// </summary>
        public ISyncSettings SyncSettings { get; private set; }

        /// <summary>
        /// Выгрузка
        /// </summary>
        /// <param name="filepath"></param>
        public abstract void SyncOut(string filepath = null);

        /// <summary>
        /// Загрузка
        /// </summary>
        /// <param name="filepath"></param>
        public abstract void SyncIn(string filepath = null);
        /// <summary>
        /// вывод синхронизации
        /// </summary>
        public string Output { get; set; }
        /// <summary>
        /// флаг загруженности 
        /// </summary>
        private bool mIsBusy;
        /// <summary>
        /// Base ctor
        /// </summary>
        /// <param name="syncContext"></param>
        /// <param name="syncSettings"></param>
        /// <param name="syncEntities"></param>
        protected BaseSyncService(IContext syncContext, ISyncSettings syncSettings, IEnumerable<ISyncEntity<DataTable>> syncEntities)
        {
            Init(syncContext, syncSettings, syncEntities);
        }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="syncContext">Synchronization context</param>
        /// <param name="syncSettings">Synchronization settings</param>
        /// <param name="syncEntities">Entities to sync</param>
        /// <returns></returns>
        public void Init(IContext syncContext, ISyncSettings syncSettings, IEnumerable<ISyncEntity<DataTable>> syncEntities)
        {
            try
            {
                ValidateSettings(syncSettings);
                SyncContext = syncContext;
                SyncSettings = syncSettings;
                SetEntitiesToSync(syncEntities);
            }
            catch (Exception ex)
            {
                if (syncSettings != null && syncSettings.Logger != null)
                    syncSettings.Logger.Error(ex);
                Out(ex.ToString());
                throw;
            }
        }
        /// <summary>
        /// Установка набора сущности для 
        /// синхронизации
        /// </summary>
        /// <param name="entities"></param>
        public virtual void SetEntitiesToSync(IEnumerable<ISyncEntity<DataTable>> entities = null)
        {
            if (entities == null)
            {
                var contextAdapter = SyncContext as IObjectContextAdapter;
                if (contextAdapter == null)
                    throw new InvalidCastException("Не удалось преобразовать IContext к IObjectContextAdapter");

                EntitiesToSync = contextAdapter.ObjectContext.MetadataWorkspace.GetItems<EntityType>(DataSpace.SSpace).Select(i => new BaseSyncEntity(i));
                return;
            }

            EntitiesToSync = entities;
        }

        /// <summary>
        /// Создание дампа
        /// </summary>
        /// <param name="dataSet"></param>
        /// <param name="archiver"></param> 
        /// <param name="filePath"></param>
        /// <returns></returns>
        public virtual bool CreateDumpFile(DataSet dataSet, IArchiver archiver = null, string filePath = null)
        {
            try
            {

                string basePath = AppDomain.CurrentDomain.BaseDirectory;

                if (!string.IsNullOrEmpty(filePath))
                {
                    basePath = Path.GetDirectoryName(filePath);
                    if (basePath == null)
                        throw new Exception("Не удалось получить директорию для произвольного пути...");
                }


                var directory = new DirectoryInfo(Path.Combine(basePath, SyncSettings.TempFolderName)); //директория по умолчанию

                if (!directory.Exists) //если папки нет, создаю
                    directory.Create();

                if (filePath == null)
                    filePath = Path.Combine(basePath, SyncSettings.ExportFileName);


                var jsonPath = Path.Combine(directory.FullName, string.Concat(Path.GetFileNameWithoutExtension(filePath),
                    SyncSettings.DataSerializer.FileExtenssion));

                SyncSettings.DataSerializer.SerializeData(dataSet, jsonPath);


                if (!File.Exists(jsonPath))
                    throw new Exception("Нет данных для сериализации");


                if (archiver != null)
                {
                    archiver.CompressDir(new FileInfo(filePath), directory);
                    directory.Delete(true);
                }


            }
            catch (Exception ex)
            {
                SyncContext.Logger.Error(ex);
                Out(ex.ToString());
                return false;
            }

            return true;
        }


        /// <summary>
        /// Логика загрузки слепка данных
        /// </summary>   
        /// <param name="archiver"></param> 
        /// <param name="filePath"></param>
        /// <returns></returns>
        public virtual DataSet LoadDumpFile(IArchiver archiver, string filePath = null)
        {
            var serializer = SyncSettings.DataSerializer as IDataSerializer<DataSet>; //кастую сериалайзер, к сериалайзеру работающему с dataset

            DataSet result;

            try
            {
                if (serializer == null)
                    throw new InvalidCastException("Настройки сериалайзера не позволяют привести объект к DataSet");

                string directory = AppDomain.CurrentDomain.BaseDirectory; //по умолчанию директория запуска

                if (filePath == null)
                    filePath = Path.Combine(directory, SyncSettings.ExportFileName); //если не указан произвольный путь, вычисляю путь к файлу синхронизации
                else
                    directory = Path.GetDirectoryName(filePath); //если указан произвольный путь к файлу, получаю путь к директории

                if (directory == null)
                    throw new Exception("Не удалось получить путь к директории загрузки");

                //временная директория
                var dir = new DirectoryInfo(Path.Combine(directory, SyncSettings.TempFolderName));

                if (dir.Exists)
                    dir.Delete(true);

                archiver.Decompress(new FileInfo(filePath), new DirectoryInfo(directory)); //распаковываю слепок данных


                var jsonPath = Path.Combine(dir.FullName,
                    string.Concat(Path.GetFileNameWithoutExtension(filePath),
                        SyncSettings.DataSerializer.FileExtenssion));  //путь к файлу с сериализованными данными БД

                if (!File.Exists(jsonPath))
                    jsonPath = Path.Combine(dir.FullName, string.Concat(Resources.cDefaultDataFilenamePrefix, SyncSettings.DataSerializer.FileExtenssion));

                if (!File.Exists(jsonPath))
                    throw new FileNotFoundException("Не удалось обнаружить данные сериализации");


                SyncSettings.Logger.Info("Чтение данных...");

                result = serializer.DeserializeData(jsonPath); //дессериализация

                RowsToUpdateCount = 0;

                foreach (DataTable item in result.Tables)
                    RowsToUpdateCount += item.Rows.Count;
           

                SyncSettings.Logger.Info("Загрузка слепка данных прошла успешно...");


            }
            catch (Exception ex)
            {
                SyncSettings.Logger.Error(ex);
                Out(ex.ToString());
                throw;
            }


            return result; //возвращаю дессериализованный набор данных 
        }

        /// <summary>
        /// Путь к бинарникам
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public string GetBynaryDataFolder(string filePath = null)
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            if (filePath != null)
                directory = Path.GetDirectoryName(filePath);

            return directory == null ? null : Path.Combine(directory, SyncSettings.TempFolderName, SyncSettings.DataSerializer.BinaryDataFolder);
        }

        /// <summary>
        /// Возвращает true, если настройки синхронизации заданы правильно,
        /// false - в случае не корректных настроек
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="generateException"></param>
        /// <returns></returns>
        public bool IsValidSettings(ISyncSettings settings, bool generateException = false)
        {
            try
            {
                ValidateSettings(settings);
            }
            catch (Exception exception)
            {
                if (settings.Logger != null)
                    settings.Logger.Error(exception);
                Out(exception.ToString());
                if (generateException) throw;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Выполняет проверку настроек синхронизации
        /// </summary>
        /// <param name="settings"></param>
        protected virtual void ValidateSettings(ISyncSettings settings)
        {
            if (settings == null)
                throw new Exception("Необходимо задать настройки синхронизации (SyncSettings)");

            if (settings.DataSerializer == null)
                throw new Exception("Необходимо задать настройки сериализации (DataSerializer)");

            if (settings.Logger == null)
                throw new Exception("Необходимо задать настройки логирования (Logger)");
        }

        /// <summary>
        /// Корректирует типы столбцов, для DataTable полученной при десериализации
        /// по образцу целевой таблицы
        /// </summary>
        /// <param name="sourceTable">исходная, для которой фиксируются изменения</param>
        /// <param name="targetTable">целевая, с набором изменений</param>
        /// <param name="customFilePath">путь к файлу, нужен для получения директории бинарников</param>
        /// <returns>DataTable с зафиксированным набором изменений и слитыми данными</returns>
        protected DataTable FixTableDataTypes(DataTable sourceTable, DataTable targetTable, string customFilePath = null)
        {
            SyncSettings.Logger.Info(string.Format("Преобразование типов данных {0}...", sourceTable.TableName));

            var clone = sourceTable.Clone();

            clone.Namespace = sourceTable.Namespace;
            clone.TableName = sourceTable.TableName;

            var columnsToDelete = new List<DataColumn>();

            try
            {

                foreach (DataRow row in targetTable.Rows) //берем строки сериализуемого 
                {
                    var newRow = clone.NewRow();

                    for (var i = 0; i < row.ItemArray.Length; i++)
                    {
                        var currentColumnName = targetTable.Columns[i].ColumnName;


                        if (!clone.Columns.Contains(currentColumnName)) continue;

                        if (targetTable.Columns[currentColumnName].DataType == typeof(DateTime) && row[currentColumnName] != DBNull.Value)
                        {
                            var newDate = Convert.ToDateTime(row[currentColumnName]);
                            newRow[currentColumnName] = newDate.ToLocalTime();

                        }


                        //если колонка в datatable после десерализации стала string, а в БД она bite[] 
                        if (targetTable.Columns[currentColumnName].DataType == typeof(string) &&
                            clone.Columns[currentColumnName].DataType == typeof(byte[]))
                        {
                            if (row[currentColumnName].ToString().Equals(Resources.cSkipColumnLabel, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (!columnsToDelete.Any(p => p.ColumnName == currentColumnName))
                                    columnsToDelete.Add(clone.Columns[currentColumnName]);
                                continue;
                            }

                            if (SyncSettings.DataSerializer.SeparateBinaryData)
                            //если в параметрах указано хранить файлы отдельно
                            {
                                var filePath = Path.Combine(GetBynaryDataFolder(customFilePath), row[currentColumnName].ToString());
                                //составляю путь к файлу
                                //если файл существует
                                if (File.Exists(filePath))
                                    newRow[currentColumnName] = File.ReadAllBytes(filePath); //пишу в клон массив байт
                            }
                            else //в противном случе, если в парметрах указано хранение бинарных данных в json (Base64)
                                newRow[currentColumnName] = Convert.FromBase64String(row[currentColumnName].ToString()); //декодирую base64
                        }
                        else
                        {
                            //если у нас нет несовпадений в типах колонок после десерализации и все нормально конвертируется
                            newRow[currentColumnName] = row[currentColumnName]; //пишу оригинальное значение
                        }
                    }

                    clone.Rows.Add(newRow);
                }

                if (columnsToDelete.Any())
                {
                    foreach (var col in columnsToDelete)
                    {
                        if (clone.Columns.Contains(col.ColumnName))
                            clone.Columns.Remove(col);
                    }
                }


                SyncSettings.Logger.Info("Преобразование типов данных выполнено успешно...");
            }
            catch (Exception ex)
            {
                SyncSettings.Logger.Error(ex);
                Out(ex.ToString());
            }

            return clone;
        }

        /// <summary>
        /// Запуск отслеживания обновления 
        /// входящих данных 
        /// </summary>
        /// <param name="onUpdate"></param>
        public abstract void StartUpdateTracking(Action<ISyncService<DataTable>, IRemoteData> onUpdate);
        /// <summary>
        /// Разовая проверка обновления
        /// </summary>
        /// <param name="onUpdate"></param>
        public abstract void CheckUpdate(Action<ISyncService<DataTable>, IRemoteData> onUpdate);
        /// <summary>
        /// Отправка данных
        /// синхронизации
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="reciver"></param>
        public abstract bool SyncOut(IRemoteData sendData, AccountInfo reciver);

        /// <summary>
        /// Получение метаданных из файла
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="archiver"></param>
        /// <returns></returns>
        public string GetFileMetadata(string filepath, IArchiver archiver = null)
        {
            if (archiver == null)
                archiver = SyncSettings.DataArchiver;

            using (var ms = new MemoryStream())
            {

                archiver.Extract(new FileInfo(filepath), Path.Combine(SyncSettings.TempFolderName, SyncSettings.MetadataFile), ms);

                ms.Position = 0;

                using (var streamReader = new StreamReader(ms))
                {
                    return streamReader.ReadToEnd();
                }

            }

        }
        /// <summary>
        /// Вывод информации по синхронизацие
        /// </summary>
        /// <param name="line"></param>
        public void Out(string line)
        {
            Output += line + "\r\n";
        }

        public bool IsBusy
        {
            get { return mIsBusy; }
            set
            {
                mIsBusy = value;
                OnPropertyChanged("IsBusy");
            }
        }


        public virtual void SetBusy(Action doWork, Action completeAction = null, bool inGridOnly = false)
        {
            throw new NotImplementedException();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }


    }
}
