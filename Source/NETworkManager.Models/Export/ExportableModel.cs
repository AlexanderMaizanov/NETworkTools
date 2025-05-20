namespace NETworkManager.Models.Export
{
    /// <summary>
    /// Purpose: 
    /// Created By: amaizanov
    /// Created On: 5/20/2025 12:12:28 PM
    /// </summary>
    public abstract class ExportableModel : IExportable
    {
        public abstract string ToCsv();
        public abstract string ToJson();
        public abstract string ToXml();
    }
}
