# Migrazione dei sorgenti beta a DDF 0.12

Questa guida raccoglie le incompatibilità intenzionali introdotte durante la
definizione del linguaggio. Conservare una copia o un commit del sorgente prima
di applicare sostituzioni estese.

Il contratto completo di destinazione è `DDF_LANGUAGE_SPEC.md`; questa guida
descrive soltanto le trasformazioni dai sorgenti beta e non ridefinisce la
semantica.

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

## Tipi e pacchetti — pacchetto 0.12.2.1

- `dict` non è più un tipo primitivo del nucleo;
- `char` non partecipa all'aritmetica e non viene promosso a numero;
- gli array sono monodimensionali;
- non è più ammesso dichiarare un nome già visibile nello scope;
- `float` non viene convertito implicitamente in `int`;
- array e strutture non supportano `==` o `!=`;
- un indice di `string` è leggibile ma non modificabile.

I pacchetti personalizzati devono portare `types.json` allo schema `1.1` e
aggiungere la sezione obbligatoria `clrMapping` conforme a
`language-types.schema.json`. Il mapping
`dotnet-roslyn-v1` non abilita compilazione o accesso alle API .NET: descrive
soltanto la rappresentazione futura dei tipi e viene validato anche quando il
pacchetto è usato dalla VM. Copiare la sezione dal pacchetto DDF `0.12.2.1` è
corretto soltanto per profili sintattici che conservano esattamente la stessa
semantica dei tipi.

## Manifesti progetto — schema 1.1 / DDF 0.12.3

Ogni `ddf-project.json` schema `1.0` deve passare a `1.1` e aggiungere
l'identità esplicita del modulo:

```json
"module": {
  "id": "azienda.prodotto.modulo",
  "version": "1.0.0",
  "exports": []
}
```

Inserire in `exports` soltanto funzioni, strutture o globali top-level realmente
dichiarate. Un elenco vuoto conserva un progetto applicativo senza API pubblica.
Le cartelle non diventano namespace. Le dipendenze restano dichiarate nel
manifesto e `@@'...'` non deve essere usato per includere file o moduli.

Le dipendenze sorgente usate dal resolver predefinito vanno installate come
cartelle complete in `.ddf/modules/<id>/<version>`. Ogni cartella contiene il
proprio `ddf-project.json`; ID e versione devono coincidere esattamente con la
dipendenza dichiarata. Copiare semplicemente un `.ddf` accanto al chiamante lo
rende parte del modulo applicativo, non una libreria separata.

## Libreria standard — pacchetto 0.12.3

I pacchetti personalizzati devono portare `standard-library.json` allo schema
`1.1` e all'adapter `ddf-standard-library-v3`, dichiarando `id`, `version` e,
per ogni funzione, `effect` ed `errorCodes`. I profili sintattici equivalenti
possono copiare il catalogo `ddf.standard@0.12.3`; pacchetti con binding o
semantica diversi richiedono invece un adapter motore supportato.

`length` conta unità UTF-16, `toInt` produce `System.Int32`, `toFloat` produce
`System.Double` finito e conversioni/input non disponibili usano `DDF408`.
L'aggiornamento non espone API .NET e non trasforma la libreria standard in un
modulo o in una capability host.

