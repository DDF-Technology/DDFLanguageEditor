# Libreria stringhe DDF 0.20 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.20`; il pacchetto canonico DDF `0.13.0`
e `ddf.standard@0.12.3` non espongono ancora le operazioni descritte. Il
contratto machine-readable è `ddf-string-library-design.json`.

## Obiettivi

- rendere le stringhe utilizzabili senza conversioni implicite o cultura host;
- offrire concatenazione, confronto, ricerca, estrazione e trasformazione;
- integrare `maybe`, sequenze ed errori tipizzati in modo coerente;
- garantire risultati identici in VM, Roslyn, standalone ed embedded;
- limitare memoria e lavoro per input provenienti da script o host.

## Contratto fondamentale

Le stringhe DDF restano sequenze immutabili, non nulle, di code unit UTF-16.
Non viene applicata normalizzazione Unicode automatica. Indici e lunghezze sono
sempre espressi in code unit, come la funzione `length` corrente.

L'operatore `+` viene esteso esclusivamente a `string + string`:

```ddf
string fullName << firstName + " " + lastName;
```

Numeri, booleani, enum e oggetti non vengono convertiti implicitamente in
testo. Il programmatore usa funzioni esplicite come `intText`, `floatText`,
`longText`, `decimalText`, `boolText` e `charText`, tutte invariant culture.

## Funzioni pure

La futura libreria standard espone firme globali non sovraccaricate:

| Funzione | Risultato |
| --- | --- |
| `compareOrdinal(left, right)` | `int` tra `-1`, `0`, `1` |
| `contains(text, fragment)` | presenza ordinale |
| `startsWith(text, prefix)` | prefisso ordinale |
| `endsWith(text, suffix)` | suffisso ordinale |
| `find(text, fragment)` | `maybe int` con il primo indice |
| `substring(text, start, length)` | porzione controllata |
| `replace(text, oldValue, newValue)` | sostituzione ordinale globale |
| `trim(text)` | rimozione White_Space ai bordi |
| `upper(text)` | maiuscolo Unicode deterministico |
| `lower(text)` | minuscolo Unicode deterministico |
| `split(text, separator)` | `sequence<string>` |
| `join(values, separator)` | unione di `sequence<string>` |
| `format(template, values)` | formattazione posizionale sicura |

`find` restituisce `none` se non trova il frammento. Un frammento vuoto è
presente all'indice zero; `contains`, `startsWith` ed `endsWith` restituiscono
quindi `true`. `substring` ammette lunghezza zero e richiede un intervallo
interamente valido. `replace` richiede `oldValue` non vuoto e sostituisce da
sinistra a destra occorrenze non sovrapposte.

`split` richiede un separatore non vuoto e conserva i segmenti vuoti, inclusi
quelli iniziali e finali. `join` non aggiunge il separatore prima o dopo la
sequenza. Una sequenza vuota produce `""`.

## Maiuscole, minuscole e spazi

`upper`, `lower` e `trim` non delegano alle impostazioni locali del sistema.
Usano tabelle DDF versionate basate su Unicode `15.1`: Default Case Conversion
e proprietà `White_Space`. Le conversioni possono cambiare la lunghezza del
testo. Un futuro aggiornamento Unicode richiederà una nuova versione della
libreria standard e corpus differenziali, non varierà silenziosamente col
runtime .NET installato.

Confronto, ricerca, prefisso, suffisso e sostituzione restano ordinali sulle
code unit e non applicano case folding. Ricerca linguistica, collazioni locali
e normalizzazione sono capacità future distinte.

## Formattazione sicura

`format(string template, sequence<string> values)` riconosce soltanto
placeholder numerici `{0}`, `{1}`, ...; `{{` e `}}` producono parentesi graffe
letterali. Non sono ammessi espressioni, accessi a proprietà, allineamento,
specificatori di formato, reflection o valutazione di codice.

Un template malformato o un indice fuori intervallo produce un errore
recuperabile dichiarato. Gli argomenti sono già stringhe: formattazione di
numeri e date avviene prima tramite funzioni esplicite e deterministiche.

## Limiti ed errori

Ogni operazione verifica prima i limiti di input, output, segmenti e
placeholder. Se il risultato eccede il budget, fallisce atomicamente senza
restituire testo o sequenze parziali. Intervalli invalidi, separatori vuoti,
template errati e limiti superati hanno identità di errore recuperabile stabile.

Le funzioni sono pure: non leggono cultura, ambiente, clock, filesystem o stato
host. I backend possono ottimizzare, ma non possono cambiare ordine, Unicode,
allocazioni osservabili tramite errori o posizione diagnostica.

## IR e strumenti

Concatenazione e funzioni stringa sono operazioni semantiche backend-neutral;
la IR non contiene chiamate a `System.String`, `StringBuilder`, regex o
`CultureInfo`. VM e Roslyn devono coincidere su testo, indici, errori e budget.

IntelliSense e hover mostrano firma, purezza, regole ordinali e possibili
errori. Debugger e Watch visualizzano escape e lunghezza senza mutare il valore.
Il formatter non riscrive contenuto o escape delle stringhe. Il Flowchart
presenta chiamate e concatenazioni DDF senza temporanei del backend.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF353` | operando di concatenazione non stringa |
| `DDF354` | intervallo stringa staticamente non valido |
| `DDF355` | separatore o valore di ricerca vietato |
| `DDF356` | template di formattazione staticamente non valido |
| `DDF357` | contratto/versione Unicode incompatibile |

## Ordine di implementazione

1. concatenazione `string + string` in type checker, IR, VM e Roslyn;
2. confronto, ricerca, estrazione e sostituzione ordinali;
3. `maybe int` per `find` e `sequence<string>` per `split`/`join`;
4. motore Unicode DDF versionato per `trim`/`upper`/`lower`;
5. formatter posizionale, quote e errori recuperabili;
6. conversioni testuali dei tipi e output canonico;
7. IntelliSense, debugger, Flowchart, profili e corpus differenziale.

L'attivazione richiederà DDF `0.20` e una nuova versione di `ddf.standard`.
Fino alla verticale completa, `+` resta numerico e le nuove funzioni non fanno
parte della libreria standard `0.12.3`.
