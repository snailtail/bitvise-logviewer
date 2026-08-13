using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;

namespace BitviseLogViewer;

public static class LogParserRunner
{
    public record RunResult(string Label, bool Success, string? ErrorMessage, DataTable? Table, string? OutputFilePath);

    public static async Task<RunResult> RunAsync(
        string logParserPath,
        QueryBuilder.QueryItem item,
        string logFolder,
        bool outputToFile,
        string? outputFilePath,
        string? rootXPath = "/log/event")
    {
        string exe = string.IsNullOrWhiteSpace(logParserPath) ? "LogParser.exe" : logParserPath;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.Exists(logFolder) ? logFolder : Environment.CurrentDirectory
        };

        psi.ArgumentList.Add("-q");
        psi.ArgumentList.Add("-i:XML");
        psi.ArgumentList.Add("-fNames:XPath");
        psi.ArgumentList.Add("-fMode:Tree");
        // Loggfilernas upprepade <event>-element kräver -rootXPath, men Stats-filernas enda
        // <stats>-rot gör det överflödigt (och ger "no attributes nor values" om det ändå sätts).
        if (!string.IsNullOrEmpty(rootXPath))
            psi.ArgumentList.Add($"-rootXPath:{rootXPath}");
        // -o:<format> väljer bara formatet. Filutdata styrs av en INTO-sats i själva SQL:en
        // (verifierat mot LogParser -h -o:CSV) — det finns ingen "-o:CSV:fil"-flagga.
        psi.ArgumentList.Add("-o:CSV");
        string sql = outputToFile && !string.IsNullOrWhiteSpace(outputFilePath)
            ? InsertIntoClause(item.Sql, outputFilePath)
            : item.Sql;
        psi.ArgumentList.Add(sql);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returnerade null.");

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string err = stderrTask.Result.Trim();
                if (err.Contains("unknown field", StringComparison.OrdinalIgnoreCase))
                {
                    // Fältet förekommer inte alls i de valda filerna (t.ex. en TLS/FTPS-fråga mot
                    // en loggmapp som bara innehåller SFTP-trafik) — LogParser kräver att ett
                    // fält förekommer minst en gång för att kunna läsa det. Det här är i praktiken
                    // "0 träffar", inte ett fel, så det behandlas som ett lyckat, tomt resultat.
                    return outputToFile
                        ? new RunResult(item.Label, true, null, null, null)
                        : new RunResult(item.Label, true, null, new DataTable(), null);
                }
                return new RunResult(item.Label, false,
                    string.IsNullOrWhiteSpace(err) ? $"LogParser exited with error code {process.ExitCode}." : err,
                    null, null);
            }

            if (outputToFile)
                return new RunResult(item.Label, true, null, null, outputFilePath);

            var table = CsvParser.Parse(stdoutTask.Result);
            return new RunResult(item.Label, true, null, table, null);
        }
        catch (Win32Exception ex)
        {
            return new RunResult(item.Label, false,
                $"Could not start LogParser (\"{exe}\"): {ex.Message}. " +
                "Check that Microsoft LogParser is installed (winget install Microsoft.LogParser) " +
                "and that the path above is correct, or leave it empty if LogParser.exe is on the PATH.",
                null, null);
        }
    }

    private static string InsertIntoClause(string sql, string outputPath)
    {
        int idx = sql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return sql;

        string escapedPath = outputPath.Replace("'", "''");
        return $"{sql[..idx]} INTO '{escapedPath}'{sql[idx..]}";
    }
}
