# Specifica del sistema di moduli DDF 0.12.3

Stato: normativa per il modello interno al progetto e per la risoluzione locale
delle dipendenze sorgente.

## 1. Modulo e progetto

Un modulo DDF è l'unità descritta da un solo `ddf-project.json` validato e
dall'insieme ordinato dei sorgenti selezionati dal campo `sources`. Non coincide
con un file, una cartella, una scheda dell'editor o un workspace grafico.

Il manifesto schema `1.1` assegna al modulo:

- un `id` stabile, minuscolo e indipendente dal percorso;
- una `version` esatta;
- l'elenco esplicito `exports` della sua API pubblica.

I progetti impliciti ricevono un'identità locale `local.<nome>` e versione
`0.0.0`. Tale identità serve alla riproducibilità, non promette pubblicazione.

## 2. Spazio dei nomi interno

Tutti i sorgenti del modulo condividono un unico spazio dei nomi, case-sensitive.
Il percorso del file non crea namespace e non modifica la visibilità. Funzioni,
strutture e globali top-level sono visibili da ogni altro sorgente dello stesso
modulo; una dichiarazione runtime duplicata è un errore `DDFB010`.

Questa regola conserva il comportamento multi-file della `0.11.0` e impedisce
che un riordino delle cartelle cambi la semantica. Namespace annidati non fanno
parte del nucleo `0.12.3`; potranno essere aggiunti solo con una nuova decisione
di linguaggio.

Il runtime qualifica internamente funzioni, strutture e globali con la coppia
`module.id@module.version`. Moduli diversi possono quindi usare gli stessi nomi
privati senza collisioni; questa qualificazione non introduce una nuova sintassi
nel sorgente e non espone automaticamente i simboli al consumatore.

## 3. Esportazioni

`module.exports` contiene nomi top-level univoci. Ogni nome deve corrispondere a
una funzione, struttura o globale del modulo, altrimenti il build produce
`DDFB012`. Un elenco vuoto significa che il modulo non pubblica API.

Le esportazioni non cambiano la visibilità interna. Sono il solo insieme
risolvibile dai moduli che dichiarano una dipendenza diretta; gli export non
vengono re-esportati transitivamente. Un riferimento a una funzione o a un
valore non visibile produce rispettivamente `DDF312` o `DDF313`. Gli export non
abilitano reflection, accesso host o API .NET. La funzione di ingresso può
essere esportata, ma non è obbligatorio farlo.

## 4. Ingresso e inizializzazione

Un programma eseguibile possiede una sola funzione con il nome indicato da
`entryPoint`; definizioni multiple producono `DDFB009`. `entrySource`, quando
presente, deve indicare il sorgente che la contiene.

Prima dell'ingresso sono inizializzate tutte le globali, seguendo:

1. l'ordine deterministico dei sorgenti normalizzati selezionati dal build;
2. l'ordine delle dichiarazioni in ciascun sorgente.

Una globale può leggere soltanto globali già inizializzate. Riferimenti in
avanti e cicli producono `DDFB011`; un errore di inizializzazione impedisce
l'avvio dell'ingresso.

## 5. Dipendenze

`dependencies` identifica moduli con coppie `id`/`version` esatte. Un modulo non
può dipendere da sé stesso (`DDFP017`). Il resolver predefinito cerca ogni
dipendenza sotto la radice dell'applicazione nel percorso riservato:

```text
.ddf/modules/<id>/<version>/ddf-project.json
```

La posizione è uno store, non un namespace e non influenza l'identità. I suoi
sorgenti sono esclusi dal workspace dell'applicazione e dai glob del modulo
radice; vengono caricati soltanto attraversando il grafo delle dipendenze. Un
host può fornire un diverso `IDdfModuleResolver` mantenendo le stesse regole.

La risoluzione è deterministica e depth-first, con dipendenze prima del modulo
che le usa. Sono rifiutati moduli mancanti (`DDFB013`), identità/versioni diverse
da quelle richieste (`DDFB014`), cicli (`DDFB015`), richieste incompatibili della
stessa identità (`DDFB016`) e moduli che usano linguaggio, pacchetto o backend
diversi (`DDFB017`). Moduli risolti, export e sorgenti partecipano alla ricevuta
e al fingerprint.

La VM conserva identità, dipendenze ed export in ogni `DdfIrModule`. Chiamate,
tipi e globali vengono risolti prima nel modulo attivo e poi negli export delle
sole dipendenze dirette. Anche inizializzazione, Watch e snapshot del debugger
usano lo stesso contesto qualificato. Se due dipendenze esportano lo stesso nome,
il type checker rifiuta l'ambiguità invece di scegliere in base all'ordine.

## 6. Direttive e librerie esterne

`@@'Nome'` non è e non diventerà un include testuale né un import di sorgenti.
Resta una direttiva legacy di richiesta libreria/capacità, senza effetto sulla
risoluzione dei nomi del modulo, ed è deprecata in attesa della libreria standard
formale. Le dipendenze fra moduli appartengono al manifesto. Se in futuro sarà
utile una sintassi `use`, selezionerà API già risolte senza copiare testo o
dedurre namespace dai percorsi.

Assembly .NET, componenti COM e librerie di terze parti non sono moduli DDF
automatici. Saranno accessibili soltanto tramite dipendenze dichiarate,
adattatori autorizzati e capability esplicite. Il mapping CLR dei tipi non
concede accesso alle API della piattaforma.

## 7. Standalone ed embedded

Il modello dei moduli, il binding e le diagnostiche sono condivisi. Lo
standalone potrà compilare l'IR bound tramite Roslyn; l'embedded continuerà a
eseguire script interpretati e capacità autorizzate dall'host, come stabilito da
DDF-0015.

## 8. Distribuzione e installazione

Un modulo sorgente può essere distribuito come archivio `.ddfmod` secondo
`DDF_MODULE_PACKAGE_FORMAT.md`. Il pacchetto conserva il manifesto alla radice e
i sorgenti selezionati, ma non introduce namespace, import impliciti o accesso a
componenti .NET. Versioni differenti convivono nello store; sostituire la stessa
coppia identità/versione richiede un'operazione esplicita.

Il gestore Core esegue staging, validazione e sostituzione atomica con rollback,
impone limiti al payload e rifiuta path traversal. Installare non significa
eseguire o autorizzare il modulo: export, grafo e semantica vengono verificati
dal build del progetto consumatore.

## 9. Diagnostiche del contratto

| Codice | Significato |
| --- | --- |
| `DDFP015` | identità o versione del modulo non valida |
| `DDFP016` | esportazione non valida o duplicata |
| `DDFP017` | dipendenza del modulo da sé stesso |
| `DDFB009` | funzione di ingresso non unica |
| `DDFB010` | dichiarazione runtime duplicata nel modulo |
| `DDFB011` | globale riferita prima dell'inizializzazione o ciclo globale |
| `DDFB012` | esportazione priva di dichiarazione top-level corrispondente |
| `DDFB013` | modulo richiesto assente dallo store configurato |
| `DDFB014` | identità o versione risolta diversa da quella richiesta |
| `DDFB015` | ciclo nel grafo delle dipendenze |
| `DDFB016` | due versioni della stessa identità richieste nello stesso grafo |
| `DDFB017` | linguaggio, pacchetto o backend incompatibile |
| `DDF312` | chiamata a funzione non visibile o sconosciuta |
| `DDF313` | riferimento a valore non visibile o sconosciuto |

