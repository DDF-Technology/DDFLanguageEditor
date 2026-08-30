# DDF language reference — editor subset 0.6

Questo documento descrive gli elementi lessicali riconosciuti da
DDFLanguageEditor 0.4.1. La grammatica sintattica implementata, comprese precedenze
e associatività, è definita in `DDF_GRAMMAR.md`. La validità semantica del
sottoinsieme implementato viene verificata dal type checker.

L'inventario centralizzato di termini, operatori e delimitatori è mantenuto in
`DDF_LANGUAGE_CATALOG.md`.

La precedente `DDF - Program Language Spec.txt` rimane nel repository come bozza
storica. In caso di contrasto, il presente documento descrive il comportamento
attuale dell'editor.

## Commenti e stringhe

```ddf
// commento fino alla fine della riga
/* commento
   su più righe */
"testo con una \"virgolette\" sottoposta a escape"
```

Un commento a blocchi o una stringa non terminati vengono evidenziati fino alla
fine del documento e producono una diagnostica lessicale.

## Inclusione di librerie

```ddf
@@'Library'
```

L'editor riconosce l'intero intervallo dall'apertura `@@'` fino al successivo
apice singolo. La semantica dell'inclusione non è ancora definita.

## Tipi riconosciuti

Tipi semplici:

```text
int float char bool
```

Tipi e costrutti complessi:

```text
void string dict struct
```

Le forme array utilizzano parentesi quadre, ad esempio `int[10] values`; la
dimensione e gli indici devono avere tipo `int`. I campi delle strutture si
accedono con `.`, ad esempio `point.x`.

## Letterali

- interi: `0`, `42`, `123`;
- decimali: `1.5`, `123.45`;
- booleani canonici: `true`, `false`;
- booleani storici ancora riconosciuti: `True`, `False`.

Il type checker tratta temporaneamente entrambe le grafie come `bool`; la forma
minuscola resta quella canonica per i nuovi sorgenti.

## Parole chiave

Controllo di flusso:

```text
if while do for
```

Operatori di funzione:

```text
ret brk end out
```

## Operatori riconosciuti

```text
<< >>
+ - * / ^ ^/ ++ --
< <= > >= == !! >><< <<>>
! & || |&|
```

La moltiplicazione canonica usa `*`. La `x` presente nella bozza storica non è
considerata un operatore perché sarebbe indistinguibile da un identificatore.

## Delimitatori

```text
{ } ( ) [ ] . , ;
```

## Regole semantiche implementate

- conversione implicita da `int`/`char` a `float`;
- condizioni di controllo obbligatoriamente `bool`;
- operatori aritmetici limitati ai tipi numerici e operatori logici ai booleani;
- inizializzazioni, assegnazioni, argomenti e ritorni compatibili con il tipo atteso;
- stringhe di un singolo carattere accettate come inizializzatori `char`;
- firme e tipi globali condivisi tra i file del workspace.

## Regole runtime implementate

- l'esecuzione parte dalla funzione `main` del documento corrente;
- `<<` assegna un valore, mentre `espressione >> Console` scrive nella palette Output;
- funzioni, blocchi e cicli usano scope lessicali distinti;
- `ret` restituisce dalla funzione, `brk` interrompe il ciclo corrente ed `end` termina il programma;
- array e campi di struttura sono modificabili tramite `[]` e `.`;
- ogni esecuzione può essere cancellata e si arresta dopo 100.000 istruzioni;
- gli errori runtime usano i codici `DDF401`–`DDF408` con riga e colonna.

Funzioni standard iniziali:

```text
print(string) out void
readLine() out string
length(string) out int
toInt(string) out int
toFloat(string) out float
```

`readLine` apre un dialogo di input nell'editor. `toInt` e `toFloat` usano una
notazione numerica invariabile e producono `DDF408` quando il testo non è valido.

La palette Output distingue il valore restituito da `main` dal numero di
"passi runtime" usato internamente per applicare il limite anti-loop. Un passo
runtime corrisponde alla valutazione di un nodo AST, non a una riga DDF.

## Elementi non ancora definiti

- regole per identificatori e scope oltre al riconoscimento lessicale di base;
- escape ammessi nelle stringhe;
- semantica di `dict`, librerie, overload e conversioni definite dall'utente;
- semantica definitiva degli operatori storici `>><<`, `<<>>` e `^/`;
- input interattivo, libreria standard e caricamento runtime multi-file.

Ogni decisione futura su questi punti dovrà aggiornare questo documento, i test
e il registro decisioni in `ROADMAP.md`.

## Diagnostiche lessicali

| Codice | Significato |
| --- | --- |
| `DDF001` | Stringa non terminata |
| `DDF002` | Commento a blocchi non terminato |
| `DDF003` | Inclusione di libreria non terminata |
| `DDF004` | Carattere non riconosciuto |

Le diagnostiche includono intervallo assoluto, riga e colonna. Un doppio clic
nel pannello diagnostico seleziona il testo corrispondente nell'editor.

## Diagnostiche sintattiche

Il parser usa i codici `DDF101`–`DDF105` per token inattesi o mancanti,
espressioni, tipi e identificatori attesi. Continua l'analisi dopo gli errori,
così il pannello può mostrare più problemi durante la stessa modifica.

## Diagnostiche semantiche

Il type checker usa `DDF301`–`DDF308` per incompatibilità di tipo, operatori,
argomenti, ritorni, tipi o membri sconosciuti, condizioni non booleane, ritorni
mancanti e destinazioni di assegnazione non modificabili.
