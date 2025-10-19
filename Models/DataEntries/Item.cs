namespace GameDataEditor.Models.DataEntries
{
    public class Item : BaseDataRow
    {
        public int? Value { get; set; }
        public string? Description { get; set; }
        public int? Damage { get; set; }
        public string? Type { get; set; }
    }
}
