namespace GameDataEditor.Models.DataEntries.Complex
{
    public class Stats
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public Resistances ElementalResistances { get; set; } = new();
    }
}
