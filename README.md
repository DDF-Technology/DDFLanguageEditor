# DDFLanguageEditor

> **Beta 0.11.2 — progetto sperimentale in sviluppo.** La versione disponibile è
> un editor con il primo interprete DDF interno; linguaggio e runtime non sono
> ancora completi né stabili.

DDFLanguageEditor conserva ed espone il primo esperimento WinForms dedicato al
linguaggio sperimentale DDF. Offre un'area di scrittura scura con numeri di riga,
evidenziazione delle regole note e indentazione assistita.

## Funzioni attualmente disponibili

Le decisioni di architettura e gli sviluppi pianificati sono raccolti in
[ECOSYSTEM_ARCHITECTURE.md](ECOSYSTEM_ARCHITECTURE.md) e nella
[roadmap](ROADMAP.md): editor incorporabile, profili sintattici, modalità
didattica opzionale, flowchart, backend .NET + Roslyn e assistenza IA locale/cloud.
Questa direzione futura è distinta dalle funzionalità già disponibili elencate sotto.

- lexer DDF formale ed evidenziazione alimentata dai token;
- parser con AST tipizzato, precedenze e recupero dagli errori;
- catalogo centralizzato ed estensibile di parole chiave e operatori;
- pacchetto DDF esterno versionato, selezionabile a runtime da cartella o archivio `.ddflang`, con scelta persistente, validazione atomica e fallback incorporato;
- requisiti opzionali per plug-in nativi identificati esattamente e soggetti ad autorizzazione separata per capacità, senza caricamento implicito di codice host;
- VM IR indipendente dalla sintassi concreta, con operatori tradotti in operazioni controllate e libreria standard dichiarata dal pacchetto;
- manifest `ddf-project.json` separato dal pacchetto, con sorgenti, entry point, dipendenze, backend, output e opzioni riproducibili;
- comandi distinti Convalida, Compila, Avvia senza debug e Avvia debug, con errori multi-file navigabili dall'Output;
- runtime multi-file: `main` può richiamare funzioni e usare strutture o globali dichiarate negli altri sorgenti selezionati dal progetto, inclusi i buffer non salvati;
- catalogo host per istanza con tipi, oggetti, funzioni, firme, documentazione e permessi condivisi da IntelliSense, type checker, build e runtime; accesso sola lettura/lettura-scrittura, durata snapshot/live e conversioni bidirezionali sono espliciti e non usano reflection automatica;
- modalità host separate per sola analisi, simulazione senza scritture esterne ed esecuzione con effetti reali; le operazioni mutanti richiedono un sostituto esplicito per poter essere simulate;
- operazioni host asincrone con token e scadenza, timeout e budget configurabili e risorse per esecuzione rilasciate deterministicamente; il codice non fidato o non cooperativo richiede un processo worker separato;
- ricevuta di build deterministica e buffer non salvati opzionali senza salvataggi impliciti;
- lexer dichiarativo per caratteri, identificatori, numeri, stringhe, escape, commenti e direttive, condiviso da colori e completamento;
- grammatica EBNF caricabile con metadati JSON, AST generico e ruoli keyword estensibili;
- menu `Help > Linguaggio` per cambiare o ripristinare il pacchetto e consultarne versione, origine, sorgente e diagnostiche;
- gestione persistente di installazione, aggiornamento, selezione e disattivazione dei pacchetti di linguaggio;
- playground isolato per token, albero sintattico, diagnostiche e anteprima del formatter;
- test dichiarativi del pacchetto eseguiti come gate prima dell'attivazione;
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
- autosave opzionale con ritardo configurabile per i file già salvati e Hot Exit per recuperare i buffer sporchi al riavvio;
- ripristino della sessione precedente con workspace, ordine e selezione delle schede, cursori e breakpoint;
- recovery locale dopo chiusure inattese, con scelta esplicita e ripristino non distruttivo dei buffer;
- rilevamento delle modifiche esterne con confronto affiancato, ricarica, conservazione del buffer e blocco pre-salvataggio;
- budget prestazionali p95 automatizzati per input, selezione, scroll, colorazione completa e apertura popup;
- aggiornamenti UI deduplicati per gutter, Outline e Diagnostica, con viewport preservata durante la colorazione;
- caricamento workspace cancellabile in background e input reattivo su documenti di dimensioni realistiche;
- schede multidocumento con buffer indipendenti, Undo/Redo preservato, indicatore modifiche e Salva tutto;
- breakpoint distinti per file con rimappatura, stato e palette navigabile;
- coppie automatiche, blocchi contestuali, commenta/decommenta e menu contestuale dell'editor;
- duplicazione, spostamento ed eliminazione di righe, con incolla multilinea riallineato al blocco;
- selezione sintattica progressiva/riducibile e salto tra delimitatori corrispondenti;
- cursori multipli con `Alt+Click`, `Ctrl+D` per l'occorrenza successiva e `Ctrl+Shift+L` per tutte;
- type checker per primitivi, strutture e array con diagnostiche `DDF3xx` e hover tipizzato;
- VM IR indipendente dalla sintassi concreta con funzioni, scope, controllo di flusso, array e strutture;
- menu Esegui con Convalida, Build (`Ctrl+Shift+B`), Run (`Ctrl+F5`), Debug (`F5`), Stop (`Shift+F5`) e palette Output;
- diagnostiche runtime `DDF4xx`, cancellazione cooperativa e limite anti-loop;
- stack delle chiamate DDF con errori e frame navigabili dalla palette Output;
- breakpoint di riga attivabili dal gutter o con `F9`, con pausa/continuazione tramite `F5`, Entra nella funzione con `F11`, Passo successivo con `F10` ed Esci dalla funzione con `Shift+F11`;
- libreria standard iniziale: `print`, `readLine`, `length`, `toInt` e `toFloat`;
- sette barre mobili con 52 comandi a icona e selettore del linguaggio per File, Modifica, ricerca, IntelliSense, correzioni, navigazione, preferenze, visualizzazione, pacchetti, build e debug;
- interfaccia uniformata al tema chiaro, mantenendo scura soltanto l'area codice;
- palette destra a tutta altezza, tab coerenti e finestre secondarie centrate;
- dimensioni e stato pin/auto-hide delle palette destra e inferiore persistenti tra gli avvii;
- diagnostica lessicale, sintattica e semantica con pannello navigabile;
- diagnostiche inline ondulate con messaggio completo al passaggio del mouse, senza alterare testo, colori o Undo;
- correzioni rapide estensibili da menu contestuale, `Ctrl+.` e toolbar per costrutti non terminati, caratteri non validi e token mancanti;
- ripristino strutturale delle graffe nel blocco corretto, con indentazione coerente e recovery dei blocchi annidati;
- analisi e completamento in background con richieste versionate e risultati obsoleti scartati;
- riga corrente, caratteri invisibili, guide di indentazione e word wrap opzionali e persistenti, disegnati senza modificare il sorgente o Undo;
- overview interattiva e disattivabile per errori, avvisi, risultati di ricerca, breakpoint e viewport visibile;
- navigazione `F6`/`Shift+F6` tra editor, Outline e Diagnostica, nomi accessibili, contrasto elevato e layout DPI per-monitor;
- cambio scheda nella palette attiva con `Ctrl+Tab`/`Ctrl+Shift+Tab` oppure `Ctrl+PagGiù`/`Ctrl+PagSu`;
- smoke di endurance multi-documento con limiti per latenza, memoria, handle e risorse grafiche Windows;
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

## Watch — 0.10.0

La palette inferiore **Espressioni di controllo** permette di inserire un nome
come `value`, un campo come `point.x`, un indice calcolato come `values[index]`
oppure un'espressione pura come `(value + 1) * 2` o `value > limit & enabled`.
Premere **Aggiungi** o Invio; selezionare una riga e usare **Rimuovi** per eliminarla.
I valori vengono letti alla pausa prima dell'istruzione evidenziata; durante
Continua restano quelli dell'ultima pausa, indicati come tali. Alla fine vengono
svuotati, mentre l'elenco delle espressioni viene conservato separatamente per
ogni cartella progetto e ripristinato tornando a quel workspace.

La scheda **Variabili** non richiede l'inserimento manuale dei simboli: durante
una pausa mostra automaticamente parametri, variabili locali e globali con tipo
dichiarato e valore. Array e strutture possono essere espansi ad albero; tutti i
contenuti provengono dallo snapshot immutabile della pausa e vengono rimossi al
termine della sessione di debug.

La scheda **Call Stack** elenca il frame corrente seguito dai chiamanti e mostra
funzione, riga, colonna e documento. Doppio clic o Invio raggiungono il punto di
chiamata. Selezionando un frame, la palette Variabili passa ai parametri e ai
locali di quello scope; le variabili dei diversi chiamanti non vengono mescolate.
`F5` continua fino al prossimo breakpoint o alla fine: non si arresta automaticamente
nel chiamante. Per terminare la funzione corrente e fermarsi alla successiva
istruzione del chiamante usare **Esci dalla funzione** (`Shift+F11`); non occorre
aggiungere un breakpoint di ritorno.

Quando la riga sospesa richiama una funzione DDF, **Entra nella funzione** (`F11`)
si arresta sulla prima istruzione eseguita nel nuovo frame. Su una riga senza
chiamate avanza semplicemente alla prossima istruzione, aggiornando le palette di debug.

Prova: aprire `samples/watch-smoke-test.ddf`, inserire `value` e `values[0]`, mettere
un breakpoint sulla riga `value++;` e avviare con F5. Premere ancora F5 per le pause
successive: i valori passano da 0 a 1 a 2. L'esecuzione termina alla continuazione
successiva. Le Watch supportano aritmetica, confronti, logica, parentesi, campi e
indici calcolati usando il pacchetto attivo. Chiamate, assegnazioni, output e
`++`/`--` sono deliberatamente vietati per evitare effetti collaterali. La Watch
e la palette Variabili mostrano il tipo dichiarato dei valori.
Gli snapshot sono limitati a 8 livelli e 4096 elementi/campi per pausa.

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

- nessun compilatore nativo, esecuzione passo-passo, ispezione variabili o runtime multi-file;
- nessun overload, generico, conversione personalizzata o risoluzione automatica delle dipendenze;
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

L'applicazione standalone è ora un launcher separato dalla libreria incorporabile:

```powershell
dotnet run --project DDFLanguageEditor.App\DDFLanguageEditor.App.csproj
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

Per avviare la prima applicazione host separata, che incorpora due istanze
isolate dell'editor con profili e documenti virtuali differenti:

```powershell
dotnet run --project samples\DDFLanguageEditor.HostDemo\DDFLanguageEditor.HostDemo.csproj
```

La demo di integrazione database combina editor, flowchart, regole host,
simulazione, transazioni e debug in una singola applicazione:

```powershell
dotnet run --project samples\DDFLanguageEditor.DatabaseDemo\DDFLanguageEditor.DatabaseDemo.csproj
```

Per iniziare dall'integrazione più piccola possibile:

```powershell
dotnet run --project samples\DDFLanguageEditor.MinimalHost\DDFLanguageEditor.MinimalHost.csproj
```

Le due superfici mantengono servizi, eventi, scorciatoie, palette, build, runtime,
debug e persistenza completamente separati. L'SDK non intercetta la tastiera a
livello globale e non legge o scrive il profilo dell'applicazione ospitante senza
una richiesta esplicita dell'host.

La demo mantiene la persistenza in memoria e non accede automaticamente alle
impostazioni utente o ai file della shell standalone. Ogni profilo dispone di
comandi Esegui/Arresta, Debug qui, Continua, Step Into/Over/Out e Stop debug, di una Watch sicura e di un Output indipendente per verificare runtime, pausa, Call Stack e variabili sui
buffer virtuali.
Il salvataggio dello stato host conserva anche breakpoint e testi Watch, persino
durante una pausa, senza serializzare la VM o permettere di riprendere in seguito
una sessione di debug viva.
I pulsanti `Salva progetto` e `Apri progetto` dimostrano inoltre il nuovo
contenitore host: ciascuna istanza conserva un progetto completo in memoria, ma
la stessa interfaccia può essere implementata sopra file, database o cloud senza
modificare l'editor.
I comandi `Convalida` e `Compila` lavorano sugli stessi buffer virtuali usati da
Run e Debug, mostrano diagnostiche e fingerprint nell'Output e non scrivono
automaticamente artefatti o sorgenti sul file system dell'applicazione host.

Per eseguire build Debug, test del core e smoke dinamico WinForms con un unico
comando:

```powershell
.\tests\run-dynamic-smoke.ps1
```

Lo smoke apre il form fuori schermo, usa il vero controllo editor e verifica
che le selezioni mouse non vengano riscritte dal matching, oltre a taglio di
direttive libreria, Undo, diagnostiche transitorie e modifiche rapide. Esercita
inoltre tutti i 74 comandi presenti nei menu File, Modifica, Esegui, Visualizza e Help; la
copertura è descritta in `MENU_TEST_MATRIX.md`. Misura inoltre i budget p95 definiti
in `EDITOR_PERFORMANCE_BUDGETS.md`. Per osservarlo mentre viene
eseguito:

```powershell
.\tests\run-dynamic-smoke.ps1 -Visible
```

La icon bar è organizzata in sette barre mobili per categoria. Ogni barra può
essere trascinata nell'area superiore oppure mostrata e nascosta da
**Visualizza > Barre degli strumenti**; visibilità, ordine, riga e posizione
vengono ripristinati all'avvio successivo.

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
- `samples/project/`: workspace con `ddf-project.json` pronto per Convalida, Build, Run e Debug;
- `samples/flowchart-workspace/`: workspace multi-file per provare navigazione tra funzioni, gruppi comprimibili, Flowchart e debug attraversando tutti i principali costrutti di controllo;
- `samples/DDFLanguageEditor.HostDemo/`: applicazione WinForms separata con due istanze isolate del controllo incorporabile;
- `samples/DDFLanguageEditor.DatabaseDemo/`: integrazione completa con database di prova, regola personalizzata, transazione, debug e flowchart;
- `samples/DDFLanguageEditor.MinimalHost/`: esempio minimo della shell embedded con esecuzione e persistenza governate dall'host;
- `DDFLanguageEditor.App/`: launcher standalone separato dalle librerie SDK;
- `LANGUAGE_REFERENCE.md`: sottoinsieme sintattico riconosciuto dall'editor;
- `DDF_GRAMMAR.md`: grammatica e precedenze implementate dal parser;
- `DDF_LANGUAGE_CATALOG.md`: inventario del vocabolario e procedura di estensione;
- `LANGUAGE_PACKAGE_FORMAT.md`: contratto, struttura, sicurezza e versionamento dei pacchetti `.ddflang`;
- `LANGUAGE_AUTHORING.md`: guida ai confini dichiarativi, estensioni del motore, plug-in e futuro editor assistito;
- `INTERMEDIATE_REPRESENTATION.md`: pipeline, modello e contratto della IR comune usata dal runtime;
- `NATIVE_EXTENSIONS.md`: confine di sicurezza, identità e autorizzazione deny-by-default dei futuri plug-in nativi;
- `PROJECT_BUILD.md`: manifest progetto, comandi, artefatti riproducibili e limiti multi-file correnti;
- `EDITOR_SDK.md`: primo contratto pubblico della superficie incorporabile, esempio d'uso e limiti correnti;
- `DDF_LANGUAGE_DESIGN.md`: obiettivi, principi e confini progettuali del linguaggio;
- `DDF_CURRENT_LANGUAGE_INVENTORY.md`: fotografia verificabile della sintassi e semantica beta, con decisioni ancora aperte;
- `DDF_LANGUAGE_DECISIONS.md`: registro versionato delle decisioni e delle alternative rifiutate;
- `DDF_LEXICAL_SPEC.md`: contratto normativo di identificatori, letterali, stringhe e commenti DDF 0.12;
- `DDF_OPERATOR_SPEC.md`: contratto normativo di operatori, precedenza e associatività DDF 0.12;
- `DDF_SYNTAX_SPEC.md`: contratto normativo di unità, dichiarazioni, blocchi, funzioni e controllo di flusso;
- `DDF_MIGRATION_0_12.md`: migrazione esplicita dei sorgenti beta alla sintassi 0.12;
- `language-lexicon.schema.json`: schema JSON delle regole lessicali dichiarative;
- `language-grammar.schema.json`: schema JSON della grammatica EBNF, dei nodi e dei metadati IDE;
- `language-intellisense.schema.json`: schema JSON di completamento, simboli, Signature Help e snippet;
- `language-formatting.schema.json`: schema JSON di layout, indentazione, coppie automatiche e Quick Fix lessicali;
- `language-types.schema.json`: schema JSON di primitivi, conversioni, compatibilità, mutabilità, scope e firme;
- `language-semantics.schema.json`: schema JSON dei collegamenti a operazioni semantiche sicure e versionate;
- `language-runtime.schema.json`: schema JSON della VM, dei limiti e dei mapping fra token e operazioni;
- `language-standard-library.schema.json`: schema JSON di firme, documentazione e binding host controllati;
- `language-build.schema.json`: schema dei backend e del formato artefatto supportati dal pacchetto;
- `ddf-project.schema.json`: schema del manifest indipendente del workspace;
- `MENU_TEST_MATRIX.md`: copertura dinamica dei comandi e delle scorciatoie dei menu;
- `EDITOR_PERFORMANCE_BUDGETS.md`: workload, target p95 e ceiling di regressione dell'esperienza editor;
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

**Beta 0.11.2 — experimental work in progress.** DDFLanguageEditor is a small
Windows Forms source editor with UTF-8 `.ddf` document workflows, recent files,
find/replace, a formal lexer, a typed AST parser, source diagnostics, line
numbers and assisted indentation. It includes a first semantic analyzer but no
external compiler or complete debugger. Its syntax-independent IR VM reports
navigable runtime failures and DDF call stacks, and supports line breakpoints,
pause/continue, Step Over and Step Out. A live document outline indexes structures,
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
indentation, saved line endings, current-line highlighting, whitespace markers,
indent guides, word wrap and optional format-on-save across open tabs.
The Shortcuts tab configures every current menu command, rejects conflicts,
synchronizes menus, toolbar hints and the Command Palette, and restores a tested
default preset on demand.
Optional idle autosave handles documents that already have a path. Hot Exit
preserves dirty named and untitled buffers across application restarts, including
their selection and breakpoints. Both safeguards are disabled by default.
Session restoration is enabled by default and reopens the workspace, ordered
tabs, active document, selections and breakpoints while keeping clean documents
backed by their current disk content.
An independent five-second recovery journal is enabled by default for unexpected
termination. On restart, a dialog can restore, discard or postpone the recovered
buffers; restoration never writes to the original files.
The right and bottom palette dimensions and their pinned/auto-hide states are
also restored across application launches.
A runtime-independent type checker validates primitive, structure and array
operations, function calls and returns, with `DDF3xx` diagnostics.
The historical language specification is an unstable design draft.
Source and documentation are available under the MIT License.
