# DDFLanguageEditor Database Application Demo

Applicazione WinForms separata che mostra l'IDE DDF completo incorporato in una
normale applicazione gestionale.

La parte superiore contiene due caselle di testo (`Nome`, `Cognome`), la casella
`Cliente`, il pulsante `Inserisci tramite script` e la griglia del database
virtuale. La parte inferiore incorpora l'intera shell dell'editor: menu, iconbar,
schede documento, gutter con numeri di riga e breakpoint, colorazione, folding,
formattazione, diagnostica, Output, Flowchart, Watch e comandi di debug.

Il pulsante non inserisce direttamente il record: cattura lo stato della form e
avvia il sorgente DDF visibile nell'editor. Lo script legge l'oggetto host
tipizzato `formData` e richiama l'operazione database `insertCustomer`. Anche F5,
F9 e Ctrl+F5 usano lo stesso catalogo host, quindi il collegamento rimane
debuggabile dall'IDE incorporato.

Il database è deliberatamente in memoria. Sostituendo `DemoDatabaseService` con
un adapter reale, lo script non deve conoscere connessioni, SQL o credenziali.

Avvio:

```powershell
dotnet run --project samples\DDFLanguageEditor.DatabaseDemo\DDFLanguageEditor.DatabaseDemo.csproj
```
