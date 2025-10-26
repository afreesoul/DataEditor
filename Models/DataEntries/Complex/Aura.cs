using GameDataEditor.Models.Utils;

namespace GameDataEditor.Models.DataEntries.Complex
{
    public class Aura
    {
        public string? Name { get; set; }
        public int Damage { get; set; }
        public float Duration { get; set; }

        public FixedLengthArray<string> Tags { get; set; } = new FixedLengthArray<string>(3);
        
    }
}
