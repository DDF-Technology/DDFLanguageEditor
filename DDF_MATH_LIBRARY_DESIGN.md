# Libreria matematica DDF 0.21 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.21`; il pacchetto canonico DDF `0.13.0`
non esporta ancora le funzioni descritte. Il contratto machine-readable è
`ddf-math-library-design.json`.

## Obiettivi

- offrire matematica quotidiana senza importare API o overload CLR;
- rendere dominio, arrotondamento e risultati non finiti errori DDF stabili;
- ottenere la stessa semantica in VM, Roslyn e futura VM bytecode;
- mantenere separate matematica pura, casualità e capacità host;
- consentire test differenziali ripetibili su valori limite.

## Superficie tipizzata

La libreria standard non introduce overload. Le famiglie che accettano più tipi
usano nomi distinti:

| Famiglia | Funzioni |
| --- | --- |
| valore assoluto | `absInt`, `absLong`, `absFloat`, `absDecimal` |
| minimo | `minInt`, `minLong`, `minFloat`, `minDecimal` |
| massimo | `maxInt`, `maxLong`, `maxFloat`, `maxDecimal` |
| limite chiuso | `clampInt`, `clampLong`, `clampFloat`, `clampDecimal` |
| arrotondamento | `floorFloat`, `ceilingFloat`, `truncateFloat`, `roundFloat`, `roundDecimal` |
| radice e potenza | `sqrt`, `powerFloat` |
| esponenziali e logaritmi | `exp`, `log`, `log10` |
| trigonometria | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2` |
| costanti | `pi`, `e` |

`long` e `decimal` dipendono dalla verticale DDF `0.19`. Tutte le funzioni sono
pure. Le costanti sono funzioni senza argomenti per non introdurre una seconda
forma di binding nella libreria standard.

## Regole esatte

`absInt` e `absLong` sono checked: il minimo valore del rispettivo tipo non ha
un opposto rappresentabile e produce errore. `absFloat(-0.0)` produce `+0.0`.

`min*` e `max*` valutano prima l'argomento sinistro e poi quello destro. Per
`float`, dove NaN e infinito non sono valori DDF validi, resta soltanto la
distinzione degli zeri con segno: il minimo fra `-0.0` e `+0.0` è `-0.0`, il
massimo è `+0.0`.

`clamp*(value, minimum, maximum)` richiede `minimum <= maximum`; altrimenti
fallisce senza produrre un valore. Gli argomenti vengono valutati una sola volta
nell'ordine scritto.

`floorFloat`, `ceilingFloat` e `truncateFloat` restituiscono ancora `float`.
`roundFloat(value, digits)` e `roundDecimal(value, digits)` arrotondano in base
10 al numero di cifre dopo il separatore usando nearest, ties-to-even. `digits`
deve essere rispettivamente nell'intervallo `0..15` e `0..28`. Non esiste una
modalità di arrotondamento scelta dalla cultura o dal backend.

## Radici, potenze e trascendenti

Le funzioni `sqrt`, `powerFloat`, `exp`, `log`, `log10`, `sin`, `cos`, `tan`,
`asin`, `acos`, `atan` e `atan2` accettano e restituiscono `float`. Gli angoli
sono sempre espressi in radianti.

- `sqrt(x)` richiede `x >= 0`; `sqrt(-0.0)` conserva `-0.0`;
- `log` e `log10` richiedono un argomento strettamente positivo;
- `asin` e `acos` richiedono un argomento compreso in `[-1, 1]`;
- `powerFloat(0, exponent)` rifiuta un esponente negativo; `0^0` vale `1`;
- una base negativa di `powerFloat` richiede un esponente finito esattamente
  intero; il segno dipende dalla parità dell'esponente;
- ogni risultato NaN o infinito è un errore recuperabile, mai un valore DDF.

`atan2(y, x)` conserva l'ordine degli argomenti e distingue gli zeri con segno.
`pi()` ed `e()` restituiscono i bit binary64 fissati dal contratto
machine-readable, non costanti scelte dal framework installato.

## Riproducibilità

Le operazioni esatte e l'arrotondamento usano primitive DDF nella IR. Le
trascendenti usano l'identità algoritmica `ddf.math.transcendental.v1`; tabelle,
riduzione degli argomenti e regole dei casi limite fanno parte della versione
della libreria standard. Chiamare direttamente `System.Math`, una `libm` o una
istruzione hardware senza l'adapter conforme non costituisce implementazione
del contratto.

VM e backend Roslyn devono produrre lo stesso pattern binary64 o lo stesso
errore. Una futura VM su un'altra piattaforma deve eseguire lo stesso algoritmo,
non aumentare una tolleranza per nascondere divergenze. Il corpus differenziale
include casi limite, zeri con segno, valori adiacenti e vettori di riferimento.

## Confini intenzionali

Casualità e tempo non appartengono a questa libreria: richiedono provider
sostituibili. Statistica, vettori, matrici, numeri complessi, unità di misura e
arbitrary precision saranno moduli separati. La prima verticale non rende le
chiamate matematiche utilizzabili negli inizializzatori `fixed`; tale estensione
richiederà un valutatore costante comune e una decisione separata.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF358` | argomento fuori dal dominio matematico |
| `DDF359` | risultato matematico non finito o non rappresentabile |
| `DDF360` | intervallo `clamp` non valido |
| `DDF361` | numero di cifre di arrotondamento non valido |
| `DDF362` | versione dell'algoritmo matematico assente o incompatibile |

## Ordine di implementazione

1. firme, metadati ed errori della libreria standard `0.21.0`;
2. operazioni esatte per `int`, `long`, `float` e `decimal`;
3. arrotondamento e casi limite binary64/decimal;
4. adapter trascendentale DDF versionato e vettori di riferimento;
5. primitive IR, VM e lowering Roslyn condivisi;
6. IntelliSense, Signature Help, hover, Watch e Playground;
7. corpus differenziale, profili sintattici e documentazione.

L'attivazione richiederà DDF `0.21`, la libreria standard `0.21.0` e il
completamento dei numerici estesi. Fino alla verticale completa, tutti i nomi
elencati restano normali identificatori nel pacchetto `0.13.0`.
