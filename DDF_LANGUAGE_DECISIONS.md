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
