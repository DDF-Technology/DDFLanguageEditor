# Contratto di equivalenza dei backend DDF

Stato: **normativo per i gate di rilascio**, versione `1.0`.

`ddf-backend-equivalence.json` descrive le osservazioni DDF che devono rimanere
identiche tra la VM `ddf-vm-ir-v1` e il backend `dotnet-roslyn-v1`. Il contratto
non confronta dettagli interni CLR o contatori d'implementazione: confronta solo
comportamenti visibili allo script, all'host e al debugger.

## Stati del gate

- `enforced`: esiste un confronto diretto VM/Roslyn e una divergenza fallisce la
  suite che blocca il rilascio;
- `partial`: esistono prove su entrambi i backend, ma manca ancora un unico
  scenario differenziale o una parte del contratto pubblico;
- `planned`: la dimensione non dispone ancora di evidenza sufficiente.

Una dimensione `partial` o `planned` non può essere descritta come equivalente.
La relativa funzione non viene promossa finché il gap dichiarato non è chiuso.
Quando si aggiunge un backend, una funzione con effetti oppure un nuovo stato
osservabile, il contratto e il gate devono essere aggiornati insieme.

## Copertura corrente

Valore restituito, output, errori runtime, effetti host, ordine di valutazione,
aliasing degli array, ordine di cleanup, stato persistibile, sospensioni e punti
di debug sono `enforced`. Il comando di rilascio
`DDFLanguageEditor.Compiler.Tests --backend-equivalence-only` esegue l'intero
gate senza attraversare le prove estranee al confronto dei backend; qualsiasi
divergenza produce un exit code diverso da zero.

Il confronto dei punti di debug comprende documento, intervallo, Call Stack e
valori DDF visibili durante Step Into, Over e Out. Il gate cleanup attraversa
la stessa sorgente e le stesse capability su VM e Roslyn con successo, errore
runtime, timeout, Stop e cleanup fallito; verifica ordine LIFO, rilascio singolo
e prosecuzione dopo un errore di rilascio. Le sospensioni coprono ripresa,
timeout, Stop, risposta tardiva e cleanup; l'ordine di valutazione comprende la
valutazione singola delle dimensioni degli array e l'aliasing distingue la
mutazione dell'istanza condivisa dalla riassegnazione locale del parametro.

Lo stato persistibile usa lo stesso insieme scalare e le stesse chiavi stabili
`modulo@versione::documento::nome` della VM. La Runtime ABI espone snapshot
immutabili backend-neutral; ogni sessione compilata importa lo snapshot prima
di `main` ed esporta un nuovo snapshot soltanto dopo esecuzione e cleanup
riusciti. Snapshot di un altro pacchetto, chiavi sconosciute e tipi incompatibili
sono rifiutati. Un fault non rende disponibile alcun nuovo snapshot: l'host
conserva quindi atomicamente l'ultimo stato committato senza riutilizzare campi
statici CLR fra sessioni.
