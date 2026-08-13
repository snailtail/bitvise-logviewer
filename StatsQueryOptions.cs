namespace BitviseLogViewer;

public class StatsQueryOptions
{
    public string StatsFolder { get; set; } = "";
    public string AccountFilter { get; set; } = "";
    public bool IncludeGroupsAndServerTotal { get; set; }

    public bool OutputToFile { get; set; }
    public string OutputFilePath { get; set; } = "";
}
