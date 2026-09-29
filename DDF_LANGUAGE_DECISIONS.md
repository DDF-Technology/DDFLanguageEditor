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
