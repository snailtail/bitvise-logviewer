using System.Data;

namespace BitviseLogViewer;

// Slår ihop resultattabeller från flera kategorier (en LogParser-körning per händelsetyp)
// till en gemensam tabell för visning, med en "Kategori"-kolumn och unionen av alla kolumner.
public static class DataTableMerger
{
    public static DataTable Merge(List<(string Label, DataTable Table)> tables)
    {
        var result = new DataTable();
        result.Columns.Add("Kategori");

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
                row["Kategori"] = label;
                foreach (var colName in columnOrder)
                    row[colName] = t.Columns.Contains(colName) ? srcRow[colName] : DBNull.Value;
                result.Rows.Add(row);
            }
        }

        return result;
    }
}
