# Specifica della libreria standard minima DDF 0.12.3

## 1. Scopo e identità

La libreria standard minima è il catalogo `ddf.standard@0.12.3`. Fa parte del
contratto del pacchetto linguaggio e viene consumata dalla stessa definizione
immutabile da IntelliSense, Signature Help, hover, type checker e VM.

Non è un modulo DDF, non viene installata nello store `.ddf/modules` e non
concede accesso alle API dell'applicazione host. Le API host, i database e le
librerie esterne restano disponibili soltanto tramite cataloghi e permessi
espliciti. La direttiva legacy `@@'...'` non abilita funzioni standard.

## 2. Metadati normativi

`standard-library.json` usa schema `1.1` e adapter
`ddf-standard-library-v3`. Ogni funzione dichiara:

- nome, tipi dei parametri e tipo restituito;
- documentazione mostrata dagli strumenti di scrittura;
- binding chiuso implementato dal motore;
- effetto `pure`, `input` oppure `output`;
- insieme completo dei codici di errore runtime previsti.

Il loader rifiuta atomicamente tipi o binding ignoti (`DDFL130`/`DDFL132`),
identità, versione o effetto non validi (`DDFL133`) e metadati di effetto/errori
incoerenti con il binding (`DDFL134`). Il JSON non contiene nomi di metodi .NET,
delegate, reflection o codice eseguibile.

## 3. API minima

| Funzione | Effetto | Semantica | Errori |
| --- | --- | --- | --- |
| `print(string) out void` | `output` | Invia il testo al canale Output dell'esecuzione. Un canale assente scarta l'output. | nessuno |
| `readLine() out string` | `input` | Richiede una riga al canale Input dell'esecuzione; un risultato nullo diventa stringa vuota. | `DDF408` se il canale non è disponibile |
| `length(string) out int` | `pure` | Restituisce `System.String.Length`, quindi il numero di unità UTF-16, non di grafemi Unicode. | nessuno |
| `toInt(string) out int` | `pure` | Parsing invariant con segno e spazi iniziali/finali nel dominio `System.Int32`. | `DDF408` per testo non valido o fuori intervallo |
| `toFloat(string) out float` | `pure` | Parsing invariant `System.Double`, punto decimale ed esponente; `NaN` e infinito non sono risultati validi. | `DDF408` per testo non valido o risultato non finito |

`float` DDF resta `System.Double` come stabilito dalla specifica dei tipi. Le
funzioni non hanno overload, parametri opzionali o conversioni implicite oltre
a quelle già ammesse dal type checker DDF.

## 4. Risoluzione ed esecuzione

Le firme vengono aggiunte al catalogo dei simboli prima dell'analisi delle
chiamate. Una funzione DDF top-level con lo stesso nome mantiene la precedenza
nel proprio modulo; in assenza di tale dichiarazione viene selezionata la
funzione standard. Il type checker verifica arità e tipi prima del runtime.

I binding `core.*` sono puri e implementati direttamente dalla VM. I binding
`host.input` e `host.output` usano esclusivamente i canali controllati presenti
nelle opzioni di esecuzione: non consultano il catalogo degli oggetti host e non
estendono i permessi dello script.

Gli errori conservano posizione e stack DDF. Il frame della funzione standard
precede quello del chiamante, così una conversione fallita o un input assente
restano diagnosticabili durante Run e Debug.

## 5. Evoluzione

Nuove funzioni richiedono una decisione registrata, un binding motore sicuro,
mapping di tipi compatibile con Roslyn, documentazione e regressioni VM/IDE.
Cambiare la semantica osservabile di una funzione esistente richiede una nuova
versione di `ddf.standard`; rinominare soltanto la sintassi di un profilo non può
cambiare risultati, effetti o diagnostiche.

Matematica avanzata, collezioni, filesystem, rete, database, reflection,
assembly .NET e componenti COM non appartengono a questo nucleo minimo. Saranno
eventualmente forniti come moduli o adattatori autorizzati, mai come capacità
implicite della libreria standard.
