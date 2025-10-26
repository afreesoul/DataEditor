using GameDataEditor.Models.DataEntries;
using GameDataEditor.Models.DataEntries.Complex;
using GameDataEditor.Models.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameDataEditor.Utils
{
    /// <summary>
    /// 数据转换器 - 处理List到FixedLengthArray的转换
    /// </summary>
    public static class DataConverter
    {
        /// <summary>
        /// 确保数据格式正确（FixedLengthArray已自动处理，此方法为空）
        /// </summary>
        /// <param name="monster">Monster实例</param>
        public static void EnsureFixedLengthFormat(Monster monster)
        {
            // FixedLengthArray已经自动处理了固定长度，无需额外处理
            // 此方法保留用于未来可能的扩展
        }
        
        /// <summary>
        /// 处理所有数据表中的Monster数据
        /// </summary>
        /// <param name="tables">数据表集合</param>
        public static void FixAllMonsterData(System.Collections.ObjectModel.ObservableCollection<GameDataEditor.Models.GameDataTable> tables)
        {
            foreach (var table in tables)
            {
                if (table.DataType == typeof(Monster))
                {
                    foreach (var row in table.Rows)
                    {
                        if (row is Monster monster)
                        {
                            EnsureFixedLengthFormat(monster);
                        }
                    }
                }
            }
        }
    }
}