# Registro delle decisioni del linguaggio DDF

Le decisioni sono append-only: una scelta superata viene marcata come tale e
collegata alla decisione sostitutiva, senza riscriverne retroattivamente la
motivazione. Gli stati ammessi sono `Accettata`, `Proposta`, `Superata` e
`Rifiutata`.

## DDF-0001 — Un'unica semantica, più profili sintattici

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Contesto:** DDF deve poter offrire una sintassi propria, profili familiari e
  profili didattici localizzati senza fingere di eseguire linguaggi differenti.
- **Decisione:** i profili condividono tipi, semantica, IR, API host e risultati.
  Se un profilo richiede una capacità semantica diversa, è un altro linguaggio o
  una nuova versione, non una semplice traduzione di keyword.
- **Alternative rifiutate:** dichiarare compatibilità C++/Java/C#/VB in base alla
  sola somiglianza sintattica; tradurre automaticamente il file quando si cambia
  pacchetto.
- **Conseguenze:** i test normativi devono poter verificare equivalenza fra
  profili; incompatibilità e conversioni saranno operazioni esplicite.

## DDF-0002 — Codice testuale come unica fonte autorevole

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** flowchart, Outline e modalità didattica sono proiezioni derivate
  dalla semantica del codice e non modificano automaticamente il sorgente.
- **Alternative rifiutate:** editing bidirezionale implicito fra diagramma e testo.
- **Conseguenze:** non esistono conflitti fra due rappresentazioni modificabili;
  la navigazione dal diagramma resta supportata.

## DDF-0003 — Accesso all'host tramite capacità esplicite

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** oggetti, proprietà, eventi, funzioni, database e librerie esterne
  sono utilizzabili soltanto tramite cataloghi e adapter tipizzati registrati
  dall'host, con permessi, limiti e modalità di effetto dichiarati.
- **Alternative rifiutate:** reflection arbitraria dal sorgente, enumerazione
  automatica di tutto l'host, caricamento diretto e non autorizzato di DLL/COM.
- **Conseguenze:** l'interoperabilità con assembly, COM e librerie di sistema
  resterà possibile tramite adapter futuri; l'in-process non è una sandbox.

## DDF-0004 — VM e backend .NET devono condividere la semantica

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** la VM resta l'implementazione di riferimento durante lo sviluppo
  del backend .NET + Roslyn. Risultati, errori, ordine di valutazione e chiamate
  host dovranno essere confrontabili tramite lo stesso corpus.
- **Alternative rifiutate:** generare C# direttamente dalla sintassi ignorando la
  IR; far divergere la modalità embedded da quella standalone.
- **Conseguenze:** la `0.12` deve chiudere le ambiguità semantiche prima della
  toolchain `0.13`.

## DDF-0005 — Originalità subordinata alla chiarezza

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** una grafia differente viene mantenuta quando rende DDF coerente
  e riconoscibile, non soltanto per distinguersi. Gli operatori storici `!!`,
  `>><<`, `<<>>`, `^/`, l'uso logico di `&` e la forma `out` devono superare una
  revisione di leggibilità nella `0.12.1`.
- **Alternative rifiutate:** copiare integralmente C; conservare ogni simbolo
  beta per evitare incompatibilità; cambiare simboli senza beneficio misurabile.
- **Conseguenze:** la beta può introdurre cambiamenti incompatibili documentati e
  accompagnati da una guida di migrazione.

## DDF-0006 — Nucleo lessicale 0.12

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** identificatori Unicode case-sensitive, numeri con cifre ASCII,
  booleani esclusivamente `true`/`false`, stringhe su una riga e insieme chiuso
  di escape `\"`, `\\`, `\n`, `\r`, `\t`. I commenti restano `//` e `/* */`.
- **Alternative rifiutate:** cifre Unicode non interpretabili uniformemente dal
  runtime; alias booleani con maiuscole; stringhe multilinea implicite; escape
  sconosciuti accettati silenziosamente.
- **Conseguenze:** `True` e `False` diventano identificatori, gli escape invalidi
  producono `DDF005` e i progetti dichiarano il pacchetto DDF `0.12.0`.

## DDF-0007 — Operatori del nucleo 0.12

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** DDF conserva `<<` per assegnazione, `>>` per output e `^` per
  potenza. Usa `!=`, `&&`, `||`, `^^` e `!` per confronto e logica. Rimuove dal
  pacchetto canonico `!!`, `&`, `|&|`, `>><<`, `<<>>` e `^/`.
- **Motivazione:** `!!` contraddice l'uso comune di `!`; `&` suggerisce
  un'operazione bitwise non disponibile; gli operatori di intervallo beta non
  codificavano due estremi e nella VM producevano risultati fuorvianti; `^/`
  era poco leggibile. `^^` mantiene distinta la potenza dallo XOR senza
  introdurre keyword speciali nel lexer.
- **Alternative rifiutate:** conservare ogni simbolo beta; copiare anche
  l'assegnazione `=`; introdurre ora operatori testuali che richiederebbero
  regole di confine lessicale ancora non definite.
- **Conseguenze:** il pacchetto passa a `0.12.1`; i sorgenti beta richiedono la
  migrazione esplicita descritta in `DDF_MIGRATION_0_12.md`. Intervalli e radici
  saranno funzioni di libreria leggibili se approvate nella `0.12.3`.

## DDF-0008 — Unità dichiarativa e blocchi obbligatori

- **Stato:** Accettata
- **Data:** 2026-09-27
- **Decisione:** un file DDF contiene direttive iniziali seguite soltanto da
  strutture, funzioni o variabili globali. Le istruzioni eseguibili top-level e
  il punto e virgola isolato non sono validi. `if`, `else`, `while`, `do` e
  `for` richiedono blocchi; è ammessa la concatenazione `else if`.
- **Motivazione:** separare dichiarazione ed esecuzione rende deterministici
  build multi-file e inizializzazione. Le graffe obbligatorie eliminano il
  dangling `else` e proteggono da cambiamenti accidentali durante editing,
  formattazione e generazione della flowchart.
- **Alternative rifiutate:** script top-level impliciti nel medesimo profilo;
  corpi a singola istruzione; terminatori vuoti usati come no-op.
- **Conseguenze:** l'EBNF diventa il validatore normativo del pacchetto tramite
  `ValidationDiagnostics`. Il parser AST storico può ancora recuperare forme
  incomplete per i servizi interattivi, senza renderle programmi validi.

## DDF-0009 — Tipi espliciti e conversioni conservative

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** dichiarazioni, parametri e risultati mantengono tipi espliciti;
  non esistono `var`, truthiness o cast impliciti con perdita. L'unica promozione
  generale è `int -> float`; una stringa di una code unit inizializza `char`
  soltanto come regola contestuale finché non esisterà un letterale dedicato.
- **Alternative rifiutate:** inferenza globale; conversioni automatiche fra
  stringhe, numeri e booleani; mantenere `char` come numero.
- **Conseguenze:** completamento, checker, VM e Roslyn possono mostrare sempre il
  tipo effettivo e produrre gli stessi errori.

## DDF-0010 — Valori gestiti non nulli e collezioni chiuse

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** primitivi e stringhe hanno semantica di valore; array
  monodimensionali e strutture hanno identità gestita condivisa ma non possono
  essere nulli. `dict` viene escluso dal nucleo finché non avrà chiavi e valori
  tipizzati, sintassi e iterazione definite.
- **Alternative rifiutate:** dizionario dinamico non tipizzato; array
  multidimensionali soltanto perché il parser beta accetta più suffissi;
  uguaglianza implicita per riferimento dei composti.
- **Conseguenze:** aliasing di array e strutture è documentato; la loro
  uguaglianza richiederà funzioni esplicite e `dict` sarà rimosso dal pacchetto.

## DDF-0011 — Errori numerici osservabili e deterministici

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** `int` è signed 32 bit con overflow checked; `float` è binary64
  finito. Divisione per zero, NaN, infinito e potenze intere non rappresentabili
  sono errori runtime DDF con posizione sorgente.
- **Alternative rifiutate:** wrap-around silenzioso; far dipendere conversioni e
  overflow dal backend; propagare NaN e infinito in codice didattico/embedded.
- **Conseguenze:** la VM deve abbandonare l'aritmetica intera mediata da `double`
  e il backend Roslyn deve emettere controlli equivalenti.

## DDF-0012 — Nomi univoci e firme senza overload

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** non è ammesso nascondere un valore già visibile; funzioni e
  strutture hanno nomi univoci nel progetto e non esistono overload. Le funzioni
  non `void` devono restituire su ogni percorso raggiungibile.
- **Alternative rifiutate:** shadowing libero della beta; risoluzione overload;
  ritorno implicito del valore iniziale.
- **Conseguenze:** navigazione, debug e diagnostiche restano non ambigui; il
  checker necessita analisi dei duplicati e dei percorsi di controllo.

## DDF-0013 — Mapping CLR obbligatorio per ogni tipo eseguibile

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** ogni tipo DDF eseguibile deve avere un mapping CLR/C# stabile,
  versionato e accettabile da Roslyn. `int`, `float`, `char`, `bool`, `string` e
  `void` corrispondono rispettivamente a `System.Int32`, `System.Double`,
  `System.Char`, `System.Boolean`, `System.String` e `System.Void`; array DDF a
  SZArray e strutture DDF a classi CLR generate per conservarne l'aliasing.
- **Alternative rifiutate:** tipi solo interpretabili dalla VM; mapping deciso
  durante la generazione senza contratto; uso di `System.Single` per il DDF
  `float`; generazione di C# `struct` con copie divergenti dalla VM.
- **Conseguenze:** lo schema dei tipi dovrà dichiarare e validare il mapping di
  backend. La compatibilità CLR non sostituisce i controlli DDF e non espone
  automaticamente reflection, assembly o membri .NET allo script.

## DDF-0014 — Roslyn consuma soltanto IR bound

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** il backend Roslyn non interpreta AST, token o nomi DDF grezzi.
  Consuma una IR bound con simboli, tipi, conversioni, chiamate e destinazioni
  già risolti dal compilatore DDF; genera alberi C# sicuri e usa Roslyn per
  validazione ed emit IL/PDB, non come fonte della semantica DDF.
- **Alternative rifiutate:** traduzione testuale DDF -> C#; affidare binding e
  conversioni al compilatore C#; concatenare identificatori o frammenti sorgente
  non attendibili nel codice generato.
- **Conseguenze:** la IR deve evolvere alla versione bound prima del backend.
  Name mangling, runtime ABI, reference set, opzioni di compilazione, Portable
  PDB e confronto VM/Roslyn diventano gate obbligatori e verificabili.

## DDF-0015 — Compilazione esclusiva dello standalone

- **Stato:** Accettata
- **Data:** 2026-09-28
- **Decisione:** Roslyn, emit, linker, loader e pubblicazione appartengono
  esclusivamente all'applicazione standalone e alla CLI. L'editor embedded resta
  sempre uno strumento di scripting interpretato che amplia l'host attraverso
  capacità registrate; non genera, carica o persiste assembly DDF.
- **Alternative rifiutate:** compilazione opzionale abilitabile dall'host;
  nascondere soltanto i pulsanti mantenendo il compilatore nell'SDK embedded;
  eseguire silenziosamente assembly generati al posto dello script.
- **Conseguenze:** il compilatore vive in un assembly separato non referenziato
  da Core/WinForms. Convalida e preparazione IR restano condivise, mentre il
  servizio compilatore non è esposto da `DdfEditorView` o
  `DdfEmbeddedEditorControl`; un test architetturale protegge il confine.

## DDF-0016 — Moduli espliciti senza include testuale

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** un modulo è il manifesto progetto con identità e versione
  stabili più il suo insieme ordinato di sorgenti. I file condividono uno spazio
  dei nomi piatto e case-sensitive; cartelle e file non creano namespace.
  `exports` dichiara l'API futura verso altri moduli. `@@'...'` resta soltanto
  una direttiva legacy deprecata di libreria/capacità e non è un include o un
  import. Le dipendenze fra moduli sono dichiarate nel manifesto.
- **Alternative rifiutate:** concatenazione testuale di sorgenti; visibilità
  derivata dalla struttura delle cartelle; rendere ogni file un modulo implicito;
  usare `@@` contemporaneamente per librerie, file e package.
- **Conseguenze:** il build valida identità, versione ed esportazioni e le lega
  al fingerprint. Il futuro resolver dovrà consumare soltanto API esportate,
  rifiutare cicli e produrre la stessa IR bound per VM e Roslyn.

## DDF-0017 — Risoluzione esatta e visibilità non transitiva

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** una dipendenza seleziona una sola coppia esatta `id@version`.
  Il consumatore vede soltanto gli export delle dipendenze dirette; un modulo
  non re-esporta implicitamente le proprie dipendenze. Lo store locale
  `.ddf/modules/<id>/<version>` è il resolver predefinito ma non partecipa ai
  nomi e può essere sostituito tramite `IDdfModuleResolver`.
- **Alternative rifiutate:** scelta automatica della versione più recente;
  intervalli di versione nella linea `0.12`; visibilità transitiva; deduzione
  dell'identità dalla cartella; inclusione dello store nel workspace applicativo.
- **Conseguenze:** risoluzione, cicli e conflitti sono deterministici e l'intero
  grafo entra nella ricevuta. `DdfIrModule` conserva identità, export e dipendenze;
  la VM qualifica il contesto di funzioni, strutture e globali, consentendo nomi
  privati uguali in moduli diversi senza introdurre namespace sintattici.

## DDF-0018 — Pacchetti modulo sorgente immutabili per versione

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** `.ddfmod` è un archivio ZIP deterministico con il manifesto alla
  radice e i sorgenti da esso selezionati. Identità e versione provengono soltanto
  dal manifesto. Versioni diverse convivono nello store; la stessa coppia è
  immutabile salvo sostituzione esplicita. Il gestore installa tramite staging,
  validazione e rollback e non esegue codice.
- **Alternative rifiutate:** dedurre identità dal filename; includere l'intera
  cartella di sviluppo; aggiornare silenziosamente una versione esistente;
  trattare il percorso di installazione come namespace; installare assembly o
  concedere capability insieme ai sorgenti.
- **Conseguenze:** i pacchetti sono riproducibili e sicuri rispetto al filesystem,
  ma non ancora firmati né provenienti da un registry. La validazione semantica
  completa resta nella pipeline di build del consumatore; la UI del gestore può
  essere aggiunta senza cambiare formato o resolver.

## DDF-0019 — Libreria standard minima, versionata e priva di capacità implicite

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** il nucleo standard è `ddf.standard@0.12.3` e contiene soltanto
  `print`, `readLine`, `length`, `toInt` e `toFloat`. Ogni funzione dichiara
  firma, documentazione, binding chiuso, effetto ed errori; IDE, type checker e
  VM consumano lo stesso catalogo. I canali Input/Output non concedono accesso
  al catalogo host e `length` conserva la semantica UTF-16 di `System.String`.
- **Alternative rifiutate:** esporre automaticamente API .NET; trattare la
  libreria come modulo installabile; lasciare effetti ed errori impliciti nel
  codice VM; aggiungere subito filesystem, rete, database, collezioni o
  matematica avanzata; usare `@@` come autorizzazione.
- **Conseguenze:** lo schema standard-library sale a `1.1` e l'adapter a
  `ddf-standard-library-v3`; metadati incoerenti vengono rifiutati atomicamente.
  Ogni futura funzione richiede binding sicuro, mapping Roslyn e test
  differenziali, mentre capacità esterne restano moduli o adattatori autorizzati.

## DDF-0020 — Store moduli embedded deny-by-default

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** la shell embedded non accede allo store moduli finché l'host
  non fornisce una capability per una radice progetto assoluta ed esistente.
  Lettura/convalida è il privilegio minimo; installazione, rimozione e creazione
  dei pacchetti sono autorizzazioni separate. La capability è per istanza, non
  persistente e revocabile; la revoca chiude il gestore e rimuove i comandi.
- **Alternative rifiutate:** ereditare automaticamente il workspace della shell;
  mostrare sempre il gestore; un singolo flag onnipotente; persistere il consenso;
  dedurre l'autorizzazione dal catalogo host o da una direttiva sorgente.
- **Conseguenze:** il contratto SDK sale a `0.33`. L'host conserva il controllo
  del filesystem e può offrire una UI in sola lettura. La capability autorizza
  esclusivamente lo store sorgente: non concede esecuzione, compilazione,
  reflection, caricamento assembly o accesso alle API dell'applicazione.

## DDF-0021 — Una specifica autonoma come autorità normativa

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** `DDF_LANGUAGE_SPEC.md` consolida in un unico documento
  autosufficiente il contratto del linguaggio DDF `0.12`, distribuito dal
  pacchetto canonico `0.12.3`. Ogni sezione è normativa salvo
  marcatura esplicita; esempi e matrice delle fonti sono non normativi. I
  documenti lessicale, sintattico, operatori, tipi, moduli e libreria standard
  restano approfondimenti, non autorità semantiche concorrenti.
- **Alternative rifiutate:** richiedere al lettore di ricostruire il linguaggio
  da molti documenti; considerare il comportamento corrente del codice come
  specifica; rendere normativi esempi informali; mantenere ambiguità storiche
  come `dict` o array multidimensionali soltanto perché recuperabili dal parser.
- **Conseguenze:** ogni cambiamento del linguaggio deve aggiornare prima o
  insieme la specifica autonoma, il pacchetto eseguibile e i test. Una
  divergenza blocca il rilascio; gli strumenti possono continuare a recuperare
  forme incomplete senza promuoverle a sintassi valida.

## DDF-0022 — Versioni separate e migrazioni conservative

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** versione del prodotto, `languageVersion`, `packageVersion`,
  `engineApi`, schemi, moduli e libreria standard sono domini indipendenti. La
  stessa versione del linguaggio garantisce la stessa semantica osservabile; i
  progetti e i moduli continuano a selezionare revisioni esatte. Durante `0.x`
  ogni cambiamento semantico incrementa il minor del linguaggio; dopo `1.0` un
  cambiamento incompatibile incrementa il major. Le migrazioni automatiche sono
  ammesse soltanto se strutturali, equivalenti, idempotenti, con anteprima e
  checkpoint; ogni caso ambiguo resta manuale.
- **Alternative rifiutate:** usare la versione dell'app per identificare il
  linguaggio; considerare compatibili tutti i pacchetti con ID simile; scegliere
  automaticamente la revisione più recente; sostituzioni testuali globali;
  migrare o salvare implicitamente durante il cambio profilo; promettere un
  migratore prima di implementarlo.
- **Conseguenze:** `DDF_LANGUAGE_VERSIONING.md` è la policy normativa e
  `ddf-language-version-policy.json` la rappresentazione verificabile. Le
  trasformazioni beta sicure sono soltanto candidate non ancora implementate;
  operatori ambigui, tipi rimossi e cambi strutturali richiedono revisione umana.

## DDF-0023 — Baseline verificabile durante il ciclo candidato

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** il primo ciclo candidato congela temporaneamente i documenti
  normativi, il pacchetto DDF canonico, la policy di versione e il manifesto del
  corpus tramite un elenco versionato di impronte SHA-256 su UTF-8/LF. Lo stato
  pubblico resta `beta` finché tutti i gate della `0.12.5` non sono superati.
  Ogni modifica al perimetro deve partire da un problema classificato, rispettare
  la policy di versione e aggiornare congiuntamente tutte le rappresentazioni.
- **Alternative rifiutate:** affidarsi soltanto alla revisione Git; dichiarare
  subito `candidate`; congelare anche UI e test; aggiornare le impronte senza una
  motivazione registrata; permettere alla VM storica di ridefinire il contratto.
- **Conseguenze:** una regressione rileva qualsiasi deriva degli artefatti
  congelati indipendentemente dai terminatori di riga. Correzioni UI e di
  conformità restano possibili, ma un cambiamento del linguaggio riavvia i gate
  interessati e può richiedere una nuova versione DDF.

## DDF-0024 — Controllo esplicito delle incompatibilità del candidato

- **Stato:** Accettata
- **Data:** 2026-09-29
- **Decisione:** durante il ciclo candidato nessuna modifica incompatibile può
  essere accettata come semplice correzione. Deve essere registrata come problema
  `breaking-contract`, dichiarare impatto normativo, riferirsi a una decisione
  accettata che citi problema e versione obiettivo e scegliere una versione DDF
  maggiore di quella congelata. La policy macchina e i test negativi costituiscono
  un gate aggiuntivo rispetto alle impronte della baseline.
- **Alternative rifiutate:** aggiornare soltanto l'impronta; riutilizzare la
  versione `0.12`; affidarsi al messaggio di commit; accettare una decisione che
  non cita il problema; modificare prima il pacchetto e documentare dopo.
- **Conseguenze:** un cambiamento breaking riapre il ciclo sotto una nuova versione
  e richiede riallineamento di specifica, pacchetto, corpus, migrazione e baseline.
  Le correzioni di conformità che non cambiano il contratto restano ammesse e
  devono comunque essere classificate nel registro del candidato.

## DDF-0025 — Costanti tipizzate esplicite con `fixed`

- **Stato:** Accettata
- **Data:** 2026-10-12
- **Decisione:** DDF `0.13`, distribuito inizialmente dal pacchetto canonico
  `ddf@0.13.0`, introduce `fixed` per dichiarare valori immutabili tipizzati
  locali o di modulo. L'inizializzatore è obbligatorio e deve essere una
  espressione pura e deterministica; la prima verticale accetta letterali,
  parentesi, operatori puri e altre costanti già visibili, ma non chiamate,
  output, assegnazioni, accessi a membri o indici. Assegnazione, incremento e
  decremento successivi sono sempre errori. AST e IR conservano l'intenzione
  `fixed`, la VM usa celle non scrivibili e la IR bound espone simboli non
  scrivibili al backend compilato. Il valutatore costante tipizzato comune
  materializza il valore nella IR, risolve dipendenze in avanti e valori
  esportati da moduli diretti e uniforma overflow e cicli fra VM e Roslyn.
- **Alternative rifiutate:** riutilizzare `const` con semantica C#/Roslyn;
  introdurre `static` e implicazioni OOP; trattare come costante una variabile
  ordinaria mai riassegnata; permettere chiamate dichiarate pure prima di avere
  effetti verificabili; aggiornare le impronte della baseline `0.12` come se il
  candidato storico non fosse mai esistito.
- **Conseguenze:** `fixed` è una nuova keyword e richiede il minor semantico
  DDF `0.13`; il candidato `0.12` resta riproducibile al commit
  `c3decfefa84eb19cdd03869417440fac6a198f97`. Profili sintattici, formatter,
  IntelliSense, debugger e SDK dovranno consumare lo stesso ruolo dichiarativo.
  Nessun dettaglio Roslyn entra nel Core; VM e backend condividono lo stesso
  valore costante materializzato e lo stesso comportamento per overflow e cicli.

## DDF-0026 — Stato mutabile esplicito per istanza con `state`

- **Stato:** Accettata
- **Data:** 2026-10-12
- **Decisione:** DDF `0.13` introduce `state` come intenzione dichiarativa
  distinta dalle variabili ordinarie e da `fixed`. La prima verticale ammette
  soltanto dichiarazioni di modulo, con inizializzatore obbligatorio e tipo
  primitivo scalare. La VM conserva i valori tra invocazioni esclusivamente
  quando l'host fornisce e riusa lo stesso `DdfStateStore`; altrimenti crea uno
  store effimero per il run. Lo store è process-local, azzerabile, osservabile
  tramite snapshot e rifiuta esecuzioni sovrapposte. AST, IR e IR bound
  conservano il marcatore senza introdurre tipi Roslyn nel Core.
- **Alternative rifiutate:** variabili statiche CLR implicite; stato globale di
  processo; persistenza automatica su disco; condivisione fra host; concorrenza
  last-write-wins; campi, array e strutture prima di definire snapshot e
  migrazione.
- **Conseguenze:** lifecycle esteso, provider persistente capability-based,
  migrazione, Watch e piena equivalenza delle invocazioni ripetute nel backend
  compilato restano gate espliciti della roadmap. Il contratto SDK `0.35`
  assegna uno store committed a ogni istanza e usa copie transazionali per run e
  debug: conferma soltanto `HostEffects` riusciti e scarta Simulation, errore e
  cancellazione. Nessun host acquisisce persistenza o condivisione fra istanze
  senza una scelta documentata. Durante una pausa Watch e Variabili espongono
  una copia in sola lettura marcata esplicitamente come `State`: è il valore
  transazionale del run corrente, non una conferma anticipata nello store. Il
  contratto SDK `0.36` aggiunge un provider di persistenza opt-in separato da
  documenti e layout: snapshot versionati e immutabili vengono applicati solo
  dopo validazione completa o migrazione esplicita dell'host.

## DDF-0027 — Le funzioni top-level sono funzioni di modulo

- **Stato:** Accettata
- **Data:** 2026-10-12
- **Decisione:** ogni funzione DDF è dichiarata al livello principale e
  appartiene al modulo, non al file, a una struttura o a un'istanza. Tutti i
  sorgenti dello stesso modulo condividono lo stesso namespace di funzioni; un
  export la rende visibile ai moduli dipendenti diretti. La funzione non ha un
  ricevitore implicito e viene richiamata senza creare oggetti.
- **Alternative rifiutate:** aggiungere `static` per imitare C#/Java;
  introdurre metodi nelle strutture prima di un modello di istanze; trattare il
  file come contenitore semantico; creare automaticamente una classe CLR
  visibile nel linguaggio.
- **Conseguenze:** `static` resta un identificatore ordinario e non compare nel
  lessico canonico. Locali e parametri nascono a ogni chiamata; nessun dato
  viene conservato implicitamente dalla funzione. Lo stato fra invocazioni usa
  esclusivamente `state` e lo store esplicito dell'istanza. VM e Roslyn possono
  scegliere contenitori interni differenti purché IR, risoluzione, debugger e
  SDK espongano soltanto il modello di funzione di modulo DDF.

## DDF-0028 — Errori recuperabili tipizzati, fault del motore non intercettabili

- **Stato:** Accettata
- **Data:** 2026-10-12
- **Decisione:** ogni errore attraversabile dal codice DDF possiede una
  identità di tipo backend-neutral stabile, distinta dal codice diagnostico,
  e conserva messaggio, documento, span e stack DDF. Le funzioni standard e le
  API host dichiarano in modo verificabile soltanto gli errori recuperabili che
  possono produrre. I fault del motore non appartengono a quel contratto e non
  possono essere nascosti dallo script.
- **Alternative rifiutate:** esporre direttamente eccezioni o tipi CLR;
  considerare recuperabile ogni `DDFnnn`; affidarsi unicamente al testo del
  messaggio; aggiungere subito una sintassi `try/catch` prima di stabilizzare
  identità, propagazione e confini di sicurezza.
- **Conseguenze:** Core e SDK possono descrivere gli errori senza dipendere da
  Roslyn o dalla VM; le external API includono le dichiarazioni nel proprio
  fingerprint. Il catalogo usa una allowlist di sei errori recuperabili; ogni
  condizione di contratto interno, capability, permesso, budget, timeout,
  cleanup, transazione, cancellazione, sicurezza, artefatto o guasto interno è
  un fault del motore. La sintassi DDF per
  recupero selettivo, propagazione e cleanup, insieme al trasporto equivalente
  in VM e backend compilato, resta un gate successivo e versionato.

## DDF-0029 — Recupero esplicito e cleanup strutturato nel DDF 0.14

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-12
- **Decisione:** la futura verticale usa `attempt` con uno o più `recover`
  tipizzati e un eventuale `always`; `recover error` è il solo handler generico
  e deve essere ultimo. Le funzioni DDF dichiarano con `raises` gli errori che
  possono oltrepassarne il confine e `raise` ripropaga soltanto un valore
  catturato, preservandone identità e stack. I fault del motore saltano gli
  handler ma attraversano il cleanup cooperativo.
- **Alternative rifiutate:** eccezioni CLR e sintassi `try/catch`; recupero per
  codice o testo; propagazione silenziosa non dichiarata; filtri arbitrari;
  gerarchie OOP degli errori; cleanup affidato a convenzioni manuali.
- **Conseguenze:** le identità di errore usano nomi qualificati compatibili con
  il lessico, per esempio `operation.invalidInput`. `always` viene eseguito una
  sola volta per ogni uscita cooperativa; un fault o errore già pendente resta
  primario se anche il cleanup fallisce. Lo stato transazionale viene confermato
  soltanto dopo il successo dell'intera invocazione. L'aggiunta delle keyword è
  un cambiamento beta che richiede DDF `0.14`; il contratto corrente `0.13.0`
  non viene modificato finché parser, IR, VM, Roslyn e strumenti non saranno
  consegnati insieme. Il disegno verificabile è
  `ddf-error-handling-design.json`.

## DDF-0030 — Valori opzionali espliciti senza `null` nel DDF 0.15

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** la futura verticale usa il tipo `maybe T`, il costruttore
  `some(expression)` e il valore contestuale `none`. Il contenuto è accessibile
  soltanto tramite una selezione esaustiva con `when some(binding)` e
  `when none`; non esistono conversioni implicite, `.value`, `unwrap` o un
  fallback `otherwise` capace di nascondere l'assenza.
- **Alternative rifiutate:** introdurre `null`; usare un valore sentinella del
  tipo payload; mappare automaticamente `null` CLR; esporre `Nullable<T>` o
  tipi Roslyn nella IR; considerare “non trovato” un errore; introdurre insieme
  opzionali e gestione strutturata degli errori nella stessa versione.
- **Conseguenze:** l'assenza è un dato normale e non produce stack di errore.
  IR e ABI usano un valore tagged backend-neutral; firme e fingerprint
  includono `maybe T`, mentre API host e database richiedono adapter espliciti.
  Watch mostra `some(...)`/`none` e il Flowchart distingue Presente/Assente.
  `state maybe T` resta rinviato fino all'estensione versionata della
  persistenza. L'attivazione completa è destinata a DDF `0.15`; nel pacchetto
  `0.13.0`, `maybe`, `some` e `none` restano identificatori ordinari. Il disegno
  verificabile è `ddf-optional-values-design.json`.

## DDF-0031 — Alias di dipendenza e qualificazione esplicita nel DDF 0.16

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** ogni dipendenza diretta del futuro manifesto `1.3` dichiara
  un alias obbligatorio e unico nel modulo consumatore. Il sorgente accede agli
  export esclusivamente con `alias::simbolo`; `.` resta riservato a membri e
  valori enum. Le dichiarazioni del modulo corrente restano non qualificate.
- **Alternative rifiutate:** dedurre namespace da file/cartelle o dall'id del
  modulo; continuare a fondere export esterni nello spazio piatto; direttive
  `use` diverse per file; wildcard, alias di singoli simboli, lookup transitivo,
  re-export impliciti e scelta del simbolo basata sull'ordine delle dipendenze.
- **Conseguenze:** dipendenze dirette possono esportare nomi uguali senza
  collisioni. Alias e tabella di binding entrano in fingerprint, unità di
  compilazione, artefatti e source map, mentre la IR bound conserva l'identità
  stabile del simbolo e non un nome CLR. La migrazione dal manifesto `1.2` non
  inventa alias e richiede conferma. L'attivazione completa è destinata a DDF
  `0.16`; nel contratto corrente `::` resta fuori dalla grammatica. Il disegno
  verificabile è `ddf-module-qualification-design.json`.

## DDF-0032 — Iterazione tipizzata e snapshot deterministico nel DDF 0.17

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** la futura verticale usa
  `each(T elemento in raccolta)`, con tipo esplicito, espressione valutata una
  volta e nuovo binding non riassegnabile a ogni iterazione. Gli array seguono
  l'ordine crescente degli indici e producono uno snapshot superficiale prima
  del corpo; `continue` avanza e `brk` termina il ciclo più interno.
- **Alternative rifiutate:** affidarsi a `IEnumerable`/`GetEnumerator` CLR;
  iterazione implicita di stringhe o oggetti host; inferenza obbligatoria del
  tipo; binding scrivibile con write-back nascosto; enumerazione live o non
  ordinata; semantica diversa fra array, VM e Roslyn.
- **Conseguenze:** la prima verticale copre gli array. Sequenze dinamiche e
  viste host richiederanno snapshot ordinati, autorizzati e limitati; le mappe
  useranno una futura vista tipizzata di coppie. La IR conserva il ciclo bound,
  non un enumeratore backend. Debugger e Flowchart espongono l'iterazione senza
  temporanei CLR. L'attivazione completa è destinata a DDF `0.17`; nel
  pacchetto `0.13.0`, `each` e `in` restano identificatori ordinari. Il disegno
  verificabile è `ddf-typed-iteration-design.json`.

## DDF-0033 — Famiglie chiuse di collezioni tipizzate nel DDF 0.18

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** la futura superficie introduce le sole famiglie integrate
  `sequence<T>`, `map<K,V>` e `view<T>`, più `entry<K,V>` prodotto
  dall'iterazione delle mappe. Non sono generici universali. Sequenze e mappe
  sono riferimenti non nulli, ordinati e mutabili; le viste host sono snapshot
  ordinati, limitati e in sola lettura.
- **Alternative rifiutate:** esporre `List<T>`, `Dictionary<K,V>`,
  `IEnumerable` o oggetti CLR; collezioni non tipizzate; mappe senza ordine;
  lookup assente tramite valore predefinito; viste host live; uguaglianza di
  collezione implicita; abilitare subito `fixed` o persistenza `state` profonda.
- **Conseguenze:** `map.find` restituisce `maybe V`; l'iterazione usa snapshot
  superficiali e `entry<K,V>` immutabili. Le chiavi iniziali sono tipi scalari
  stabili ed enum, non `float` o valori composti. IR e SDK descrivono operazioni
  DDF, mai tipi CLR; budget e fallimenti restano atomici. L'attivazione completa
  è destinata a DDF `0.18`; nel pacchetto `0.13.0`, `sequence`, `map`, `view` ed
  `entry` restano identificatori ordinari. Il disegno verificabile è
  `ddf-typed-collections-design.json`.

## DDF-0034 — `long` checked e `decimal` esatto nel DDF 0.19

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** la futura superficie introduce `long`, intero signed a 64 bit
  con aritmetica checked e suffisso `i64`, e `decimal`, decimale base 10 con
  coefficiente a 96 bit, scala `0..28`, arrotondamento tie-to-even e suffisso
  `dec`. I letterali senza suffisso restano rispettivamente `int` e `float`.
- **Alternative rifiutate:** adottare suffissi CLR `L`/`M`; inferire il tipo dal
  backend; usare `double` per importi; promuovere implicitamente fra
  `float`/`decimal` o `long`/`float`; esporre `System.Decimal` o tipi Roslyn
  nella IR; introdurre l'intera famiglia numerica CLR.
- **Conseguenze:** sono implicite soltanto `int -> long`, `int -> decimal` e
  `long -> decimal`, oltre alla preesistente `int -> float`. Le altre
  conversioni sono funzioni standard esplicite e fallibili. VM e Roslyn devono
  coincidere su overflow, scala, arrotondamento, costanti e output. `long` potrà
  essere chiave di mappa; `decimal` no nella prima verticale. La persistenza
  `state` richiede uno schema versionato. L'attivazione completa è destinata a
  DDF `0.19`; nel pacchetto `0.13.0`, `long` e `decimal` restano identificatori
  ordinari. Il disegno verificabile è `ddf-extended-numerics-design.json`.

## DDF-0035 — Stringhe ordinali, Unicode versionato e formato sicuro nel DDF 0.20

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** `+` concatenerà soltanto due `string`, senza conversioni
  implicite. La libreria standard offrirà confronto e ricerca ordinali,
  prefisso/suffisso, sottostringa, sostituzione, trim, conversione di caso,
  split, join e formattazione posizionale di stringhe già convertite. `find`
  restituirà `maybe int`; indici e posizioni conteranno code unit UTF-16.
- **Alternative rifiutate:** concatenazione implicita di qualunque valore;
  confronto o conversione dipendenti dalla cultura di sistema; `-1` come
  sentinella di ricerca; regex come primitiva iniziale; formatter che valuti
  espressioni, reflection o specificatori CLR; operazioni `System.String`
  inserite direttamente nella IR; dati Unicode scelti dal runtime installato.
- **Conseguenze:** il comportamento Unicode iniziale è fissato alla versione
  `15.1`; una variazione richiede una nuova versione della libreria standard.
  `split` e `join` dipendono da `sequence<string>` e `find` da `maybe int`.
  Quote e fallimenti restano atomici e recuperabili. VM e Roslyn devono usare
  le stesse primitive DDF e gli stessi dati Unicode. L'attivazione completa è
  destinata a DDF `0.20` e alla libreria standard `0.20.0`; nel contratto
  corrente `+` resta numerico e le nuove funzioni non sono esportate. Il
  disegno verificabile è `ddf-string-library-design.json`.

## DDF-0036 — Matematica pura e riproducibile nel DDF 0.21

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** la libreria standard `0.21.0` offrirà famiglie tipizzate e non
  sovraccaricate per valore assoluto, min/max, clamp e arrotondamento, oltre a
  radice, potenza floating, esponenziali, logaritmi, trigonometria e costanti.
  Gli angoli saranno in radianti; domini, zeri con segno, tie-to-even e ordine
  di valutazione saranno parte del contratto DDF.
- **Alternative rifiutate:** esporre `System.Math` o overload CLR; usare
  cultura, `libm` o istruzioni hardware come semantica implicita; accettare NaN
  e infinito; usare tolleranze crescenti fra backend; mescolare casualità,
  tempo, statistiche o matrici nel nucleo matematico; consentire subito queste
  chiamate negli inizializzatori `fixed`.
- **Conseguenze:** le trascendenti useranno l'algoritmo versionato
  `ddf.math.transcendental.v1` e vettori di riferimento; VM, Roslyn e futura VM
  dovranno produrre lo stesso pattern binary64 o lo stesso errore. Le famiglie
  relative a `long` e `decimal` dipendono da DDF `0.19`. Errori di dominio,
  risultato, clamp, cifre e provider hanno diagnostiche riservate. La verticale
  completa è destinata a DDF `0.21`; le funzioni restano assenti dalla libreria
  corrente. Il disegno verificabile è `ddf-math-library-design.json`.

## DDF-0037 — Tempo civile separato e nondeterminismo iniettato nel DDF 0.22

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** DDF introdurrà i valori distinti `date`, `time`, `datetime`,
  `instant` e `duration`, con calendario gregoriano prolettico, tick da 100 ns,
  calcolo checked e testo canonico invariant. `datetime` non implica un fuso;
  la conversione in `instant` richiede un offset UTC esplicito. Ora corrente e
  casualità useranno rispettivamente `ddf.clock.v1` e `ddf.random.v1`.
- **Alternative rifiutate:** un unico tipo equivalente a `DateTime`; fuso,
  cultura o timezone del sistema impliciti; tipi CLR nella IR; DST senza dati
  versionati; leap second parziali; `System.Random.Shared`, seed temporale o
  stato globale; bias modulo; sleep e timer prima del modello asincrono.
- **Conseguenze:** timezone IANA/DST richiederanno un modulo con database
  bloccato a versione. Provider clock/random sono espliciti, revocabili,
  limitati e sostituibili con snapshot deterministici. Parsing restituisce
  `maybe T`; persistenza e artefatti registrano tipo, tick e versione, non testo
  locale. La verticale completa è destinata a DDF `0.22` e alla libreria
  standard `0.22.0`; tipi e funzioni restano inattivi nella `0.13`. Il disegno
  verificabile è `ddf-temporal-random-design.json`.

## DDF-0038 — Descrittore semantico unico per ogni funzione standard

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** lo schema futuro `2.0` e l'adapter
  `ddf-standard-library-v4` descriveranno ogni funzione tramite identità stabile,
  firma, documentazione, effetti, capability, errori, determinismo, portabilità,
  costo, regola costante, dipendenze e binding backend-neutral. Tutti i consumer
  useranno lo stesso snapshot immutabile e fingerprintato.
- **Alternative rifiutate:** riconoscere funzioni per nome in parser o UI;
  overload CLR; nomi di assembly/metodi, reflection o C# nei JSON; effetti o
  errori dedotti dal binding; funzioni platform-bound nella libreria standard;
  valutazione costante diretta dal backend; fallback su adapter non
  corrispondenti.
- **Conseguenze:** profili sintattici possono cambiare presentazione, non
  identità semantica. Artifact e worker negoziano schema, adapter, versione,
  operazioni e fingerprint in modalità fail-closed. Lo schema corrente `1.1`
  e `ddf-standard-library-v3` restano attivi finché la migrazione delle cinque
  funzioni esistenti non è completa. Il nuovo contratto è prerequisito della
  libreria stringhe DDF `0.20`. Il disegno verificabile è
  `ddf-standard-library-binding-design.json`.

## DDF-0039 — Dialoghi tipizzati come capability UI sospensiva

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** `dialogMessage(text, title, buttons, icon)` restituirà un
  `dialogResult` usando enum chiusi e la capability `ui.dialog.message`. Il
  provider `ddf.ui.dialog.message.v1` riceverà una richiesta immutabile, verrà
  eseguito dal dispatcher scelto dall'host e sospenderà cooperativamente lo
  script senza spostare istruzioni DDF sul thread UI.
- **Alternative rifiutate:** esporre `MessageBox`, handle o tipi WinForms;
  pulsanti/icone arbitrari; rilevare la UI dal framework installato; concedere
  automaticamente la capability embedded; bloccare il thread UI; code nascoste,
  reentrancy, retry automatici o toolkit grafici nel worker; usare `cancel` per
  mascherare Stop e dispose.
- **Conseguenze:** standalone offre un adapter governato; embedded è
  deny-by-default; headless fallisce senza aprire finestre. Owner, focus,
  policy, quote e accessibilità restano responsabilità dell'host. Debugger e
  ricevute espongono solo l'identità della capability. L'SDK `0.38` aggiungerà
  esclusivamente interfacce opzionali e host legacy continueranno senza il
  servizio. Il disegno verificabile è `ddf-host-dialog-design.json`.

## DDF-0040 — Test di progetto isolati senza nuova sintassi

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** `ddf-test-plan@1.0` registrerà funzioni DDF ordinarie come casi
  applicativi; `ddf.testing@1` fornirà asserzioni tipizzate soltanto in modalità
  test. Fixture dichiarative e fingerprintate inizializzeranno `state`, input,
  clock, random, valori host, capability simulate e budget. Editor e CLI
  consumeranno `ddf-test-report@1.0`.
- **Alternative rifiutate:** introdurre subito una keyword `test`; confondere
  test del pacchetto linguaggio e del progetto; includere sorgenti test negli
  artefatti; setup/teardown arbitrari prima del contratto risorse; effetti reali
  impliciti; dipendenza dall'ordine; engine fault atteso come successo; messaggi
  localizzati, stack CLR, timestamp o durata come oracoli.
- **Conseguenze:** ogni caso usa stato nuovo e capability non dichiarate sono
  negate; gli effetti simulati vengono sempre annullati. VM e Roslyn devono
  concordare o il caso diventa errore infrastrutturale. Il test adapter corrente
  `ddf-tests-v1` resta separato e continua a convalidare soltanto i pacchetti
  linguaggio. L'SDK opzionale previsto è `0.39`. Il disegno verificabile è
  `ddf-project-testing-design.json`.

## DDF-0041 — Ownership lessicale delle risorse tramite `use`

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** DDF `0.23` introdurrà la forma
  `use tipo nome << acquisizione { ... }`. I tipi risorsa saranno nominali e
  opachi; il binding sarà immutabile, non copiabile e confinato allo scope. Ogni
  acquisizione riuscita avrà un solo tentativo di cleanup e gli scope annidati
  verranno chiusi in ordine LIFO su qualunque uscita cooperativa.
- **Alternative rifiutate:** un `defer` generico; esposizione di `IDisposable`,
  `IAsyncDisposable`, handle nativi o oggetti CLR; cleanup soltanto a fine
  esecuzione; rilascio manuale; garbage collection come garanzia; abort del
  thread; concessione di capability derivata dal possesso dell'handle.
- **Conseguenze:** il registro di risorse capability corrente resta fallback di
  sicurezza, mentre gli scope `use` consentiranno rilascio anticipato senza
  doppio dispose. Errori di cleanup hanno precedenza e soppressione stabili,
  `state` viene confermato solo dopo cleanup riuscito e VM/Roslyn devono
  concordare sull'ordine osservabile. L'SDK opzionale previsto è `0.40`; `use`
  resta un identificatore ordinario in DDF `0.13`. Il disegno verificabile è
  `ddf-resource-lifetime-design.json`.

## DDF-0042 — Sospensione cooperativa senza `async`/`await`

- **Stato:** Accettata per implementazione futura
- **Data:** 2026-10-14
- **Decisione:** una normale chiamata DDF potrà essere dichiarata sospensiva dal
  descrittore semantico. Il motore conserverà una continuazione correlata alla
  generazione dell'esecuzione, rilascerà il thread e riprenderà con un risultato
  tipizzato. Ogni istanza ammetterà una sola esecuzione e una sola sospensione
  pendente, restando proprietaria di `state`, transazioni e risorse fino al
  completamento o cleanup.
- **Alternative rifiutate:** keyword `async`/`await`; tipi task/future nel
  linguaggio; thread condivisi o callback rientranti; concorrenza implicita su
  `state`; attesa bloccante sul thread UI; abort di thread .NET; continuazioni
  contenenti tipi CLR; timeout sempre intercettabili; accettazione di risposte
  tardive o duplicate.
- **Conseguenze:** Stop e dispose sono cancellazioni terminali, mentre solo i
  timeout dichiarati recuperabili diventano errori DDF. Cleanup DDF-0041 usa un
  contesto indipendente e limitato. Debugger e Flowchart conservano la chiamata
  sorgente e mostrano `awaiting-capability` senza frame provider. VM, Roslyn e
  worker condividono il protocollo versionato e gli stessi esiti. L'SDK
  opzionale previsto è `0.41`; `async` e `await` restano identificatori ordinari.
  Il disegno verificabile è `ddf-cooperative-async-design.json`.
