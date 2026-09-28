# Changelog

## 0.12.2 - in sviluppo

- Pubblicata la specifica normativa del sistema dei tipi: primitivi espliciti, array monodimensionali e strutture non nulle, unica promozione implicita `int -> float`, nessuna truthiness o overload, shadowing vietato, aritmetica checked e firme con ritorno completo. `dict` viene escluso dal nucleo finché non avrà una semantica parametrica. Ogni tipo eseguibile richiede ora un mapping CLR/C# versionato accettabile da Roslyn (`float` DDF è `System.Double`; le strutture diventano classi CLR generate), senza esporre automaticamente API .NET. Un audit separa tredici gap dell'implementazione beta dal contratto da raggiungere e le decisioni DDF-0009–DDF-0013 ne registrano le motivazioni.
- Completato l'audit trasversale Roslyn: la soluzione .NET 10 compila in Release senza warning e la suite Core supera 209 test, mentre la compilazione dei sorgenti DDF resta correttamente non dichiarata finché non esistono IR bound, mapping CLR eseguibili, runtime ABI, name mangling, riferimenti chiusi, emit/PDB e corpus differenziale. Vietati anche i cicli di contenimento fra strutture non-null. `ROSLYN_COMPATIBILITY_AUDIT.md` definisce dieci gate e DDF-0014 stabilisce che Roslyn emette la semantica già risolta da DDF, senza reinterpretarla.
- Fissato il confine DDF-0015: compilatore Roslyn, emit, linker, loader e pubblicazione saranno disponibili esclusivamente nello standalone/CLI. L'embedded rimane sempre scripting interpretato sulle capacità dell'host; i comandi sono già assenti dalla shell e il futuro compilatore vivrà in un assembly non referenziato dai pacchetti Core/WinForms. L'attuale `BuildService` SDK viene classificato come preparazione IR in memoria e sarà separato nominalmente dal compilatore prima della `0.13`.
- Avviato l'allineamento eseguibile alla specifica tipi `0.12.2`: i tre pacchetti salgono a `0.12.2`, il DDF non include più `dict`, `char` non è numerico, gli array sono monodimensionali, lo shadowing è diagnosticato e il controllo dei ritorni considera tutti i percorsi. Checker e IDE rifiutano inoltre uguaglianza di array/strutture, scrittura negli indici di stringa e cicli diretti o indiretti fra strutture, inclusi i campi array. Un primo corpus negativo versionato protegge le nuove regole; la suite Core sale a 211 test e lo smoke WinForms resta verde sui 74 comandi.
- Allineata la VM al contratto numerico `0.12.2`: `int -> float` converte ora il valore realmente memorizzato in inizializzazioni, assegnazioni, argomenti, campi e ritorni; l'aritmetica `int` usa operazioni checked e la divisione tronca verso zero; potenze intere negative, overflow e risultati `float` non finiti producono rispettivamente `DDF419`, `DDF417` e `DDF418`. Il corpus tipi include casi runtime validi e non validi e la suite Core sale a 212 test.

## 0.12.1 - 2026-09-28

- Fissato il primo nucleo lessicale della `0.12.1`: identificatori Unicode case-sensitive, numeri con cifre ASCII, soli booleani minuscoli, stringhe su una riga ed escape dichiarati. Gli escape sconosciuti producono `DDF005` e una prova Core verifica il contratto.
- Portato il pacchetto DDF a `0.12.1` con la prima chiusura intenzionale degli operatori: `!=`, `&&` e `^^` sostituiscono `!!`, `&` e `|&|`; gli pseudo-operatori di intervallo `>><<`/`<<>>` e radice `^/` sono rimossi. Precedenza, runtime, type checker, campioni e smoke sono allineati e la migrazione dalle beta è documentata.
- Disaccoppiato il type checker dalla grafia degli operatori: validazione unaria, binaria e postfissa usa ora gli ID semantici dichiarati in `runtime.json`, così i profili sintattici possono rinominare gli operatori senza modificare il motore.
- Definito il nucleo sintattico DDF `0.12.1`: direttive prima delle dichiarazioni, globali soltanto dichiarative, nessuna istruzione vuota e blocchi obbligatori per `if`/`else`, cicli e `else if`. `DDF_SYNTAX_SPEC.md` documenta unità, keyword, delimitatori, funzioni, strutture ed espressioni.
- Aggiunto `ValidationDiagnostics`: l'EBNF del pacchetto è ora il validatore normativo usato da analisi, Build e test dichiarativi, mentre le diagnostiche del parser storico restano disponibili separatamente per recupero interattivo, Watch e Quick Fix.
- Aggiunti i pacchetti di riferimento `ddf-it-didactic` e `ddf-c-familiar`: il primo introduce controllo di flusso e booleani italiani, il secondo `return`, `break` e assegnazione `=` senza dichiarare compatibilità con C. Un corpus comune usa la stessa API host, confronta la IR semantica ed esegue entrambi insieme al DDF canonico con risultato equivalente.
- Rimossa l'assunzione interna `true`/`false`: `booleanLiteralValues` associa esplicitamente grafia e valore, che parser e IR conservano per type checker, Watch e VM. I pacchetti storici con le grafie canoniche restano compatibili.
- Completato l’audit di conformità sintattica del nucleo `0.12.1`: l’EBNF non usa più `<Operator>` come scorciatoia universale ma descrive inizializzatori, livelli di precedenza e associatività. Il nuovo corpus valido/non valido verifica i tre profili, formatter idempotente, Quick Fix localizzate, snippet collegati a keyword reali e rifiuto degli alias beta; la prima bozza è ora marcata esplicitamente come storica.
- Il nuovo convertitore semantico riscrive dinamicamente tutte le schede aperte quando viene attivato un profilo equivalente, usando ruoli di keyword, booleani e operatore di inizializzazione, preservando commenti/stringhe/identificatori, senza salvataggi impliciti e con un solo Undo per documento. La suite Core passa a 208 test e lo smoke verifica DDF → italiano → familiare C, analisi senza diagnostiche e Undo/Redo su buffer reali. Corretta inoltre la chiusura dell’ultima scheda: il modello crea il documento vuoto sostitutivo prima di notificare la rimozione, evitando lo stato transitorio senza documento attivo e il conseguente errore UI.
- Reso il DDF l'unico linguaggio incluso e il solo fallback incorporato. Italiano didattico e sintassi familiare C sono ora installati al primo avvio nel catalogo utente, compaiono nel Gestore pacchetti e non sono più caricabili dal vecchio menu `Profili inclusi`; anche cartelle e archivi esterni passano dal flusso di installazione. Le preferenze che puntavano ai vecchi profili distribuiti vengono migrate alla copia installata.
- La palette Flowchart viene ora svuotata quando cambia il documento attivo; chiudendo tutte le schede non resta più visibile il diagramma dell'ultimo file al posto del nuovo documento vuoto.
- Completata la presentazione localizzata dei profili: `intellisense.json` governa ora messaggi diagnostici visibili, etichette `Sì/No` o `Yes/No`, uscita e spiegazioni didattiche della Flowchart. Il messaggio tecnico originale resta stabile per Quick Fix e automazioni; analisi, hover, palette e diagramma applicano il testo del pacchetto attivo. Le seed italiano/C salgono a `0.12.1.1` e una preferenza che usa una loro revisione distribuita precedente viene aggiornata alla nuova copia installata. La suite Core passa a 209 test e lo smoke verifica il cambio reale di presentazione.
- Portato il contratto host a `0.32`: la shell embedded mantiene il Gestore linguaggi disponibile all'utente e ora usa esclusivamente la propria sessione per editor, completamento, workspace, Flowchart, esecuzione e finestre informative. Cambio da menu, `LanguageSession.TryActivate`, `UseEmbedded` e `AttachLanguageSession` attraversano la stessa pipeline, convertono i buffer aperti e aggiornano immediatamente tutti i servizi; soltanto i comandi espliciti dell'utente persistono la scelta, mentre l'host conserva il controllo della propria persistenza. `DdfEmbeddedEditorControl` espone inoltre `ActivateLanguagePackage` e `UseEmbeddedLanguage`.

## 0.12.0 - 2026-09-26

- Definiti identità, casi d'uso, principi e non-obiettivi del linguaggio; inventariati lessico, sintassi, semantica e comportamenti storici; aggiunti confronto mirato con linguaggi affini e registro append-only delle decisioni DDF-0001–DDF-0005. Nessun comportamento del parser o del runtime è stato modificato.

## 0.11.2 - 2026-09-26

- Portato il contratto host alla versione `0.31`: `DdfEditorExecutionService` e `DdfEditorDebugService` accettano un handler pubblico di preparazione della build per modelli progetto posseduti dall'host; il completamento espone l'artefatto realmente eseguito. Run e Debug della shell standalone passano ora dagli stessi servizi dell'SDK conservando workspace multifile, file non aperti, buffer non salvati, breakpoint, Watch, Call Stack, navigazione automatica e Flowchart. Corretta anche una race grafica che poteva mostrare lo stato Pausa prima dello snapshot. La suite Core passa a 202 test e lo smoke completo copre tutti i 74 comandi.
- Portato il contratto host alla versione `0.30`: `DdfEditorBuildService` accetta un handler di build pubblico opzionale mantenendo snapshot, stato ed eventi uniformi. I comandi Convalida/Compila della shell usano ora `IDdfEditorBuildService`, pur conservando manifest, buffer non salvati e ricevuta standalone; il comportamento SDK predefinito resta in-memory e senza I/O implicito. La suite Core passa a 201 test e lo smoke verifica il percorso condiviso.
- Separati gli artefatti distribuibili: il progetto grafico produce ora `DDFLanguageEditor.WinForms.dll`, `DDFLanguageEditor.Core` resta indipendente dalla UI e il nuovo `DDFLanguageEditor.App` genera l'eseguibile standalone. I due SDK sono impacchettabili come NuGet `0.11.2-beta`; il publish self-contained conserva `DDFLanguageEditor.exe`, pacchetto linguistico e schemi.
- Aggiunta `samples/DDFLanguageEditor.MinimalHost`, integrazione essenziale della shell completa con pulsante applicativo, script virtuale, salvataggio controllato dall'host e risultato dell'esecuzione. Le demo non referenziano più un progetto `WinExe`, ma una libreria WinForms autonoma.
- Portato il contratto host alla versione `0.29` con `DdfEmbeddedEditorControl`, controllo WinForms pubblico che incorpora la shell completa e ne governa automaticamente configurazione e lifecycle. La DatabaseDemo non costruisce più direttamente `MainForm`: usa API host per documenti, persistenza, Run, procedure e debug; build, 200 test Core e smoke applicativo mirato risultano superati.
- Il precedente contratto `0.28` separa il profilo embedded da quello standalone: i comandi di compilazione e avvio manuale sono nascosti, `Ctrl+S` delega il salvataggio all'host e la palette `API host` mostra controlli, proprietà scrivibili, eventi, tipi, funzioni, database e permessi dallo stesso catalogo usato da IntelliSense e runtime.
- Corretto il gutter dopo il ripristino della viewport: anche l'ultima riga logica parzialmente visibile viene ora inclusa, evitando un numero mancante al bordo inferiore. Lo smoke rende inoltre deterministica la verifica della Modalità didattica attivando prima la palette Flowchart interessata.
- Eliminata una race di chiusura della shell: l'analisi asincrona conserva il servizio con cui è stata avviata e non accede a `DdfEditorView` dopo che il controllo ne ha completato il rilascio.
- Trasformata `samples/DDFLanguageEditor.DatabaseDemo` in una vera applicazione host: Nome, Cognome e stato Cliente vengono acquisiti da due TextBox e una CheckBox e il pulsante applicativo avvia direttamente lo script DDF visibile. La parte inferiore incorpora l'intera shell IDE con menu, iconbar, schede, colorazione, formattazione, gutter, palette e debugger; F5/F9 e il pulsante condividono lo stesso catalogo host tipizzato e l'operazione `insertCustomer` sul database virtuale. Uno smoke mirato verifica sia l'inserimento normale sia la pausa e la ripresa su breakpoint.
- Portato il contratto host alla versione `0.27`: ogni completamento espone un riepilogo ordinato di funzioni mutanti e scritture host, distinguendo effetti simulati, applicati, parziali, confermati e annullati. La policy predefinita `Immediate` resta compatibile; `RollbackOnFailure` richiede una transazione dell'applicazione e coordina commit/rollback con l'esito della Run, propagandone l'ID opaco alle funzioni. Undo/Redo resta esclusivamente editoriale. I test coprono errore successivo a effetti reali, simulazione, commit e rollback; la suite Core passa a 200 test.
- Portato il contratto host alla versione `0.26` con `DdfHostFunctionLibraryDefinition`: matematica, logica e algoritmi applicativi possono essere raggruppati e registrati atomicamente con documentazione e permessi comuni. Le funzioni confluiscono nello stesso catalogo usato da IntelliSense, type checker, Build, Run e Debug; le mutazioni mantengono delegate reali e di simulazione distinti. La Host Demo usa una libreria per calcolare, validare e applicare un punteggio sugli oggetti dell'applicazione; la suite Core passa a 199 test.
- Portato il contratto host alla versione `0.25` con `IDdfHostDatabaseService`: l'host registra query e comandi come operazioni nominate e tipizzate, riceve esclusivamente parametri strutturati, contesto di cancellazione e un eventuale ID transazione opaco. Connessioni, SQL concreto e credenziali non sono esposti allo script; i comandi rispettano Simulation/HostEffects, permessi e timeout della pipeline host. Una prova con database simulato verifica query strutturate, scritture, transazioni e timeout; la suite Core passa a 198 test.
- Portato il contratto host alla versione `0.24` con `DdfEditorProcedureRequest` e `InvokeAsync`: l'applicazione può richiamare funzioni DDF manualmente o inoltrare eventi espliciti con argomenti nominati, origine osservabile, tipo e risultato immutabile. `DDF416` distingue argomenti mancanti, superflui o non convertibili; la stessa istanza rifiuta invocazioni sovrapposte e riusa modalità, cancellazione, timeout e budget di Run. La Host Demo include comandi Procedura/Evento e la suite Core passa a 197 test.
- Portato il contratto host alla versione `0.23`: ogni Run/Debug restituisce `DdfHostValueResult` immutabili con nome, tipo, valore iniziale/finale, accesso, durata e indicazione di scrittura. Gli snapshot profondi non espongono celle della VM; in simulazione descrivono il risultato senza applicare setter reali. La Host Demo espone variabili e oggetti personalizzati distinti per istanza e li mostra nell'Output; la suite Core passa a 196 test.
- Portato il contratto host alla versione `0.22` con `DdfHostInvocationContext`, delegate `ValueTask` asincroni, cancellazione e scadenza propagate, timeout globale e per chiamata, budget di chiamate e risorse e rilascio LIFO deterministico di `IDisposable`/`IAsyncDisposable`. Le diagnostiche `DDF413`, `DDF414` e `DDF415` separano superamento limiti, timeout e cleanup; la guida chiarisce il threading e richiede isolamento in un processo worker per codice non fidato o non cooperativo. La suite Core passa a 195 test.
- Portato il contratto host alla versione `0.21` separando `AnalysisOnly`, `Simulation` e `HostEffects`. L'analisi compila senza avviare la VM o leggere l'host; la simulazione acquisisce valori isolati, non chiama setter reali e usa esclusivamente delegate `simulate` per funzioni dichiarate mutanti, restituendo `DDF412` quando il sostituto manca; la modalità con effetti conserva il comportamento reale esplicito. Il debug rifiuta la modalità di sola analisi e la suite Core passa a 194 test.
- Portato il contratto host alla versione `0.20`: oggetti e operazioni restano accessibili soltanto se registrati esplicitamente; gli oggetti dichiarano accesso sola lettura o lettura/scrittura e durata `ExecutionSnapshot` o `Live`, mentre `DdfHostValueAdapter` governa le conversioni bidirezionali di oggetti, parametri e risultati. La VM non consegna mai le proprie celle interne ai delegate, limita ogni riferimento alla singola esecuzione e rifiuta scritture non autorizzate con `DDF411`; il type checker le anticipa con `DDF309` e la suite Core passa a 193 test.
- Portato il contratto host alla versione `0.19` con `IDdfHostCatalog` e `DdfEditorHostCatalog`: tipi con campi, oggetti letti tramite getter, funzioni con firme e documentazione e permessi espliciti confluiscono nello stesso snapshot versionato usato da completamento, type checker, Build, Run e Debug. La VM invoca esclusivamente delegate registrati, rifiuta capacità non concesse con `DDF409` e non usa reflection; la suite Core passa a 192 test.
- Portato il contratto host alla versione `0.18` completando l'isolamento multi-istanza: le scorciatoie aggiornano soltanto i metadati del catalogo locale, nessun filtro tastiera globale viene installato e modifica, analisi, build, Run e Debug non attivano persistenza implicita. Lo smoke verifica servizi, eventi, palette, comandi, breakpoint, Watch, snapshot e risultati indipendenti, oltre alla proprietà host dei servizi iniettati.
- Portato il contratto host alla versione `0.17` con `IDdfEditorBuildService`: Convalida e Build usano snapshot immutabili dei buffer e la stessa pipeline in memoria di Run/Debug, senza letture o scritture implicite. `DdfEditorView` inoltra eventi tipizzati per documenti, selezione, diagnostiche, build, esecuzione e debug e registra i comandi `run.validate` e `run.build`; la Host Demo li rende osservabili con fingerprint e diagnostiche. La suite Core passa a 191 test.
- Portato il contratto host alla versione `0.16` con `IDdfEditorProjectContainer` e `DdfEditorProjectState`: l'host può conservare progetti immutabili in file, archivi, database o cloud senza che l'SDK imponga I/O o formato. Apertura e salvataggio includono documenti, preferenze e debug, verificano il pacchetto linguistico e pubblicano eventi e comandi dedicati; la demo espone due contenitori in memoria indipendenti.
- Protetto il pulsante `Apri progetto` della Host Demo: durante Run/Debug comunica che l'operazione non è disponibile, verifica il pacchetto prima di sostituire i documenti e ripristina il servizio precedente se l'apertura fallisce, evitando eccezioni UI non gestite. Lo smoke usa ora i pulsanti reali su entrambi i profili.
- Portato il contratto host alla versione `0.15`: gli snapshot di persistenza includono breakpoint rimappabili e testi Watch, ma escludono esplicitamente VM, Call Stack e valori vivi. Il salvataggio resta disponibile durante una pausa; ripristino e sostituzione dei servizi operativi sono bloccati durante Run/Debug e il debugger posseduto dalla superficie viene annullato in sicurezza alla distruzione.
- Portato il contratto host alla versione `0.14`: `EvaluateWatch` valuta riferimenti, campi, indici ed espressioni pure sul frame selezionato dello snapshot di pausa, mentre rifiuta chiamate, assegnazioni e mutazioni. La demo include un campo Watch con risultato e diagnostica nell'Output.
- Portato il contratto host alla versione `0.13`: ogni pausa pubblica uno snapshot profondamente immutabile con Call Stack multifile, posizione e variabili per frame, variabili visibili, globali e figli di array/strutture. La demo ne mostra una sintesi nel proprio Output.
- Portato il contratto host alla versione `0.12`: `IDdfEditorDebugService` espone ora Step Into, Step Over e Step Out con transizioni di stato osservabili e posizione aggiornata attraverso chiamate multifile. I comandi F11, F10 e Shift+F11 sono registrati dalla superficie incorporabile e disponibili nella demo.
- Portato il contratto host alla versione `0.11` con `IDdfEditorDebugService`: breakpoint associati all'identità dei buffer virtuali, stati Running/Paused/Stopping, posizione sorgente completa, Continue/Stop ed eventi isolati per istanza.
- La demo host può avviare il debug sulla riga corrente, continuarlo o arrestarlo e naviga automaticamente al documento della pausa. Un nuovo test Core copre debug multifile e distruzione dell'host durante una pausa; la suite passa a 190 test.
- Portato il contratto host alla versione `0.10` con `IDdfEditorExecutionService`: ogni istanza fotografa e compila i propri buffer in memoria, espone Run/Stop cooperativo, stato, generazione, output e completamento senza leggere o scrivere sorgenti implicitamente.
- Il servizio rifiuta esecuzioni concorrenti sulla stessa istanza ma consente a host distinti di lavorare in parallelo; build failure, diagnostiche runtime, annullamento ed errori interni rimangono risultati separati e osservabili.
- La demo host espone ora comandi descritti `Esegui`/`Arresta` e un Output per profilo. Lo smoke avvia contemporaneamente i due programmi virtuali e la suite Core verifica multi-file, output, isolamento e arresto.
- Corretto il lifecycle del debugger standalone emerso durante il nuovo smoke: ogni esecuzione cattura la propria sessione e il proprio token, lo stato torna inattivo soltanto dopo il cleanup e il segnale di pausa viene rilasciato dopo l'uscita dell'ultimo waiter. La suite Core passa a 189 test.
- Resa sicura la chiusura della Flowchart durante un aggiornamento asincrono: l'applicazione annulla le richieste pendenti, riporta l'applicazione dello snapshot sul thread UI e impedisce la ricostruzione del menu funzioni dopo l'avvio della distruzione degli handle WinForms.
- Portato il contratto host alla versione `0.9` con `IDdfEditorPersistenceProvider` e snapshot immutabili di documenti e preferenze. L'host decide autonomamente se usare memoria, JSON, database o cloud; l'SDK non esegue I/O implicito.
- `SaveState`, `RestoreState` e i relativi comandi conservano buffer, documento attivo, testo, dirty state, tema, font, zoom, word wrap, tabulazione, scorciatoie e Modalità didattica. Il ripristino rifiuta raccolte documentali già popolate e ripulisce un tentativo parziale in caso di errore.
- Aggiunta `samples/DDFLanguageEditor.HostDemo`, prima applicazione WinForms separata che incorpora due `DdfEditorView` contemporaneamente. I profili tecnico e didattico usano temi, documenti virtuali, configurazioni e provider di persistenza distinti, dimostrando navigazione multi-buffer e ripristino senza accessi impliciti al profilo utente o al file system.
- Esteso lo smoke WinForms per aprire realmente l'host dimostrativo e verificare isolamento fra istanze, analisi e navigazione indipendenti, Modalità didattica e ricostruzione del servizio documentale dai due snapshot host.
- Portato il contratto host alla versione `0.8`: `AnalyzeDocument` produce uno snapshot pubblico coerente e genera `AnalysisCompleted`, riutilizzando testo, versione, pacchetto e buffer esterni forniti dall'host.
- Aggiunti `DdfEditorNavigationService`, `DdfEditorNavigationTarget`, il comando `navigate.goToDefinition`, `TryGoToDefinition` e `DefinitionNavigated`. La risoluzione raggiunge dichiarazioni locali o altri documenti virtuali senza leggere file né dipendere dalle schede della shell; la suite Core passa a 187 test.
- L'interprete e il debugger della shell usano ora un task dedicato a lunga esecuzione, evitando che analisi asincrone concorrenti ritardino l'avvio o la sospensione ai breakpoint durante sessioni intensive.
- Portato il contratto host alla versione `0.7` con `IDdfEditorCommandService`, `IDdfEditorCommand` e `DdfEditorCommandCatalog`: ogni istanza può registrare, trovare, eseguire e rimuovere comandi con ID stabile, metadati e stato dinamico senza conoscere menu o toolbar.
- `TryExecute` rifiuta comandi nascosti o disabilitati; `Refresh` notifica soltanto variazioni effettive di disponibilità, visibilità, selezione e scorciatoia. Cataloghi distinti non condividono registrazioni o stato.
- La Command Palette della shell è ora costruita dal catalogo pubblico e avvia i veri handler tramite il servizio. I principali comandi File, Modifica, Navigazione, Visualizza, Build e Debug hanno costanti pubbliche in `DdfEditorCommandIds`; gli altri mantengono un ID `shell.*` stabile.
- La superficie incorporabile registra ora Undo, Redo, Seleziona tutto, Elimina selezione, Accapo automatico e Focus come comandi nativi privi di dialoghi; un catalogo sostitutivo dell'host conserva le proprie implementazioni omonime.
- Testo, selezione, sola lettura, word wrap e cambio dell'editor rivalutano automaticamente gli stati del catalogo. La shell propaga inoltre aggiornamenti di documento, folding, cronologia e Run/Debug senza attendere l'apertura dei menu o della Command Palette.
- Portato il contratto host alla versione `0.6` con `IDdfEditorAnalysisService`: richieste immutabili catturano documento, posizione e pacchetto linguistico; gli snapshot riuniscono diagnostiche deduplicate, Outline, completamenti, lexer, parser, semantica, tipi, IR e folding senza dipendenze dalla UI.
- Il servizio annulla automaticamente l'analisi asincrona precedente e impedisce a snapshot obsoleti di risultare correnti; istanze diverse possiedono servizi distinti e non modificano il catalogo linguistico globale.
- La pipeline principale della shell usa ora lo stesso servizio pubblico per alimentare evidenziazione, diagnostiche, Outline, folding e Flowchart. Aggiunti test Core e smoke del controllo incorporabile; la suite Core passa a 186 test.
- Anche il popup IntelliSense usa ora richieste e snapshot del servizio pubblico anziché invocare direttamente il motore di completamento. Un coordinatore dedicato evita interferenze con diagnostiche e Outline, mentre testo, cursore, documento e revisione linguistica obsoleti vengono scartati prima di mostrare risultati.
- Il calcolo dei completamenti può essere escluso esplicitamente dagli snapshot destinati soltanto a diagnostiche e Outline; la shell riusa inoltre il risultato del lexer incrementale, mantenendo la colorazione completa entro il budget p95 di 250 ms.
- Portato il contratto host alla versione `0.5` con `IDdfLanguagePackageSession`: ogni istanza seleziona cartelle, archivi o fallback incorporato in modo atomico, conserva diagnostiche e revisione e non modifica il catalogo globale né altri editor.
- Aggiunto `IDdfEditorExperienceState` per la Modalità didattica per istanza. La shell e `DdfEditorView` condividono lo stesso stato osservabile, mentre persistenza, sorgente ed esecuzione restano indipendenti; il controllo `FlowchartView` mantiene il comando visibile dedicato.
- Portato il contratto host alla versione `0.4` con `IDdfEditorHostConfiguration` e opzioni isolate per tema del codice, font, zoom, word wrap, tab/spazi, indentazione e scorciatoie; il servizio non legge né scrive impostazioni globali.
- `DdfEditorView` applica immediatamente le opzioni visuali dell'host e usa tab o spazi secondo la configurazione durante l'inserimento. La shell pubblica attraverso lo stesso contratto le preferenze e le scorciatoie correnti, mantenendo esplicita la propria persistenza.
- Portato il contratto host alla versione `0.3` con `IDdfEditorPaletteService`, `IDdfEditorPalette`, collocazioni, eventi e `DdfEditorPaletteCatalog`; gli host possono registrare, mostrare, nascondere, attivare e rimuovere strumenti senza accedere ai controlli interni.
- La shell registra nel catalogo condiviso File, Flowchart, Outline, Diagnostica, Output, Breakpoint, Variabili, Call Stack, Watch e Ricerca workspace. Il nuovo sottomenu `Visualizza > Palette` usa le stesse API e conserva la visibilità delle singole palette insieme a dimensioni e pin.
- Aggiornato `IDdfEditorSurface` alla versione `0.2`: `DdfEditorView` accetta un `IDdfEditorDocumentService`, segue automaticamente il documento attivo e propaga al buffer le modifiche prodotte dall'utente o dall'host senza cicli di eventi.
- Estratti `DocumentView` e `DdfDocumentViewCoordinator` dalla `MainForm`: la shell conserva editor, scheda, selezioni multiple e viewport per documento tramite un adapter dedicato, mentre testo, identità e stato dirty rimangono unicamente nel servizio pubblico.
- Introdotti `IDdfEditorDocument` e `IDdfEditorDocumentService`: documenti virtuali o associati a un percorso espongono identità, nome, testo, stato dirty e versione; apertura, creazione, attivazione, modifica, salvataggio logico e chiusura producono eventi osservabili.
- `OpenDocumentCollection`, già usata dalla shell standalone, implementa ora il contratto pubblico senza I/O implicito. La chiusura host rifiuta per default documenti modificati e richiede un'autorizzazione esplicita allo scarto; istanze indipendenti sono coperte da test Core.
- Introdotto il primo confine incorporabile: `DdfEditorView` è un controllo WinForms pubblico implementato sopra il contratto versionato `IDdfEditorSurface` `0.1`. Espone testo, selezione, sola lettura, word wrap, focus ed eventi senza pubblicare `RichTextBox`, gutter o dettagli multi-cursore.
- La shell standalone usa ora `DdfEditorView` come superficie centrale e aggiorna il binding quando cambia il documento attivo; lo stesso controllo può essere istanziato isolatamente. Uno smoke dedicato verifica entrambe le modalità e garantisce un evento sorgente deterministico anche prima della creazione dell'handle WinForms.
- Aggiunto `EDITOR_SDK.md` con utilizzo, garanzie e limiti espliciti del primo contratto; linguaggio, diagnostica, palette, build e debug rimangono ancora interni alla shell e saranno estratti nei prossimi incrementi.

## 0.11.1 - 2026-09-13

- Introdotto nel Core un modello di flowchart indipendente dalla UI, con nodi, archi, ruoli semantici e intervalli sorgente navigabili.
- Il builder deriva dalla IR ingresso, uscita, sequenze, condizioni, cicli `while`/`do while`/`for`, chiamate, ritorni, `brk` e `end`, senza dipendere dal testo delle keyword.
- I riferimenti alle funzioni chiamate restano metadati separati dagli archi di controllo; i rami vero/falso e gli archi di ritorno dei cicli conservano informazioni ortogonali.
- Aggiunti test Core per grafo condizionale, chiamate, sorgenti collegati e tutte le forme di ciclo; la suite passa da 174 a 176 test.
- Aggiunto un servizio asincrono versionato per gli snapshot della flowchart: applica un debounce breve, cancella il lavoro precedente e impedisce a risultati obsoleti di sostituire il documento corrente.
- Gli snapshot conservano versione e testo esatti del documento, funzione, percorso, risultato e istante di generazione.
- Chiarito il contratto della flowchart: è una vista derivata in sola lettura e non prevede la generazione o riscrittura automatica del codice tramite modifiche al diagramma.
- Aggiunto un layout Core deterministico per ranghi e la prima palette Flowchart laterale, con forme semantiche, rami Sì/No, archi di ritorno, evidenziazione del nodo al cursore e navigazione al sorgente.
- La palette segue la funzione contenente il cursore, mantiene l'ultima vista valida durante gli errori transitori ed è mostrabile o nascondibile dal menu Visualizza con preferenza persistente; la suite Core passa a 178 test.
- Corretto il layout dei diagrammi articolati: il flusso principale resta nella corsia centrale, i corpi dei cicli usano una corsia laterale e i rami `if`/`else` vengono separati sui due lati, riducendo sovrapposizioni e incroci.
- Aggiunta alla palette una barra compatta con puntatore, zoom avanti/indietro e salvataggio JPEG dell'intero diagramma; il puntatore seleziona il relativo intervallo di codice con un singolo clic.
- Separate le etichette `Sì` e `No` su punti di uscita diversi dei rombi, con riquadro chiaro e colori distinti; i cicli mantengono l'uscita in basso e il ramo del corpo lateralmente.
- Resi completamente ortogonali i raccordi dei collegamenti, incluso l'ultimo tratto d'ingresso nei nodi e gli archi di ritorno tratteggiati; l'export JPEG usa ora un'icona esplicita con immagine e freccia di salvataggio.
- Uniformata la regola di uscita dei collegamenti: ogni linea lascia il modulo con un tratto verticale e devia lateralmente solo dopo un gomito ortogonale; `Sì` e `No` usano distanze di diramazione diverse.
- Ottimizzato il routing: i nodi allineati usano una retta verticale, quelli adiacenti su corsie diverse una sola deviazione laterale, mentre il percorso esterno più articolato resta riservato a ritorni e collegamenti lunghi che devono evitare altri moduli.
- Allineati i gomiti dei rami della stessa decisione; ogni arco tratteggiato usa una corsia dedicata e distinta dai canali continui. Le etichette `Sì`/`No` sono disegnate per ultime su badge opachi, impedendo che qualsiasi linea le sovrasti.
- Sostituito l'instradamento dei collegamenti lunghi verso i bordi estremi con la corsia locale libera più vicina; gli archi convergenti condividono ora lo stesso gomito e tronco finale. I ritorni tratteggiati scelgono canali separati e ogni eventuale sovrapposizione collineare con una linea continua viene esclusa dal rendering.
- Il router prenota progressivamente i segmenti già assegnati e scarta le corsie che produrrebbero intersezioni perpendicolari; restano consentiti soltanto punti di giunzione e tronchi condivisi che rappresentano un ricongiungimento logico.
- Gli snapshot della flowchart preservano zoom e viewport: durante una modifica strutturale il nodo corrente rimane nella stessa posizione sullo schermo; se non è più disponibile viene conservato il centro relativo, mentre il cambio funzione porta in vista il nuovo nodo attivo.
- Aggiunte piccole frecce direzionali immediatamente prima dei ricongiungimenti: vengono individuati i vertici che entrano in un tronco condiviso e il marcatore segue orientamento e colore sia delle linee continue sia di quelle tratteggiate.
- Aggiunta alla barra della Flowchart la modalità Pan con icona a forma di mano: è esclusiva rispetto al puntatore e permette di trascinare liberamente la viewport senza selezionare nodi né modificare il codice.
- La Flowchart permette ora di raggiungere le funzioni chiamate con doppio clic sul nodo o dal menu `ƒ`; le destinazioni vengono risolte anche negli altri file del workspace e l'editor attiva automaticamente documento e funzione corretti.
- Il menu `ƒ` espone anche Torna indietro/Vai avanti con una cronologia dedicata alle chiamate della Flowchart: una chiamata aperta in un altro file torna sempre al punto chiamante anche se, nel frattempo, il cursore visita altre funzioni del file di destinazione. Testi, dimensione e stile del menu sono uniformi ai menu principali.
- Aggiunta la compressione opzionale delle sequenze lineari: il pulsante `▤` crea gruppi collegati al relativo intervallo sorgente, espandibili singolarmente con doppio clic. Il workspace `samples/flowchart-workspace` copre navigazione multi-file, debug, ricorsione controllata, array, strutture e tutti i principali costrutti di flusso; la suite Core passa a 180 test.
- Gli snapshot distinguono ora gli stati aggiornato, parziale recuperabile e non disponibile: durante errori sintattici rappresentabili la palette mantiene un grafo navigabile con avviso ambra `PARZIALE`, mentre gli errori che eliminano la funzione conservano esplicitamente la vista precedente come `NON AGGIORNATA`.
- Le diagnostiche del parser e della IR accompagnano lo snapshot; i costrutti `DDFIR001` omessi dal grafo sono conteggiati nello stato e i dettagli degli errori sono disponibili sulla barra della palette. Dopo la correzione, l'avviso scompare automaticamente senza perdere zoom o viewport; la suite Core passa a 181 test.
- Completata la sincronizzazione codice/nodi: il cursore evidenzia il nodo o gruppo corrispondente e lo porta nella viewport soltanto quando necessario; cliccando un nodo viene selezionato il relativo intervallo sorgente. Frecce, Invio e Spazio consentono la stessa navigazione da tastiera, senza modificare testo, stato del documento o Undo; la navigazione al sorgente viene bloccata sulle viste precedenti con intervalli obsoleti.
- Collegata la Flowchart al debugger usando i moduli IR immutabili prodotti dalla build realmente in esecuzione: breakpoint, Step Into, Step Over e Step Out aggiornano documento, funzione e nodo anche tra file diversi. Il nodo runtime è distinto dalla selezione tramite riempimento giallo, bordo arancio e indicatore verde; la palette viene portata in primo piano alla pausa e l'indicatore scompare durante Continua, Stop e al termine della sessione.
- Estratta l'intera superficie grafica nel controllo pubblico `FlowchartView`: canvas, stato, toolbar, puntatore, Pan, gruppi, zoom e icone non vengono più costruiti dalla `MainForm`. Il controllo espone diagramma, selezione, posizione runtime, navigazione, zoom, avvisi ed export tramite proprietà, metodi ed eventi; uno smoke lo istanzia e usa anche isolatamente dalla shell, preparando il confezionamento SDK della `0.11.2`.
- Aggiunta la Modalità didattica indipendente dal profilo sintattico: è sempre commutabile dal menu `Visualizza` e dalla barra del controllo `FlowchartView`, che ne espone proprietà ed evento per gli host incorporanti. La shell ne persiste lo stato per utente; il cambio modifica soltanto la presentazione didattica e conserva sorgente, selezione, Undo, debug, flowchart ordinaria e impostazioni di build.
- La Modalità didattica mostra ora un pannello contestuale che spiega in linguaggio semplice ingressi, istruzioni, chiamate, decisioni, cicli, ritorni, interruzioni, nodi parziali e gruppi. I rami `Sì`/`No` della decisione selezionata vengono enfatizzati in verde e rosso; durante una pausa sono mostrati il passo corrente e i valori immutabili delle variabili coinvolte nell'istruzione.
- L'esportazione della Flowchart propone ora SVG come formato predefinito, mantenendo JPEG come alternativa. Il file vettoriale riusa layout e routing della vista completa e conserva forme, testi, frecce, rami tratteggiati, ricongiungimenti, nodo selezionato/runtime ed enfasi della Modalità didattica senza incorporare immagini raster.
- Chiuso il gate della `0.11.1` con una matrice strutturale sul campione complesso: raggiungibilità, biforcazioni, cicli, terminali, chiamate, intervalli sorgente, assenza di nodi sovrapposti e monotonia degli archi vengono ora verificati automaticamente. La suite Core passa a 182 test; lo smoke WinForms copre inoltre snapshot parziali, workspace e debug multi-file, export e un budget dedicato alla pipeline build/layout/proiezione con target p95 di 50 ms.

## 0.11.0 - 2026-09-06

- La VM esegue ora un unico programma composto da tutti i sorgenti selezionati dal manifest, inclusi i buffer aperti modificati ma non salvati.
- Funzioni, strutture e variabili globali sono risolte tra file: `main` può richiamare direttamente una funzione dichiarata in un altro modulo del progetto.
- Breakpoint, Step Into/Over/Out, Call Stack, Watch, Variabili e diagnostiche runtime conservano documento e intervallo sorgente e navigano automaticamente tra le schede.
- Lo switch del debugger forza ora anche l'aggiornamento visivo della linguetta mentre le schede sono protette; `F11`, `F10` e `Shift+F11` sono intercettati esplicitamente e percorrono istruzione per istruzione anche le funzioni esterne.
- I breakpoint sono identificati da percorso e riga, evitando collisioni tra righe uguali di file differenti.
- La build segnala dichiarazioni runtime duplicate con `DDFB010` e dipendenze globali in avanti o cicliche con `DDFB011`; l'ordine di inizializzazione è deterministico.
- `@@'...'` resta una direttiva di libreria/capacità: i file del programma sono scelti esclusivamente da `sources` in `ddf-project.json`.
- Aggiunti test Core e smoke WinForms end-to-end per chiamate, buffer non salvati, breakpoint, scorciatoie step, switch visibile e ritorno al chiamante tra file.

## 0.10.1 - 2026-09-06

- Il debugger aggiunge Entra nella funzione (`F11`): da una chiamata sospesa raggiunge la prima istruzione DDF eseguita nel nuovo frame; sulle altre istruzioni avanza al passo seguente.
- Step Into aggiorna evidenziazione sorgente, Variabili, Watch e Call Stack e condivide gli stati sicuri di pausa, continuazione, arresto e fine esecuzione con Step Over e Step Out.
- Il comando è disponibile dal menu Esegui e dalla barra mobile Esecuzione e debug, con shortcut configurabile e disabilitazione fuori dalla pausa.
- I test Core e gli smoke WinForms coprono ingresso nella funzione, chiamante, proseguimento e coerenza dei comandi.

## 0.10.0 - 2026-09-06

- La singola icon bar è stata suddivisa in sette barre mobili per categoria (File, Modifica, Codice, Navigazione, Visualizzazione, Progetto/Linguaggio, Esecuzione/Debug); il menu Visualizza può mostrarle o nasconderle singolarmente e visibilità, ordine, riga e posizione persistono tra gli avvii anche con scaling DPI.
- Il ripristino delle barre ricostruisce ora le righe in ordine deterministico invece di affidarsi all'ordine di inserimento del `ToolStripPanel`; uno smoke di riapertura verifica ordine personalizzato e barre nascoste.
- L'area occupata da menu e barre mobili viene ora riservata nel layout dell'editor: le linguette dei documenti e le prime righe non possono più restare coperte, anche quando cambia la visibilità delle barre.
- Il debugger espone ora Passo successivo (`F10`), aggiorna anche gli elementi array nella Watch a ogni pausa, evidenzia le righe con breakpoint prima dell'avvio e mostra uno stato visibile Esecuzione/Pausa bloccando i comandi che potrebbero modificare il sorgente durante la sessione.
- La nuova palette Variabili elenca automaticamente parametri, locali e globali con tipo dichiarato e valore; array e strutture sono esplorabili ad albero tramite copie immutabili acquisite alla pausa.
- La nuova palette Call Stack mostra il frame corrente e i chiamanti con funzione, riga, colonna e documento; doppio clic o Invio navigano al punto di chiamata e ogni frame conserva l'identità sorgente per la futura esecuzione multi-file.
- Selezionando un frame del Call Stack, la palette Variabili mostra parametri e locali appartenenti esclusivamente a quello scope; il frame corrente indica l'istruzione sospesa e i chiamanti il relativo punto di chiamata.
- Durante una pausa del debugger l'intera riga corrente è ora evidenziata in giallo senza alterare colori sintattici o testo; l'indicatore scompare con Continua, Stop e fine esecuzione.
- F5 e Ctrl+F5 rispettano ora il documento attivo quando è esterno al workspace aperto o non è ancora salvato; i file appartenenti al workspace continuano invece a usare l'entry point del progetto.
- Nei workspace privi di manifest, Debug e Run individuano ora l'unica `main` invece di usare la scheda attiva; più definizioni di `main` bloccano la build con `DDFB009` e i breakpoint appartenenti a sorgenti non eseguiti non sospendono la sessione.
- Il debugger espone ora Esci dalla funzione (`Shift+F11`): completa il frame corrente, torna alla prima istruzione successiva nel chiamante e aggiorna riga, Call Stack, Variabili e Watch.
- Il rilevamento delle modifiche esterne riconosce come uguali disco e buffer anche quando differiscono soltanto per terminazioni di riga; una scrittura esterna già coincidente viene sincronizzata senza mostrare un falso confronto.
- La Watch valuta ora espressioni pure tramite lexer, precedenze e binding runtime del pacchetto attivo: aritmetica, confronti, logica, parentesi, campi e indici calcolati sono supportati senza invocare il runtime.
- Assegnazioni, output, chiamate e operatori postfix vengono rifiutati prima della valutazione; lo snapshot acquisito alla pausa resta immutabile.
- Le espressioni Watch vengono memorizzate separatamente per cartella progetto e ripristinate quando si riapre o si torna al workspace.

- distinguere l'icona Gestione pacchetti da Impostazioni e allineare i comandi del gestore su due righe stabili;
- correggere il pulsante Chiudi del Playground aperto in modalità non modale;
- mantenere identità e breakpoint del buffer senza percorso durante il debug, senza sostituirli con quelli di un'altra scheda;
- aggiungere smoke di chiusura reale, allineamento alla dimensione minima e pausa nei documenti non salvati.

- aggiungere Espressioni di controllo (Watch) nella palette inferiore, con nomi, campi e indici costanti in sola lettura;
- acquisire snapshot solo ai breakpoint e preservare campi/elementi senza riferimenti mutabili al runtime;
- aggiornare i valori alle pause, indicare snapshot precedenti durante Continua e cancellare i valori a fine esecuzione;
- escludere le variabili locali dei chiamanti dallo scope Watch e segnalare percorsi non disponibili;
- mantenere la milestone aperta: espressioni pure, persistenza Watch e integrazione multi-file restano da completare.

## 0.9.6.7 - 2026-09-05

- aggiungere un archivio locale versionato per installare e aggiornare atomicamente pacchetti da cartella o `.ddflang`;
- consentire selezione, convalida e disattivazione dei pacchetti dalla nuova finestra di gestione;
- introdurre un playground isolato con token, albero sintattico, diagnostiche e anteprima del formatter;
- aggiungere `tests.json` e il relativo schema, eseguendo il corpus dichiarativo prima di ogni attivazione;
- rifiutare con `DDFT001` un pacchetto i cui test non passano, conservando il linguaggio già attivo;
- documentare il confine fra personalizzazione dichiarativa, nuove operazioni del motore e plug-in autorizzati;
- verificare un secondo linguaggio con keyword e operatori differenti senza ricompilare l'editor;
- estendere la suite a 171 test Core, 63 comandi di menu e 49 pulsanti della toolbar;

## 0.9.6.6 - 2026-09-05

- separare il pacchetto linguaggio dal nuovo manifest workspace `ddf-project.json`;
- selezionare sorgenti, entry point, dipendenze versionate, backend, output e opzioni senza modificare il pacchetto;
- introdurre il contratto sostituibile `IDdfBuildBackend` e il primo backend `ddf-vm-ir-v1` basato sulla IR comune;
- aggiungere i comandi Convalida, Compila, Avvia senza debug e Avvia debug anche nella icon bar;
- usare opzionalmente i buffer aperti non salvati senza salvarli implicitamente;
- produrre una ricevuta JSON deterministica con versione esatta, hash dei sorgenti e fingerprint della configurazione;
- rendere navigabili dall'Output le diagnostiche di compilazione appartenenti a file diversi;
- pubblicare `language-build.schema.json`, `ddf-project.schema.json`, documentazione e progetto di esempio;
- verificare la pipeline con 168 test Core e smoke WinForms estesi ai 60 comandi di menu;

## 0.9.6.5 - 2026-09-05

- introdurre `DdfVirtualMachine` come consumer esclusivo della IR, lasciando parsing e lowering nella facciata di compatibilità;
- tradurre gli operatori concreti in identificatori di operazione stabili durante il lowering, eliminando dalla VM i confronti con i simboli DDF;
- definire un insieme chiuso di operazioni per valori, scope, chiamate, flusso, array, strutture e accesso host;
- rendere entry point, limite predefinito, destinazione output e binding delle espressioni configurabili tramite `runtime.json`;
- spostare nomi, firme, documentazione e binding delle funzioni standard in `standard-library.json`;
- consentire soltanto binding host incorporati e validati, senza reflection, script o risoluzione di metodi dal JSON;
- conservare cancellazione, limite istruzioni, coordinate sorgente, stack, breakpoint e snapshot immutabili dello stato runtime;
- pubblicare gli schemi `language-runtime.schema.json` e `language-standard-library.schema.json`;
- verificare esecuzione con un operatore di assegnazione rinominato nel pacchetto e con una funzione standard rinominata senza ricompilare il motore;

## 0.9.6.4 - 2026-09-05

- trasformare `types.json` in un contratto dichiarativo validato per primitivi, conversioni, compatibilità, mutabilità, scope, firme, literal, numeri e indicizzazione;
- collegare il type checker e il tipo atteso dell'IntelliSense allo stesso snapshot del pacchetto usato da lexer e parser;
- consentire di modificare una conversione nel pacchetto e osservarne l'effetto senza ricompilare l'editor;
- rifiutare atomicamente riferimenti e categorie di tipo incoerenti con diagnostiche `DDFL09x`;
- pubblicare `language-types.schema.json` e includerlo negli output e nelle pubblicazioni self-contained;
- collegare le produzioni dell'AST generico al catalogo chiuso `ddf-safe-semantics` `1.0` tramite `semantics.json`;
- validare regole, campi, argomenti e identificatori delle operazioni prima dell'attivazione del pacchetto;
- produrre un piano semantico con intervalli sorgente senza consentire script, reflection, delegate o codice host arbitrario;
- pubblicare `language-semantics.schema.json` e includerlo negli output e nelle pubblicazioni self-contained;
- introdurre la IR pubblica `1.0` con nodi propri per programma, dichiarazioni, flusso, tipi ed espressioni;
- conservare piano semantico e intervalli sorgente nella IR per backend, diagnostiche, breakpoint e stack trace;
- spostare completamente l'interprete dalle classi `*Syntax` ai nodi `DdfIr*`, mantenendo un overload legacy che effettua il lowering;
- costruire la IR negli snapshot dell'editor e bloccare Run sulle eventuali diagnostiche di lowering;
- classificare ogni diagnostica come codice sorgente, definizione del linguaggio, funzionalità del motore o runtime;
- distinguere le incompatibilità di adapter, API e operazioni sicure dalle definizioni JSON non valide durante il caricamento atomico;
- mostrare l'origine nelle righe, negli hover e nel riepilogo della palette Diagnostica, usando codici `DDFE00x` per i limiti del motore incontrati in esecuzione;
- aggiungere al manifest requisiti opzionali per plug-in nativi identificati da ID, autore, versione esatta, SHA-256 e capacità richieste;
- applicare una policy deny-by-default che autorizza separatamente ogni identità e capacità senza consentire al pacchetto di approvare se stesso;
- mostrare i requisiti nativi nelle informazioni del pacchetto senza cercare, caricare o eseguire assembly esterni;
- mantenere stabile la viewport verticale durante apertura, formattazione, salvataggio e cambio scheda, persistendola inoltre per ogni documento nella sessione del progetto;

## 0.9.6.3 - 2026-09-05

- dichiarare in `grammar.json` i metadati IDE per simboli, folding, breadcrumb e nodi selezionabili;
- alimentare Outline, folding, breadcrumb, selezione sintattica e navigazione workspace dall'AST generico del pacchetto;
- validare atomicamente riferimenti errati dei metadati IDE e mantenere il fallback legacy sui documenti incompleti;
- preservare temporaneamente i dettagli tipizzati legacy dei simboli finché completamento, hover e Signature Help non migreranno al pacchetto;
- verificare che una modifica al solo pacchetto cambi il breadcrumb senza ricompilare il motore;
- caricare da `intellisense.json` snippet, descrizioni di completamento, origini di Signature Help ed etichette/documentazione hover;
- derivare tipi, firme e classificazione contestuale dei simboli dai campi dichiarati nell'AST generico;
- caricare da `formatting.json` ruoli dei delimitatori, separazione delle istruzioni, trigger di indentazione, coppie automatiche e Quick Fix lessicali;
- mantenere le Quick Fix sintattiche collegate ai token attesi dal parser senza duplicare le produzioni EBNF;
- consentire il cambio atomico del pacchetto attivo da cartella o archivio `.ddflang`, con ripristino esplicito del DDF incorporato;
- persistere la scelta e aggiornare servizi editor, workspace, filtri file, menu, impostazioni, icon bar e barra di stato senza riavvio;
- mantenere attivo il linguaggio precedente e mostrare le diagnostiche quando il nuovo pacchetto non supera la validazione;
- associare una revisione atomica a ogni pacchetto attivo e scartare analisi, completamenti e indicizzazioni prodotti da revisioni superate;
- verificare sei cambi consecutivi fra pacchetti da 256 keyword e 96 snippet con un documento da 2.000 righe e un workspace da 40 file;
- aggiungere un budget p95 dedicato al cambio di pacchetto e chiudere tutti i requisiti della milestone `0.9.6.3`;

## 0.9.6.2 - 2026-09-02

- make `grammar.ebnf` the canonical package syntax and keep JSON limited to compilation and node metadata;
- compile grammar packages atomically with start-rule, reference, node-field and left-recursion validation;
- expose a generic typed-by-kind syntax tree with declared fields, exact spans and missing-syntax recovery diagnostics;
- retain the typed DDF AST as a compatibility projection while IDE services migrate in 0.9.6.3;
- replace the closed keyword-role loader enum with extensible role identifiers;
- add DDF `else` across lexing, parsing, traversal, type checking, folding, breakpoints and runtime execution;
- publish `language-grammar.schema.json` and include EBNF in embedded, folder, archive and self-contained packages;
- extend the Core suite with canonical-corpus, incomplete-source, extensible-role, `else` runtime and invalid-grammar cases;
- keep gutter numbering tied to source newlines after multi-file/session restore, including wrapped and folded views;

## 0.9.6.1 - 2026-09-02

- move whitespace, identifier, number, string, escape, line/block comment and library-directive rules into `lexicon.json`;
- introduce safe declarative character sets and delimited-token rules without regular expressions or executable host code;
- apply configurable longest-match ordering to overlapping token markers and retain longest-match operator handling;
- derive lexer, syntax classification, rename validation and lexical completion prefixes from one loaded definition;
- remove the fixed `@@'...` assumption from library completion and support package-defined directive delimiters;
- reject incomplete definitions, duplicate markers, unreachable rules and character-class/category conflicts atomically during package loading;
- preserve exact token spans, configured unterminated-construct diagnostics and incremental re-lexing across custom lexical rules;
- publish a strict `language-lexicon.schema.json` and verify a substantially different external lexer without rebuilding the editor;

## 0.9.6.0 - 2026-09-02

- define the versioned `1.0` contract for folder-based or archived `.ddflang` language packages;
- split the reference DDF package into manifest, lexicon, grammar, types, semantics, formatting, IntelliSense, runtime, build and standard-library documents;
- load the existing DDF vocabulary from external JSON while preserving lexer, parser and formatter behavior;
- embed the complete reference package in Core and activate it atomically when the external copy is missing or invalid;
- reject incompatible versions, unsafe or duplicate references, unknown sections, unsupported adapters and duplicate lexical definitions with file/JSON-path diagnostics;
- display active language, language version, package version, source and origin in the application status area;
- add a discoverable Help popup for the active package, its effective source and all load/fallback diagnostics;
- verify embedded, directory, `.ddflang` archive, invalid-package and fallback paths in the dependency-free Core suite;

## 0.9.5.6 - 2026-09-02

- add an accelerated endurance gate that runs 160 mixed editing cycles across four independent document tabs;
- alternate transient edits, Undo/Redo, background analysis, caret and overview updates, pane/tab keyboard navigation, word wrap, formatting, folding and repeated Command Palette lifetimes;
- verify a UI-thread heartbeat, all document contents and the absence of new WinForms thread exceptions throughout the session;
- establish bounded growth gates for managed memory, process handles and Windows GDI/USER resources after forced collection;
- record per-iteration p95 latency and retain the existing input, selection, scroll, highlighting, popup and realistic workspace budgets;
- complete the `0.9.5` fluidity, accessibility and refinement milestone;

## 0.9.5.5 - 2026-09-02

- add bidirectional `F6`/`Shift+F6` keyboard traversal across the editor, Outline and Diagnostics panes, expanding auto-hidden targets without changing their pin state;
- infer stable accessible names for focusable controls and ToolStrip commands while retaining explicit descriptions for the editor overview and non-focusable line-number gutter;
- enforce per-monitor DPI autoscaling across the main window and dialogs;
- persist palette dimensions in logical pixels and convert them at the active monitor DPI, preventing size drift when moving between displays;
- honor the Windows high-contrast color scheme on application chrome while preserving the explicitly configured code-editor surface;
- verify names, keyboard traversal, a minimum `4.5:1` chrome contrast ratio and reversible 100%/150% DPI geometry in dynamic WinForms smoke;
- route pane traversal through command, dialog-key and key-preview paths, activate hidden Outline/Diagnostics tabs and keep auto-hide palettes open while keyboard focus remains inside;
- return focus to the visible folded editor when code folding is active instead of targeting the hidden source RichEdit;
- cycle tabs inside the focused side or bottom palette with `Ctrl+Tab`, `Ctrl+Shift+Tab`, `Ctrl+PageDown` and `Ctrl+PageUp`, then focus the selected tab's primary control;

## 0.9.5.4 - 2026-09-02

- add a compact editor overview for errors, warnings, workspace-search matches, breakpoints and the visible viewport;
- make the overview interactive, source-preserving, optional and persistent through View, Settings, Command Palette and the icon bar;
- deduplicate marker snapshots and update only the viewport during scrolling, retaining the established scroll p95 target;
- verify real diagnostic and breakpoint markers, line navigation, settings persistence, all 56 menu commands and 44 toolbar controls with dynamic smoke;
- evaluate a full text minimap and defer it as an optional future enhancement because the lightweight overview provides the useful navigation signals without duplicating text rendering;
- enlarge and increase contrast after visual inspection, paint markers above the viewport and keep the current caret line visible even in clean documents;
- align caret and diagnostic markers for visible lines to the editor's real RichEdit pixel coordinates, retaining proportional document mapping only for off-screen lines;
- update the overview directly from SelectionChanged, use one ordered proportional scale whenever the document scrolls and reduce diagnostic marker thickness to two pixels;
- split visible and off-screen signals: the main lane aligns markers to real editor rows while a two-pixel edge lane retains proportional whole-document positions;
- derive overview rows from the same diagnostic character offsets used by native waves, discard stale diagnostic snapshots while typing and transform coordinates through screen space for DPI-safe alignment;
- rasterize caret and diagnostic ticks through the same clamped two-pixel rectangle geometry so coincident markers share the exact vertical pixels;

## 0.9.5.3 - 2026-09-02

- add source-preserving overlays for the current line, spaces, tabs, line endings and indentation guides;
- make all four visual aids configurable and persistent through View, Settings, Command Palette and four new toolbar buttons;
- support optional word wrap across document tabs and folded view, with logical line numbers shown only on the first wrapped row;
- defer overlay refreshes outside synchronous selection and scrolling paths, retaining the established input, selection and scroll p95 targets;
- stop and harden crash-recovery snapshots during form teardown after dynamic smoke exposed a disposed-tab timer race;
- extend the menu gate to all 55 commands and the icon bar to 43 tested controls;
- add the planned 0.9.6 language-package, declarative grammar, safe semantics, VM and configurable-build architecture to the roadmap;

## 0.9.5.2 - 2026-09-01

- load folder workspaces on cancellable, versioned background tasks while keeping tree updates on the UI thread;
- discard superseded workspace results and preserve the newest open-buffer source when a load completes;
- update workspace source snapshots without synchronously reparsing the document on every keystroke;
- define realistic responsiveness budgets for a 40-file workspace and a 2,000-line active document;
- extend dynamic smoke coverage with UI-heartbeat, cancellation, stale-analysis and large-input scenarios;
- retain all established 0.9.5 p95 targets and pass 138 dependency-free core tests plus all 51 WinForms menu commands;

## 0.9.5.1 - 2026-09-01

- cache gutter text and source-line mappings so unchanged typing, analysis and layout requests do not rebuild the line-number control;
- update changed gutter content under one lightweight native redraw suspension instead of exposing intermediate selection/format states;
- preserve the exact RichEdit scroll position while syntax colors and diagnostic decorations temporarily select token ranges;
- fingerprint Outline symbols and diagnostics so identical analysis snapshots do not clear, repopulate or expand their controls;
- invalidate the editor once after a formatting transaction rather than once per token or diagnostic;
- add smoke assertions for stable viewport, bounded editor invalidations and unchanged gutter/Outline/Diagnostics render counts;
- retain all 0.9.5.0 p95 targets after the rendering changes, including input, selection and scroll below 16 ms;

## 0.9.5.0 - 2026-09-01

- define explicit p95 responsiveness targets and separate regression ceilings for editor input, selection, scroll/gutter, full highlighting and popup opening;
- establish one generated, deterministic 500-line DDF workload and fixed interactive sample counts;
- add a reusable budget catalog with percentile calculation and documented workload metadata;
- measure real WinForms input, selection, viewport/gutter work, complete synchronous analysis/color application and Command Palette first-show layout;
- fail the dynamic smoke suite when a scenario exceeds its environment-tolerant ceiling while reporting whether the tighter product target is met;
- document measurement conditions, interpretation and the rule that ceilings cannot be loosened merely to hide regressions;

## 0.9.4.6 - 2026-09-01

- track the last known disk content independently for every named document tab;
- periodically detect external edits, replacements, deletion and recreation without relying on fragile watcher event sequences;
- show a centered light side-by-side comparison between the editor buffer and the current disk version;
- let the user reload from disk, keep the editor buffer or postpone the decision without automatic overwrites;
- preserve dirty buffers when keeping a conflict and remap breakpoints when reloading external content;
- suppress repeated prompts for the same postponed version while rechecking the conflict before every manual or automatic save;
- block writes after reload, postpone or disk-read failure, while allowing an explicitly kept buffer to be saved;
- verify reload, keep, postpone, pre-save protection and deleted-file handling through dynamic WinForms smoke tests;

## 0.9.4.5 - 2026-09-01

- add an enabled-by-default five-second recovery journal for dirty named and untitled buffers;
- update recovery data only when document content or saved/closed state changes and remove it after a regular close;
- detect a leftover journal on startup and show a centered light recovery dialog with document names, sizes and timestamp;
- let the user restore, discard or postpone recovery without silently choosing on their behalf;
- restore recovered content as dirty editor buffers without writing or overwriting the associated files;
- coordinate recovered tabs with the previous-session index, including multiple untitled documents;
- allow recovery journaling to be disabled independently from autosave, Hot Exit and session restoration;
- verify the complete journal/dialog/restore round trip and assert that the original disk file remains unchanged;

## 0.9.4.4 - 2026-09-01

- persist the current workspace, ordered document tabs, active tab, selections and document breakpoints on clean application close;
- restore named documents from their files and recreate untitled tabs at the next launch;
- overlay dirty Hot Exit content onto its exact restored tab while leaving clean documents disk-backed;
- preserve the active clean tab when dirty buffers are recovered and disambiguate multiple untitled documents by session index;
- skip missing document paths without preventing the remaining session or workspace from opening;
- add an enabled-by-default Settings option to disable session restoration and clear its stored snapshot;
- isolate real session data in smoke runs and verify an actual disk reload with selection and breakpoint recovery;

## 0.9.4.3 - 2026-09-01

- add opt-in autosave for modified documents that already have a file path, with a configurable one-to-300-second idle delay;
- keep autosave unobtrusive by preserving the editor buffer and avoiding formatter-driven caret changes;
- add opt-in Hot Exit that stores dirty named and untitled buffers without showing close prompts;
- restore source, dirty state, selection and document breakpoints automatically on the next launch;
- write application settings and Hot Exit data through temporary files before replacing their destination;
- fall back to the normal save confirmation when Hot Exit storage fails, avoiding silent data loss;
- keep autosave and Hot Exit disabled in the default preset and document the external-change limitation;
- isolate the real user cache in smoke tests and verify idle autosave plus a complete Hot Exit round trip;

## 0.9.4.2 - 2026-09-01

- add a dedicated Shortcuts tab to the centered Settings window with every current menu command grouped by category;
- capture custom combinations, allow commands to be unassigned and reject unsafe bare typing keys;
- detect conflicts before assignment and identify the command already using the requested combination;
- persist the complete shortcut profile and restore the tested default preset in one step;
- synchronize live shortcuts across menus, the Command Palette, toolbar tooltips and editor-specific command routing;
- preserve the Italian keyboard-layout alias for the default line-comment shortcut without keeping it after customization;
- verify conflicts, serialization, reset, synchronized surfaces and real execution through the WinForms smoke gate;

## 0.9.4.1 - 2026-09-01

- persist the preferred width of the right Outline/workspace palette and the height of the bottom Diagnostics/Output palette;
- restore the pinned or auto-hide state of both palettes at application startup;
- preserve preferred dimensions when the current window is temporarily too small to display them in full;
- tolerate missing, invalid or out-of-range layout values and fall back to usable defaults;
- verify real splitter changes, pin transitions and serialized layout through the WinForms smoke gate;

## 0.9.4.0 - 2026-09-01

- add a centered light Settings window through `Ctrl+,`, View, Command Palette and a thirty-ninth toolbar icon;
- persist font family/size, zoom, dark or light code theme, tabs/spaces, indentation width, LF/CRLF and format-on-save;
- apply visual preferences immediately to every open editor tab, folded view, gutter, completion and Signature Help;
- drive Tab, Enter, multi-cursor indentation and the formatter from the same indentation preferences;
- apply the selected line ending and optional formatting before writing a document without changing the in-memory buffer convention;
- provide a one-click reset to stable defaults and tolerate missing or invalid persisted values;
- isolate smoke preferences from the real user profile and verify persistence, reset, Tab formatting and CRLF output;

## 0.9.3.7 - 2026-09-01

- open one or more `.ddf` files by dropping them onto the editor, document tabs or workspace surfaces;
- open a folder as the active workspace by drag-and-drop while rejecting unsupported files;
- persist up to ten recent workspace folders separately from recent documents;
- expose recent workspaces in the File menu and remove paths that disappear before activation;
- preserve open tabs, unsaved buffers and per-file state when files are dropped;
- complete the `0.9.3` workspace search and navigation milestone;
- verify supported and rejected drops, recent workspace registration and real multi-document opening in the WinForms smoke;

## 0.9.3.6 - 2026-09-01

- add a searchable Command Palette through `Ctrl+Shift+P`, the View menu and a thirty-eighth toolbar icon;
- derive commands automatically from the main menus so labels, categories, shortcuts, enabled state and handlers remain synchronized;
- filter immediately across multiple title/category/shortcut terms with keyboard, mouse, Enter and Escape interaction;
- keep unavailable actions visible but clearly marked and impossible to execute;
- reuse original menu handlers instead of duplicating command behavior;
- verify real filtering, state display, toolbar access and document formatting in the WinForms smoke;

## 0.9.3.5 - 2026-08-31

- add a compact contextual breadcrumb between document tabs and the editor;
- derive structure, function and nested `if`/`while`/`do…while`/`for` paths from the recoverable AST;
- make every segment navigable while preserving unsaved buffers, text Undo and Back/Forward history;
- update file names after Save As and refresh context after edits, caret moves, tab changes and programmatic jumps;
- avoid redundant control reconstruction while the caret remains in the same syntax context;
- cover complete and incomplete syntax paths in 135 Core tests and verify clickable WinForms navigation dynamically;

## 0.9.3.4 - 2026-08-31

- add browser-style Back and Forward navigation across documents, positions and selections;
- record successful jumps from definitions, references, workspace search, quick navigation, Outline, diagnostics, breakpoints, runtime output, files and matching delimiters;
- preserve unsaved buffers and text Undo/Redo while restoring navigation locations;
- reopen available disk documents, skip invalid targets safely and clear Forward history after a new branch;
- expose `Alt+Left` / `Alt+Right`, two Edit commands and two toolbar icons;
- verify cross-file Back/Forward, dirty-buffer preservation and forward-branch invalidation in the WinForms smoke;

## 0.9.3.3 - 2026-08-31

- add one filterable light navigation palette for files, symbols, semantic references and line/column positions;
- add per-document last-edit tracking and navigation without modifying source or Undo history;
- resolve global references across workspace buffers while excluding comments, strings and shadowing local symbols;
- expose navigation through five Edit commands, standard shortcuts, five toolbar icons and the editor context menu;
- route standard cut/copy/paste/select-all/undo shortcuts to every focused text box instead of always mutating the code editor;
- add a consistent editing context menu to workspace fields, dialogs and read-only text surfaces where copying is allowed;
- cover navigation indexing, reference identity, line parsing and real cross-file UI navigation in 133 Core tests and the WinForms smoke;

## 0.9.3.2 - 2026-08-31

- add workspace-wide replacement through `Ctrl+Alt+H`, the Edit menu and a thirtieth toolbar icon;
- require a fresh preview and allow every text occurrence to be selected independently;
- reject the whole operation when any involved disk file or unsaved buffer changed after preview;
- apply replacements only to open dirty buffers, never save automatically, and preserve one Undo action per modified document;
- repaint replaced documents from the first character so an initial comment cannot propagate its green format into following code;
- cover selective multi-file transformations in Core and exercise preview, application and Undo in the WinForms smoke;

## 0.9.3.1 - 2026-08-31

- add a horizontal drag splitter between the code editor and the bottom palette;
- preserve the user-selected palette height when switching between pinned and auto-hide modes;
- hide and disable the splitter while the palette is compact, restoring it only when appropriate;
- enforce a minimum editor area and a usable minimum palette height;
- extend the WinForms smoke with resize, layout and remembered-height assertions;

## 0.9.3.0 - 2026-08-31

- add cancellable text and declaration search across every workspace file and open document buffer;
- prefer unsaved in-memory source over the indexed disk snapshot for the same path;
- add a live Search palette with text/symbol mode, case sensitivity, result previews and keyboard or mouse navigation;
- expose Find in Workspace through `Ctrl+Alt+F`, the Edit menu and a twenty-ninth toolbar icon;
- refresh active searches after source changes while discarding obsolete result snapshots;
- cover line/column mapping, nested symbols, cancellation and real multi-file unsaved-buffer navigation in 129 Core tests and the WinForms smoke;

## 0.9.2.10 - 2026-08-31

- move lexical, syntactic, semantic and type analysis off the WinForms UI thread;
- version every analysis request, cancel superseded work and apply results only when source and request still match;
- compute completion asynchronously and prevent stale or explicitly dismissed popup results from reappearing;
- marshal every completed snapshot back to the editor thread before touching WinForms controls;
- document the ambiguity limit of brace quick fixes when the remaining source is still grammatically valid;
- extend the dynamic smoke with rapid-edit analysis and stale-completion scenarios;

## 0.9.2.9 - 2026-08-31

- attach the owning syntax context to missing-delimiter diagnostics;
- reconstruct mandatory opening and closing braces on their own correctly indented lines;
- use indentation-aware parser recovery so an outer closing brace is not consumed by an incomplete nested block;
- retain generic token and keyword fixes while specializing structural delimiters independently from the UI;
- cover removed function, inner-block and outer-block braces without making valid syntax indentation-sensitive in 126 Core tests and the dynamic WinForms smoke;

## 0.9.2.8 - 2026-08-31

- anchor missing-token quick fixes after the previous valid token instead of on the token that exposed the parser error;
- preserve intervening spaces, comments and blank lines when inserting punctuation on the preceding statement;
- add context-aware spacing around missing keyword fixes;
- cover the reported multiline semicolon scenario in Core and real WinForms smoke tests, bringing Core coverage to 123 tests;

## 0.9.2.7 - 2026-08-31

- add an extensible provider-based quick-fix service independent from WinForms;
- close unterminated strings, comments and library directives, remove bad characters and insert parser-expected tokens;
- preserve exact parser insertion positions so fixes before closing delimiters and at end of file remain correct;
- expose fixes from the editor context menu, `Ctrl+.`, the Edit menu and a new toolbar button with native Undo;
- expand regression coverage to 122 Core tests, 39 menu commands, 35 shortcuts and 28 toolbar actions;

## 0.9.2.6 - 2026-08-31

- replace diagnostic background fills with native RichEdit wavy underlines;
- preserve syntax colors, source selection and native Undo while diagnostics update incrementally;
- show the complete diagnostic code, severity, position and message on editor hover;
- keep source and folded views synchronized with the same diagnostic collection;
- introduce extensible error/warning severity styling and expand coverage to 119 Core tests plus an inline-diagnostic WinForms smoke scenario;

## 0.9.2.5 - 2026-08-31

- centralize structured hover data independently from the WinForms tooltip;
- show symbol category, calculated/declared type, DDF signature, document origin and declaration position;
- count references and display their first source positions for local and workspace declarations;
- extract contiguous `///` documentation and document every standard runtime function;
- expand regression coverage to 118 Core tests plus local, standard and workspace hover smoke scenarios;

## 0.9.2.4 - 2026-08-31

- add a central snippet catalog for `if`, `while`, `do`, `for`, functions, `main` and structures;
- expose snippets only in statement completion context and respect alternative language catalogs;
- expand templates relative to the current indentation and navigate editable fields with `Tab`/`Shift+Tab`;
- preserve a single native Undo step for insertion and close the active session with `Escape`;
- expand regression coverage to 115 Core tests plus a dynamic WinForms snippet scenario;

## 0.9.2.3 - 2026-08-31

- insert one blank line between consecutive formatted statements and independent control-flow contexts;
- avoid blank lines inside `for` headers and immediately before closing braces;
- retain `do { ... } while (...);` as one visual context and preserve inline comment attachment;
- expand regression coverage to 113 Core tests plus the dynamic formatting smoke;

## 0.9.2.2 - 2026-08-31

- show function signatures automatically while the caret is inside local, workspace or standard-library calls;
- track the active parameter across nested calls while ignoring commas inside strings and comments;
- expose signature origin and a bold current-parameter row without stealing editor focus;
- expand regression coverage to 112 Core tests plus a dynamic WinForms Signature Help scenario;

## 0.9.2.1 - 2026-08-31

- repaint the complete RichEdit buffer after document formatting so a leading comment cannot leak its green character format into unchanged code;
- extend the WinForms formatting smoke with explicit comment and keyword color assertions;

## 0.9.2.0 - 2026-08-31

- rank completion by statement/expression/type context, expected type, semantic scope, usage frequency and proximity;
- expose only symbols genuinely visible at the caret, including correct shadowing and scope exit behavior;
- retain relative file origins for workspace completion while preferring local declarations on duplicate names;
- render structured popup rows with category glyph, category, type and origin metadata;
- expose completion on the icon toolbar and expand coverage to 107 Core tests, 38 menu commands, 34 shortcuts and 27 toolbar actions;

## 0.9.1.3 - 2026-08-30

- add persistent per-tab multiple cursors through `Alt+Click` with visible secondary selections;
- select the next token occurrence with `Ctrl+D` or all code occurrences with `Ctrl+Shift+L`;
- apply typing, paired characters, Enter, Tab, Backspace, Delete, Cut and Paste to all selections as one Undo step;
- move Duplicate Lines to `Ctrl+Shift+D` and expose occurrence commands in Edit, context menu and toolbar;
- complete roadmap milestone 0.9.1 with 102 Core tests, 26 toolbar commands, 38 menu commands and 34 shortcuts;

## 0.9.1.2 - 2026-08-30

- add AST-aware progressive selection from token through expression, statement, block, function and document;
- retain an independent shrink-selection history for each open document tab;
- navigate between matching parentheses, brackets and braces while ignoring strings and comments;
- expose the three navigation commands in Edit, the context menu, keyboard shortcuts and the icon toolbar;
- expand regression coverage to 98 Core tests, 24 toolbar commands, 36 menu commands and 32 shortcuts;

## 0.9.1.1 - 2026-08-30

- duplicate the current lines with `Ctrl+D` while retaining the corresponding caret or selection;
- move complete selected lines with `Alt+Up` and `Alt+Down` as one native Undo operation;
- delete complete lines with `Ctrl+Shift+K`, including correct first/last-line newline handling;
- normalize and reindent multiline clipboard text relative to the current code block;
- expose all line operations in Edit, the editor context menu and the main icon toolbar;
- expand regression coverage to 94 Core tests, 21 toolbar commands, 33 menu commands and 29 shortcuts;

## 0.9.1.0 - 2026-08-30

- auto-close parentheses, brackets, braces, quotes and apostrophes while avoiding duplicate closing characters;
- expand Enter between braces and delete empty pairs with one Backspace, preserving a single native Undo step;
- wrap selections with typed pairs and retain the selected inner text;
- add indentation-preserving line comment toggling through Edit, `Ctrl+/` and the main toolbar;
- add a light-themed editor context menu with current Undo, clipboard, Find, Rename and comment states;
- cover the editing gestures with 90 Core tests and dynamic WinForms smoke scenarios;

## 0.9.0.1 - 2026-08-30

- add document tabs backed by independent editor controls and in-memory buffers;
- keep the tab strip in a dedicated layout row so it cannot cover the first source lines;
- preserve unsaved text, caret, selection, scroll and native Undo/Redo while switching files;
- add dirty markers, close-tab actions, Save All and an aggregate exit confirmation;
- keep workspace analysis synchronized with every open in-memory document;
- scope breakpoints to their document, remap them after edits and flag unbound locations;
- add a navigable Breakpoint palette plus Save All and Close Document toolbar commands;
- expand the Core suite to 87 tests and the UI smoke to 28 menu commands and 24 shortcuts;

## 0.9.0 - 2026-08-30

- add line breakpoints toggled from the gutter or with `F9`;
- pause the interpreter before the matching executable statement without blocking the WinForms UI;
- reuse `F5` and the Run toolbar command as Continue while execution is paused;
- keep Stop cooperative and able to release a paused runtime safely;
- mark active breakpoint lines in the non-selectable gutter and navigate to the paused statement;
- add a dedicated breakpoint icon to the toolbar and keep Run synchronized with Continue;
- keep the Edit menu responsive when another Windows process temporarily owns the clipboard;
- make keyboard Cut, Copy and Paste independent from stale Edit-menu enabled states;
- derive self-contained release package names and validation from assembly version metadata;
- expand the Core suite to 85 tests and the menu smoke to 26 commands and 22 shortcuts;

## 0.8.1 - 2026-08-30

- adopt self-contained Windows x64 as the official distribution format;
- add a repeatable release script that publishes and validates the bundled .NET and Windows Desktop hosts;
- generate a ready-to-distribute ZIP and its SHA-256 checksum under `.artifacts\release`;
- publish the same verified package as a GitHub Actions artifact;

## 0.8.0 - 2026-08-30

- convert all application and test projects to concise SDK-style project files;
- verify the intermediate SDK conversion while still targeting .NET Framework 4.8;
- retarget the UI and smoke suite to `net10.0-windows` and the platform-neutral Core/tests to `net10.0`;
- adopt the modern WinForms bootstrap, platform annotations and Segoe UI autoscaling metrics;
- replace legacy `System.Configuration` user settings with a dependency-free UTF-8 local store;
- move local and CI builds to the .NET 10 CLI and pin the supported SDK family with `global.json`;
- preserve all 84 Core tests, 25 menu commands and dynamic WinForms scenarios without warnings;

## 0.7.4 - 2026-08-30

- capture structured DDF call stacks for runtime failures in user and standard functions;
- show runtime diagnostics and call frames together in the Output palette;
- make errors and individual frames navigable to their exact source span by double-click;
- batch final runtime output to avoid partial WinForms updates while preserving colored links;
- add Core and WinForms regressions for nested failures, source highlighting and call-site navigation;
- disable pending symbol tooltips before form disposal to avoid delayed UI callbacks;

## 0.7.3 - 2026-08-29

- regenerate the Release application icon as a newly named multi-resolution resource;
- use a simplified high-contrast navy/orange chevron mark for small shell and title-bar sizes;
- load the window icon directly from the embedded assembly resource instead of shell extraction;
- center Find/Replace, Rename, runtime input and About on the active screen;
- render Workspace/Outline with the same standard tab style used by Diagnostics/Output;
- extend the right palette to the full usable form height and constrain the bottom palette to the remaining width;
- add icon-content, dialog-position and docking-geometry checks to the WinForms smoke;
- reorganize Find/Replace with aligned fields and checkbox plus one uniformly spaced action bar;

## 0.7.2 - 2026-08-29

- introduce one shared light application palette for forms and dynamically created controls;
- keep only the source editor, folded projection and line-number gutter on the code-oriented dark theme;
- convert Workspace, Outline, Diagnostics, Output, completion and status surfaces to light colors;
- restyle About, Find/Replace, Rename and runtime-input dialogs consistently;
- restore high-contrast toolbar and palette-pin glyphs on light backgrounds;
- add a WinForms regression smoke that rejects dark non-editor surfaces and low-contrast command icons;

## 0.7.1 - 2026-08-29

- add a centralized minimal standard library with `print`, `readLine`, `length`, `toInt` and `toFloat`;
- make standard functions available to semantic analysis, type checking and completion;
- connect `readLine` to a native editor input dialog through a testable runtime callback;
- report failed standard conversions as positioned `DDF408` diagnostics;
- add a flat icon toolbar for 13 primary editing, navigation, folding and execution commands;
- keep toolbar Run/Stop state synchronized with the existing menu commands;
- expand the dependency-free core suite to 83 tests and cover toolbar/input in the WinForms smoke;

## 0.7.0 - 2026-08-29

- add the first internal AST interpreter without external compiler dependencies;
- execute functions, lexical scopes, declarations, assignments, control flow and returns;
- support primitive values, arrays, structures, indexes and member access at runtime;
- route `value >> Console` to the new Output palette;
- add Run (`F5`) and Stop (`Shift+F5`) with cooperative cancellation;
- report positioned runtime diagnostics `DDF401`-`DDF407` and cap execution at 100,000 instructions;
- display the value returned by `main` separately from the internal runtime-step counter;
- expand the dependency-free core suite to 81 tests and the menu smoke to 25 commands and 21 shortcuts;

## 0.6.0 - 2026-08-29

- add a runtime-independent type model for primitives, structures and arrays;
- infer expression types and validate declarations, assignments, operators and boolean conditions;
- check function argument counts/types and return values, including signatures from workspace files;
- parse and type-check structure member access through `.` and array element access;
- report stable semantic diagnostics `DDF301`-`DDF308`;
- enrich editor hover with calculated types and merge type errors into source diagnostics;
- align a typed closing brace with its unmatched block indentation while ignoring comments and strings;
- preserve the requested 300-pixel right navigation palette for Workspace and Outline;
- expand the dependency-free core suite to 76 tests;

## 0.5.6 - 2026-08-29

- add a recursively indexed workspace for small folders of `.ddf` sources;
- add File > Open Folder and Close Folder with a compact file explorer beside the Outline;
- open workspace documents by double-click while preserving unsaved-change confirmation;
- offer top-level functions, structures and variables from other workspace documents in completion;
- resolve document diagnostics against the shared index and navigate across files with `F12`;
- keep modified workspace documents indexed from their in-memory contents;
- expand the core suite to 65 tests and the WinForms smoke to 23 commands and 19 shortcuts;

## 0.5.5 - 2026-08-29

- add a document-local semantic model that resolves declarations and references by lexical scope;
- report unresolved names (`DDF201`) and duplicate declarations in the same scope (`DDF202`);
- show symbol kind, signature/type and declaration line while hovering over resolved names;
- add Go to Definition (`F12`) and scope-safe Rename Symbol (`F2`) with one-step Undo;
- keep shadowed and same-named symbols in separate rename sets;
- expand the core suite to 62 tests and the WinForms menu smoke to 21 commands and 18 shortcuts;

- start the IDE maximized with the source diagnostics panel always visible;
- remove the beta notification banner while retaining beta version metadata;
- add a flat, geometric, multi-resolution application icon on a white rounded tile;
- load the embedded high-resolution PNG in About instead of enlarging the small executable icon;
- adopt an IDE-oriented dark editor palette with higher-contrast strings, comments, types and keywords;
- support multiple simultaneous folded blocks with selective expansion and source-preserving projection mapping;
- left-align line numbers in a dedicated non-selectable gutter that redirects focus to the editor;
- reduce the initial Outline width and add pinned/auto-hide modes to Outline and source diagnostics;
- center the About window on the active screen;
- expand the core suite to 55 tests and the WinForms smoke to 12 editor scenarios;
- add Help > About with author, website, MIT license and beta version details;
- expand the WinForms menu smoke to all 19 commands and cover the startup layout and About popup;

## 0.5.4 - 2026-08-28

- added a catalog-driven whole-document formatter with stable Allman indentation and token spacing;
- preserved comments, strings, library directives and incomplete token streams during formatting;
- added source-to-output caret and selection mapping;
- exposed Format Document in the Edit menu with `Ctrl+Shift+F` and one-step Undo;
- added idempotence, control-flow, array, call, invalid-source and alternative-catalog tests;
- expanded the dependency-free core suite from 47 to 54 tests and the WinForms smoke to 9 editor scenarios;
- expanded menu coverage to 18 commands and 16 shortcuts;

## 0.5.3 - 2026-08-28

- added catalog-driven contextual completion for keywords, types and boolean literals;
- added document-symbol suggestions scoped to the current declaration and caret position;
- completed known library names inside `@@'...'` directives;
- suppressed completion inside comments and strings;
- added an editor popup with automatic activation, `Ctrl+Space`, keyboard navigation and undoable insertion;
- added a discoverable Edit menu command and expanded menu coverage to 17 commands and 15 shortcuts;
- expanded the dependency-free core suite from 42 to 47 tests and the WinForms smoke to 8 editor scenarios;
- suppressed native RichEdit redraw and selection notifications during syntax formatting;
- deferred delimiter highlighting while the mouse is selecting, restoring backward drag and word double-click behavior;
- preserved syntax and diagnostic colors in the read-only folded projection;
- kept the line-number gutter visible in folded view with original source numbers and a `⋯` marker;
- made folding available from declaration headers and refreshed stale ranges when the command is invoked;
- handled `Ctrl+M` directly in the editor so folding does not depend on stale menu state;
- kept syntax and delimiter formatting out of the user's Undo history;
- fixed incremental re-lexing when inserted text shares a prefix with old tokens;
- prevented delimiter matching from consuming a stale lexical snapshot during rapid edits;
- added a deterministic 400-edit lexer smoke and raised the core suite from 39 to 42 tests;
- added a repeatable WinForms smoke covering colored and numbered folding, mouse-selection preservation, library cuts, Undo and 80 rapid edits;
- covered all 17 File, Edit and View menu commands plus their 15 keyboard shortcuts through the real WinForms menu items;
- isolated Open/Save dialogs, temporary files, recent-file persistence and the Windows clipboard during menu smoke tests;
- stopped UI timers during form shutdown and ignored stale folding ranges while a document is being replaced;
- documented the maintained menu coverage matrix in `MENU_TEST_MATRIX.md`.

## 0.5.2 - 2026-08-28

- added token-aware matching for parentheses, brackets and braces;
- ignored delimiter-like characters inside strings and comments;
- derived multiline folding ranges from recoverable AST blocks;
- recorded explicit closing-brace presence to avoid invalid folds in incomplete code;
- added a compact read-only fold projection that never replaces the source buffer;
- added View menu commands and keyboard shortcuts for folding and expansion;
- preserved the editor selection and Undo buffer across folded previews;
- expanded the dependency-free regression suite from 33 to 39 tests;
- verified matching, folding and expansion with a Windows UI smoke test.

## 0.5.1 - 2026-08-28

- added exact declaration-name spans to the typed syntax tree;
- added a UI-independent hierarchical document symbol index;
- indexed libraries, structures, fields, functions, parameters and local variables;
- kept useful symbols available while parsing incomplete source text;
- added a resizable live outline panel with type and signature details;
- added double-click navigation from outline entries to exact declaration names;
- expanded the dependency-free regression suite from 30 to 33 tests;
- verified outline layout, population and navigation with a Windows UI smoke test.

## 0.4.1 - 2026-08-28

- centralized keywords, literals, operators and punctuation in an immutable language definition;
- replaced parser checks for concrete keyword spelling with reusable syntax roles;
- moved operator precedence, associativity and prefix/postfix behavior into the catalog;
- made full and incremental lexing configurable with alternative language definitions;
- added a maintained vocabulary inventory and extension workflow for the future C comparison;
- added tests proving that an alternative vocabulary drives both lexer and parser;
- expanded the dependency-free regression suite from 27 to 30 tests.

## 0.4.0 - 2026-08-28

- added a formal grammar for the supported DDF subset;
- added a typed AST for compilation units, declarations, statements and expressions;
- added a recursive parser with documented precedence and associativity;
- added recoverable syntax diagnostics `DDF101` through `DDF105`;
- unified lexical and syntax diagnostics in the navigable editor panel;
- added valid and invalid parser corpora;
- expanded the dependency-free regression suite from 21 to 27 tests;
- verified that the full editor smoke sample parses without diagnostics.

## 0.3.0 - 2026-08-28

- replaced heuristic classification with a formal DDF token model and lexer;
- added absolute source spans and line/column lexical diagnostics;
- added diagnostics for unterminated strings, block comments and library directives;
- added diagnostics for unrecognized characters;
- added incremental re-lexing and recoloring from a safe boundary near each edit;
- added a navigable diagnostics panel with source highlighting;
- added valid and invalid DDF lexer corpora;
- expanded the dependency-free regression suite from 16 to 21 tests;
- verified diagnostic rendering and panel layout with an automated UI smoke test.

## 0.2.0 - 2026-08-28

- added New, Open, Save and Save As workflows for `.ddf` documents;
- added strict UTF-8 reading and staged UTF-8 writes without a byte-order mark;
- added unsaved-change prompts and a dirty marker in the title and status bar;
- added persistent recent files and standard document keyboard shortcuts;
- added Find and Replace, including case matching and Replace All;
- added line, column, file path and encoding status information;
- added document, recent-file and search services to the testable core;
- expanded the dependency-free regression suite from 9 to 16 tests;
- verified the main window and Find dialog with an automated UI smoke test.

## 0.1.1 - 2026-08-28

- separated syntax classification and editing transformations from WinForms;
- corrected highlighting for full line comments, strings, numbers and longest operators;
- preserved selections and undo history when handling Tab, Shift+Tab and Enter;
- added multiline indentation and outdentation;
- debounced document highlighting and removed repeated font allocations;
- improved visible line-number calculation and disabled word wrapping;
- documented the recognized DDF editor subset and the long-term roadmap;
- added dependency-free regression tests and Windows CI builds.

## 0.1.0-beta.1 - 2026-08-26

- first public MIT-licensed beta;
- explicit in-application beta banner and version identity;
- responsive resizing of the editor and line-number gutter;
- documented implemented features and missing language toolchain;
- documented the non-persistent editor buffer and unsigned build;
- added third-party framework notices.
