# Formato dei pacchetti di linguaggio `.ddflang`

La versione 1.0 del contratto separa la definizione di un linguaggio dai motori
dell'editor. Un pacchetto può essere una cartella oppure un archivio ZIP con
estensione `.ddflang`; in entrambi i casi i file JSON e l'eventuale EBNF risiedono nella
radice del contenitore.

## Struttura

`manifest.json` identifica pacchetto e linguaggio, dichiara la versione minima
dell'API del motore, gli eventuali requisiti nativi e riferisce queste nove
sezioni obbligatorie:

- `lexicon`: keyword, letterali booleani con valore dichiarato, operatori, precedenza e punteggiatura;
- `grammar`: grammatica e forma dell'albero sintattico;
- `types`: tipi e regole di compatibilità;
- `semantics`: risoluzione, scope e controlli semantici;
- `formatting`: regole di formattazione e indentazione;
- `intellisense`: completamento, hover, firme, simboli e snippet;
- `runtime`: motore di esecuzione e operazioni ammesse;
- `build`: profilo di convalida, Run e build;
- `standardLibrary`: firme e collegamenti della libreria standard.

Lo schema pubblico del manifest è in
[`language-package.schema.json`](language-package.schema.json), mentre
[`language-lexicon.schema.json`](language-lexicon.schema.json) e
[`language-grammar.schema.json`](language-grammar.schema.json) descrivono il
lessico e la grammatica. [`language-intellisense.schema.json`](language-intellisense.schema.json)
descrive completamento, presentazione dei simboli, Signature Help e snippet.
[`language-formatting.schema.json`](language-formatting.schema.json) descrive le
regole di layout, indentazione, coppie automatiche e Quick Fix lessicali.
[`language-types.schema.json`](language-types.schema.json) descrive tipi primitivi,
conversioni, compatibilità, mutabilità, scope, firme, tipi dei literal e regole
di indicizzazione.
[`language-semantics.schema.json`](language-semantics.schema.json) descrive il
collegamento fra produzioni grammaticali e operazioni semantiche sicure.
[`language-runtime.schema.json`](language-runtime.schema.json) e
[`language-standard-library.schema.json`](language-standard-library.schema.json)
descrivono VM, mapping delle operazioni e funzioni host controllate.
Il pacchetto DDF di riferimento è in
[`language-packages/ddf`](language-packages/ddf).

## Stato dell'adattamento

Dalla `0.9.6.1` `lexicon.json` definisce anche spazi, classi degli identificatori,
cifre e separatore decimale, stringhe, escape, commenti, direttive, multilinea,
diagnostiche per token incompleti e longest match. La stessa definizione alimenta
lexer incrementale, classificazione, prefissi del completamento e validazione dei
nomi.

Dalla `0.12.1`, `booleanLiteralValues` associa ogni grafia non canonica al
valore `true` o `false`. La mappa è obbligatoria per booleani localizzati; i
soli nomi storici `true` e `false` conservano il fallback di compatibilità. Il
valore dichiarato attraversa parser e IR senza dipendere dal testo concreto.

La `0.9.6.2` usa `grammar.ebnf` come unica rappresentazione canonica delle
produzioni e `grammar.json` soltanto per start rule, strategia delle alternative
e metadati di nodi/campi. Il loader compila la grammatica prima dell'attivazione,
rifiuta riferimenti mancanti e ricorsione sinistra, e produce un albero generico
con nodi mancanti e intervalli sorgente esatti. Il precedente AST DDF resta una
proiezione temporanea di compatibilità per formatter, semantica e runtime e sarà
sostituito dai servizi dichiarativi della `0.9.6.3`.

Dalla `0.9.6.3`, ogni nodo può dichiarare anche la sezione opzionale `ide` in
`grammar.json`. I metadati `symbolKind`/`nameField`, `foldingKind`/`foldingField`,
`breadcrumbKind`/`breadcrumbLabel` e `selectable` alimentano rispettivamente
Outline e navigazione, folding, breadcrumb e selezione sintattica. Se il parsing
generico di un documento incompleto non è disponibile, l'editor mantiene
temporaneamente il servizio legacy come fallback durante la digitazione.

La sezione `intellisense.json` contiene inoltre le descrizioni e le origini
mostrate dal completamento, le etichette e la documentazione predefinita dei
simboli, i testi di origine della Signature Help e l'intero catalogo snippet.
I tipi e le firme delle dichiarazioni locali sono estratti dai campi
`typeField`, `returnTypeField` e `parametersField` dichiarati sui nodi della
grammatica; `childSymbolKind` consente, per esempio, di presentare come campi le
variabili annidate in una struttura.

Dalla `0.12.1`, la stessa sezione accetta due blocchi di presentazione opzionali:

- `diagnostics` associa un codice stabile (`DDF201`, `DDF306`, ...) al messaggio
  mostrato da palette e hover. Il motore conserva separatamente il testo tecnico,
  così Quick Fix e integrazioni non dipendono dalla lingua dell'interfaccia;
- `flowchart` definisce etichette dei rami vero/falso, formato dell'uscita,
  titoli e prompt della modalità didattica e una spiegazione per ogni tipo di nodo.
  I placeholder ammessi sono `{function}`, `{code}`, `{true}` e `{false}`.

Entrambi i blocchi hanno fallback completo alla presentazione DDF incorporata:
un pacchetto storico privo dei nuovi campi resta caricabile e un codice
diagnostico non mappato conserva il messaggio prodotto dal motore.

La sezione `formatting.json` assegna ruoli ai delimitatori già riconosciuti dal
lessico, configura le coppie automatiche e i trigger di indentazione, decide se
separare le istruzioni con una riga vuota e associa correzioni sicure ai codici
diagnostici lessicali. Le Quick Fix per token sintattici mancanti continuano a
usare direttamente il token atteso dal parser e quindi non replicano le
produzioni EBNF.

Dalla `0.9.6.4`, `types.json` non è più una sezione segnaposto. Ogni primitivo
dichiara nome, categoria, possibilità di essere usato per una variabile e,
quando previsto, tipo dell'elemento indicizzato. Le conversioni implicite sono
direzionali e possono valere in generale oppure soltanto per un literal di un
carattere usato come inizializzatore. La stessa sezione dichiara in modo
esplicito compatibilità degli array e dei tipi utente, destinazioni assegnabili,
mutabilità di variabili, parametri e campi, confini degli scope e vincoli delle
firme. I riferimenti devono puntare a primitivi definiti e le categorie dei tipi
usati per literal, `void` e calcoli numerici devono essere coerenti.

Il type checker riceve lo snapshot del pacchetto usato da lexer e parser, così
un cambio di conversione, tipo booleano, tipo indice, arità o destinazione
assegnabile entra in vigore senza ricompilare l'editor. Le politiche di scope e
mutabilità sono già parte del contratto validato; il loro collegamento completo
all'esecuzione avverrà durante la costruzione della rappresentazione intermedia
nei passi successivi. Un pacchetto incoerente viene rifiutato atomicamente
con diagnostica `DDFL09x` riferita a `types.json`.

La sezione `semantics.json` seleziona un insieme di operazioni e una sua versione,
quindi associa ogni regola che produce un nodo a una o più operazioni. La mappa
`arguments` traduce i nomi stabili richiesti dall'operazione nei nomi dei campi
dichiarati in `grammar.json`: per esempio, `DeclareFunction` riceve `name`,
`parameters`, `returnType` e `body`, indipendentemente da come un altro linguaggio
decida di chiamare quei campi nella propria grammatica.

L'insieme `ddf-safe-semantics` versione `1.0` è un catalogo chiuso e interrogabile
di operazioni dati. Il loader verifica versione, identificatori, argomenti
obbligatori e facoltativi, regole e riferimenti ai campi prima di attivare il
pacchetto. Il planner attraversa poi l'AST generico e produce operazioni con
intervalli sorgente e nodi-argomento, senza eseguirle ancora. Il JSON non può
fornire delegate, reflection, script, assembly o nomi di metodi host; un'operazione
non presente nel catalogo viene rifiutata con una diagnostica `DDFL10x`. Questo
piano sintattico-semantico costituisce il confine sicuro usato dal lowerer della
rappresentazione intermedia `1.0`; il ponte dall'AST DDF legacy verrà eliminato
gradualmente mentre il runtime generico acquisisce tutte le operazioni necessarie.

`runtime.json` usa l'instruction set chiuso `ddf-ir-vm-1.0`. Dichiara entry point,
limite istruzioni, destinazione output, operazioni VM richieste e la traduzione
dei token unari, binari e postfix in ID stabili. Il lowerer risolve questa mappa;
la VM non confronta mai la grafia concreta degli operatori. Operazioni mancanti,
duplicate o non supportate causano il rifiuto atomico `DDFL12x`.

`standard-library.json` schema `1.1` assegna identità e versione alla libreria e
dichiara nomi, tipi dei parametri, tipo restituito, documentazione, binding,
effetto (`pure`, `input`, `output`) ed errori di ogni funzione standard. I binding ammessi sono un
catalogo host chiuso (`host.output`, `host.input`, conversioni e lunghezza
incorporate): non sono nomi di metodi e non attivano reflection. Tipi o binding
non disponibili o metadati incoerenti producono diagnostiche `DDFL13x`. Il
contratto minimo è descritto in `DDF_STANDARD_LIBRARY_SPEC.md`; gli schemi
completi sono `language-runtime.schema.json` e
`language-standard-library.schema.json`.

`build.json` usa l'adapter `ddf-build-v2` e dichiara backend consentiti e formato
dell'artefatto. Il backend iniziale `ddf-vm-ir-v1` implementa il contratto
`IDdfBuildBackend`: riceve sorgenti e IR senza dipendere dalla sintassi concreta.
La configurazione del singolo programma non appartiene al pacchetto, ma al
manifest separato `ddf-project.json`, descritto in [`PROJECT_BUILD.md`](PROJECT_BUILD.md).
Un pacchetto può selezionare soltanto backend già supportati dal motore; non può
fornire codice eseguibile attraverso il JSON. Lo schema è
`language-build.schema.json`.

Un pacchetto dati non carica assembly, script o nomi di tipi .NET. Il campo
`adapter` deve corrispondere a un identificatore già supportato dall'applicazione:
il JSON non può quindi autorizzare l'esecuzione di codice host arbitrario.

## Estensioni native opzionali

La proprietà opzionale `nativeExtensions` del manifest dichiara soltanto
requisiti inerti. Ogni elemento deve specificare `id`, `publisher`, `version`,
un'impronta `sha256` di 64 cifre esadecimali e almeno una voce in `capabilities`.
Identificatori, capacità e riferimenti duplicati o non validi causano il rifiuto
atomico con diagnostiche `DDFL110`–`DDFL114`.

La presenza di un requisito non carica alcun binario. L'host deve fornire un
grant separato che coincida esattamente per ID, autore, versione e impronta e che
autorizzi individualmente tutte le capacità richieste. Il pacchetto non contiene
grant e non può autorizzare se stesso. Il contratto di sicurezza e lo stato
dell'implementazione sono descritti in
[`NATIVE_EXTENSIONS.md`](NATIVE_EXTENSIONS.md).

## Regole lessicali 1.0

L'oggetto `lexical` contiene cinque elementi:

- `whitespace`: classe dei caratteri ignorati tra i token;
- `identifier.start` e `identifier.part`: classi valide all'inizio e nel resto di un nome;
- `number`: classe delle cifre, separatore decimale e obbligatorietà delle cifre frazionarie;
- `delimitedTokens`: commenti, stringhe e direttive con marcatori, escape e diagnostica di terminazione;
- `longestMatch`: se `true`, prova prima marcatori e operatori più lunghi; se `false`, conserva l'ordine dichiarato e rifiuta quelli resi irraggiungibili da una regola precedente.

Una classe di caratteri combina i flag `unicodeLetters`, `unicodeDigits` e
`unicodeWhitespace` con la stringa `characters` di caratteri espliciti. Non
vengono eseguite espressioni regolari fornite dal pacchetto.

Ogni elemento di `delimitedTokens` dichiara `tokenKind` e `start`. I commenti di
riga usano `endsAtLineBreak`; commenti a blocchi, stringhe e direttive richiedono
invece `end`, `unterminatedCode` e `unterminatedMessage`. `allowLineBreak` decide
se il token può continuare sulla riga seguente e `escapeCharacter` protegge il
carattere immediatamente successivo.

Il caricatore rifiuta atomicamente classi sovrapposte incompatibili, keyword non
riconoscibili come identificatori, categorie duplicate, marcatori duplicati e
regole od operatori irraggiungibili. I dettagli completi e i valori enumerati
sono in `language-lexicon.schema.json`.

Per una prova manuale non distruttiva, duplicare l'intera cartella pubblicata e
modificare soltanto la copia di `language-packages/ddf/lexicon.json`. Installare
quindi la copia da `Linguaggio > Gestisci pacchetti > Installa/Aggiorna cartella`:
non è necessario riavviare l'editor. Per esempio, aggiungendo `$` a `identifier.start.characters` e
`identifier.part.characters`, un nome come `$value` viene tokenizzato come
identificatore; cambiando il marcatore di `LineComment`, cambia immediatamente la
colorazione dei commenti. Keyword e costrutti sintattici devono restare coerenti
con `grammar.ebnf`. I ruoli delle keyword sono identificatori estensibili e non
richiedono più una modifica dell'enum interno; perché un nuovo costrutto influenzi
tutti i servizi IDE serviranno anche i metadati previsti dalla `0.9.6.3`.

## Caricamento atomico e fallback

All'avvio l'applicazione cerca `language-packages/ddf` accanto all'eseguibile.
Prima dell'attivazione legge e convalida l'intero pacchetto: manifest, versioni,
sezioni, adapter, dipendenze, riferimenti sicuri, duplicati e valori lessicali.
Solo un pacchetto completamente valido diventa attivo. Se quello esterno manca o
non è valido, viene usata la copia DDF incorporata nell'assembly Core.

La barra di stato mostra nome e versione del linguaggio. Il relativo tooltip
espone ID, versione del pacchetto, origine (`cartella`, `archivio` o
`incorporato`), sorgente e le eventuali diagnostiche che hanno causato il
fallback.

Le stesse informazioni sono sempre consultabili da
`Help > Linguaggio > Informazioni sul pacchetto attivo`, senza dipendere dalla larghezza della barra
di stato o dalla comparsa del tooltip. La finestra distingue esplicitamente un
caricamento esterno riuscito dal fallback incorporato e consente di copiare tutti
i dettagli.

Il menu `Linguaggio` e il pulsante Linguaggio della barra strumenti aprono il
Gestore pacchetti oppure ripristinano il DDF incorporato. Cartelle e archivi
`.ddflang` diversi dal DDF devono essere installati nel catalogo utente prima
dell'attivazione. L'attivazione riuscita viene memorizzata tra gli avvii, rigenera i
servizi configurabili dell'editor e reindicizza l'eventuale workspace usando le
estensioni dichiarate nel manifest. Un errore lascia invece invariato il
pacchetto già attivo e viene esposto nel messaggio e nel tooltip di stato.
Ogni attivazione valida incrementa inoltre una revisione interna: risultati di
analisi, completamento o indicizzazione avviati con una revisione precedente
vengono ignorati anche se terminano dopo il cambio di linguaggio.

Ogni diagnostica del loader contiene:

- un codice stabile `DDFLxxx`;
- gravità e messaggio;
- un dominio: `Definizione linguaggio` per dati incoerenti oppure
  `Funzionalità motore` per versioni, adapter e operazioni che il motore corrente
  non supporta;
- nome del file;
- percorso JSON, per esempio `$.keywords` o `$.sections.grammar`.

File e percorso JSON rendono il problema direttamente localizzabile senza
attivare una configurazione parziale. La stessa distinzione appare nella finestra
del pacchetto, nei messaggi di attivazione e nel tooltip della barra di stato.

## Versionamento

Per la direzione futura dei pacchetti come profili sintattici della piattaforma,
la distinzione tra SDK host e plug-in e il backend .NET + Roslyn, vedere
[ECOSYSTEM_ARCHITECTURE.md](ECOSYSTEM_ARCHITECTURE.md). I contratti descritti qui
restano quelli attualmente implementati; le nuove capacità seguiranno la roadmap.

- `schemaVersion` cambia quando cambia la forma dei documenti JSON;
- `engineApi` dichiara il contratto richiesto all'applicazione;
- `packageVersion` identifica la revisione del pacchetto;
- `languageVersion` identifica la revisione del linguaggio esposto all'utente.
  Il pacchetto DDF corrente usa `0.12`; le stringhe possono inoltre dichiarare
  `allowedEscapeCharacters` accanto a `escapeCharacter` per rifiutare escape non
  previsti invece di accettarli silenziosamente.

La `0.9.6.0` accetta `schemaVersion` ed `engineApi` `1.0`. Versioni incompatibili
vengono rifiutate prima di creare il catalogo attivo.

La policy normativa che stabilisce incrementi, compatibilità fra profili,
deprecazioni e requisiti delle migrazioni è `DDF_LANGUAGE_VERSIONING.md`, con
contratto macchina in `ddf-language-version-policy.json`. In particolare, una
patch del pacchetto non può cambiare il significato osservabile della stessa
`languageVersion`.

## Creazione di un archivio

Per distribuire un pacchetto, comprimere direttamente il contenuto della sua
cartella (non la cartella contenitore) in un normale ZIP e rinominare il file con
estensione `.ddflang`. `manifest.json` deve quindi trovarsi alla radice
dell'archivio.

## Test dichiarativi e gestione dei pacchetti

`tests.json` contiene casi eseguiti dal loader prima di ogni attivazione. Un caso
può dichiarare sorgente, sequenza esatta dei token, codici diagnostici attesi,
testo formattato e valore restituito dal runtime. Se una sola aspettativa fallisce,
il pacchetto viene rifiutato atomicamente con diagnostica `DDFT001` e il linguaggio
attivo resta invariato.

`Help > Linguaggio > Gestisci pacchetti` installa o aggiorna in modo transazionale
una cartella o un archivio `.ddflang` nel profilo locale, elenca le versioni valide,
le convalida, le attiva oppure disattiva la selezione esterna tornando alla copia
DDF incorporata. `Playground linguaggio` prova invece token, albero sintattico,
diagnostiche e formatter senza modificare il documento aperto.

Il DDF è l'unico pacchetto collocato accanto all'eseguibile ed è anche incorporato
come fallback. Gli eventuali profili dimostrativi distribuiti con l'applicazione
sono sorgenti di installazione: al primo avvio vengono copiati nel catalogo locale
e successivamente si comportano come normali pacchetti utente. Una preferenza di
versioni precedenti che puntava direttamente a uno di questi profili viene migrata
alla corrispondente copia installata.

La distinzione tra modifiche dichiarative, nuove operazioni del motore e plug-in
nativi, insieme al progetto del futuro editor assistito, è descritta in
`LANGUAGE_AUTHORING.md`.
