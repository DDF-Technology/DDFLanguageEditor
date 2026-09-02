# Formato dei pacchetti di linguaggio `.ddflang`

La versione 1.0 del contratto separa la definizione di un linguaggio dai motori
dell'editor. Un pacchetto può essere una cartella oppure un archivio ZIP con
estensione `.ddflang`; in entrambi i casi tutti i file JSON risiedono nella
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
[`language-package.schema.json`](language-package.schema.json). Il pacchetto DDF
di riferimento è in [`language-packages/ddf`](language-packages/ddf).

## Stato dell'adattamento

Nella `0.9.6.0` il lessico DDF viene materialmente costruito da `lexicon.json` e
continua ad alimentare lexer, parser, classificazione, completamento e formatter.
Le altre sezioni dichiarano adapter interni versionati che mantengono invariato
il comportamento dei motori DDF correnti. Le successive versioni `0.9.6.x`
sostituiranno questi adapter, una sezione alla volta, con regole dichiarative.

Un pacchetto dati non carica assembly, script o nomi di tipi .NET. Il campo
`adapter` deve corrispondere a un identificatore già supportato dall'applicazione:
il JSON non può quindi autorizzare l'esecuzione di codice host arbitrario.

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
