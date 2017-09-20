using System.Data;

namespace DM.SyncModule.Services
{
    public interface IDataSaveService
    {
        /// <summary>
        /// Сохранение данных таблицы на
        /// целевой БД
        /// </summary>
        /// <param name="targetDbConnection">Строка подключения к целевой БД</param>
        /// <param name="dataToUpsert">Данные для сохранения</param>
        void SaveDataOnTarget(string targetDbConnection, params DataTable[] dataToUpsert);
    }
}
