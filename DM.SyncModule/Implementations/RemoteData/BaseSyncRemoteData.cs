using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DM.Infrastructure.Database;
using DM.Infrastructure.Remote;
using DM.SyncModule.Properties;

namespace DM.SyncModule.Implementations.RemoteData
{
    /// <summary>
    /// Данные передаваемы при синхронизации
    /// </summary>
    public class BaseSyncRemoteData : IRemoteData
    {

        #region Properties

        /// <summary>
        /// MessageId, в виду особенностей стринга
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Заголовок
        /// </summary>
        public string Header { get; set; }

        private string mText;
        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Text
        {
            get { return mText ?? (mText = GetBodyByMetadata()); }
            set
            {
                mText = value;
                FillMetadataByText(mText);
            }
        }

        /// <summary>
        /// Аттач
        /// </summary>
        public IList<FileInfo> Attachments { get; set; }

        /// <summary>
        /// Метаданные сообщения
        /// </summary>
        public IDictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Возвращает по ключю
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object GetMetadataValue(string key)
        {
            return Metadata.ContainsKey(key) ? Metadata[key] : null;
        }

        public RemoteState State { get; set; }

        #endregion

        #region Ctor

        /// <summary>
        /// Default ctor
        /// </summary>
        public BaseSyncRemoteData()
        {
            Attachments = new List<FileInfo>();
            //сделан стрингой специально
            Id = Id ?? Guid.NewGuid().ToString();
            Metadata = new Dictionary<string, object>();
        }


        /// <summary>
        /// Constructor
        /// </summary>
        public BaseSyncRemoteData(string dataFilePath, string msgId = null)
            : this()
        {
            Id = msgId;
            Attachments.Add(new FileInfo(dataFilePath));
        }
        #endregion

        /// <summary>
        /// Добавление метаданных
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddMetadata(string key, object value)
        {
            if (value == null)
                value = Resources.cNull;
            Metadata.Add(key, value);
        }

        /// <summary>
        /// Метаданные отправляемого 
        /// файла синхронизации
        /// </summary>
        /// <returns></returns>
        public virtual string GetBodyByMetadata()
        {
            const string cAttachedFileMask = "File=\"{0}\";";

            const string cHashMask = "Checksum=\"{1}\";"; //todo: опредилиться с вычислением контрольной суммы

            const bool calculateHash = false; //todo: опредилиться с вычислением контрольной суммы

            var body = Resources.cMetadataHeader + "\r\n";

            body += string.Join(";\r\n", Metadata.Select(x => x.Key + "=" + x.Value).ToArray());

            body += ";";

            body += Resources.cEndMetadata;

            body += "[attached]\r\n";
            //перечисление прикрепленных документов 
            body = Attachments.Aggregate(body, (current, file) =>
                current + (string.Format(calculateHash ? cAttachedFileMask + cHashMask : cAttachedFileMask, file.Name, 0) + "\r\n"));

            body += "[end_attached]";

            return body;
        }

        /// <summary>
        /// Заполение метаданных
        /// </summary>
        /// <param name="message"></param>
        private void FillMetadataByText(string message)
        {
            var messageSplit = message.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var metadataExpresstions = new List<string>();

            if (messageSplit.Any() && messageSplit.First() == Resources.cMetadataHeader)
                metadataExpresstions.AddRange(messageSplit.TakeWhile(line => !line.StartsWith("[end_")));

            foreach (var metadataLine in metadataExpresstions)
            {
                if (metadataLine == Resources.cMetadataHeader) continue;

                var split = metadataLine.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                var key = split.First();
                var value = split.Last();

                if (key.Contains("cSetAdbAddress"))
                    value = GetAddressValue(split);

                if (!Metadata.ContainsKey(key))
                    Metadata.Add(key, value);
            }
        }

        /// <summary>
        /// Собрать адрес из частей
        /// </summary>
        /// <param name="split"></param>
        /// <returns></returns>
        private string GetAddressValue(string[] split)
        {
            var value = string.Empty;
            try
            {
                var index = 0;
                foreach (var symbol in split)
                {
                    if (index != 0)
                        value += symbol + "=";
                    index++;
                }
                value = value.Remove(value.LastIndexOf("="), 1);

                return value;
            }
            catch (Exception)
            {
                return value;
            }
        }

        public void RefreshMetadataText()
        {
            mText = GetBodyByMetadata();
        }
    }


}
