namespace NETworkManager.Models.Export
{
    /// <summary>
    /// Purpose:
    /// Created By: amaizanov
    /// Created On: 5/20/2025 12:10:38 PM
    /// </summary>
    public interface IExportable
    {
        public string ToCsv();
        public string ToXml();
        public string ToJson();
    }
}
