# Migrazione dei sorgenti beta a DDF 0.12

Questa guida raccoglie le incompatibilità intenzionali introdotte durante la
definizione del linguaggio. Conservare una copia o un commit del sorgente prima
di applicare sostituzioni estese.

## Operatori — pacchetto 0.12.1

| Forma beta | Forma 0.12 | Nota |
| --- | --- | --- |
| `left !! right` | `left != right` | disuguaglianza |
| `left & right` | `left && right` | AND booleano con corto circuito |
| `left \|&\| right` | `left ^^ right` | XOR booleano; entrambi gli operandi sono valutati |
| `value >><< limit` | nessuna sostituzione diretta | la forma beta era logicamente difettosa; scrivere i due confronti espliciti |
| `value <<>> limit` | nessuna sostituzione diretta | scrivere i confronti espliciti |
| `value ^/ degree` | `value ^ (1.0 / degree)` | soluzione temporanea; una funzione `root` è candidata per la libreria standard |

Gli operatori di intervallo beta accettavano soltanto due operandi, quindi non
potevano esprimere correttamente un valore compreso fra due estremi. Migrare in
modo intenzionale:

```ddf
bool inside << value > minimum && value < maximum;
bool outside << value < minimum || value > maximum;
```

Non eseguire una sostituzione automatica di `&` dentro stringhe o commenti. Il
formatter non modifica la semantica e non migra le vecchie grafie.

## Lessico — pacchetto 0.12.0

- `True` e `False` non sono più letterali: usare `true` e `false`;
- sono ammessi soltanto gli escape `\"`, `\\`, `\n`, `\r`, `\t`;
- i numeri usano cifre ASCII e `.` come separatore decimale;
- le stringhe non possono attraversare una nuova riga.

Le successive incompatibilità della linea 0.12 saranno aggiunte a questo file
insieme alla versione minima del pacchetto che le introduce.

