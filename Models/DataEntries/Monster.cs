using GameDataEditor.Models.DataEntries.Complex;
using GameDataEditor.Models.Utils;

namespace GameDataEditor.Models.DataEntries
{
    public class Monster : BaseDataRow
    {
        public int HP { get; set; }
        public int Attack { get; set; }
        public int Experience { get; set; }

        public Stats? BaseStats { get; set; }
        public FixedLengthArray<string> Tags { get; set; } = new FixedLengthArray<string>(3);
        public FixedLengthArray<Aura> Auras { get; set; } = new FixedLengthArray<Aura>(8);
    }
}
