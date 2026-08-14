namespace BitviseLogViewer;

public static class QueryBuilder
{
    public record QueryItem(string Label, string EventName, string Sql);

    public static List<QueryItem> Build(QueryOptions o)
    {
        var items = new List<QueryItem>();
        string remoteAddr = RemoteAddressExpr(o);
        string from = $"'{EscapeLiteral(o.LogFolder)}\\*.log'";

        if (o.IncludeLogons)
        {
            var where = new List<string> { "/event/@name = 'I_LOGON_AUTH_COMPLETED'" };
            AddCommonFilters(where, o, hasVirtAccount: true, hasRemoteAddress: true, hasPath: false);
            string sql = $"SELECT /event/@time AS Time, /event/conn/@windowsAccount AS WinAccount, " +
                         $"/event/conn/@virtualAccount AS VirtAccount, {remoteAddr} AS RemoteAddress " +
                         $"FROM {from} WHERE {string.Join(" AND ", where)}";
            items.Add(new QueryItem("Logons", "I_LOGON_AUTH_COMPLETED", sql));
        }

        if (o.IncludeTransfers)
        {
            var where = new List<string> { "/event/@name = 'I_SFS_TRANSFER_FILE'" };
            AddCommonFilters(where, o, hasVirtAccount: true, hasRemoteAddress: true, hasPath: true);
            if (o.TransfersUploadOnly && !o.TransfersDownloadOnly) where.Add("BytesWritten <> 0");
            if (o.TransfersDownloadOnly && !o.TransfersUploadOnly) where.Add("BytesRead <> 0");
            string sql = $"SELECT /event/@time AS Time, /event/conn/@windowsAccount AS WinAccount, " +
                         $"/event/conn/@virtualAccount AS VirtAccount, {remoteAddr} AS RemoteAddress, " +
                         $"/event/sfs/parameters/@path AS Path, /event/sfs/parameters/@bytesWritten AS BytesWritten, " +
                         $"/event/sfs/parameters/@bytesRead AS BytesRead " +
                         $"FROM {from} WHERE {string.Join(" AND ", where)}";
            items.Add(new QueryItem("Uploads/downloads", "I_SFS_TRANSFER_FILE", sql));
        }

        if (o.IncludeRemoves)
        {
            var where = new List<string> { "/event/@name = 'I_SFS_REMOVE_FILE'" };
            AddCommonFilters(where, o, hasVirtAccount: true, hasRemoteAddress: true, hasPath: true);
            string sql = $"SELECT /event/@time AS Time, /event/conn/@windowsAccount AS WinAccount, " +
                         $"/event/conn/@virtualAccount AS VirtAccount, {remoteAddr} AS RemoteAddress, " +
                         $"/event/sfs/parameters/@path AS Path " +
                         $"FROM {from} WHERE {string.Join(" AND ", where)}";
            items.Add(new QueryItem("Deleted files", "I_SFS_REMOVE_FILE", sql));
        }

        if (o.IncludeSecurity)
        {
            var where1 = new List<string> { "/event/@name = 'I_SSH_KEY_EXCHANGE_ALGORITHMS'" };
            AddCommonFilters(where1, o, hasVirtAccount: false, hasRemoteAddress: true, hasPath: false);
            string sql1 = $"SELECT /event/@time AS Time, /event/conn/@id AS ConnId, {remoteAddr} AS RemoteAddress, " +
                          $"/event/parameters/@kexAlg AS KexAlg, /event/parameters/@hostKeyAlg AS HostKeyAlg, " +
                          $"/event/parameters/@cipherAlgIn AS CipherAlgIn, /event/parameters/@cipherAlgOut AS CipherAlgOut " +
                          $"FROM {from} WHERE {string.Join(" AND ", where1)}";
            items.Add(new QueryItem("Algorithms (SSH key exchange)", "I_SSH_KEY_EXCHANGE_ALGORITHMS", sql1));

            var where2 = new List<string> { "/event/@name = 'I_FTP_CONTROL_TLS_NEGOTIATED'" };
            AddCommonFilters(where2, o, hasVirtAccount: false, hasRemoteAddress: true, hasPath: false);
            string sql2 = $"SELECT /event/@time AS Time, /event/conn/@id AS ConnId, {remoteAddr} AS RemoteAddress, " +
                          $"/event/parameters/@protocol AS Protocol, /event/parameters/@cipherSuite AS CipherSuite " +
                          $"FROM {from} WHERE {string.Join(" AND ", where2)}";
            items.Add(new QueryItem("TLS (FTPS)", "I_FTP_CONTROL_TLS_NEGOTIATED", sql2));
        }

        return items;
    }

    private static void AddCommonFilters(List<string> where, QueryOptions o, bool hasVirtAccount, bool hasRemoteAddress, bool hasPath)
    {
        if (hasVirtAccount && !string.IsNullOrWhiteSpace(o.VirtualAccount))
            where.Add($"VirtAccount LIKE '{ToSqlLikePattern(o.VirtualAccount)}'");
        if (hasRemoteAddress && !string.IsNullOrWhiteSpace(o.RemoteIp))
            where.Add($"RemoteAddress LIKE '{ToSqlLikePattern(o.RemoteIp)}'");
        if (hasPath && !string.IsNullOrWhiteSpace(o.FilePattern))
            where.Add($"Path LIKE '{ToSqlLikePattern(o.FilePattern)}'");

        if (!string.IsNullOrWhiteSpace(o.DateFromText) && !string.IsNullOrWhiteSpace(o.DateToText))
            where.Add($"Time BETWEEN '{EscapeLiteral(o.DateFromText)}' AND '{EscapeLiteral(o.DateToText)}'");
        else if (!string.IsNullOrWhiteSpace(o.DateFromText))
            where.Add($"Time >= '{EscapeLiteral(o.DateFromText)}'");
        else if (!string.IsNullOrWhiteSpace(o.DateToText))
            where.Add($"Time <= '{EscapeLiteral(o.DateToText)}'");
    }

    private static string RemoteAddressExpr(QueryOptions o) =>
        o.LegacyRemoteAddressFormat
            ? "EXTRACT_PREFIX(/event/conn/@remoteAddress, 0, ':')"
            : "/event/conn/@remoteAddr";

    // Converts user-friendly patterns ("*.docx", "accountname") to LogParser's SQL LIKE syntax.
    // Without an explicit '%', a "contains" pattern is assumed, to avoid requiring users to know
    // SQL LIKE syntax. Internal (not private) because StatsQueryBuilder reuses the same logic.
    internal static string ToSqlLikePattern(string input)
    {
        var pattern = input.Trim().Replace('*', '%');
        if (!pattern.Contains('%'))
            pattern = $"%{pattern}%";
        return EscapeLiteral(pattern);
    }

    internal static string EscapeLiteral(string s) => s.Replace("'", "''");
}
