# Workspace di debug delle funzionalità DDF

Progetto multifile eseguibile che raccoglie i costrutti attualmente supportati
dal linguaggio DDF e attraversa chiamate definite in file differenti.

## Copertura

- punto di ingresso unico `main()` e funzioni multifile con parametri e ritorni;
- tipi `int`, `float`, `char`, `bool`, `string`, `void`;
- `enum`, `struct`, campi, array monodimensionali e accesso indicizzato;
- valori `fixed` primitivi ed enum, stato persistente `state` e variabile globale modificabile;
- `if`, `else if`, `else`, `select`, `when`, `otherwise`;
- cicli `for`, `while`, `do while`, oltre a `brk` e `continue`;
- operatori aritmetici, relazionali, logici, assegnazione `<<`, `++` e `--`;
- libreria standard (`print`, `length`, `toInt`) e output `>> Console`;
- terminazione esplicita `end` nella funzione non invocata
  `stopImmediately()`.

La gestione preventiva degli errori usa `OperationResult` e
`OperationStatus`: prima di dividere, convertire o indicizzare, il codice
controlla le precondizioni e rende esplicito l'esito. La sintassi strutturata
`attempt/recover/always`, `raise` e `raises` è pianificata per DDF 0.14 e non
viene simulata con costrutti non ancora validi.

## Esecuzione normale

1. Aprire questa cartella con **File > Apri cartella progetto**.
2. Avviare il debug: l'esecuzione deve partire da `main.ddf`.
3. Al primo avvio il valore restituito atteso è `419`; a ogni nuova
   esecuzione nella stessa istanza il valore cresce di uno perché
   `completedRuns` è dichiarata `state`.

## Percorso di debug consigliato

Impostare breakpoint nelle righe seguenti:

- `main.ddf`: chiamata a `safeDivide`;
- `calculations.ddf`: prima istruzione di `safeDivide` e corpo del `for`;
- `text-and-records.ddf`: chiamata a `toInt`;
- `classification.ddf`: `select(priority)`;
- `arrays.ddf`: assegnazione a `values[2]` e corpo del `while`.

Usare F11 per entrare nelle funzioni esterne, F10 per avanzare e Shift+F11
per tornare al chiamante. Le schede devono cambiare automaticamente seguendo
il frame corrente. Aggiungere a Watch, in base al frame:

```text
completedRuns
division.status
division.value
record.label
values[0]
values[2]
index
total
```

## Laboratorio degli errori runtime

In `main.ddf`, cambiare temporaneamente `enableRuntimeErrorLab` in `true` e
scegliere un solo valore per `errorScenario`:

- `1`: divisione per zero, errore tipizzato `DDF404`;
- `2`: indice array fuori intervallo, errore tipizzato `DDF405`;
- `3`: conversione non valida, errore tipizzato `DDF408`.

Mettere un breakpoint dentro `runRuntimeErrorScenario`, entrare con F11 e
verificare file, riga e stack diagnostico. Questi errori sono intenzionali e
interrompono l'esecuzione: finché i costrutti DDF 0.14 non saranno
implementati, il runtime li propaga ma il codice non può ancora recuperarli.
Al termine ripristinare `enableRuntimeErrorLab` a `false`.
