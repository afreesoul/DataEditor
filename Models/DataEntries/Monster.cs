using GameDataEditor.Models.DataEntries.Complex;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameDataEditor.Models.DataEntries
{
    public class Monster : BaseDataRow
    {
        public int HP { get; set; }
        public int Attack { get; set; }
        public int Experience { get; set; }
        public Stats? BaseStats { get; set; }
        public List<string> Tags { get; set; } = Enumerable.Repeat(string.Empty, 3).ToList();
        public List<Aura> Auras { get; set; } = Enumerable.Range(0, 2).Select(_ => new Aura()).ToList();
    }
}
