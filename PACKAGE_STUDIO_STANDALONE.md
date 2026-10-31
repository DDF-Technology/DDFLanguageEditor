# DDF Language Package Studio autonomo

La milestone `0.14.3` fornisce un'applicazione Windows separata dall'IDE per
creare e verificare pacchetti linguaggio. L'app riusa **lo stesso form WinForms**
del Package Studio integrato e le API di authoring del Core; non contiene un
secondo parser, loader o formato di progetto. Richiede Windows x64 per il
profilo self-contained qui documentato.

## Avvio per sviluppo

Dalla radice del repository:

```powershell
dotnet run --project DDFLanguageEditor.PackageStudio -c Release
```

La schermata iniziale permette di aprire il DDF incorporato come modello, una
cartella di authoring o un archivio `.ddflang`. Si può anche passare il percorso
come unico argomento, utile per integrazione e prove automatiche:

```powershell
dotnet run --project DDFLanguageEditor.PackageStudio -c Release -- "D:\DdfPackages\mio-pacchetto"
```

L'apertura lavora su uno snapshot separato. `Genera cartella...` e
`Genera .ddflang...` richiedono una destinazione nuova: non modificano la
cartella o l'archivio originari. Una cartella con JSON temporaneamente errato
rimane apribile nella scheda `Sorgenti JSON / EBNF` per correggere il file;
la convalida e la pubblicazione restano bloccate finché il progetto non è valido.
L'app autonoma non installa né attiva pacchetti nell'IDE e non compila codice DDF.

## EXE self-contained

```powershell
dotnet publish DDFLanguageEditor.PackageStudio/DDFLanguageEditor.PackageStudio.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -o artifacts/package-studio-preview
```

L'EXE è `artifacts/package-studio-preview/DDFLanguagePackageStudio.exe`.
Per uno smoke senza finestre usare `--smoke-home` oppure `--smoke` con una
cartella o un archivio. Questi comandi verificano solo bootstrap e struttura
della UI: non sostituiscono la suite Core e gli smoke WinForms.

Il flusso JSON/EBNF manuale e la CLI di esempio `starter`, `clone-ddf`,
`publish` rimangono disponibili e producono progetti leggibili dallo stesso
Studio. Per le istruzioni di pubblicazione e installazione consultare
[`DDF_LANGUAGE_PACKAGE_PUBLISHING.md`](DDF_LANGUAGE_PACKAGE_PUBLISHING.md).
