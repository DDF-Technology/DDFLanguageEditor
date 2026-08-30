# DDFLanguageEditor

> **Beta 0.9.2.1 — progetto sperimentale in sviluppo.** La versione disponibile è
> un editor con il primo interprete DDF interno; linguaggio e runtime non sono
> ancora completi né stabili.

DDFLanguageEditor conserva ed espone il primo esperimento WinForms dedicato al
linguaggio sperimentale DDF. Offre un'area di scrittura scura con numeri di riga,
evidenziazione delle regole note e indentazione assistita.

## Funzioni attualmente disponibili

- lexer DDF formale ed evidenziazione alimentata dai token;
- parser con AST tipizzato, precedenze e recupero dagli errori;
- catalogo centralizzato ed estensibile di parole chiave e operatori;
- indice gerarchico e outline navigabile di strutture, funzioni e variabili;
- evidenziazione dei delimitatori corrispondenti e compressione colorata simultanea di più blocchi;
- completamento contestuale ordinato per tipo atteso, scope, frequenza e prossimità, con categoria, tipo e origine;
- formattazione automatica idempotente con `Ctrl+Shift+F` e singolo Undo;
- risoluzione documentale dei simboli, hover informativo, definizione con `F12` e rinomina scoped con `F2`;
- workspace di cartella con explorer, completamento condiviso e navigazione `F12` tra file;
- schede multidocumento con buffer indipendenti, Undo/Redo preservato, indicatore modifiche e Salva tutto;
- breakpoint distinti per file con rimappatura, stato e palette navigabile;
- coppie automatiche, blocchi contestuali, commenta/decommenta e menu contestuale dell'editor;
- duplicazione, spostamento ed eliminazione di righe, con incolla multilinea riallineato al blocco;
- selezione sintattica progressiva/riducibile e salto tra delimitatori corrispondenti;
- cursori multipli con `Alt+Click`, `Ctrl+D` per l'occorrenza successiva e `Ctrl+Shift+L` per tutte;
- type checker per primitivi, strutture e array con diagnostiche `DDF3xx` e hover tipizzato;
- interprete AST interno con funzioni, scope, controllo di flusso, array e strutture;
- menu Esegui con Run (`F5`), Stop (`Shift+F5`) e palette Output;
- diagnostiche runtime `DDF4xx`, cancellazione cooperativa e limite anti-loop;
- stack delle chiamate DDF con errori e frame navigabili dalla palette Output;
- breakpoint di riga attivabili dal gutter o con `F9`, con pausa e continuazione tramite `F5`;
- libreria standard iniziale: `print`, `readLine`, `length`, `toInt` e `toFloat`;
- barra a 27 icone per i principali comandi File, Modifica, IntelliSense, navigazione, folding, breakpoint ed esecuzione;
- interfaccia uniformata al tema chiaro, mantenendo scura soltanto l'area codice;
- palette destra a tutta altezza, tab coerenti e finestre secondarie centrate;
- diagnostica lessicale, sintattica e semantica con pannello navigabile;
- numerazione delle righe visibili in un gutter non selezionabile;
- Outline e Diagnostica pinnabili oppure richiudibili automaticamente;
- conversione di Tab in quattro spazi;
- indentazione multilinea, de-indentazione e rientro automatico dopo `{` e `(`;
- riallineamento automatico di `}` alla graffa aperta corrispondente;
- creazione, apertura e salvataggio di sorgenti `.ddf` in UTF-8;
- protezione delle modifiche non salvate e file recenti;
- trova, sostituisci, scorciatoie standard e barra di stato;
- editor locale senza rete, account o telemetria;
- bozza storica della sintassi in `DDF - Program Language Spec.txt`.

## Limiti della beta

- nessun compilatore, esecuzione passo-passo, ispezione variabili o runtime multi-file;
- nessun overload, generico, conversione personalizzata o sistema di build/progetto;
- la ri-lessicalizzazione riutilizza il prefisso invariato ma analizza ancora
  dalla zona modificata fino alla fine del documento;
- la grammatica 0.4 copre un sottoinsieme sperimentale e può ancora cambiare;
- la vista compressa mostra più blocchi contemporaneamente in sola lettura, mantenendo i
  colori dei token visibili e i numeri di riga originali; `⋯` indica le righe
  nascoste. Il sorgente originale resta nell'editor e viene ripristinato prima
  di modificarlo.

Il contenuto di un documento nuovo rimane in memoria finché non viene salvato.
Prima di chiudere, aprire un altro file o creare un nuovo documento, l'editor
richiede se salvare le modifiche non persistite.

## Requisiti e build

- Windows 10 o 11;
- .NET 10 SDK per compilare e verificare il progetto.

```powershell
dotnet build "DDF - Program Language Editor.sln" --configuration Release --maxcpucount:1
```

La distribuzione ufficiale è **self-contained Windows x64**: include .NET 10 e
non richiede l'installazione separata del Desktop Runtime. Per creare e verificare
lo ZIP distribuibile con il relativo checksum SHA-256:

```powershell
.\tools\publish-release.ps1
```

Gli artefatti vengono scritti in `.artifacts\release`. Tutti i file contenuti
nello ZIP devono restare insieme. La release pubblica è unsigned: Windows può
mostrare un avviso di reputazione.

Per eseguire build Debug, test del core e smoke dinamico WinForms con un unico
comando:

```powershell
.\tests\run-dynamic-smoke.ps1
```

Lo smoke apre il form fuori schermo, usa il vero controllo editor e verifica
che le selezioni mouse non vengano riscritte dal matching, oltre a taglio di
direttive libreria, Undo, diagnostiche transitorie e modifiche rapide. Esercita
inoltre tutti i 38 comandi presenti nei menu File, Modifica, Esegui, Visualizza e Help; la
copertura è descritta in `MENU_TEST_MATRIX.md`. Per osservarlo mentre viene
eseguito:

```powershell
.\tests\run-dynamic-smoke.ps1 -Visible
```

Per eseguire soltanto i test del core dopo una build Debug:

```powershell
.\tests\DDFLanguageEditor.Tests\bin\Debug\DDFLanguageEditor.Tests.exe
```

## Struttura

- `Main.cs`, `Main.Editor.cs`, `Main.Completion.cs`, `Main.Formatting.cs`, `Main.Semantics.cs`, `Main.Workspace.cs`, `Main.Execution.cs` e `Main.Designer.cs`: finestra e flussi utente;
- `FindReplaceForm.cs`: ricerca e sostituzione modeless;
- `AboutForm.cs`: informazioni su autore, sito, licenza e versione beta;
- `DDFLanguageEditor.Core/`: documenti, lexer, parser, AST, analisi semantica e interprete testabile;
- `tests/DDFLanguageEditor.Tests/`: suite di regressione eseguibile senza dipendenze;
- `tests/DDFLanguageEditor.EditorSmokeTests/`: smoke dinamico sul form WinForms reale;
- `tests/run-dynamic-smoke.ps1`: build e gate di test ripetibile;
- `tools/publish-release.ps1`: pubblicazione self-contained Windows x64, validazione, ZIP e SHA-256;
- `samples/editor-smoke-test.ddf`: controllo visivo manuale dell'editor;
- `samples/formatter-smoke-test.ddf`: controllo manuale di formattazione, idempotenza e Undo;
- `samples/lexer/`: corpus lessicale valido e non valido;
- `samples/parser/`: corpus sintattico valido e non valido;
- `LANGUAGE_REFERENCE.md`: sottoinsieme sintattico riconosciuto dall'editor;
- `DDF_GRAMMAR.md`: grammatica e precedenze implementate dal parser;
- `DDF_LANGUAGE_CATALOG.md`: inventario del vocabolario e procedura di estensione;
- `MENU_TEST_MATRIX.md`: copertura dinamica dei comandi e delle scorciatoie dei menu;
- `ROADMAP.md`: piano di sviluppo e registro delle decisioni;
- `DDF - Program Language Spec.txt`: specifica storica in bozza;
- `Properties/`: metadati e risorse dell'applicazione .NET 10.

## Licenza e dipendenze

Codice e documentazione originali sono distribuiti con licenza [MIT](LICENSE),
Copyright © 2026 Fabio De Deo.

Non sono presenti pacchetti NuGet o librerie applicative incorporate. Windows Forms e il
runtime .NET 10 appartengono a Microsoft e sono inclusi nella distribuzione self-contained;
vedere [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## English summary

**Beta 0.9.2.1 — experimental work in progress.** DDFLanguageEditor is a small
Windows Forms source editor with UTF-8 `.ddf` document workflows, recent files,
find/replace, a formal lexer, a typed AST parser, source diagnostics, line
numbers and assisted indentation. It includes a first semantic analyzer but no
external compiler or complete debugger. Its internal AST interpreter reports
navigable runtime failures and DDF call stacks, and supports line breakpoints
with pause/continue. A live document outline indexes structures,
functions, parameters and variables. Matching delimiters and a source-preserving
read-only fold projection are included. Contextual completion ranks catalog,
local and workspace symbols by expected type, scope, usage and proximity, and
shows category, type and origin automatically or with `Ctrl+Space`.
The idempotent document formatter normalizes indentation and token spacing with
`Ctrl+Shift+F` while preserving protected token contents and Undo.
Document-local symbol resolution powers hover information, `F12` definition
navigation and scope-safe rename with `F2`.
A small-folder workspace indexes `.ddf` files recursively and extends completion
and definition navigation across documents.
A runtime-independent type checker validates primitive, structure and array
operations, function calls and returns, with `DDF3xx` diagnostics.
The historical language specification is an unstable design draft.
Source and documentation are available under the MIT License.
