namespace GameDataEditor.Models.DataEntries.Complex
{
    public class Aura
    {
        public Aura()
        {
            Name = string.Empty;
            Damage = 0;
            Duration = 0f;
        }
        
        public string Name { get; set; }
        public int Damage { get; set; }
        public float Duration { get; set; }
    }
}
