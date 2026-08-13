# Bitvise Log Viewer

A GUI tool (WPF/.NET) that helps you interpret Bitvise SSH Server log files without writing
Microsoft LogParser syntax by hand. The app builds the right LogParser query from your choices in
the GUI and runs it as a subprocess — LogParser still does the actual XML parsing.

Note: the application UI itself is in Swedish. Swedish labels are quoted verbatim below, with an
English gloss in parentheses, so they match what you see on screen.

## Requirements

- .NET 10 SDK (or later) to build.
- [Microsoft LogParser](https://www.microsoft.com/en-us/download/details.aspx?id=24659) installed
  on the machine that runs the app (`winget install Microsoft.LogParser`). The app is a thin GUI
  client on top of LogParser, not a replacement for it.
- Local copies of the Bitvise log files (`*.log`, XML format). If you don't have RDP access to the
  SFTP server: ask someone who does to copy the files from
  `C:\Program Files\Bitvise SSH Server\Logs` to a shared location.

## Running

```
dotnet run
```

## Using the app

The app has two tabs.

### The "Loggfrågor" (Log queries) tab

1. Point out the folder containing the `*.log` files.
2. Tick the event types you're interested in (logons, uploads/downloads, deleted files,
   security/algorithms) and fill in any filters (account, file name, IP, date range).
3. Click **Bygg fråga** (Build query) — the generated LogParser query appears in an editable text
   box (adjust it manually if needed, e.g. for conditions more advanced than the GUI covers).
4. Choose whether the result goes to the **Skärm** (screen — table inside the app) or to a **Fil**
   (file — CSV).
5. Click **Kör** (Run).

The most recent/active log file is held open by the SSH server and cannot be read by LogParser
until it has been copied elsewhere — copy the log files locally before pointing the app at them.

### The "Kontostatistik (Stats)" (Account statistics) tab

Gives a per-account overview (last logon, number of logons, bytes received/sent) built from Bitvise
SSH Server's `Stats` subdirectory (`C:\Program Files\Bitvise SSH Server\Stats` on the server, one
`*.xml` file per account/group) — same copy-locally flow as the log files.

1. Point out the Stats folder.
2. Optionally filter on account name, and tick "Inkludera grupper och servertotal" (Include groups
   and server total) if you also want the aggregated rows (`VirtGroup`/`ServerTotal`) and not just
   individual accounts.
3. Build query → Run, exactly as on the log tab. The result is sorted with the most recently logged
   on account at the top.

## Running LogParser manually (without the app)

The app's **Kopiera fråga** (Copy query) button copies the query the app would run, so you can
paste it straight into `LogParser.exe` from Windows Terminal (PowerShell) if you'd rather run it
yourself or debug a query.

Fixed flags the app always uses on the **log tab**:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -rootXPath:/log/event -o:CSV "<paste the SELECT statement here>"
```

On the **Stats tab**, `-rootXPath` is omitted entirely (the root of the Stats files is `<stats>`,
not repeated `<event>` elements as in the logs — setting `-rootXPath:/log/event` there anyway
produces the error "All nodes at and below root node(s) have no attributes nor values"). There the
`SELECT` must also always be `SELECT DISTINCT`: otherwise LogParser emits the same row once per
`<data>` element in the whole file (total/monthly/daily) when several Stats XML files are read at
once — confirmed against real files, where an account with a long daily history produced tens of
thousands of duplicates of the same row without `DISTINCT`. The pattern is the same as in
[Bitvise's own documentation for Stats files](https://bitvise.com/ssh-server-guide-logparser).

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -o:CSV "<paste the SELECT DISTINCT statement here>"
```

- `-o:CSV` gives the same table format as the app's result view. Switch to `-o:DATAGRID` for an
  interactive, sortable window straight from LogParser (good for quick exploration without going
  through the GUI). `-o:<format>` only selects the **format** — to write to a **file** instead of
  the screen, add `INTO 'path.csv'` to the SQL statement itself, right after the column list and
  before `FROM` (e.g. `SELECT ... INTO 'C:\result.csv' FROM '*.log' WHERE ...`). The app does
  exactly this when you pick **Fil** (File) as the output — there is no `-o:CSV:file` flag despite
  how natural it looks (tested and confirmed wrong: gives `Unknown output format`).
- If you ticked **several** event types in the GUI, the copied text contains several blocks, one
  per category, separated by a heading line `### Kategori`. Remove the heading line and run each
  `SELECT ...;` block on its own — the app also runs them separately, one LogParser invocation per
  category.
- Keep the SELECT statement in **double quotes** at the PowerShell level (as above), since the
  query already contains single quotes internally (e.g. `FROM '<folder>\*.log'` and
  `WHERE ... LIKE '%account%'`).

Concrete example — the log tab (logons, older Bitvise version with `remoteAddress` instead of
`remoteAddr`), verified against a real log folder:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -rootXPath:/log/event -o:CSV "SELECT /event/@time AS Time, /event/conn/@windowsAccount AS WinAccount, /event/conn/@virtualAccount AS VirtAccount, EXTRACT_PREFIX(/event/conn/@remoteAddress, 0, ':') AS RemoteAddress FROM 'C:\Logs\*.log' WHERE /event/@name = 'I_LOGON_AUTH_COMPLETED' AND VirtAccount LIKE '%accountname%'"
```

Concrete example — the Stats tab (account overview), verified against real Stats files. The column
aliases are Swedish because that is what the app generates:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -o:CSV "SELECT DISTINCT /stats/@type AS Typ, /stats/@account AS Konto, /stats/info/@lastLogin AS SenasteInloggning, /stats/total/data/@loginCount AS AntalInloggningar, /stats/total/data/@bytesReceived AS BytesMottagna, /stats/total/data/@bytesSent AS BytesSkickade FROM 'C:\Stats\*.xml' WHERE Typ = 'VirtAccount' ORDER BY SenasteInloggning DESC"
```
