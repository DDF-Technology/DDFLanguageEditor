# Formato dei pacchetti di linguaggio `.ddflang`

La versione 1.0 del contratto separa la definizione di un linguaggio dai motori
dell'editor. Un pacchetto può essere una cartella oppure un archivio ZIP con
estensione `.ddflang`; in entrambi i casi i file JSON e l'eventuale EBNF risiedono nella
radice del contenitore.

## Struttura

`manifest.json` identifica pacchetto e linguaggio, dichiara la versione minima
dell'API del motore e riferisce queste nove sezioni obbligatorie:

- `lexicon`: keyword, letterali booleani, operatori, precedenza e punteggiatura;
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
lessico e la grammatica. Il pacchetto DDF di riferimento è in
[`language-packages/ddf`](language-packages/ddf).

## Stato dell'adattamento

Dalla `0.9.6.1` `lexicon.json` definisce anche spazi, classi degli identificatori,
cifre e separatore decimale, stringhe, escape, commenti, direttive, multilinea,
diagnostiche per token incompleti e longest match. La stessa definizione alimenta
lexer incrementale, classificazione, prefissi del completamento e validazione dei
nomi.

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

Le altre sezioni dichiarano adapter interni versionati che mantengono
invariato il comportamento dei motori DDF correnti. Le successive versioni
`0.9.6.x` sostituiranno questi adapter, una sezione alla volta, con regole
dichiarative.

Un pacchetto dati non carica assembly, script o nomi di tipi .NET. Il campo
`adapter` deve corrispondere a un identificatore già supportato dall'applicazione:
il JSON non può quindi autorizzare l'esecuzione di codice host arbitrario.

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
modificare soltanto la copia di `language-packages/ddf/lexicon.json`. Dopo il
riavvio, `Help > Pacchetto linguaggio attivo` deve confermare il caricamento
esterno. Per esempio, aggiungendo `$` a `identifier.start.characters` e
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
`Help > Pacchetto linguaggio attivo`, senza dipendere dalla larghezza della barra
di stato o dalla comparsa del tooltip. La finestra distingue esplicitamente un
caricamento esterno riuscito dal fallback incorporato e consente di copiare tutti
i dettagli.

Ogni diagnostica del loader contiene:

- un codice stabile `DDFLxxx`;
- gravità e messaggio;
- nome del file;
- percorso JSON, per esempio `$.keywords` o `$.sections.grammar`.

File e percorso JSON rendono il problema direttamente localizzabile senza
attivare una configurazione parziale.

## Versionamento

- `schemaVersion` cambia quando cambia la forma dei documenti JSON;
- `engineApi` dichiara il contratto richiesto all'applicazione;
- `packageVersion` identifica la revisione del pacchetto;
- `languageVersion` identifica la revisione del linguaggio esposto all'utente.

La `0.9.6.0` accetta `schemaVersion` ed `engineApi` `1.0`. Versioni incompatibili
vengono rifiutate prima di creare il catalogo attivo.

## Creazione di un archivio

Per distribuire un pacchetto, comprimere direttamente il contenuto della sua
cartella (non la cartella contenitore) in un normale ZIP e rinominare il file con
estensione `.ddflang`. `manifest.json` deve quindi trovarsi alla radice
dell'archivio.

La selezione e installazione di pacchetti utente arriveranno nella `0.9.6.7`.
Modificare oggi la copia DDF accanto all'eseguibile è utile per validare il
contratto, ma soltanto le proprietà già gestite dagli adapter correnti producono
un comportamento diverso.
