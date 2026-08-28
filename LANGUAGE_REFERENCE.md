# DDF language reference — editor subset 0.4.1

Questo documento descrive gli elementi lessicali riconosciuti da
DDFLanguageEditor 0.4.1. La grammatica sintattica implementata, comprese precedenze
e associatività, è definita in `DDF_GRAMMAR.md`. La validità semantica dei
programmi non viene ancora verificata.

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

Le forme array utilizzano parentesi quadre, ad esempio `int[10] values`. La
validità della dimensione non viene ancora verificata.

## Letterali

- interi: `0`, `42`, `123`;
- decimali: `1.5`, `123.45`;
- booleani canonici: `true`, `false`;
- booleani storici ancora riconosciuti: `True`, `False`.

La normalizzazione definitiva dei booleani verrà decisa prima dell'analisi semantica.

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

## Elementi non ancora definiti

- regole per identificatori e scope oltre al riconoscimento lessicale di base;
- escape ammessi nelle stringhe;
- semantica dei tipi, delle funzioni e delle librerie;
- diagnostiche e comportamento di esecuzione.

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
