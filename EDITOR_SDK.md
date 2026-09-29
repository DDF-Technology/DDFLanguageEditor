# DDFLanguageEditor — Editor SDK

## Stato

La `0.11.2` introduce in modo incrementale il confine incorporabile dell'editor.
Il contratto pubblico corrente è `DdfEditorSdkContract.CurrentVersion == "0.33"`.
È ancora sperimentale: non coincide con la versione dell'applicazione e verrà
evoluto esplicitamente quando saranno estratti nuovi servizi.

## Pacchetti e applicazione standalone

Il codice è separato in tre artefatti:

- `DDFLanguageEditor.Core` (`net10.0`), privo di dipendenze WinForms;
- `DDFLanguageEditor.WinForms` (`net10.0-windows`), contenente
  `DdfEditorView`, `DdfEmbeddedEditorControl` e la shell grafica;
- `DDFLanguageEditor.App`, launcher standalone che produce
  `DDFLanguageEditor.exe` e dipende dalla libreria WinForms.

I due progetti SDK generano pacchetti NuGet beta locali versionati. Non sono
ancora pubblicati su un feed pubblico; dopo aver configurato una sorgente che li
contiene, un host WinForms può dichiarare:

```xml
<PackageReference Include="DDFLanguageEditor.WinForms" Version="0.11.2-beta" />
```

La dipendenza da `DDFLanguageEditor.Core` viene risolta automaticamente. Durante
lo sviluppo nel repository, gli esempi usano invece un `ProjectReference` alla
libreria WinForms per verificare sempre il sorgente corrente.

I pacchetti locali si generano in modo ripetibile con:

```powershell
.\tools\pack-sdk.ps1
```

## Prima superficie pubblica

`DdfEditorView` è un controllo WinForms riutilizzabile che implementa
`IDdfEditorSurface`. La shell standalone usa la stessa classe come contenitore
della propria superficie centrale e le associa l'editor del documento attivo.

Il contratto `0.33` espone:

- testo sorgente tramite `SourceText`;
- selezione normalizzata tramite `SetSelection`, `SelectionStart` e
  `SelectionLength`;
- stato di sola lettura e ritorno a capo automatico;
- eventi per modifica del testo, selezione e cambio dell'editor attivo;
- comando per trasferire il focus alla superficie di scrittura.
- il servizio documentale associato e `AttachDocumentService` per collegarlo.
- il catalogo delle palette associato e `AttachPaletteService` per collegarlo.
- la configurazione per istanza e `AttachHostConfiguration` per collegarla.
- la sessione del pacchetto linguaggio e lo stato della modalità didattica.
- l'evento `LanguageSessionChanged`, che consente alla shell completa di seguire
  anche la sostituzione della sessione effettuata dall'host;
- il servizio di analisi versionato e sostituibile tramite `AnalysisService` e
  `AttachAnalysisService`, insieme all'ultimo snapshot e al relativo evento;
- analisi esplicita e navigazione alla definizione locale o tra buffer host;
- convalida e preparazione IR in memoria di tutti i buffer, senza I/O implicito;
- il catalogo dei comandi associato tramite `CommandService` e
  `AttachCommandService`.
- una strategia di persistenza opzionale fornita dall'host.
- un contenitore progetto opzionale, interamente controllato dall'host.
- un servizio di esecuzione isolato con Run, Stop, stato, output e risultato.
- un catalogo host per tipi, oggetti, funzioni, documentazione e permessi.
- librerie atomiche di funzioni host tipizzate per matematica, logica e algoritmi
  applicativi personalizzati.
- riepiloghi degli effetti host e una politica transazionale opzionale con
  commit o rollback forniti dall'applicazione.
- una capability revocabile e a privilegi minimi per l'eventuale store moduli
  della shell embedded; senza autorizzazione i relativi comandi non sono visibili.
- risultati host tipizzati e immutabili con valore iniziale/finale, accesso,
  durata e indicazione di scrittura.
- procedure DDF richiamabili manualmente o da eventi host espliciti, con
  argomenti nominati e risultati tipizzati.
- servizi database host con operazioni parametrizzate e tipizzate, cancellazione
  e identificatori di transazione opachi.

Esempio minimo:

```csharp
var editor = new DdfEditorView
{
    Dock = DockStyle.Fill,
    SourceText = "main() out int { ret 0; }"
};
editor.SourceTextChanged += (sender, args) => SaveDraft(editor.SourceText);
hostPanel.Controls.Add(editor);
var documents = new OpenDocumentCollection();
editor.AttachDocumentService(documents);
documents.CreateVirtual("Regola.ddf", editor.SourceText);
editor.SetSelection(0, 4);
editor.FocusEditor();
```

## Shell completa incorporabile

`DdfEmbeddedEditorControl` è il controllo WinForms pubblico destinato agli host
che vogliono incorporare l'intera esperienza grafica: schede, gutter, menu,
toolbar, diagnostiche, Output, Flowchart e palette di debug. Il controllo crea,
configura e rilascia internamente la shell embedded; l'applicazione non deve
conoscere `MainForm`, impostare `TopLevel` o chiamare `Show` su una finestra figlia.

```csharp
var editorHost = new DdfEmbeddedEditorControl
{
    Dock = DockStyle.Fill,
    HostDisplayName = "Applicazione ordini"
};
hostPanel.Controls.Add(editorHost);

editorHost.Editor.AttachHostCatalog(hostCatalog);
string scriptId = editorHost.SetPrimaryDocument("ordine.ddf", source);
editorHost.DocumentSaveRequested += SaveScriptInHostStorage;
editorHost.ExecuteScript(debug: editorHost.HasBreakpoints);
```

### Capability dello store moduli embedded

Lo store `.ddf/modules` non è accessibile implicitamente dalla shell embedded.
Se l'applicazione vuole offrire il Gestore moduli deve indicare una radice
assoluta già esistente e concedere soltanto le operazioni necessarie:

```csharp
var moduleCapability = new DdfEmbeddedModuleStoreCapability(
    projectRoot,
    DdfEmbeddedModuleStoreOperations.BrowseAndValidate |
    DdfEmbeddedModuleStoreOperations.Install);

editorHost.SetModuleStoreCapability(moduleCapability);
// In seguito, anche mentre il gestore è aperto:
editorHost.RevokeModuleStoreCapability();
```

`BrowseAndValidate` è obbligatoria e abilita elenco, dettaglio e convalida.
`Install`, `Remove` e `CreatePackage` sono privilegi indipendenti. La revoca
chiude il gestore aperto e nasconde di nuovo menu e toolbar. La capability non
autorizza esecuzione, compilazione, assembly .NET, reflection o API host e non
viene persistita dall'editor: deve essere ricreata consapevolmente dall'host.

Il controllo inoltra salvataggio, documenti virtuali, attivazione, esecuzione,
invocazione di procedure, breakpoint e continuazione del debug. In modalità
embedded la compilazione e l'avvio manuale restano nascosti: è l'evento
dell'applicazione ospitante a richiedere l'esecuzione. `DdfEditorView` rimane
disponibile per host che vogliono costruire una UI personalizzata sopra i servizi.

### Confine permanente fra scripting e compilazione

La shell embedded resta intenzionalmente compiler-free anche dopo
l'introduzione di Roslyn. `ExecuteScript` ed `ExecuteProcedure` useranno sempre
la pipeline interpretata governata dall'host; non produrranno né caricheranno
assembly DDF. Compila, Pubblica, linker, loader, PE e PDB appartengono soltanto a
`DDFLanguageEditor.App` e alla CLI standalone.

Il servizio oggi denominato `IDdfEditorBuildService` crea un piano IR in memoria
usato da convalida, Run e Debug: non emette IL e non è il futuro compilatore.
Prima della `0.13` il contratto verrà separato in modo esplicito in
convalida/preparazione script e compilazione standalone. Quest'ultima non sarà
collegabile a `DdfEditorView` o `DdfEmbeddedEditorControl`, nemmeno tramite API.
Core e WinForms non acquisiranno riferimenti a `Microsoft.CodeAnalysis`.

Le API devono essere usate dal thread UI WinForms. L'evento
`SourceTextChanged` viene emesso una sola volta anche quando l'host assegna il
testo prima che sia stato creato l'handle nativo del controllo.

## Servizio documentale

`IDdfEditorDocumentService` è il primo servizio pubblico indipendente dalla UI.
`OpenDocumentCollection` ne è l'implementazione usata anche dalla shell
standalone. Ogni `IDdfEditorDocument` espone identità stabile, nome, eventuale
percorso, testo, stato modificato e versione crescente del contenuto.

```csharp
IDdfEditorDocumentService documents = new OpenDocumentCollection();
IDdfEditorDocument draft = documents.CreateVirtual(
    "Regola temporanea.ddf",
    "main() out int { ret 0; }");

documents.DocumentChanged += (sender, args) =>
    SaveDraft(args.Document.Id, args.Document.SourceText);
documents.UpdateDocument(draft.Id, "main() out int { ret 1; }");
```

Il servizio può creare documenti virtuali oppure registrare documenti su disco
il cui contenuto è fornito dall'host. Non legge e non scrive file implicitamente:
persistenza, finestre di conferma e policy di salvataggio restano sotto il
controllo dell'applicazione ospitante. `TryCloseDocument` rifiuta per impostazione
predefinita un buffer modificato; l'host deve salvarlo oppure autorizzarne
esplicitamente lo scarto. Gli eventi `DocumentAdded`, `ActiveDocumentChanged`,
`DocumentChanged` e `DocumentClosed` rendono osservabile l'intero ciclo di vita.
Quando viene chiuso l'ultimo documento, `ActiveDocumentChanged` comunica una
destinazione nulla.

Istanze differenti del servizio non condividono documenti, selezioni o stato.
Quando è collegato a una normale `DdfEditorView`, il documento attivo alimenta
automaticamente la superficie e ogni modifica dell'editor aggiorna il buffer.
Attivazioni e modifiche originate dall'host vengono riflesse subito nel controllo.

La shell usa la stessa implementazione pubblica per buffer, attivazione,
salvataggio e chiusura protetta. Poiché mantiene un editor distinto per ogni
scheda, usa un coordinatore dedicato che associa documenti, controlli e stato di
vista senza duplicare il contenuto dei buffer dentro `MainForm`.

## Catalogo delle palette

`IDdfEditorPaletteService` descrive gli strumenti disponibili senza esporre
`TabControl`, pannelli o altri dettagli della shell. Ogni palette ha un ID stabile,
un nome, una collocazione preferita, visibilità e stato attivo. Il catalogo
predefinito `DdfEditorPaletteCatalog` permette di registrare, mostrare, nascondere,
attivare e rimuovere palette, notificando ogni variazione tramite `PaletteChanged`.

```csharp
var palettes = new DdfEditorPaletteCatalog();
palettes.Register("properties", "Proprietà", DdfEditorPalettePlacement.Sidebar);
editor.AttachPaletteService(palettes);
palettes.SetVisible("properties", false);
palettes.Activate("properties"); // la rende nuovamente visibile e attiva
```

La shell registra dieci strumenti con gli ID pubblici di `DdfEditorPaletteIds`:
File, Flowchart, Outline, Diagnostica, Output, Breakpoint, Variabili, Call Stack,
Espressioni di controllo e Ricerca workspace. `Visualizza > Palette` opera sullo
stesso catalogo accessibile agli host; ordine, controlli concreti e composizione
grafica restano privati. Visibilità, dimensioni e stato dei pin vengono conservati
tra gli avvii della shell.

## Configurazione per istanza

`IDdfEditorHostConfiguration` separa le preferenze incorporabili dal profilo
della shell. `DdfEditorHostOptions` comprende tema chiaro/scuro della superficie
di codice, font, dimensione, zoom, word wrap, uso di tab o spazi e ampiezza
dell'indentazione. La superficie isolata applica immediatamente le opzioni e usa
la preferenza tab/spazi durante la digitazione.

```csharp
var configuration = new DdfEditorHostConfiguration();
configuration.ApplyOptions(new DdfEditorHostOptions(
    fontFamily: "Consolas",
    fontSize: 11,
    zoomPercent: 110,
    theme: EditorTheme.Dark,
    wordWrap: false,
    useTabs: false,
    indentSize: 4));
configuration.SetShortcut("save", Keys.Control | Keys.S);
editor.AttachHostConfiguration(configuration);
```

Ogni istanza possiede una configurazione distinta. Il servizio valida le
scorciatoie e rifiuta conflitti nella stessa istanza, ma non salva file né legge
preferenze globali. I metadati `ShortcutText` dei comandi della superficie
seguono la configurazione della sola istanza interessata; l'SDK non installa
intercettazioni globali della tastiera. L'host decide come instradare realmente
le combinazioni verso `TryExecute`. Può inoltre serializzare le opzioni nel
proprio formato; la shell continua a farlo esplicitamente tramite le impostazioni applicative. La
mappa delle scorciatoie sarà collegata ai comandi eseguibili quando verrà estratto
il relativo servizio operativo.

## Linguaggio e modalità didattica

`IDdfLanguagePackageSession` mantiene il pacchetto attivo, le diagnostiche di
caricamento e una revisione crescente all'interno di una sola istanza. Può
caricare una cartella o un archivio, ripristinare il pacchetto incorporato oppure
adottare un pacchetto già validato. L'attivazione è atomica: in caso di errore il
pacchetto precedente resta attivo. Non modifica `DdfLanguageCatalog` né altre
istanze dell'editor.

```csharp
var language = new DdfLanguagePackageSession();
var result = language.TryActivate(packagePath);
if (!result.Success)
    ShowDiagnostics(result.Diagnostics);
editor.AttachLanguageSession(language);
```

`IDdfEditorExperienceState` espone invece la Modalità didattica come stato
puramente presentazionale e osservabile. Cambiarla non modifica il sorgente, non
avvia il runtime e non salva preferenze. L'host può condividere la stessa istanza
con `DdfEditorView` e `FlowchartView`; quest'ultima conserva già il pulsante `D`
sempre disponibile.

La shell associa al controllo la propria sessione linguaggio e il proprio stato
didattico. Solo i comandi espliciti della shell persistono le scelte nel profilo;
un host incorporante decide autonomamente se e dove salvarle.

`DdfEmbeddedEditorControl` mantiene visibile il menu `Linguaggio`: l'utente può
quindi aprire il Gestore pacchetti, installare o selezionare un profilo e tornare
al DDF incorporato anche dentro l'applicazione host. Il cambio usa la stessa
pipeline dello standalone: converte in memoria i buffer aperti, aggiorna lexer,
formatter, IntelliSense, diagnostiche e Flowchart e non salva implicitamente i
documenti. La shell embedded non installa automaticamente pacchetti aggiuntivi:
l'host può distribuirli oppure l'utente può installarli dal Gestore.

L'host può attivare la stessa pipeline senza simulare comandi UI:

```csharp
DdfLanguagePackageLoadResult result = embeddedEditor.ActivateLanguagePackage(packagePath);
// oppure: embeddedEditor.UseEmbeddedLanguage();
```

Anche `embeddedEditor.Editor.LanguageSession.TryActivate(...)` e la sostituzione
tramite `AttachLanguageSession(...)` aggiornano immediatamente l'intera shell.
Le attivazioni programmatiche non scrivono preferenze globali: la persistenza
rimane responsabilità dell'host.

## Servizio di analisi

`IDdfEditorAnalysisService` produce uno snapshot coerente e indipendente dalla UI
per una versione esatta del documento e del pacchetto linguaggio. Lo stesso
risultato contiene diagnostiche aggregate senza duplicati, simboli gerarchici per
l'Outline e completamenti alla posizione richiesta; espone inoltre lexer, parser,
modello semantico, tipi, IR e intervalli di folding per i consumer avanzati.

```csharp
IDdfEditorDocument document = editor.DocumentService.ActiveDocumentInfo;
var request = new DdfEditorAnalysisRequest(
    document.SourceText,
    editor.SelectionStart,
    document.Version,
    editor.LanguageSession.ActivePackage,
    editor.LanguageSession.Revision,
    document.Id,
    document.FilePath,
    includeAllCompletions: true);

DdfEditorAnalysisSnapshot snapshot = await editor.AnalysisService.RequestAsync(request);
if (editor.AnalysisService.IsCurrent(snapshot))
{
    ShowDiagnostics(snapshot.Diagnostics);
    ShowOutline(snapshot.Symbols);
    ShowCompletion(snapshot.Completions);
}
```

Ogni richiesta cattura sorgente, posizione, identità/versione del documento,
pacchetto e revisione del linguaggio. Le collezioni del workspace opzionali
vengono copiate all'ingresso. Una nuova richiesta asincrona cancella la precedente
e un risultato superato non può completare come corrente. `Cancel` e `Dispose`
consentono all'host di interrompere il lavoro e rilasciare le risorse. Il servizio
non modifica `DdfLanguageCatalog`, non legge file e non effettua callback sulla UI:
l'host applica lo snapshot sul proprio thread grafico.

Per analisi che non devono popolare subito IntelliSense, `computeCompletions` può
essere disattivato; `CompletionsComputed` distingue questo caso da un risultato
calcolato ma vuoto. Un host con lessicalizzazione incrementale può inoltre fornire
il proprio `precomputedLexResult`, purché appartenga esattamente al testo e al
pacchetto catturati dalla richiesta.

La shell standalone usa già questo servizio sia per la pipeline principale che
alimenta colori, diagnostiche, Outline, folding e Flowchart, sia per il popup di
completamento interattivo. I due consumer possiedono coordinatori distinti per non
annullarsi reciprocamente, ma producono lo stesso tipo di richiesta e snapshot e
applicano le medesime regole di versione, cancellazione e pacchetto linguistico.

## Catalogo dei comandi

`IDdfEditorCommandService` consente a ogni istanza di registrare comandi con ID
stabile, titolo, categoria, descrizione, scorciatoia e stato corrente. Esecuzione,
visibilità e stato selezionato vengono forniti dall'host tramite delegate; un
comando disabilitato o nascosto non viene eseguito da `TryExecute`.

```csharp
bool canCalculate = true;
editor.CommandService.Register(
    "host.calculate",
    "Calcola risultato",
    "Applicazione",
    execute: CalculateResult,
    canExecute: () => canCalculate,
    description: "Elabora i dati correnti dell'host.");

editor.CommandService.CommandChanged += (sender, args) =>
    RefreshHostToolbar(args.Command.Id);

editor.CommandService.Refresh();
editor.CommandService.TryExecute("host.calculate");
```

`Refresh` rivaluta i delegate e pubblica `CommandChanged` solo quando cambia
disponibilità, visibilità, selezione o testo della scorciatoia. Registrazioni
duplicate vengono rifiutate; `Find` e `Remove` permettono di gestire il ciclo di
vita senza conoscere i controlli grafici che presenteranno il comando.

La shell registra nello stesso catalogo i comandi foglia dei menu e costruisce la
Command Palette a partire dal servizio, anziché enumerare direttamente i menu.
Gli ID più comuni sono dichiarati in `DdfEditorCommandIds` (`file.save`,
`edit.formatDocument`, `debug.start` e altri); le funzioni non ancora promosse a
ID SDK conservano un ID stabile con prefisso `shell.`. Le scorciatoie vengono
lette dinamicamente, quindi le personalizzazioni dell'utente diventano visibili
dopo `Refresh`.

Una `DdfEditorView` creata direttamente registra inoltre le operazioni prive di
dialoghi e dipendenze dalla shell per modifica, analisi, navigazione, persistenza,
progetti, build, esecuzione e debug. Sono richiamabili tramite le costanti di
`DdfEditorCommandIds`; testo, selezione, sola lettura e word wrap rivalutano
automaticamente lo stato e generano `CommandChanged` quando necessario. Se
l'host collega un proprio catalogo con `AttachCommandService`, i comandi nativi
mancanti vengono aggiunti senza sostituire eventuali implementazioni omonime già
registrate dall'host.

`AnalyzeDocument()` produce e memorizza in `LastAnalysisSnapshot` uno snapshot
coerente con testo, posizione, versione del buffer e pacchetto linguistico
dell'istanza. Gli altri documenti aperti vengono passati come radici e simboli
esterni senza leggerli dal disco. `AnalysisCompleted` permette all'host di
aggiornare diagnostiche, Outline o propri indicatori.

`TryGoToDefinition()` usa lo stesso snapshot per risolvere variabili, parametri,
funzioni e strutture. Una destinazione locale seleziona direttamente la
dichiarazione; una destinazione in un altro buffer usa esclusivamente
`IDdfEditorDocumentService.ActivateDocument`, quindi funziona anche con documenti
virtuali. L'esito è restituito come `bool` e una navigazione riuscita pubblica
`DefinitionNavigated` con `DdfEditorNavigationTarget`.

## Eventi operativi e build

La superficie inoltra in modo uniforme il lifecycle che prima era osservabile
soltanto collegandosi ai singoli servizi. Sono disponibili eventi per aggiunta,
modifica, attivazione e chiusura dei documenti, uno snapshot immutabile della
selezione, aggiornamento delle diagnostiche, navigazione, build, output,
esecuzione e debug. Gli eventi di Run e Debug mantengono il thread del servizio:
un host grafico deve quindi effettuare il marshal verso la propria UI.

`IDdfEditorBuildService` fotografa testo e versione di ogni buffer e condivide la
stessa pipeline usata da Run e Debug. `ValidateProject()` e `BuildProject()` non
leggono i sorgenti dal disco e non scrivono artefatti: il risultato contiene
diagnostiche, fingerprint e artefatto immutabile che l'host può conservare nel
proprio contenitore.

```csharp
editor.BuildCompleted += (sender, args) =>
    ShowIssues(args.Snapshot.Issues);

DdfEditorBuildSnapshot validation = editor.ValidateProject();
DdfEditorBuildSnapshot build = editor.BuildProject();
```

Per gli host che possiedono già un modello di progetto, il contratto `0.30`
consente di costruire `DdfEditorBuildService` con un `DdfEditorBuildHandler`.
Il delegate riceve modalità, richiesta, snapshot dei documenti, pacchetto e
catalogo host, ma pubblica il risultato attraverso gli stessi stati, eventi e
snapshot di `IDdfEditorBuildService`. Senza handler resta valido il comportamento
sicuro predefinito: build interamente in memoria e nessun I/O implicito. La shell
standalone usa questa estensione per aggiungere manifest, sorgenti del workspace
e ricevuta su disco senza creare un secondo percorso operativo per menu e toolbar.

I comandi `run.validate` e `run.build` seguono automaticamente disponibilità dei
documenti e stato di Build, Run e Debug. La superficie inoltra inoltre
`ExecutionStateChanged`, `ExecutionOutputReceived`, `ExecutionCompleted`,
`DebugStateChanged`, `DebugPaused`, `DebugOutputReceived` e `DebugCompleted`,
senza obbligare l'host a conoscere l'implementazione concreta dei servizi.

## Isolamento e proprietà dei servizi

Ogni `DdfEditorView` crea configurazione, sessione linguistica, stato didattico,
analisi, build, catalogo comandi, esecuzione e debug indipendenti. Anche eventi,
snapshot, breakpoint, Watch, palette e scorciatoie rimangono confinati
all'istanza. Il solo dato condiviso internamente è il pacchetto DDF incorporato,
caricato una volta e trattato come definizione immutabile; ogni sessione conserva
comunque selezione, revisione e diagnostiche proprie.

Nessuna modifica, analisi, build, esecuzione o sessione di debug invoca
automaticamente `Save` o `Load` sui provider. Persistenza e contenitori progetto
vengono consultati soltanto dalle operazioni esplicite richieste dall'host. I
servizi sostituibili iniettati dall'host restano di sua proprietà e non vengono
distrutti con la superficie; i servizi creati internamente vengono invece
rilasciati da `Dispose`.

## Catalogo host tipizzato

`IDdfHostCatalog` espone uno snapshot immutabile e versionato delle capacità che
l'applicazione ospitante rende visibili allo script. `DdfEditorHostCatalog`
permette di registrare:

- tipi con campi documentati;
- oggetti nominati e tipizzati, letti tramite un getter esplicito;
- funzioni con parametri, tipo restituito, firma e documentazione;
- permessi nominati, concessi o revocati esclusivamente dall'host.

```csharp
var host = new DdfEditorHostCatalog();
host.RegisterPermission(new DdfHostPermissionDefinition(
    "math.use", "Calcoli applicativi"));
host.RegisterObject(new DdfHostObjectDefinition(
    "threshold", "int", () => model.Threshold,
    "Soglia corrente", writeValue: value => model.Threshold = (int)value,
    lifetime: DdfHostObjectLifetime.Live));
host.RegisterFunction(new DdfHostFunctionDefinition(
    "hostScale", "int",
    new[] { new DdfHostParameterDefinition("value", "int") },
    args => Convert.ToInt32(args[0]) * 2,
    "Raddoppia il valore usando il servizio dell'applicazione.",
    new[] { "math.use" }));
host.SetPermission("math.use", true);
editor.AttachHostCatalog(host);
```

Lo stesso snapshot viene passato a IntelliSense, type checker, Build, Run e
Debug. I completamenti mostrano origine `Host`, tipo, firma e documentazione;
il type checker verifica oggetti, campi, argomenti e valori restituiti. La VM
richiama soltanto il delegate registrato e produce `DDF409` se manca un permesso.
Il catalogo non enumera assembly, proprietà o metodi tramite reflection e resta
separato tra istanze.

Ogni oggetto è sola lettura quando non possiede `writeValue`; l'assegnazione
viene rifiutata dal type checker con `DDF309` e dalla VM con `DDF411`. L'host può
marcare singoli `DdfHostFieldDefinition` come proprietà `ReadWrite`: in tal caso
un'assegnazione come `firstNameTextBox.backColor << "LightGreen";` viene inoltrata
al setter dell'oggetto completo. Membri non dichiarati o non scrivibili restano
inaccessibili. Non viene mai usata reflection automatica.

La shell completa in modalità embedded espone inoltre la palette laterale
`API host`. La struttura ad albero è generata dallo stesso snapshot consumato da
IntelliSense, type checker e runtime e mostra progetto host, oggetti/controlli,
proprietà con accesso, eventi documentati, classi/tipi, funzioni, librerie,
database e permessi. Il doppio clic sui membri inseribili li copia nel sorgente.
Gli eventi restano sotto il controllo dell'applicazione: è l'host a collegare,
per esempio, `Button.Click` alla procedura DDF da eseguire.

Nella shell embedded i comandi che avviano autonomamente Run o Debug e il comando
di compilazione non sono visibili a riposo. Lo script viene avviato dall'evento
host; se contiene breakpoint la stessa richiesta apre automaticamente la sessione
di debug. Durante una pausa ricompaiono soltanto Continua, Step e Arresta.

`DdfHostObjectLifetime.ExecutionSnapshot` legge il getter una volta all'inizio
di ogni Run/Debug e conserva il valore soltanto per quell'esecuzione.
`DdfHostObjectLifetime.Live` richiama invece il getter a ogni lettura. Nessuna
cella o riferimento della VM sopravvive alla fine dell'esecuzione.

`DdfHostValueAdapter` dichiara le conversioni `host -> script` e
`script -> host`. Può essere associato a oggetti, parametri e valori restituiti:

```csharp
var integerText = new DdfHostValueAdapter(
    hostValue => int.Parse((string)hostValue),
    scriptValue => Convert.ToString(scriptValue,
        CultureInfo.InvariantCulture));
```

Prima di chiamare l'host, array e oggetti strutturati vengono esportati come
raccolte .NET di soli valori; le celle private della VM non attraversano mai il
confine. Al ritorno, la VM ricostruisce esclusivamente tipi primitivi, array e
campi dichiarati nel catalogo. Tipi e membri non registrati non sono raggiungibili
dal sorgente e non viene eseguita reflection automatica.

## Risultati strutturati restituiti all'host

Ogni Run o Debug concluso espone `DdfEditorExecutionCompletion.HostValues`.
La raccolta contiene un `DdfHostValueResult` per ogni valore autorizzato che la
VM ha acquisito dal catalogo, con:

- `Name` e `TypeName` dichiarati dall'host;
- `InitialValue` e `Value` finale convertiti nuovamente nel formato host;
- `Access` (`ReadOnly` oppure `ReadWrite`) e `Lifetime`;
- `WasWritten`, che distingue un'assegnazione DDF da una semplice lettura.

```csharp
DdfEditorExecutionCompletion completed =
    await editor.ExecutionService.RunAsync();

foreach (DdfHostValueResult value in completed.HostValues)
    Console.WriteLine($"{value.Name}: {value.InitialValue} -> {value.Value}");
```

Array, liste e oggetti a campi sono copiati ricorsivamente in raccolte di sola
lettura: l'host può conservare il risultato oltre la sessione senza trattenere
celle o riferimenti della VM. In `Simulation` il valore finale descrive ciò che
lo script avrebbe prodotto, ma i setter reali non vengono chiamati. In
`HostEffects` il risultato descrive invece i valori consegnati ai setter; gli
oggetti `Live` continuano a usare il getter durante le letture effettive.

## Procedure richiamate dall'applicazione host

`IDdfEditorExecutionService.InvokeAsync` esegue una funzione DDF scelta
esplicitamente dall'host senza cambiare il punto di ingresso del progetto. Gli
argomenti sono nominati, copiati al momento della richiesta e associati ai
parametri dichiarati dalla procedura.

```csharp
DdfEditorExecutionCompletion result =
    await editor.ExecutionService.InvokeAsync(
        new DdfEditorProcedureRequest(
            "calculateTotal",
            new Dictionary<string, object>
            {
                ["quantity"] = 6,
                ["unitPrice"] = 7
            }));

Console.WriteLine(result.ExecutionResult.ReturnTypeName); // int
Console.WriteLine(result.ExecutionResult.ReturnValue);    // 42
```

Un evento dell'applicazione non viene sottoscritto implicitamente dall'editor:
è l'host a scegliere l'evento autorizzato e a inoltrarlo con un identificatore
osservabile nel risultato.

```csharp
await editor.ExecutionService.InvokeAsync(
    new DdfEditorProcedureRequest(
        "onOrderUpdated", arguments,
        DdfEditorProcedureTrigger.HostEvent,
        "orders.updated"));
```

`DdfEditorExecutionCompletion.ProcedureInvocation` conserva procedura,
argomenti e origine manuale/evento. Il valore restituito è convertito in un
oggetto profondamente distaccato dalle celle della VM e `ReturnTypeName` espone
il tipo DDF dichiarato. Argomenti mancanti, superflui o non convertibili
producono `DDF416`; errori di build e runtime seguono gli stessi canali di Run.

Ogni `IDdfEditorExecutionService` accetta una sola Run o procedura per volta:
una seconda richiesta concorrente viene rifiutata prima dell'avvio. Host diversi
possono usare istanze differenti in parallelo. Timeout, cancellazione, modalità
Simulation/HostEffects, budget, output e risultati host restano gli stessi
dell'esecuzione ordinaria. L'host decide se accodare, ignorare o riprovare gli
eventi ricevuti mentre l'istanza è occupata.

## Librerie matematiche, logiche e algoritmi host

`DdfHostFunctionLibraryDefinition` raggruppa una superficie coerente di funzioni
tipizzate. La registrazione è atomica: in caso di nome duplicato o permesso non
registrato non viene aggiunta neppure una funzione. Lo snapshot conserva sia la
libreria sia le singole funzioni, che diventano immediatamente disponibili allo
stesso modo in IntelliSense, type checker, Build, Run e Debug.

```csharp
host.RegisterPermission(new DdfHostPermissionDefinition(
    "algorithms.use", "Uso algoritmi applicativi"));

host.RegisterFunctionLibrary(new DdfHostFunctionLibraryDefinition(
    "scoring", "Calcolo punteggi",
    new[]
    {
        new DdfHostFunctionDefinition(
            "weightedScore", "int",
            new[]
            {
                new DdfHostParameterDefinition("current", "int"),
                new DdfHostParameterDefinition("sample", "int"),
                new DdfHostParameterDefinition("weight", "int")
            },
            values => Convert.ToInt32(values[0]) +
                Convert.ToInt32(values[1]) * Convert.ToInt32(values[2])),
        new DdfHostFunctionDefinition(
            "isWithinRange", "bool",
            new[]
            {
                new DdfHostParameterDefinition("value", "int"),
                new DdfHostParameterDefinition("minimum", "int"),
                new DdfHostParameterDefinition("maximum", "int")
            },
            values => Convert.ToInt32(values[0]) >= Convert.ToInt32(values[1]) &&
                Convert.ToInt32(values[0]) <= Convert.ToInt32(values[2]))
    },
    requiredPermissions: new[] { "algorithms.use" }));

host.SetPermission("algorithms.use", true);
```

I permessi comuni della libreria vengono uniti a quelli specifici di ogni
funzione. Un algoritmo che aggiorna l'applicazione usa
`DdfHostFunctionEffect.HostMutation` e deve fornire un delegate `simulate` se
deve essere eseguibile senza effetti reali. Adattatori, timeout, cancellazione,
budget e diagnostiche sono gli stessi delle funzioni registrate singolarmente.
La Host Demo registra `weightedScore`, `isWithinRange` e `applyHostScore` e li
usa direttamente nei propri documenti virtuali.

## Servizi database controllati dall'host

`IDdfHostDatabaseService` non rappresenta una connessione accessibile allo
script. L'applicazione registra un database logico e un elenco chiuso di
operazioni; ogni operazione dichiara nome della funzione DDF, identificatore
dello statement, parametri, tipo restituito, permessi ed eventuale supporto alla
simulazione.

```csharp
host.RegisterType(new DdfHostTypeDefinition("CustomerRow", new[]
{
    new DdfHostFieldDefinition("id", "int"),
    new DdfHostFieldDefinition("name", "string")
}));

host.RegisterDatabase(new DdfHostDatabaseDefinition(
    "crm", databaseService,
    new[]
    {
        new DdfHostDatabaseOperationDefinition(
            "loadCustomer", "customer.byId",
            DdfHostDatabaseOperationKind.Query,
            "CustomerRow",
            new[] { new DdfHostParameterDefinition("customerId", "int") })
    }));
```

Il sorgente può chiamare `loadCustomer(id)`, ma non riceve una connessione, una
stringa SQL o credenziali. Il servizio ottiene un `DdfHostDatabaseRequest` con
lo statement registrato e un dizionario di parametri già convertiti. È quindi
l'host a creare query parametrizzate usando il proprio driver e a trasformare il
risultato nel tipo dichiarato.

`request.Invocation.CancellationToken` deve essere propagato al provider dati.
Il timeout, il budget delle chiamate, i permessi e le diagnostiche restano quelli
della pipeline host. Una `Query` è leggibile anche in Simulation; un `Command`
viene ammesso in Simulation soltanto se l'operazione dichiara
`supportsSimulation` e il servizio, osservando `Invocation.Mode`, evita effetti
reali.

L'applicazione può associare alla singola Run o procedura un ID transazione già
gestito nel proprio livello dati:

```csharp
var execution = new DdfEditorExecutionRequest(
    databaseTransactions: new Dictionary<string, string>
    {
        ["crm"] = transaction.Id
    });
```

Il servizio riceve soltanto l'identificatore opaco in `TransactionId` e decide
connessione e isolamento. L'SDK non apre connessioni e non conserva password.
Commit e rollback restano responsabilità dell'applicazione; possono essere
coordinati con l'intera Run tramite la politica degli effetti descritta sotto.

## Modalità di esecuzione

Ogni `DdfEditorExecutionRequest` dichiara una `DdfEditorExecutionMode`:

- `AnalysisOnly` esegue la build e produce diagnostiche senza avviare la VM,
  leggere getter host o invocare operazioni;
- `Simulation` esegue la VM, acquisisce gli oggetti host una sola volta e
  mantiene le successive scritture soltanto nella sessione locale;
- `HostEffects` applica getter live, setter e operazioni reali registrate.

```csharp
DdfEditorExecutionCompletion preview = await editor.ExecutionService.RunAsync(
    new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation));
```

Le funzioni host dichiarano `DdfHostFunctionEffect.Pure` oppure
`DdfHostFunctionEffect.HostMutation`. In simulazione una funzione pura usa il
delegate ordinario, del quale l'host garantisce l'assenza di effetti; una funzione
mutante deve fornire `simulate`, altrimenti la VM restituisce `DDF412` senza
chiamare l'operazione reale. `AnalysisOnly` non è una sessione di debug valida:
per breakpoint e stepping occorre scegliere `Simulation` o `HostEffects`.

La modalità predefinita resta `HostEffects` per compatibilità con gli host già
integrati. Le applicazioni che mostrano un comando di anteprima devono quindi
richiedere esplicitamente `Simulation`; non esiste un passaggio implicito dalla
simulazione agli effetti reali.

## Applicazione, risultati parziali e rollback degli effetti host

`DdfEditorExecutionRequest.HostEffectPolicy` stabilisce esplicitamente quando
gli effetti appartengono all'applicazione:

- `Immediate` mantiene il comportamento predefinito. Ogni setter o funzione
  mutante viene applicato subito; se una fase successiva fallisce, il riepilogo
  restituisce `PartiallyApplied` e l'host decide l'eventuale compensazione;
- `RollbackOnFailure` richiede un `IDdfHostEffectTransactionFactory`. Prima
  della VM l'host crea una transazione; l'SDK chiama `CommitAsync` soltanto dopo
  un'esecuzione riuscita e `RollbackAsync` in caso di diagnostica runtime,
  annullamento o errore.

```csharp
var request = new DdfEditorExecutionRequest(
    hostEffectPolicy: DdfHostEffectPolicy.RollbackOnFailure,
    hostEffectTransactionFactory: transactionFactory);

DdfEditorExecutionCompletion completion =
    await editor.ExecutionService.RunAsync(request);

Console.WriteLine(completion.HostEffects.Disposition);
foreach (DdfHostEffectRecord effect in completion.HostEffects.Operations)
    Console.WriteLine($"{effect.Sequence}: {effect.OperationId} - {effect.Status}");
```

`DdfHostEffectSummary` distingue `Applied`, `PartiallyApplied`, `Simulated`,
`Committed`, `RolledBack` e gli errori di apertura, commit o rollback. Registra
le funzioni mutanti e le scritture sugli oggetti host nell'ordine osservato. In
`Simulation` non viene aperta alcuna transazione reale e le operazioni risultano
`Simulated`.

L'identificatore opaco della transazione è disponibile alle funzioni tramite
`DdfHostInvocationContext.HostEffectTransactionId`, così database e servizi
esterni possono partecipare allo stesso scope deciso dall'host. La factory deve
includere nel proprio rollback tutti gli effetti che desidera rendere atomici;
l'SDK non può inventare una compensazione per API esterne arbitrarie.

Questa politica non è collegata a Undo/Redo: l'Undo modifica esclusivamente il
buffer di testo. Una Run già applicata viene annullata soltanto dalla transazione
host o da una successiva operazione compensativa esplicita.

## Operazioni asincrone, limiti e risorse

Una funzione host può fornire `invokeAsync` e, se mutante, `simulateAsync` come
delegate che restituiscono `ValueTask<object>`. Riceve un
`DdfHostInvocationContext` contenente identità univoca dell'esecuzione, modalità,
istante iniziale, scadenza globale e `CancellationToken`. Il delegate non deve
accedere direttamente ai controlli grafici: viene eseguito fuori dal thread UI e
l'host deve effettuare esplicitamente il marshaling verso il proprio dispatcher.

```csharp
host.RegisterFunction(new DdfHostFunctionDefinition(
    "loadValue", "int",
    new[] { new DdfHostParameterDefinition("key", "string") },
    invoke: null,
    invokeAsync: async (context, args) =>
    {
        await using var connection = await OpenConnectionAsync(
            context.CancellationToken);
        return await LoadAsync((string)args[0], context.CancellationToken);
    }));
```

`DdfEditorExecutionRequest` accetta:

- `timeout`, scadenza cooperativa dell'intera esecuzione;
- `hostCallTimeout`, limite di ogni singola operazione host, 30 secondi di default;
- `maxHostCalls`, budget predefinito di 1024 chiamate;
- `maxHostResources`, massimo di 128 risorse trattenute dalla sessione.

Una funzione può trasferire alla sessione la proprietà temporanea di un
`IDisposable` o `IAsyncDisposable` usando `RegisterResource` o
`RegisterAsyncResource`. La VM li rilascia in ordine inverso anche dopo errore,
timeout o annullamento. Il superamento dei budget produce `DDF413`, il timeout di
una chiamata `DDF414` e un errore di cleanup `DDF415`. Una singola istanza di
`IDdfEditorExecutionService` continua ad accettare una sola sessione alla volta;
istanze indipendenti possono lavorare in parallelo.

La cancellazione in-process è necessariamente cooperativa. Un delegate asincrono
deve osservare il token; un delegate sincrono che si blocca o ignora il timeout
può continuare sul thread pool anche dopo che la VM lo ha abbandonato. Per codice
non fidato, estensioni native o operazioni non interrompibili, l'host deve usare
un processo worker separato, con IPC limitato, limiti del sistema operativo e
terminazione del processo. Il runtime in-process non è e non viene presentato
come sandbox di sicurezza.

## Persistenza fornita dall'host

`IDdfEditorPersistenceProvider` separa lo stato dell'editor dal supporto scelto
dall'applicazione ospitante. L'SDK non conosce file di configurazione, cartelle
utente, database o servizi cloud: consegna e riceve soltanto un
`DdfEditorPersistedState` immutabile.

```csharp
var persistence = new DdfInMemoryEditorPersistenceProvider();
editor.AttachPersistenceProvider(persistence);

editor.SaveState();       // chiama persistence.Save(snapshot)
bool restored = editor.RestoreState();
```

Lo snapshot contiene tutti i buffer, documento attivo, testo e stato dirty,
opzioni grafiche, tabulazione, scorciatoie, Modalità didattica e configurazione
del debug. `Debugging.Breakpoints` identifica i documenti tramite il loro indice
nello snapshot e `Debugging.WatchExpressions` conserva le espressioni Watch;
durante il ripristino gli indici vengono rimappati sulle nuove identità documento.
Un provider reale
può serializzare questi valori nel formato e nel luogo stabiliti dall'host.
`DdfInMemoryEditorPersistenceProvider` è soltanto un'implementazione minima per
sessioni temporanee e test: non effettua I/O.

Per proteggere il lavoro dell'utente, `ApplyPersistenceState` accetta più
documenti soltanto quando è collegato un servizio documentale vuoto e rifiuta il
ripristino se sono già presenti buffer. Se la creazione di uno dei documenti
fallisce, i buffer creati durante quel tentativo vengono rimossi. I comandi
`persistence.saveState` e `persistence.restoreState` restano disabilitati finché
non viene associato esplicitamente un provider.

Il salvataggio è consentito anche mentre il debugger è in pausa, ma fotografa
soltanto documenti, preferenze, breakpoint e testi Watch. Lo snapshot non contiene
lo stato vivo della VM, valori runtime, Call Stack o una sessione riprendibile.
Durante Run o Debug, il ripristino e la sostituzione dei servizi documentale,
linguistico, di esecuzione o di debug sono rifiutati per non invalidare la
fotografia in uso. Alla distruzione della superficie, un debugger creato e
posseduto dall'SDK viene annullato e sbloccato; un servizio iniettato resta invece
di proprietà dell'host, che ne governa la durata.

## Progetti nei contenitori dell'host

`IDdfEditorProjectContainer` rappresenta un singolo contenitore già scelto
dall'applicazione ospitante. L'implementazione può scrivere in un file, archivio,
database, object storage o servizio cloud: l'SDK chiama soltanto `Save` e `Load`
e non conosce formato, percorso, credenziali o protocollo.

```csharp
var container = new MyDatabaseProjectContainer(projectKey);
editor.AttachProjectContainer(container);

editor.SaveProject("calculation-42", "Calcolo portata",
    new[] { new KeyValuePair<string, string>("tenant", "plant-a") });

// Su una superficie con raccolta documentale vuota:
bool opened = editor.OpenProject();
```

`DdfEditorProjectState` è immutabile e contiene identità e nome del progetto,
metadati opachi dell'host, ID/versione del pacchetto linguistico e lo snapshot
editor completo. Prima dell'apertura l'host deve attivare esattamente il pacchetto
richiesto; una mancata corrispondenza viene rifiutata senza creare documenti.
L'apertura mantiene inoltre la protezione non distruttiva della persistenza: il
servizio documentale deve essere vuoto e non può esserci Run o Debug attivo.

La prima chiamata `SaveProject(projectId, displayName, metadata)` assegna
l'identità; le successive possono usare `SaveProject()` o il comando
`project.save`. Il comando `project.open` è disponibile soltanto quando un
contenitore è associato e la raccolta è vuota. Gli eventi `ProjectSaved` e
`ProjectOpened` consentono all'host di aggiornare titolo, recenti e propria UI.
`DdfInMemoryEditorProjectContainer` serve unicamente per demo e test.

## Esecuzione per applicazioni host

`IDdfEditorExecutionService` esegue una fotografia immutabile dei documenti
forniti dall'host. Non legge né salva automaticamente i percorsi associati ai
buffer: sorgenti virtuali e modifiche non persistite sono compilati direttamente
nella IR della VM.

```csharp
editor.ExecutionService.OutputReceived += (sender, args) =>
    ShowOutput(args.Text);
editor.ExecutionService.StateChanged += (sender, args) =>
    ShowState(args.State);

DdfEditorExecutionCompletion completion =
    await editor.ExecutionService.RunAsync();
```

Il contratto espone gli stati `Idle`, `Running` e `Stopping`, una generazione
crescente, gli eventi di output/completamento e `Stop()` cooperativo. Il risultato
distingue errori di build, diagnostiche runtime, annullamento ed errore interno.
Una seconda chiamata Run sulla stessa istanza viene rifiutata mentre istanze
diverse possono essere eseguite contemporaneamente.

Il contratto `0.31` aggiunge `DdfEditorExecutionBuildHandler` e
`DdfEditorExecutionBuildContext`. Un host che possiede manifest, dipendenze o
sorgenti non tutti aperti nell'editor può preparare la build sul thread chiamante
e consegnare al runtime l'artefatto, il pacchetto e la mappa fra identità dei
documenti e percorsi runtime. Run e Debug continuano poi attraverso gli stessi
servizi, eventi e snapshot pubblici. Senza handler resta invariata la pipeline
in-memory priva di I/O implicito. `DdfEditorExecutionCompletion.Artifact` rende
inoltre osservabile l'artefatto effettivamente eseguito.

Gli eventi del servizio sono emessi dal worker dedicato: un host grafico deve
eseguire il marshal verso il proprio thread UI.

## Debug per applicazioni host

`IDdfEditorDebugService` espone il primo ciclo di debug incorporabile senza
rendere pubblica la sessione interna della VM. I breakpoint identificano il
buffer con `DocumentId`, non con un percorso obbligatorio: durante ogni avvio
vengono tradotti nei percorsi dello snapshot di build, quindi funzionano anche
su documenti virtuali e modifiche non salvate.

```csharp
IDdfEditorDocument helper = editor.DocumentService.OpenDocuments[1];
editor.DebugService.SetBreakpoints(new[]
{
    new DdfEditorBreakpoint(helper.Id, 4)
});
editor.DebugService.Paused += (sender, args) =>
{
    ShowSource(args.Location.DocumentId, args.Location.Start,
        args.Location.Length);
};

Task<DdfEditorExecutionCompletion> debug =
    editor.DebugService.RunAsync();
// Quando State == Paused:
editor.DebugService.StepInto(); // oppure StepOver, StepOut o Continue
```

Gli stati pubblici sono `Idle`, `Running`, `Paused` e `Stopping`. La posizione
di pausa contiene identità e nome del documento, eventuale percorso, riga,
colonna e intervallo sorgente. `Stop()` è cooperativo e può essere richiesto
anche durante una pausa; la distruzione del servizio annulla la sessione e
rilascia il waiter senza bloccare l'host. I breakpoint si sostituiscono soltanto
a debugger inattivo, così ogni esecuzione usa una configurazione immutabile.
Gli eventi di debug, come quelli di esecuzione, arrivano dal worker e richiedono
marshal esplicito nelle UI.

`StepInto()` sospende sulla prossima istruzione, entrando in una funzione
richiamata; `StepOver()` completa una chiamata restando nel frame corrente;
`StepOut()` continua fino al chiamante. Tutte e tre le operazioni sono accettate
soltanto nello stato `Paused`, liberano la posizione corrente e producono una
nuova pausa con il documento sorgente corretto, anche attraversando più buffer.

Ogni evento `Paused` contiene anche un `DdfEditorDebugSnapshot` immutabile. La
proprietà `CallStack` ordina i frame da quello corrente ai chiamanti e associa
ciascuno a funzione, documento, posizione e variabili del relativo scope.
`Variables` contiene i valori visibili nel frame corrente insieme alle globali;
`Globals` offre la vista filtrata delle sole globali. Ogni variabile espone nome,
tipo, valore formattato, categoria, profondità di scope, figli per array/strutture
e l'indicazione di eventuale troncamento. L'host può mostrare un frame qualsiasi
e navigare usando `frame.Location`, senza accedere alla memoria viva della VM.

### Watch sicure

Durante lo stato `Paused`, `EvaluateWatch(expression, frameIndex)` valuta
un'espressione sul frame scelto e sulle globali catturate nello stesso snapshot.
Il risultato espone disponibilità, messaggio, tipo e valore profondamente copiato.

```csharp
DdfEditorWatchResult current =
    editor.DebugService.EvaluateWatch("values[0] + point.x");
DdfEditorWatchResult caller =
    editor.DebugService.EvaluateWatch("callerValue", frameIndex: 1);
```

Sono supportati riferimenti, campi, indici, parentesi e operatori puri definiti
dal pacchetto linguaggio attivo per la sessione. Chiamate di funzione,
assegnazioni, output, incrementi e altre operazioni con effetti collaterali sono
rifiutati prima della valutazione. Fuori da una pausa, con frame inesistente o
snapshot troncato, il risultato è indisponibile e contiene una spiegazione;
nessuna Watch può leggere o modificare la memoria viva del runtime.

## Confini attuali

`DdfEditorView` resta una superficie isolata: i servizi restituiscono i dati di
analisi ma non creano automaticamente schede, palette o popup, perché tale policy
resta sotto il controllo dell'host. Chi richiede la shell completa può usare
`DdfEmbeddedEditorControl`.

Core e controlli WinForms sono ora librerie separate e impacchettabili; il
launcher standalone non è più una dipendenza degli host. La pubblicazione su un
feed, la compatibilità binaria definitiva e la garanzia semantica stabile restano
differite perché il contratto è ancora beta.

I dettagli concreti come `RichTextBox`, gutter e gestione multi-cursore non
fanno parte del contratto pubblico. Questo consente di sostituirli senza
obbligare gli host a modificare la propria integrazione.

## Applicazione host dimostrativa

`samples/DDFLanguageEditor.MinimalHost` è l'esempio iniziale consigliato. Aggiunge
un solo `DdfEmbeddedEditorControl`, collega un pulsante host allo script, inoltra
il salvataggio a una variabile dell'applicazione e mostra il risultato:

```powershell
dotnet run --project samples\DDFLanguageEditor.MinimalHost\DDFLanguageEditor.MinimalHost.csproj
```

`samples/DDFLanguageEditor.HostDemo` è una vera applicazione WinForms separata
che referenzia la libreria WinForms e incorpora due `DdfEditorView` nello
stesso processo. Il profilo tecnico usa due documenti virtuali e un tema scuro;
quello didattico usa un documento, un tema chiaro e la Modalità didattica. Ogni
istanza possiede servizi documentali, configurazione e persistenza in memoria
indipendenti.

```powershell
dotnet run --project samples\DDFLanguageEditor.HostDemo\DDFLanguageEditor.HostDemo.csproj
```

I pulsanti permettono di analizzare il buffer attivo, navigare alla definizione,
eseguire o arrestare il programma e avviare un debug sulla riga corrente con
Continua, Step Into/Over/Out e Stop. Durante la pausa il relativo Output elenca
Call Stack e variabili visibili. Il campo Watch consente di valutare manualmente
un'espressione pura sulla pausa corrente. La demo può inoltre salvare lo snapshot del profilo e ripristinarlo
dentro una nuova raccolta di documenti. Ogni profilo mostra il proprio Output e,
alla pausa, attiva il documento corretto e seleziona l'intervallo sorgente.
La demo non legge il profilo della shell e non scrive file: rende
quindi osservabile il confine fra responsabilità dell'SDK e responsabilità
dell'applicazione ospitante.

`samples/DDFLanguageEditor.DatabaseDemo` mostra il percorso applicativo
completo: un database clienti in memoria implementa `IDdfHostDatabaseService`,
una libreria host calcola e valida il nuovo punteggio, lo script aggiorna database
e risultato applicativo nello stesso scope `RollbackOnFailure`, mentre editor,
griglia, Output e `FlowchartView` restano sincronizzati anche durante il debug.
Il database può essere sostituito da un adapter reale senza modificare lo script.

```powershell
dotnet run --project samples\DDFLanguageEditor.DatabaseDemo\DDFLanguageEditor.DatabaseDemo.csproj
```

## Prossimo confine

Il successivo incremento SDK accompagnerà la specifica verificabile e la
migrazione della `0.12.4`. Qualunque futura capacità esterna conserverà lo stesso
modello esplicito, per istanza e deny-by-default, senza reflection arbitraria dal
sorgente.
