using GameDataEditor.Models.DataEntries;
using System;
using System.Collections.ObjectModel;

namespace GameDataEditor.Models
{
    public class GameDataTable
    {
        public string Name { get; set; }
        public ObservableCollection<BaseDataRow> Rows { get; set; }
        public Type DataType { get; set; }

        public GameDataTable(string name, Type dataType)
        {
            Name = name;
            DataType = dataType;
            Rows = new ObservableCollection<BaseDataRow>();
        }
    }
}