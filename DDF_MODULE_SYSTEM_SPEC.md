# Specifica del sistema di moduli DDF 0.12.3

Stato: normativa per il modello interno al progetto; la risoluzione delle
dipendenze fra moduli resta pianificata.

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

## 3. Esportazioni

`module.exports` contiene nomi top-level univoci. Ogni nome deve corrispondere a
una funzione, struttura o globale del modulo, altrimenti il build produce
`DDFB012`. Un elenco vuoto significa che il modulo non pubblica API.

Le esportazioni non cambiano la visibilità interna. Diventeranno il solo insieme
risolvibile dai moduli dipendenti quando sarà disponibile il resolver; non
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
può dipendere da sé stesso (`DDFP017`). Il resolver trans-modulo non è ancora
implementato: oggi le dipendenze sono validate e incluse nella ricevuta e nel
fingerprint, ma non rendono disponibili simboli o codice esterno.

Il resolver futuro dovrà:

- selezionare esattamente la versione richiesta;
- esporre soltanto i simboli elencati in `exports`;
- rifiutare moduli mancanti, versioni incompatibili e cicli;
- produrre una IR bound indipendente da cartelle e nomi file;
- conservare la medesima semantica in VM e nel backend Roslyn.

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

## 8. Diagnostiche del contratto

| Codice | Significato |
| --- | --- |
| `DDFP015` | identità o versione del modulo non valida |
| `DDFP016` | esportazione non valida o duplicata |
| `DDFP017` | dipendenza del modulo da sé stesso |
| `DDFB009` | funzione di ingresso non unica |
| `DDFB010` | dichiarazione runtime duplicata nel modulo |
| `DDFB011` | globale riferita prima dell'inizializzazione o ciclo globale |
| `DDFB012` | esportazione priva di dichiarazione top-level corrispondente |

