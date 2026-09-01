# DDFLanguageEditor

> **Beta 0.9.4.2 — progetto sperimentale in sviluppo.** La versione disponibile è
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
- Signature Help automatica durante le chiamate, con firma, origine e parametro corrente evidenziato;
- snippet contestuali per funzioni, `main`, strutture e controlli, con campi navigabili tramite `Tab` e `Shift+Tab`;
- formattazione automatica idempotente con `Ctrl+Shift+F`, separazione leggibile tra istruzioni e singolo Undo;
- risoluzione documentale dei simboli, hover strutturato con commenti `///`, definizione con `F12` e rinomina scoped con `F2`;
- workspace di cartella con explorer, completamento condiviso e navigazione `F12` tra file;
- apertura drag-and-drop di uno o più file `.ddf` o di una cartella workspace, con cartelle recenti persistenti;
- ricerca asincrona di testo e simboli in tutto il workspace e nei buffer non salvati, con risultati navigabili;
- sostituzione workspace con anteprima obbligatoria, scelta per occorrenza e un singolo Undo per ogni documento modificato;
- navigazione rapida unificata verso file, simboli, riferimenti semantici, riga/colonna e ultima modifica;
- cronologia di navigazione Indietro/Avanti tra file e selezioni, indipendente da Undo/Redo;
- breadcrumb contestuale cliccabile per file, struttura, funzione e blocchi di controllo annidati;
- Command Palette con `Ctrl+Shift+P`, ricerca immediata, categorie, scorciatoie e disponibilità contestuale;
- preferenze persistenti con `Ctrl+,` per font, zoom, tema del codice, tab/spazi, ampiezza, LF/CRLF e formattazione al salvataggio;
- scorciatoie configurabili dalla relativa scheda delle Impostazioni, con conflitti espliciti, rimozione e preset ripristinabile;
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
- barra a 39 icone per i principali comandi File, Modifica, ricerca, IntelliSense, correzioni, navigazione, preferenze, Command Palette, folding, breakpoint ed esecuzione;
- interfaccia uniformata al tema chiaro, mantenendo scura soltanto l'area codice;
- palette destra a tutta altezza, tab coerenti e finestre secondarie centrate;
- dimensioni e stato pin/auto-hide delle palette destra e inferiore persistenti tra gli avvii;
- diagnostica lessicale, sintattica e semantica con pannello navigabile;
- diagnostiche inline ondulate con messaggio completo al passaggio del mouse, senza alterare testo, colori o Undo;
- correzioni rapide estensibili da menu contestuale, `Ctrl+.` e toolbar per costrutti non terminati, caratteri non validi e token mancanti;
- ripristino strutturale delle graffe nel blocco corretto, con indentazione coerente e recovery dei blocchi annidati;
- analisi e completamento in background con richieste versionate e risultati obsoleti scartati;
- numerazione delle righe visibili in un gutter non selezionabile;
- Outline e Diagnostica pinnabili oppure richiudibili automaticamente;
- indentazione configurabile con Tab o da uno a otto spazi;
- indentazione multilinea, de-indentazione e rientro automatico dopo `{` e `(`;
- riallineamento automatico di `}` alla graffa aperta corrispondente;
- creazione, apertura e salvataggio di sorgenti `.ddf` in UTF-8;
- protezione delle modifiche non salvate e file recenti;
- trova, sostituisci, scorciatoie standard e barra di stato;
- taglia, copia, incolla, annulla e seleziona tutto instradati alla casella di testo focalizzata, con menu contestuale coerente anche nei popup;
- editor locale senza rete, account o telemetria;
- bozza storica della sintassi in `DDF - Program Language Spec.txt`.

## Ricerca nel workspace

Premere `Ctrl+Alt+F` oppure usare **Modifica > Trova nel workspace**. La palette
**Ricerca** permette di scegliere tra testo e dichiarazioni di simboli, attivare
il confronto maiuscole/minuscole e aprire un risultato con doppio clic o `Invio`.
La ricerca comprende anche le schede senza percorso e usa sempre il contenuto
non salvato dei buffer aperti al posto della precedente copia su disco.

Premere `Ctrl+Alt+H` oppure usare **Modifica > Sostituisci nel workspace** per
generare l'anteprima. Ogni occorrenza può essere inclusa o esclusa prima di
**Applica selezionati**. Lo snapshot di tutti i file coinvolti viene ricontrollato
prima di modificare qualunque documento: in caso di conflitto non viene applicata
alcuna sostituzione. I risultati finiscono sempre in schede aperte e non salvate,
senza scrittura automatica su disco, e ogni documento si ripristina con un solo Undo.

## Navigazione rapida

La stessa palette chiara permette di filtrare e raggiungere file (`Ctrl+P`),
simboli (`Ctrl+Shift+O`), riferimenti del simbolo corrente (`Shift+F12`) e una
riga o colonna (`Ctrl+G`). `Ctrl+Shift+Backspace` torna invece alla posizione
dell'ultima modifica nella scheda attiva. File e simboli comprendono i buffer
non salvati; la sola navigazione non modifica il sorgente né la cronologia Undo.
`Alt+Sinistra` e `Alt+Destra` percorrono la cronologia dei salti conservando file,
posizione e selezione. Un nuovo salto dopo essere tornati indietro elimina il
ramo Avanti, come in un browser, senza perdere le modifiche non salvate.
La riga breadcrumb tra le schede e il codice segue il cursore e mostra il percorso
come `file.ddf › main() › if › while`; ogni segmento torna alla relativa
dichiarazione o istruzione e partecipa alla stessa cronologia Indietro/Avanti.

## Command Palette

`Ctrl+Shift+P`, **Visualizza > Command Palette** o il relativo pulsante della
toolbar aprono l'elenco ricercabile delle azioni principali. Ogni risultato
mostra categoria, scorciatoia e disponibilità corrente. La ricerca accetta più
termini, si usa con tastiera o mouse e richiama direttamente lo stesso comando
dei menu, senza duplicarne il comportamento.

## Limiti della beta

- nessun compilatore, esecuzione passo-passo, ispezione variabili o runtime multi-file;
- nessun overload, generico, conversione personalizzata o sistema di build/progetto;
- l'analisi in background mantiene reattiva l'interfaccia ma rielabora ancora
  l'intero documento; non è ancora ottimizzata per sorgenti molto grandi;
- una correzione rapida può ricostruire solo errori diagnosticabili: se la rimozione
  di una graffa produce comunque una forma sintatticamente valida, l'intento originale
  è ambiguo e il blocco va controllato manualmente;
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
inoltre tutti i 51 comandi presenti nei menu File, Modifica, Esegui, Visualizza e Help; la
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

**Beta 0.9.4.2 — experimental work in progress.** DDFLanguageEditor is a small
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
Signature Help follows nested calls and highlights the current parameter for
document, workspace and standard-library functions.
Context-aware snippets expand functions, structures and control-flow blocks at
the current indentation; `Tab` and `Shift+Tab` navigate their editable fields.
The idempotent document formatter normalizes indentation, token spacing and
blank-line separation with `Ctrl+Shift+F` while preserving protected token
contents and Undo.
Document-local symbol resolution powers hover information, `F12` definition
navigation and scope-safe rename with `F2`.
Structured hover includes type, signature, source origin, declaration position,
principal references and contiguous `///` documentation comments.
A small-folder workspace indexes `.ddf` files recursively and extends completion
and definition navigation across documents.
Dropping `.ddf` files opens them in independent tabs, while dropping a folder
opens it as the workspace and stores it in a dedicated recent-workspaces list.
Workspace-wide text and symbol search runs in the background, prefers unsaved
open buffers over disk snapshots and exposes navigable results.
One light navigation palette filters files, symbols and semantic references,
jumps to line/column positions and returns to the last edit without changing Undo.
Browser-style Back/Forward navigation restores document, selection and unsaved
buffer position independently from text Undo/Redo.
A clickable contextual breadcrumb follows the caret through the current file,
structure, function and nested control-flow blocks and shares navigation history.
A searchable Command Palette exposes current menu actions, categories, shortcuts
and availability through `Ctrl+Shift+P` while reusing their original handlers.
Persistent settings opened with `Ctrl+,` configure font, zoom, code theme,
indentation, saved line endings and optional format-on-save across open tabs.
The Shortcuts tab configures every current menu command, rejects conflicts,
synchronizes menus, toolbar hints and the Command Palette, and restores a tested
default preset on demand.
The right and bottom palette dimensions and their pinned/auto-hide states are
also restored across application launches.
A runtime-independent type checker validates primitive, structure and array
operations, function calls and returns, with `DDF3xx` diagnostics.
The historical language specification is an unstable design draft.
Source and documentation are available under the MIT License.
