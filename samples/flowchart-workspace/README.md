# Workspace di prova Flowchart

Aprire questa cartella con **File > Apri cartella progetto** e quindi aprire
`main.ddf`. Il workspace è pensato per verificare insieme:

- navigazione Flowchart verso funzioni nello stesso workspace e in file diversi;
- doppio clic sui nodi di chiamata e menu `ƒ` della palette;
- ritorno alla funzione chiamante tramite `ƒ > Torna indietro` dopo una navigazione
  locale o verso un altro file, anche dopo aver spostato il cursore su un'altra
  funzione del file raggiunto;
- compressione delle sequenze lineari tramite il pulsante `▤` e loro espansione
  con doppio clic sul gruppo;
- puntatore, Pan, zoom, selezione del sorgente ed esportazione JPEG;
- `if` annidati, `for`, `while`, `do while`, `brk`, `ret` ed `end`;
- chiamata ricorsiva controllata, array, struttura, globale e libreria Console;
- breakpoint e Step Into/Over/Out attraverso più file.

Per il debug, inserire un breakpoint sulla chiamata `processScore` in `main.ddf`,
uno dentro il ciclo `do while` di `processing.ddf` e uno dentro
`recursiveBonus` in `utilities.ddf`. Il programma deve terminare restituendo
`301` e stampando il messaggio `Flowchart workspace completed`.
