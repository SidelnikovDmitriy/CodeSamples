using System;
using DM.Database;

namespace DM.SyncModule
{
    public interface ISyncEntity<out T> : ISyncEntity where T : class
    {
        /// <summary>
        /// Возвращает данные 
        /// для синхронизации
        /// </summary>
        /// <param name="context"></param>
        /// <param name="allData">все данные, вне зависимости от условия</param>
        /// <returns></returns>
        T GetSyncData(IContext context, bool allData = false);
    }

    public interface ISyncEntity
    {
        /// <summary>
        /// Дата, по которой осуществляется выборка
        /// </summary>
        DateTime SyncDate { get; set; }
      
    }
  
}
