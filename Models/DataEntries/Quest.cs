namespace GameDataEditor.Models.DataEntries
{
    public class Quest : BaseDataRow
    {
        public string? Title { get; set; }
        public int? RequiredLevel { get; set; }
        public ForeignKey<Monster> GiverNPC { get; set; }
    }
}
