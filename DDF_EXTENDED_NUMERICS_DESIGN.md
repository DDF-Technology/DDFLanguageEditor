# Numerici estesi DDF 0.19 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.19`; il pacchetto canonico DDF `0.13.0`
non riconosce ancora le forme descritte. Il contratto machine-readable è
`ddf-extended-numerics-design.json`.

## Obiettivi

- aggiungere contatori e identificativi interi oltre i 32 bit;
- rappresentare importi e quantità decimali senza errore binario;
- mantenere overflow, conversioni e arrotondamento identici in VM e Roslyn;
- impedire che tipi, costanti o regole CLR trapelino nella IR;
- conservare un insieme numerico piccolo e comprensibile.

## Tipi e letterali

`long` è un intero con segno a 64 bit, intervallo
`-9223372036854775808..9223372036854775807`, valore iniziale `0i64` e aritmetica
checked.

`decimal` è un decimale finito base 10 con segno, coefficiente intero massimo di
96 bit e scala `0..28`, valore iniziale `0dec`. La sua semantica è definita da
DDF; il backend Roslyn la rappresenta con `System.Decimal`, ma tale tipo non
compare in AST, IR, SDK o debugger.

I suffissi canonici, minuscoli e case-sensitive, eliminano ambiguità:

```ddf
long identifier << 9223372036854775807i64;
decimal price << 19.95dec;
decimal tax << 0.22dec;
decimal total << price + (price * tax);
```

Un intero senza suffisso resta `int` e deve rientrare nei 32 bit. Un letterale
con punto senza suffisso resta `float`. Il segno è sempre un operatore unario,
non parte del token numerico. `_`, separatori locali, esponenti e suffissi CLR
`L`/`M` non sono accettati nella prima verticale.

## Promozioni e conversioni

Le sole promozioni implicite estese sono:

- `int -> long`;
- `int -> decimal`;
- `long -> decimal`.

Resta valida `int -> float`. Non esistono promozioni implicite fra `float` e
`decimal`, né da `long` a `float`, perché potrebbero perdere informazione. Le
operazioni miste seguono la destinazione esatta della promozione: `int + long`
produce `long`; `int/long + decimal` produce `decimal`.

Le conversioni fallibili sono funzioni standard con nomi non sovraccaricati:
`toLong(string)`, `toDecimal(string)`, `longToInt(long)`,
`decimalToInt(decimal)`, `decimalToLong(decimal)`,
`floatToDecimal(float)` e `decimalToFloat(decimal)`. Parsing e formattazione
sono invariant culture. Overflow, testo invalido, risultato non finito o
perdita non autorizzata producono errori recuperabili dichiarati.

Le conversioni da `decimal` a intero richiedono parte frazionaria zero; non
arrotondano implicitamente. `floatToDecimal` accetta soltanto valori finiti e
usa la rappresentazione decimale round-trip del valore DDF; l'eventuale perdita
è esplicita nel nome e nel contratto della funzione.

## Operatori

`long` supporta `+`, `-`, `*`, `/`, `^`, confronti e uguaglianza. `decimal`
supporta `+`, `-`, `*`, `/`, confronti e uguaglianza. L'esponente resta un
`int` non negativo; il risultato conserva il tipo della base.

Ogni operazione è checked. Divisione per zero, overflow e scala non
rappresentabile producono errori recuperabili stabili. La divisione e ogni
riduzione di scala arrotondano al valore rappresentabile più vicino con
tie-to-even e scala massima 28. Zeri finali possono essere normalizzati senza
cambiare uguaglianza, output canonico o fingerprint costante.

`float` conserva il proprio contratto IEEE 754 finito. Un'espressione che
mescola `float` con `long` o `decimal` è un errore statico finché il programmatore
non sceglie una conversione esplicita.

## Costanti, collezioni e stato

`fixed long` e `fixed decimal` sono ammessi quando il valutatore costante comune
può materializzare il risultato con le stesse regole checked. La serializzazione
canonica conserva valore e scala normalizzata, mai una stringa dipendente dalla
cultura.

`long` potrà essere una chiave stabile di `map<K,V>`. `decimal` resta escluso
dalle chiavi nella prima verticale delle mappe, così la canonicalizzazione di
scala non diventa parte prematura del loro hashing. `state long` e
`state decimal` richiedono l'estensione versionata dello schema di persistenza e
una migrazione esplicita; non vengono attivati automaticamente.

## IR, SDK e strumenti

La IR usa primitive DDF `Int64` e `Decimal96`, con costanti backend-neutral.
Nessun nodo contiene `System.Int64`, `System.Decimal`, `decimal` Roslyn o
`BigInteger`. VM e backend compilato devono concordare su valore, errore,
posizione, arrotondamento, output e Watch.

Debugger, hover e Watch usano formattazione invariant e suffisso DDF quando
serve a distinguere il tipo. IntelliSense presenta dominio, conversioni e
possibili errori; formatter preserva i suffissi. Flowchart mostra il testo DDF,
non cast o temporanei generati dal backend.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF348` | letterale `i64` fuori intervallo |
| `DDF349` | letterale `dec` non rappresentabile |
| `DDF350` | operazione numerica mista senza conversione ammessa |
| `DDF351` | conversione numerica con perdita non autorizzata |
| `DDF352` | `state` numerico esteso senza schema compatibile |

## Ordine di implementazione

1. lessico dei suffissi, tipi dichiarativi, parser e diagnostiche;
2. lattice delle conversioni e valutatore `fixed` checked;
3. primitive IR, VM e serializzazione canonica;
4. funzioni standard di conversione ed errori recuperabili;
5. lowering Roslyn e corpus differenziale ai limiti;
6. persistenza `state`, debugger, formatter, IntelliSense e Flowchart;
7. profili sintattici, mappe con chiave `long` e documentazione.

L'attivazione richiederà DDF `0.19` e un nuovo pacchetto canonico. Fino alla
verticale completa, `long` e `decimal` restano identificatori ordinari e i
suffissi `i64`/`dec` non sono letterali validi nella `0.13`.
