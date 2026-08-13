# Bitvise-loggviewer

GUI-verktyg (WPF/.NET) som hjälper till att tolka Bitvise SSH Server-loggfiler utan att behöva
skriva Microsoft LogParser-syntax för hand. Appen bygger själv rätt LogParser-fråga utifrån val
i GUI:t och kör den som subprocess — LogParser gör fortfarande själva XML-tolkningen.

## Krav

- .NET 10 SDK (eller senare) för att bygga.
- [Microsoft LogParser](https://www.microsoft.com/en-us/download/details.aspx?id=24659)
  installerat på maskinen som ska köra appen (`winget install Microsoft.LogParser`). Appen är en
  tunn GUI-klient ovanpå LogParser, inte en ersättning för det.
- Lokala kopior av Bitvise-loggfilerna (`*.log`, XML-format). Om du inte har RDP-åtkomst till
  SFTP-servern: be någon med åtkomst kopiera filerna från
  `C:\Program Files\Bitvise SSH Server\Logs` till en delad plats.

## Köra

```
dotnet run
```

## Använda appen

Appen har två flikar.

### Fliken Loggfrågor

1. Peka ut mappen med `*.log`-filer.
2. Kryssa i vilka händelsetyper som är intressanta (inloggningar, upp-/nedladdningar, borttagna
   filer, säkerhet/algoritmer) och fyll i eventuella filter (konto, filnamn, IP, datumintervall).
3. Klicka **Bygg fråga** — den genererade LogParser-frågan visas i ett redigerbart textfält
   (justera manuellt vid behov, t.ex. för mer avancerade villkor än GUI:t täcker).
4. Välj om resultatet ska visas på **skärm** (tabell i appen) eller skrivas till **fil** (CSV).
5. Klicka **Kör**.

Den senaste/aktiva loggfilen hålls öppen av SSH-servern och kan inte läsas av LogParser förrän
den kopierats till en annan plats — kopiera loggfilerna lokalt innan de pekas ut i appen.

### Fliken Kontostatistik (Stats)

Ger en översikt per konto (senaste inloggning, antal inloggningar, bytes mottaget/skickat) byggd
från Bitvise SSH Servers `Stats`-underkatalog (`C:\Program Files\Bitvise SSH Server\Stats` på
servern, en `*.xml`-fil per konto/grupp) — samma kopiera-lokalt-flöde som loggfilerna.

1. Peka ut Stats-mappen.
2. Filtrera valfritt på kontonamn, och kryssa i "Inkludera grupper och servertotal" om du även
   vill se de sammanslagna raderna (`VirtGroup`/`ServerTotal`) och inte bara enskilda konton.
3. Bygg fråga → Kör, precis som på loggfliken. Resultatet sorteras med senast inloggade konto
   överst.

## Köra LogParser manuellt (utan appen)

Appens **Kopiera fråga**-knapp kopierar frågan appen skulle köra, så du kan klistra in den
direkt i `LogParser.exe` från Windows Terminal (PowerShell) om du hellre vill köra den själv
eller felsöka en fråga.

Fasta flaggor appen alltid använder på **loggfliken**:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -rootXPath:/log/event -o:CSV "<klistra in SELECT-satsen här>"
```

På **Stats-fliken** utelämnas `-rootXPath` helt (Stats-filernas rot är `<stats>`, inte upprepade
`<event>`-element som i loggarna — sätter man ändå `-rootXPath:/log/event` där blir felet "All
nodes at and below root node(s) have no attributes nor values"). Där måste `SELECT` också alltid
vara `SELECT DISTINCT`: LogParser radar annars ut samma rad en gång per `<data>`-element i hela
filen (total/monthly/daily) när flera Stats-XML-filer läses samtidigt — bekräftat mot skarpa
filer där ett konto med lång dagshistorik gav tiotusentals dubbletter av samma rad utan
`DISTINCT`. Mönstret är samma som i [Bitvise egen dokumentation för Stats-filer](https://bitvise.com/ssh-server-guide-logparser).

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -o:CSV "<klistra in SELECT DISTINCT-satsen här>"
```

- `-o:CSV` ger samma tabellformat som appens resultatvy. Byt till `-o:DATAGRID` för ett
  interaktivt, sorterbart fönster direkt från LogParser (bra för snabb utforskning utan att gå
  via GUI:t). `-o:<format>` väljer bara **formatet** — för att skriva till **fil** istället för
  skärmen, lägg till `INTO 'sökväg.csv'` i själva SQL-satsen, direkt efter kolumnlistan och
  innan `FROM` (t.ex. `SELECT ... INTO 'C:\resultat.csv' FROM '*.log' WHERE ...`). Appen gör
  exakt detta när du väljer **Fil** som output — det finns ingen `-o:CSV:fil`-flagga trots att
  det ser naturligt ut (testat och bekräftat felaktigt: ger `Unknown output format`).
- Om du kryssat i **flera** händelsetyper i GUI:t innehåller den kopierade texten flera block,
  ett per kategori, separerade med en rubrikrad `### Kategori`. Ta bort rubrikraden och kör
  varje `SELECT ...;`-block för sig — appen kör dem också var för sig, ett LogParser-anrop per
  kategori.
- Håll SELECT-satsen i **dubbla citattecken** på PowerShell-nivå (som ovan) eftersom frågan
  redan innehåller enkla citattecken internt (t.ex. `FROM '<mapp>\*.log'` och
  `WHERE ... LIKE '%konto%'`).

Konkret exempel — loggfliken (inloggningar, äldre Bitvise-version med `remoteAddress` istället
för `remoteAddr`), verifierat mot en skarp loggmapp:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -rootXPath:/log/event -o:CSV "SELECT /event/@time AS Time, /event/conn/@windowsAccount AS WinAccount, /event/conn/@virtualAccount AS VirtAccount, EXTRACT_PREFIX(/event/conn/@remoteAddress, 0, ':') AS RemoteAddress FROM 'C:\Loggar\*.log' WHERE /event/@name = 'I_LOGON_AUTH_COMPLETED' AND VirtAccount LIKE '%kontonamn%'"
```

Konkret exempel — Stats-fliken (kontoöversikt), verifierat mot skarpa Stats-filer:

```powershell
LogParser.exe -q -i:XML -fNames:XPath -fMode:Tree -o:CSV "SELECT DISTINCT /stats/@type AS Typ, /stats/@account AS Konto, /stats/info/@lastLogin AS SenasteInloggning, /stats/total/data/@loginCount AS AntalInloggningar, /stats/total/data/@bytesReceived AS BytesMottagna, /stats/total/data/@bytesSent AS BytesSkickade FROM 'C:\Stats\*.xml' WHERE Typ = 'VirtAccount' ORDER BY SenasteInloggning DESC"
```


