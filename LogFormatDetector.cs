using System.IO;
using System.Text.RegularExpressions;

namespace BitviseLogViewer;

// Bitvise SSH Server bytte från ett kombinerat "remoteAddress" (IP:port) till separata
// "remoteAddr"/"remoteAddrPort" i version 9.51. Att gissa fel på det ger ett rent LogParser-fel
// (attributet saknas helt i filen, inte bara null), så vi läser hellre appVersion ur loggfilens
// egen <start .../>-rad än att kräva att användaren känner till sin serverversion.
public static class LogFormatDetector
{
    public record DetectionResult(bool IsLegacy, string Message);

    public static DetectionResult Detect(string logFolder)
    {
        if (!Directory.Exists(logFolder))
            return new DetectionResult(false, "The log folder does not exist — defaulting to the new remoteAddr format.");

        var files = Directory.GetFiles(logFolder, "*.log");
        if (files.Length == 0)
            return new DetectionResult(false, "No .log files were found in the folder — defaulting to the new remoteAddr format.");

        foreach (var file in files)
        {
            try
            {
                using var reader = new StreamReader(file);
                for (int i = 0; i < 5 && !reader.EndOfStream; i++)
                {
                    var line = reader.ReadLine();
                    if (line == null || !line.Contains("<start "))
                        continue;

                    var match = Regex.Match(line, "appVersion=\"(\\d+)\\.(\\d+)");
                    if (!match.Success)
                        continue;

                    int major = int.Parse(match.Groups[1].Value);
                    int minor = int.Parse(match.Groups[2].Value);
                    bool legacy = major < 9 || (major == 9 && minor < 51);
                    string formatDesc = legacy ? "the legacy remoteAddress format (IP:port in a single field)" : "the new remoteAddr format";
                    return new DetectionResult(legacy,
                        $"Detected Bitvise SSH Server {major}.{minor} in \"{Path.GetFileName(file)}\" → using {formatDesc}.");
                }
            }
            catch (IOException)
            {
                // Filen kan vara låst av den körande servern — prova nästa.
            }
        }

        return new DetectionResult(false,
            "Could not read appVersion from any log file's <start> line — defaulting to the new remoteAddr format. " +
            "Select it manually in the list if the query fails.");
    }
}
