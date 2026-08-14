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
        // Log files' repeated <event> elements require -rootXPath, but Stats files' single
        // <stats> root makes it redundant (and produces "no attributes nor values" if set anyway).
        if (!string.IsNullOrEmpty(rootXPath))
            psi.ArgumentList.Add($"-rootXPath:{rootXPath}");
        // -o:<format> only selects the format. File output is controlled by an INTO clause in the
        // SQL itself (verified against LogParser -h -o:CSV) — there is no "-o:CSV:file" flag.
        psi.ArgumentList.Add("-o:CSV");
        string sql = outputToFile && !string.IsNullOrWhiteSpace(outputFilePath)
            ? InsertIntoClause(item.Sql, outputFilePath)
            : item.Sql;
        psi.ArgumentList.Add(sql);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null.");

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string err = stderrTask.Result.Trim();
                if (err.Contains("unknown field", StringComparison.OrdinalIgnoreCase))
                {
                    // The field doesn't occur at all in the selected files (e.g. a TLS/FTPS query against
                    // a log folder that only contains SFTP traffic) — LogParser requires a field to occur
                    // at least once to be able to read it. This is effectively "0 matches", not an error,
                    // so it's treated as a successful, empty result.
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
