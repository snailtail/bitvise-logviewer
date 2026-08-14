using System.Data;

namespace BitviseLogViewer;

// Merges result tables from multiple categories (one LogParser run per event type)
// into a single table for display, with a "Category" column and the union of all columns.
public static class DataTableMerger
{
    public static DataTable Merge(List<(string Label, DataTable Table)> tables)
    {
        var result = new DataTable();
        result.Columns.Add("Category");

        var columnOrder = new List<string>();
        foreach (var (_, t) in tables)
            foreach (DataColumn col in t.Columns)
                if (!columnOrder.Contains(col.ColumnName))
                    columnOrder.Add(col.ColumnName);

        foreach (var colName in columnOrder)
            result.Columns.Add(colName);

        foreach (var (label, t) in tables)
        {
            foreach (DataRow srcRow in t.Rows)
            {
                var row = result.NewRow();
                row["Category"] = label;
                foreach (var colName in columnOrder)
                    row[colName] = t.Columns.Contains(colName) ? srcRow[colName] : DBNull.Value;
                result.Rows.Add(row);
            }
        }

        return result;
    }
}
