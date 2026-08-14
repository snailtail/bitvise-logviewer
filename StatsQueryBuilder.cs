namespace BitviseLogViewer;

public static class StatsQueryBuilder
{
    // Each *.xml in the Stats folder has a single <stats> root (one account/group/server total
    // per file), unlike log files' repeated <event> elements. LogParser still emits the same row
    // multiple times per file when several XML files are read together in Tree mode (confirmed
    // against real Stats files: without DISTINCT, a file with a long day history produced tens of
    // thousands of duplicates of the same row) — so SELECT DISTINCT must always be included, just
    // like in Bitvise's own documentation examples for Stats files.
    public static string Build(StatsQueryOptions o)
    {
        var where = new List<string>();
        if (!o.IncludeGroupsAndServerTotal)
            where.Add("Type = 'VirtAccount'");
        if (!string.IsNullOrWhiteSpace(o.AccountFilter))
            where.Add($"Account LIKE '{QueryBuilder.ToSqlLikePattern(o.AccountFilter)}'");

        string from = $"'{QueryBuilder.EscapeLiteral(o.StatsFolder)}\\*.xml'";
        string whereClause = where.Count > 0 ? $" WHERE {string.Join(" AND ", where)}" : "";

        return "SELECT DISTINCT /stats/@type AS Type, /stats/@account AS Account, " +
               "/stats/info/@lastLogin AS LastLogin, /stats/total/data/@loginCount AS LoginCount, " +
               "/stats/total/data/@bytesReceived AS BytesReceived, /stats/total/data/@bytesSent AS BytesSent " +
               $"FROM {from}{whereClause} ORDER BY LastLogin DESC";
    }
}
