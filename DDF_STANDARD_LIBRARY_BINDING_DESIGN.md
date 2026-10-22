# Contratto dei binding della libreria standard DDF — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce lo
schema futuro `2.0` dei descrittori della libreria standard e l'adapter
`ddf-standard-library-v4`. Non modifica `ddf.standard@0.12.3`, che continua a
usare schema `1.1` e adapter `ddf-standard-library-v3`. Il contratto
machine-readable è `ddf-standard-library-binding-design.json`.

## Obiettivo

Ogni funzione standard deve possedere una sola descrizione normativa, caricata
atomicamente in uno snapshot immutabile e usata da:

- risoluzione dei simboli e type checker;
- completamento, Signature Help, hover e documentazione;
- lowering nella IR e valutazione costante, quando autorizzata;
- VM, backend Roslyn e futura VM bytecode;
- debugger, Watch, Flowchart, Playground e Package Studio;
- fingerprint, artifact contract e verifica del worker.

Parser, formatter e interfaccia non possono riconoscere una funzione tramite il
nome scritto nel codice o uno `switch` locale. I profili sintattici possono
presentare un alias soltanto collegandolo alla stessa identità semantica.

## Identità e versione

L'identità stabile ha forma `ddf.standard/<dominio>/<nome>@<major>`, per esempio
`ddf.standard/string/contains@1`. Il nome sorgente è unico nella versione della
libreria: lo schema `2.0` non introduce overload. Una modifica compatibile
incrementa la versione della libreria; firma, semantica, effetti, errori o
binding incompatibili richiedono una nuova identità major.

Il descrittore dichiara `since`, eventuale deprecazione e sostituzione. La
rimozione non è implicita: richiede la policy di compatibilità della libreria e
una diagnostica di migrazione.

## Metadati obbligatori

Ogni funzione dichiara:

- `functionId`, `sourceName`, parametri nominati e tipo restituito;
- documentazione di riepilogo, parametri, risultato ed errori;
- effetti ordinati e capability eventualmente necessarie;
- errori recuperabili tipizzati e fault del motore ammessi;
- determinismo e classe di portabilità;
- costo base e dimensioni di quota dipendenti da input/output;
- regola di valutazione costante;
- operazione IR, adapter/versione e contratto backend;
- dipendenze minime da linguaggio, libreria e tipi;
- versione di introduzione, deprecazione e sostituzione.

Campi ignoti, duplicati, riferimenti assenti o combinazioni incoerenti fanno
fallire l'intero caricamento. Testo localizzato e icone sono presentazione: non
entrano nella semantica, ma puntano alla stessa identità.

## Effetti e capacità

Una lista effetti vuota significa `pure`. Gli effetti integrati iniziali sono
`input`, `output`, `clock` e `random`; altri effetti usano una capability con ID
e versione esatti. Una funzione pura non può dichiarare capability, mutazione,
provider o errori da revoca. Una funzione con capability non acquisisce mai
permessi per il solo fatto di essere presente nel catalogo.

L'ordine degli effetti, i limiti e il comportamento di rollback fanno parte del
contratto. Analisi e UI mostrano gli effetti dal descrittore; Run, Debug e Build
consumano lo stesso snapshot e non ricostruiscono regole dal nome del binding.

## Errori e fault

`recoverableErrors` contiene identità di valori `error` che il programma potrà
gestire con la verticale DDF `0.14`. `engineFaults` elenca soltanto condizioni
che interrompono l'esecuzione, come artefatto corrotto o contratto backend
violato. Un codice diagnostico non sostituisce l'identità dell'errore.

L'implementazione non può produrre un errore recuperabile non dichiarato. Un
backend che incontra un risultato estraneo al contratto produce un fault del
motore e non inventa una conversione o un fallback.

## Binding backend-neutral

Il binding contiene:

- `operationId`, identità DDF stabile dell'operazione;
- `irOperation`, opcode o chiamata intrinseca backend-neutral;
- `adapterId` e `adapterVersion`;
- `backendContract`, identità verificabile condivisa dai backend.

Sono vietati nomi di assembly, tipi o metodi CLR, frammenti C#, reflection,
delegate serializzati e percorsi nativi. Il compilatore inserisce nell'artefatto
solo le identità e versioni richieste; il worker accetta esclusivamente adapter
registrati che coincidono con fingerprint e policy.

## Determinismo, portabilità e costo

Le classi di determinismo sono `deterministic` e `provider-deterministic`.
Quest'ultima richiede un provider esplicito e registrato nel piano di
esecuzione. Le classi di portabilità ammesse nella libreria standard sono
`portable` e `provider-bound`; una funzione `platform-bound` appartiene a un
adapter o modulo esterno.

Ogni chiamata consuma un costo base e può dichiarare dimensioni quali code unit
lette/prodotte, elementi, allocazioni o chiamate provider. Quote e overflow del
costo falliscono prima di pubblicare effetti o risultati parziali.

## Valutazione costante

Il valore predefinito è `forbidden`. `eligible-shared-evaluator` è ammesso solo
per funzioni pure, deterministiche, portabili e implementate dal medesimo
valutatore versionato usato da VM e compilatore. Il flag non autorizza il parser
o Roslyn a eseguire direttamente codice host durante la compilazione.

## Fingerprint e compatibilità

Firma, effetti, errori, determinismo, portabilità, costo, binding, dipendenze e
regola costante entrano nel fingerprint canonico. Ordine JSON e documentazione
puramente editoriale non lo cambiano. Artifact e worker verificano schema,
adapter, versione della libreria, fingerprint e insieme esatto delle operazioni
prima dell'esecuzione.

## Diagnostiche loader riservate

| Codice | Significato futuro |
| --- | --- |
| `DDFL135` | identità o versione funzione non valida/duplicata |
| `DDFL136` | firma, dipendenza o documentazione incoerente |
| `DDFL137` | effetti, capability o errori incoerenti |
| `DDFL138` | binding, adapter o operazione IR sconosciuti |
| `DDFL139` | determinismo, portabilità, costo o costante incompatibili |
| `DDFL140` | fingerprint o contratto artifact/worker non corrispondente |

## Ordine di implementazione

I futuri descrittori risorsa `ddf-resource-descriptor@1.0` di `DDF-0041`
riuseranno identità, effetti, capability, errori, costo e binding dello schema
`2.0`, aggiungendo ownership, operazioni di prestito e cleanup. Una normale
funzione non potrà dichiararsi acquisizione per convenzione nominale: il ruolo
deve essere validato e incluso nel fingerprint.

Le funzioni che potranno sospendere useranno inoltre il contratto
`ddf-cooperative-suspension@1.0` di `DDF-0042`. L'effetto, i timeout
recuperabili, le quote e il provider richiesto saranno dati del descrittore
`2.0`, non convenzioni dedotte dal nome o dal binding. La sintassi della chiamata
resterà ordinaria e il fingerprint impedirà di sostituire silenziosamente una
funzione sincrona con una sospensiva o viceversa.

1. schema `2.0`, modello immutabile e validatore atomico;
2. registry chiuso di effetti, errori, operazioni e adapter;
3. fingerprint canonico e migrazione deterministica da schema `1.1`;
4. consumo comune in analisi, strumenti, IR, VM e Roslyn;
5. artifact/worker negotiation e rifiuto fail-closed;
6. conversione delle cinque funzioni correnti senza cambiarne il comportamento;
7. gate differenziale e rimozione dei casi speciali residui.

Lo schema `2.0` deve essere operativo prima dell'attivazione della libreria
stringhe DDF `0.20`. Fino alla verticale completa, loader e pacchetto canonico
continuano ad accettare soltanto il contratto corrente `1.1`/`v3`.
