namespace BitviseLogViewer;

public class QueryOptions
{
    public string LogFolder { get; set; } = "";

    public bool IncludeLogons { get; set; }
    public bool IncludeTransfers { get; set; }
    public bool TransfersUploadOnly { get; set; }
    public bool TransfersDownloadOnly { get; set; }
    public bool IncludeRemoves { get; set; }
    public bool IncludeSecurity { get; set; }

    public string VirtualAccount { get; set; } = "";
    public string FilePattern { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    public string DateFromText { get; set; } = "";
    public string DateToText { get; set; } = "";

    public bool LegacyRemoteAddressFormat { get; set; }

    public bool OutputToFile { get; set; }
    public string OutputFilePath { get; set; } = "";
}
