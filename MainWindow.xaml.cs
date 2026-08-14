using System.Data;
using System.IO;
using System.Text;
using System.Windows;

namespace BitviseLogViewer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TransfersCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (UploadOnlyCheckBox == null || DownloadOnlyCheckBox == null || TransfersCheckBox == null)
            return;

        bool enabled = TransfersCheckBox.IsChecked == true;
        UploadOnlyCheckBox.IsEnabled = enabled;
        DownloadOnlyCheckBox.IsEnabled = enabled;
        if (!enabled)
        {
            UploadOnlyCheckBox.IsChecked = false;
            DownloadOnlyCheckBox.IsChecked = false;
        }
    }

    private void OutputMode_Changed(object sender, RoutedEventArgs e)
    {
        // Can be triggered by XAML loading before all named controls are wired up.
        if (OutputFileTextBox == null || BrowseOutputButton == null || OutputFileRadio == null)
            return;

        bool toFile = OutputFileRadio.IsChecked == true;
        OutputFileTextBox.IsEnabled = toFile;
        BrowseOutputButton.IsEnabled = toFile;
    }

    private void BrowseLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select the folder containing Bitvise log files" };
        if (dlg.ShowDialog() == true)
            LogFolderTextBox.Text = dlg.FolderName;
    }

    private void BrowseLogParserExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select LogParser.exe",
            Filter = "LogParser.exe|LogParser.exe|Programs (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            LogParserPathTextBox.Text = dlg.FileName;
    }

    private void BrowseOutputFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save results as",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "bitvise-results.csv"
        };
        if (dlg.ShowDialog() == true)
            OutputFileTextBox.Text = dlg.FileName;
    }

    private void CopyQueryButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(QueryTextBox.Text))
        {
            StatusTextBlock.Text = "No query to copy — click Build query first.";
            return;
        }
        try
        {
            Clipboard.SetText(QueryTextBox.Text);
            StatusTextBlock.Text = "Query copied to the clipboard.";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Could not copy: {ex.Message}";
        }
    }

    private void BuildQueryButton_Click(object sender, RoutedEventArgs e)
    {
        BuildQueryFromUi();
    }

    private bool BuildQueryFromUi()
    {
        if (string.IsNullOrWhiteSpace(LogFolderTextBox.Text))
        {
            StatusTextBlock.Text = "Enter a log folder before building the query.";
            return false;
        }

        string logFolder = LogFolderTextBox.Text.Trim();
        bool legacyRemoteAddress;
        string detectionNote = "";

        switch (RemoteAddressFormatComboBox.SelectedIndex)
        {
            case 1:
                legacyRemoteAddress = false;
                break;
            case 2:
                legacyRemoteAddress = true;
                break;
            default:
                var detection = LogFormatDetector.Detect(logFolder);
                legacyRemoteAddress = detection.IsLegacy;
                detectionNote = " " + detection.Message;
                break;
        }

        var options = GatherOptionsFromUi(legacyRemoteAddress);
        var items = QueryBuilder.Build(options);

        if (items.Count == 0)
        {
            StatusTextBlock.Text = "Select at least one event type before building the query.";
            return false;
        }

        QueryTextBox.Text = FormatQueryText(items);
        StatusTextBlock.Text = "Query built. Review/edit if needed, then click Run." + detectionNote;
        return true;
    }

    private QueryOptions GatherOptionsFromUi(bool legacyRemoteAddress) => new()
    {
        LogFolder = LogFolderTextBox.Text.Trim(),
        IncludeLogons = LogonsCheckBox.IsChecked == true,
        IncludeTransfers = TransfersCheckBox.IsChecked == true,
        TransfersUploadOnly = UploadOnlyCheckBox.IsChecked == true,
        TransfersDownloadOnly = DownloadOnlyCheckBox.IsChecked == true,
        IncludeRemoves = RemovesCheckBox.IsChecked == true,
        IncludeSecurity = SecurityCheckBox.IsChecked == true,
        VirtualAccount = VirtualAccountTextBox.Text,
        FilePattern = FilePatternTextBox.Text,
        RemoteIp = RemoteIpTextBox.Text,
        DateFromText = FormatDateTimeText(DateFromPicker.SelectedDate, TimeFromTextBox.Text),
        DateToText = FormatDateTimeText(DateToPicker.SelectedDate, TimeToTextBox.Text),
        LegacyRemoteAddressFormat = legacyRemoteAddress,
        OutputToFile = OutputFileRadio.IsChecked == true,
        OutputFilePath = OutputFileTextBox.Text.Trim(),
    };

    private static string FormatDateTimeText(DateTime? date, string? timeText)
    {
        if (date == null) return "";
        string d = date.Value.ToString("yyyy-MM-dd");
        timeText = timeText?.Trim() ?? "";
        return timeText.Length == 0 ? d : $"{d} {timeText}";
    }

    private static string FormatQueryText(List<QueryBuilder.QueryItem> items) =>
        string.Join("\n\n", items.Select(i => $"### {i.Label}\n{i.Sql};"));

    private static List<(string Label, string Sql)> ParseQueryText(string text)
    {
        var result = new List<(string, string)>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        string? currentLabel = null;
        var sb = new StringBuilder();

        void Flush()
        {
            if (currentLabel != null)
            {
                var sql = sb.ToString().Trim().TrimEnd(';').Trim();
                if (!string.IsNullOrWhiteSpace(sql))
                    result.Add((currentLabel, sql));
            }
            sb.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("### "))
            {
                Flush();
                currentLabel = line[4..].Trim();
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        Flush();
        return result;
    }

    private static string SanitizeForFileName(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        var result = sb.ToString().Trim('-');
        return result.Length == 0 ? "result" : result;
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(QueryTextBox.Text))
        {
            if (!BuildQueryFromUi())
                return;
        }

        var items = ParseQueryText(QueryTextBox.Text);
        if (items.Count == 0)
        {
            StatusTextBlock.Text = "No valid query was found in the query box.";
            return;
        }

        string logFolder = LogFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(logFolder))
        {
            StatusTextBlock.Text = "Enter a log folder before running.";
            return;
        }

        bool outputToFile = OutputFileRadio.IsChecked == true;
        if (outputToFile && string.IsNullOrWhiteSpace(OutputFileTextBox.Text))
        {
            StatusTextBlock.Text = "Enter a file to save the results to, or select Screen as the output.";
            return;
        }

        RunButton.IsEnabled = false;
        BuildQueryButton.IsEnabled = false;
        StatusTextBlock.Text = "Running LogParser...";

        try
        {
            string logParserPath = LogParserPathTextBox.Text.Trim();
            var successTables = new List<(string Label, DataTable Table)>();
            var createdFiles = new List<string>();
            var errors = new List<string>();

            foreach (var (label, sql) in items)
            {
                string? outputPath = null;
                if (outputToFile)
                {
                    outputPath = items.Count == 1
                        ? OutputFileTextBox.Text.Trim()
                        : BuildSuffixedPath(OutputFileTextBox.Text.Trim(), label);
                }

                var queryItem = new QueryBuilder.QueryItem(label, "", sql);
                var result = await LogParserRunner.RunAsync(logParserPath, queryItem, logFolder, outputToFile, outputPath);

                if (!result.Success)
                {
                    errors.Add($"{label}: {result.ErrorMessage}");
                }
                else if (outputToFile && result.OutputFilePath != null)
                {
                    createdFiles.Add(result.OutputFilePath);
                }
                else if (result.Table != null)
                {
                    successTables.Add((label, result.Table));
                }
            }

            if (!outputToFile)
            {
                var merged = successTables.Count > 0
                    ? DataTableMerger.Merge(successTables)
                    : new DataTable();
                ResultsDataGrid.ItemsSource = merged.DefaultView;

                int rowCount = merged.Rows.Count;
                StatusTextBlock.Text = errors.Count > 0
                    ? $"Finished with errors. {rowCount} row(s) shown. Errors: {string.Join(" | ", errors)}"
                    : $"Finished. {rowCount} row(s) shown.";
            }
            else
            {
                StatusTextBlock.Text = errors.Count > 0
                    ? $"Finished with errors. Files created: {string.Join(", ", createdFiles)}. Errors: {string.Join(" | ", errors)}"
                    : $"Finished. Files created: {string.Join(", ", createdFiles)}";
            }
        }
        finally
        {
            RunButton.IsEnabled = true;
            BuildQueryButton.IsEnabled = true;
        }
    }

    private static string BuildSuffixedPath(string basePath, string label)
    {
        string dir = Path.GetDirectoryName(basePath) is { Length: > 0 } d ? d : ".";
        string name = Path.GetFileNameWithoutExtension(basePath);
        string ext = Path.GetExtension(basePath);
        if (string.IsNullOrEmpty(ext)) ext = ".csv";
        return Path.Combine(dir, $"{name}_{SanitizeForFileName(label)}{ext}");
    }

    // --- Account statistics (Stats) ---

    private void StatsOutputMode_Changed(object sender, RoutedEventArgs e)
    {
        if (StatsOutputFileTextBox == null || StatsBrowseOutputButton == null || StatsOutputFileRadio == null)
            return;

        bool toFile = StatsOutputFileRadio.IsChecked == true;
        StatsOutputFileTextBox.IsEnabled = toFile;
        StatsBrowseOutputButton.IsEnabled = toFile;
    }

    private void BrowseStatsFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select the folder containing Bitvise Stats files" };
        if (dlg.ShowDialog() == true)
            StatsFolderTextBox.Text = dlg.FolderName;
    }

    private void BrowseStatsLogParserExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select LogParser.exe",
            Filter = "LogParser.exe|LogParser.exe|Programs (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            StatsLogParserPathTextBox.Text = dlg.FileName;
    }

    private void BrowseStatsOutputFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save results as",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "bitvise-account-statistics.csv"
        };
        if (dlg.ShowDialog() == true)
            StatsOutputFileTextBox.Text = dlg.FileName;
    }

    private void StatsCopyQueryButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StatsQueryTextBox.Text))
        {
            StatusTextBlock.Text = "No query to copy — click Build query first.";
            return;
        }
        try
        {
            Clipboard.SetText(StatsQueryTextBox.Text);
            StatusTextBlock.Text = "Query copied to the clipboard.";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Could not copy: {ex.Message}";
        }
    }

    private void StatsBuildQueryButton_Click(object sender, RoutedEventArgs e)
    {
        BuildStatsQueryFromUi();
    }

    private bool BuildStatsQueryFromUi()
    {
        if (string.IsNullOrWhiteSpace(StatsFolderTextBox.Text))
        {
            StatusTextBlock.Text = "Enter a Stats folder before building the query.";
            return false;
        }

        var options = new StatsQueryOptions
        {
            StatsFolder = StatsFolderTextBox.Text.Trim(),
            AccountFilter = StatsAccountTextBox.Text,
            IncludeGroupsAndServerTotal = StatsIncludeGroupsCheckBox.IsChecked == true,
            OutputToFile = StatsOutputFileRadio.IsChecked == true,
            OutputFilePath = StatsOutputFileTextBox.Text.Trim(),
        };

        StatsQueryTextBox.Text = StatsQueryBuilder.Build(options) + ";";
        StatusTextBlock.Text = "Query built. Review/edit if needed, then click Run.";
        return true;
    }

    private async void StatsRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StatsQueryTextBox.Text))
        {
            if (!BuildStatsQueryFromUi())
                return;
        }

        string sql = StatsQueryTextBox.Text.Trim().TrimEnd(';').Trim();
        if (string.IsNullOrWhiteSpace(sql))
        {
            StatusTextBlock.Text = "No valid query was found in the query box.";
            return;
        }

        string statsFolder = StatsFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(statsFolder))
        {
            StatusTextBlock.Text = "Enter a Stats folder before running.";
            return;
        }

        bool outputToFile = StatsOutputFileRadio.IsChecked == true;
        if (outputToFile && string.IsNullOrWhiteSpace(StatsOutputFileTextBox.Text))
        {
            StatusTextBlock.Text = "Enter a file to save the results to, or select Screen as the output.";
            return;
        }

        StatsRunButton.IsEnabled = false;
        StatsBuildQueryButton.IsEnabled = false;
        StatusTextBlock.Text = "Running LogParser...";

        try
        {
            string logParserPath = StatsLogParserPathTextBox.Text.Trim();
            string? outputPath = outputToFile ? StatsOutputFileTextBox.Text.Trim() : null;
            var queryItem = new QueryBuilder.QueryItem("Account statistics", "", sql);
            var result = await LogParserRunner.RunAsync(logParserPath, queryItem, statsFolder, outputToFile, outputPath, rootXPath: null);

            if (!result.Success)
            {
                StatusTextBlock.Text = $"Error: {result.ErrorMessage}";
            }
            else if (outputToFile)
            {
                StatusTextBlock.Text = $"Finished. File created: {result.OutputFilePath}";
            }
            else
            {
                var table = result.Table ?? new DataTable();
                StatsResultsDataGrid.ItemsSource = table.DefaultView;
                StatusTextBlock.Text = $"Finished. {table.Rows.Count} row(s) shown.";
            }
        }
        finally
        {
            StatsRunButton.IsEnabled = true;
            StatsBuildQueryButton.IsEnabled = true;
        }
    }
}
