using System.Data;
using System.Text;

namespace BitviseLogViewer;

// Minimal CSV-tolkare för LogParsers -o:CSV-utdata (hanterar citerade fält med kommatecken).
public static class CsvParser
{
    public static DataTable Parse(string csvText)
    {
        var table = new DataTable();
        var lines = csvText.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            return table;

        foreach (var h in ParseLine(lines[0]))
            table.Columns.Add(h);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var fields = ParseLine(lines[i]);
            var row = table.NewRow();
            for (int c = 0; c < table.Columns.Count && c < fields.Count; c++)
                row[c] = fields[c];
            table.Rows.Add(row);
        }

        return table;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
