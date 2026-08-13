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
            return new DetectionResult(false, "Loggmappen finns inte — använder nya remoteAddr-formatet som standard.");

        var files = Directory.GetFiles(logFolder, "*.log");
        if (files.Length == 0)
            return new DetectionResult(false, "Inga .log-filer hittades i mappen — använder nya remoteAddr-formatet som standard.");

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
                    string formatDesc = legacy ? "äldre remoteAddress-formatet (IP:port i ett fält)" : "nya remoteAddr-formatet";
                    return new DetectionResult(legacy,
                        $"Upptäckte Bitvise SSH Server {major}.{minor} i \"{Path.GetFileName(file)}\" → använder {formatDesc}.");
                }
            }
            catch (IOException)
            {
                // Filen kan vara låst av den körande servern — prova nästa.
            }
        }

        return new DetectionResult(false,
            "Kunde inte läsa appVersion ur någon loggfils <start>-rad — använder nya remoteAddr-formatet som standard. " +
            "Välj manuellt i listan om frågan misslyckas.");
    }
}
