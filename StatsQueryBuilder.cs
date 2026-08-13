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
            where.Add("Typ = 'VirtAccount'");
        if (!string.IsNullOrWhiteSpace(o.AccountFilter))
            where.Add($"Konto LIKE '{QueryBuilder.ToSqlLikePattern(o.AccountFilter)}'");

        string from = $"'{QueryBuilder.EscapeLiteral(o.StatsFolder)}\\*.xml'";
        string whereClause = where.Count > 0 ? $" WHERE {string.Join(" AND ", where)}" : "";

        return "SELECT DISTINCT /stats/@type AS Typ, /stats/@account AS Konto, " +
               "/stats/info/@lastLogin AS SenasteInloggning, /stats/total/data/@loginCount AS AntalInloggningar, " +
               "/stats/total/data/@bytesReceived AS BytesMottagna, /stats/total/data/@bytesSent AS BytesSkickade " +
               $"FROM {from}{whereClause} ORDER BY SenasteInloggning DESC";
    }
}
