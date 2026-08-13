namespace BitviseLogViewer;

public static class StatsQueryBuilder
{
    // Varje *.xml i Stats-mappen har en enda <stats>-rot (ett konto/grupp/servertotal per fil),
    // till skillnad från loggfilernas upprepade <event>-element. LogParser radar ändå ut samma
    // rad flera gånger per fil när flera XML-filer läses samtidigt i Tree-läge (bekräftat mot
    // skarpa Stats-filer: utan DISTINCT gav en fil med lång dagshistorik tiotusentals dubbletter
    // av samma rad) — därför måste SELECT DISTINCT alltid vara med, precis som i Bitvise egen
    // dokumentationsexempel för Stats-filer.
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
