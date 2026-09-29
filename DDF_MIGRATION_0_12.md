# Migrazione dei sorgenti beta a DDF 0.12

Questa guida raccoglie le incompatibilità intenzionali introdotte durante la
definizione del linguaggio. Conservare una copia o un commit del sorgente prima
di applicare sostituzioni estese.

Il contratto completo di destinazione è `DDF_LANGUAGE_SPEC.md`; questa guida
descrive soltanto le trasformazioni dai sorgenti beta e non ridefinisce la
semantica.

La classificazione normativa fra migrazioni candidate all'automazione e
trasformazioni che richiedono revisione umana è in
`DDF_LANGUAGE_VERSIONING.md` e `ddf-language-version-policy.json`. Le candidate
sono attualmente **specificate ma non implementate**: questa guida non implica
modifiche automatiche dei file.

## Percorso consigliato

1. creare un branch, commit o backup dell'intero workspace, inclusi manifesti;
2. annotare pacchetto e profilo usati dal progetto beta;
3. aggiornare prima lessico e sintassi, senza formattare per nascondere errori;
4. correggere tipi e semantica una diagnostica alla volta;
5. portare `ddf-project.json` allo schema `1.1` e installare i moduli richiesti;
6. eseguire **Convalida**, quindi Run e Debug sui percorsi significativi;
7. confrontare output, valori restituiti ed effetti host con la versione beta;
8. salvare soltanto dopo avere esaminato il diff completo.

Il formatter rende coerente un sorgente già interpretabile, ma non è un
migratore e non deve essere usato come prova di equivalenza semantica.

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

## Unità, dichiarazioni e blocchi — pacchetto 0.12.1

- al livello principale sono ammesse soltanto direttive legacy, strutture,
  funzioni e dichiarazioni globali; spostare le istruzioni eseguibili in una
  funzione richiamata dall'entry point;
- i blocchi `{ ... }` sono obbligatori per `if`, `else`, `while`, `do` e `for`,
  anche quando contengono una sola istruzione;
- l'istruzione vuota `;` non è un costrutto DDF valido;
- una dichiarazione, assegnazione, chiamata, `ret`, `brk` o `end` termina con `;`;
- `else if` è valido come catena strutturata; un `else` isolato non lo è;
- `brk;` può comparire soltanto dentro il ciclo corrente;
- `ret` termina la funzione, mentre `end;` termina l'intero programma: non sono
  intercambiabili.

Esempio beta con istruzione top-level:

```ddf
int total << calculate();
print(total);              // non valido al livello principale
```

Forma `0.12`:

```ddf
calculate() out int
{
    ret 4;
}

main() out int
{
    int total << calculate();
    print(total);
    ret total;
}
```

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

### Correzioni manuali dei tipi

- sostituire `dict` con una struttura o un'API/modulo esplicitamente tipizzato;
- trasformare `T[a][b]` in una struttura dati progettata intenzionalmente: il
  nucleo non possiede array multidimensionali o array annidati;
- rinominare le dichiarazioni in shadowing e aggiornare soltanto i riferimenti
  risolti a quel simbolo;
- rendere intenzionale ogni conversione `float -> int`: il nucleo non offre un
  cast numerico generale implicito;
- sostituire confronti fra array/strutture con funzioni di confronto dei campi;
- non assegnare a `text[index]`: costruire una nuova stringa tramite API
  disponibili oppure spostare l'operazione in una capability host tipizzata;
- spezzare strutture che si contengono direttamente o indirettamente: i valori
  DDF sono non null e il contenimento ricorsivo non avrebbe un caso base.

## Funzioni, nomi e ritorni — pacchetto 0.12.2

- non esistono overload: ogni funzione visibile ha un nome univoco;
- argomenti e parametri devono avere arità e tipi esatti, salvo `int -> float`;
- ogni percorso raggiungibile di una funzione non `void` deve eseguire `ret`;
- una funzione che restituisce `void` usa `ret;`, senza valore;
- lo shadowing è vietato anche fra parametri, locali e nomi già visibili;
- l'entry point scelto dal manifesto deve essere unico nel modulo applicativo;
- un breakpoint dimenticato non rende raggiungibile una funzione: Run e Debug
  partono dall'entry point e seguono soltanto chiamate reali.

Per una funzione con `ret` presente soltanto nell'`if`, aggiungere un ritorno
nell'`else` o dopo la condizione; non limitarsi a inserire un valore arbitrario
per eliminare `DDF307`.

## Semantica numerica e runtime — pacchetto 0.12.2

- `int` corrisponde a `System.Int32` e l'aritmetica è checked (`DDF417`);
- la divisione fra interi tronca verso zero;
- `float` DDF corrisponde a `System.Double`, ma NaN e infinito non sono risultati
  validi (`DDF418`);
- una potenza con base/esponente interi non accetta un esponente negativo
  (`DDF419`);
- `char` è testo UTF-16 di lunghezza uno, non un numero;
- le condizioni richiedono `bool`: numeri, stringhe e oggetti non hanno
  truthiness;
- indici fuori limite e accessi non validi restano errori runtime, non valori
  predefiniti silenziosi.

Eseguire test di confine su massimo/minimo `int`, divisioni negative, potenze,
indici e conversioni. Un risultato diverso che non produce più eccezione non è
automaticamente una migrazione riuscita.

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

### Da file condivisi a moduli

Non esistono include testuali. Se più file appartengono allo stesso progetto,
elencarli nei `sources` del medesimo manifesto. Se costituiscono una libreria:

1. assegnare al modulo un `id` minuscolo stabile e una versione esatta;
2. dichiarare soltanto l'API pubblica in `exports`;
3. creare il pacchetto `.ddfmod` o installare la cartella completa nello store;
4. aggiungere la coppia esatta `id`/`version` alle `dependencies` del chiamante;
5. correggere gli accessi a simboli privati: le dipendenze transitive non sono
   visibili e non vengono re-esportate automaticamente.

La direttiva `@@'Nome'` (`legacy-library-directive`) è deprecata ma non viene
convertita automaticamente, perché il testo storico non distingue modulo,
libreria standard o capability host.

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

## Pacchetti sintattici personalizzati

Un profilo didattico o familiare può cambiare grafie e presentazione soltanto se
conserva `languageVersion` e intero contratto semantico. Aggiornare insieme
manifesto, EBNF, lexicon, runtime operations, formatter, IntelliSense e
`tests.json`; il loader rifiuta atomicamente adapter, schemi o test incoerenti.

Non copiare il mapping CLR o la libreria standard canonica in un linguaggio con
semantica differente. In quel caso serve un nuovo `languageId`, una versione del
linguaggio propria e adapter motore esplicitamente supportati.

## Registro operativo delle migrazioni

Gli ID seguenti coincidono con `ddf-language-version-policy.json`:

| ID | Trattamento | Istruzione |
| --- | --- | --- |
| `beta-not-equal` | candidata automatica | sostituzione token-aware `!! -> !=` |
| `beta-logical-and` | candidata automatica | sostituzione token-aware `& -> &&` |
| `beta-logical-xor` | candidata automatica | sostituzione token-aware `\|&\| -> ^^` |
| `project-manifest-1-0-to-1-1` | candidata guidata | aggiungere identità/versione/export dopo anteprima |
| `beta-range-operators` | manuale | riscrivere usando entrambi i limiti e confronti espliciti |
| `beta-root-operator` | manuale | verificare dominio e precisione della formula sostitutiva |
| `removed-dict-type` | manuale | progettare struttura, modulo o API tipizzata |
| `multidimensional-arrays` | manuale | progettare una rappresentazione monodimensionale/strutturata |
| `shadowed-declarations` | manuale | rinominare simboli con risoluzione semantica dei riferimenti |

“Candidata” non significa disponibile nell'interfaccia corrente. Ogni futura
applicazione automatica dovrà offrire anteprima e checkpoint e dovrà ignorare
commenti, stringhe e identificatori.

## Diagnostiche utili durante l'aggiornamento

| Famiglia | Azione |
| --- | --- |
| `DDF0xx` / `DDF1xx` / `DDFGxxx` | correggere lessico e struttura prima dei tipi |
| `DDF3xx` | correggere firme, tipi, scope, ritorni e simboli |
| `DDF4xx` | eseguire il percorso e correggere il comportamento runtime |
| `DDFPxxx` | aggiornare forma e dati del manifesto progetto |
| `DDFBxxx` | correggere sorgenti, entry point, export o grafo moduli |
| `DDFLxxx` / `DDFTxxx` | correggere il pacchetto linguistico e i suoi test |

Una Quick Fix è un suggerimento locale, non una prova che il programma abbia
conservato l'intento. Dopo una modifica strutturale rivalidare l'intero workspace.

## Criteri di completamento

La migrazione è completata soltanto quando:

- il progetto seleziona esplicitamente `ddf` `0.12.3` e il manifest è schema
  `1.1` con modulo, sorgenti, entry point, backend e dipendenze esatte;
- Convalida non produce errori lessicali, grammaticali, semantici o di build;
- non restano `dict`, operatori beta, array multidimensionali, shadowing o uso di
  `@@` come include;
- formattare due volte produce lo stesso testo e non trasforma commenti/stringhe;
- Run termina con valore, output ed effetti attesi;
- Debug attraversa entry point, chiamate multi-file e ritorni al chiamante;
- i casi limite numerici e gli indici sono stati esercitati;
- il diff è stato revisionato e il nuovo stato è stato salvato in un checkpoint.

Per confronti ripetibili usare `samples/language-corpus` come riferimento del
linguaggio di destinazione. I programmi dell'applicazione devono comunque avere
test propri per gli effetti host e per il comportamento di dominio.

