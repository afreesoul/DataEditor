namespace GameDataEditor.Models
{
    public class MoveTableParameters
    {
        public DataTableWrapper? Table { get; set; }
        public DataDirectory? TargetDirectory { get; set; }
        public bool MoveToOuter { get; set; }

        public MoveTableParameters(DataTableWrapper? table = null, DataDirectory? targetDirectory = null, bool moveToOuter = false)
        {
            Table = table;
            TargetDirectory = targetDirectory;
            MoveToOuter = moveToOuter;
        }
    }
}