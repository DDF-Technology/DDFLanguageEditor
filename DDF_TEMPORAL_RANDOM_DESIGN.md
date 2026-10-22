# Tempo, durate e casualità DDF 0.22 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.22`; il pacchetto canonico DDF `0.13.0`
non riconosce ancora i tipi o le funzioni descritti. Il contratto
machine-readable è `ddf-temporal-random-design.json`.

## Obiettivi

- separare tempo civile, istanti UTC e intervalli;
- evitare l'ambiguità di `DateTime`, fuso locale e cultura del sistema;
- offrire precisione e serializzazione identiche in VM, Roslyn e futura VM;
- rendere orologio e casualità capability esplicite e sostituibili nei test;
- non trasformare timezone, timer o generatori CLR in semantica implicita DDF.

## Tipi temporali

La verticale introduce cinque tipi integrati non nulli:

| Tipo | Significato |
| --- | --- |
| `date` | data civile nel calendario gregoriano prolettico, anni `0001..9999` |
| `time` | ora del giorno `00:00:00` inclusa e `24:00:00` esclusa |
| `datetime` | coppia civile `date` + `time`, priva di fuso e offset |
| `instant` | punto assoluto sulla timeline UTC |
| `duration` | intervallo signed indipendente da calendario e fuso |

La risoluzione comune è un tick di 100 nanosecondi. `time` contiene
`0..863999999999` tick dalla mezzanotte. `duration` usa tick signed a 64 bit
checked. `instant` usa tick signed dall'epoch Unix `1970-01-01T00:00:00Z`, ma
accetta soltanto valori convertibili nell'intervallo civile `0001..9999`.

Secondi intercalari non sono rappresentati. `datetime` non diventa mai
implicitamente un `instant`: la conversione richiede un offset UTC esplicito.

## Costruzione, accesso e calcolo

La prima verticale non aggiunge letterali speciali. Costruttori e accessori
sono funzioni standard tipizzate, senza overload:

- `dateFromParts`, `dateYear`, `dateMonth`, `dateDay`, `dateAddDays`,
  `dateDaysBetween`;
- `timeFromParts`, `timeHour`, `timeMinute`, `timeSecond`, `timeSubsecondTicks`;
- `datetimeFromParts`, `datetimeDate`, `datetimeTime`, `datetimeAdd`,
  `datetimeDifference`;
- `durationFromTicks`, `durationFromMilliseconds`, `durationTicks`,
  `durationAdd`, `durationSubtract`, `durationNegate`;
- `instantFromDatetime`, `datetimeFromInstant`, `instantAdd`,
  `instantDifference`.

I costruttori con componenti fuori intervallo falliscono atomicamente.
`dateAddDays`, somme, sottrazioni e negazione sono checked. L'offset delle
conversioni `instant`/`datetime` è espresso in minuti e deve essere compreso in
`-840..840`; non viene dedotto dal computer.

Ogni tipo supporta uguaglianza e confronto ordinato soltanto con lo stesso
tipo. Non esistono operatori aritmetici temporali nella prima verticale: i nomi
espliciti impediscono di confondere giorni civili e durate.

## Testo canonico

La rappresentazione è invariant e ASCII:

- `date`: `yyyy-MM-dd`;
- `time`: `HH:mm:ss.fffffff`;
- `datetime`: `yyyy-MM-ddTHH:mm:ss.fffffff`;
- `instant`: `yyyy-MM-ddTHH:mm:ss.fffffffZ`;
- `duration`: numero signed di tick seguito da `ticks`.

`dateText`, `timeText`, `datetimeText`, `instantText` e `durationText` producono
questa forma. `parseDate`, `parseTime`, `parseDatetime`, `parseInstant` e
`parseDuration` accettano esclusivamente la forma canonica e restituiscono
`maybe T`; non consultano cultura o timezone. Queste funzioni dipendono dalle
verticali `maybe` e stringhe.

## Fusi orari

Il nucleo accetta soltanto offset UTC espliciti. Identificatori IANA, regole
DST, nomi localizzati e conversioni Windows/IANA appartengono a un modulo
timezone separato, che deve bloccare una versione dei dati e dichiararla negli
artefatti. Nessuna esecuzione usa silenziosamente il fuso locale del sistema.

## Orologio sostituibile

`currentInstant() out instant` ha effetto `clock`, non `pure`. Richiede un
provider `ddf.clock.v1` iniettato dall'ambiente di esecuzione. Uno snapshot di
test può restituire una sequenza deterministica; lo standalone può autorizzare
un provider di sistema. Provider assente, revocato, esaurito o oltre quota
produce un errore DDF recuperabile.

Il provider non espone `DateTime.Now`, timer, thread o API host allo script.
Ogni lettura viene conteggiata e osserva l'ordine di valutazione DDF. Sleep,
timer e callback richiedono prima il modello asincrono e non fanno parte della
prima verticale.

## Casualità sostituibile

`randomInt(minimum, maximumExclusive)`, `randomLong(minimum,
maximumExclusive)` e `randomFloat()` hanno effetto `random`. I limiti interi
devono descrivere un intervallo non vuoto; `randomFloat` restituisce un valore
in `[0, 1)`. Tutte richiedono il provider `ddf.random.v1` e usano rejection
sampling specificato dall'adapter per evitare bias modulo.

Test e build riproducibili usano uno snapshot di valori o un provider seeded la
cui identità e versione entrano nel piano di esecuzione. Lo standalone può
scegliere un provider crittograficamente sicuro autorizzato. Nessuna funzione
usa `System.Random.Shared`, seed temporali o stato globale; la sequenza non è
persistita automaticamente da `state`.

## IR, SDK e persistenza

AST e IR usano primitive DDF `Date`, `TimeOfDay`, `CivilDateTime`, `Instant` e
`Duration`; non contengono `System.DateTime`, `DateTimeOffset`, `TimeSpan` o
nomi Roslyn. Il runtime .NET usa value type interni DDF con controlli comuni.

Debugger, Watch, hover e output usano sempre il testo canonico. La persistenza
`state` richiede uno schema versionato che memorizzi tick e identità del tipo,
mai testo locale. I costruttori standard non sono inizialmente ammessi negli
inizializzatori `fixed`.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF363` | componente temporale o testo canonico non valido |
| `DDF364` | overflow o risultato temporale fuori intervallo |
| `DDF365` | offset UTC fuori intervallo |
| `DDF366` | provider clock assente, revocato o incompatibile |
| `DDF367` | intervallo casuale non valido |
| `DDF368` | provider random assente, revocato o incompatibile |
| `DDF369` | quota o snapshot clock/random esaurito |

## Ordine di implementazione

1. tipi, confronti e primitive IR backend-neutral;
2. costruttori, accessori e calcolo checked;
3. testo canonico e parsing `maybe`;
4. provider `ddf.clock.v1` e `ddf.random.v1` con snapshot deterministici;
5. VM, lowering Roslyn e persistenza versionata;
6. debugger, Watch, IntelliSense, Flowchart e Playground;
7. corpus differenziale su limiti, culture, timezone host e provider revocati.

L'attivazione richiederà DDF `0.22`, `ddf.standard@0.22.0` e le verticali
`maybe`, stringhe e numerici estesi. Fino alla consegna completa, tipi e
funzioni qui elencati restano normali identificatori nel pacchetto `0.13.0`.
