# Sistema dei tipi e semantica DDF 0.13

Stato: contratto normativo aggiornato dal pacchetto DDF `0.13.0`.

Questa specifica di area è consolidata dalla specifica autonoma
`DDF_LANGUAGE_SPEC.md`, che prevale in caso di divergenza accidentale.

Questo documento definisce il significato dei tipi e delle operazioni del
profilo DDF canonico. I profili sintattici equivalenti possono cambiare la
grafia, ma non tipi, conversioni, risultati, errori o ordine di valutazione.
Le parole **deve**, **non deve** e **può** hanno valore normativo.

La specifica descrive il comportamento da raggiungere. La sezione
«Gap di conformità» elenca con precisione ciò che l'implementazione beta deve
ancora allineare; la presenza di un comportamento nella VM corrente non lo
rende parte del linguaggio.

## Principi

- DDF è staticamente e nominalmente tipizzato.
- Il tipo di variabili, campi, parametri e risultati è sempre scritto: il nucleo
  0.12 non introduce `var` né inferenza delle dichiarazioni.
- Non esistono conversioni implicite che possano perdere informazione.
- Non esistono truthiness, `null`, puntatori, cast arbitrari o lookup dinamico.
- Ogni errore numerico o di accesso deve produrre una diagnostica DDF stabile,
  non un'eccezione .NET visibile allo script.
- VM, debugger e futuro backend Roslyn devono osservare la stessa semantica.
- Ogni tipo eseguibile deve possedere una rappresentazione CLR/C# esplicita e
  verificabile da Roslyn; un pacchetto sintattico non può inventare tipi privi
  di tale mapping.

## Tipi primitivi

| Tipo | Modello normativo | Valore iniziale | Operazioni principali |
| --- | --- | --- | --- |
| `int` | intero con segno a 32 bit, complemento a due | `0` | aritmetica e confronti ordinati |
| `float` | IEEE 754 binary64 finito | `0.0` | aritmetica e confronti ordinati |
| `char` | una code unit UTF-16 a 16 bit | `U+0000` | uguaglianza; indice di `string` |
| `bool` | `true` oppure `false` | `false` | `!`, `&&`, `||`, `^^`, uguaglianza |
| `string` | sequenza immutabile di code unit UTF-16 | `""` | uguaglianza e indice in sola lettura |
| `void` | assenza di risultato | nessuno | solo tipo restituito |

`void` non è dichiarabile come variabile, campo, parametro o elemento di array.
Una `string` non è nulla e non è modificabile per indice. L'indice restituisce
un `char`; un indice negativo o maggiore o uguale alla lunghezza è un errore di
runtime. La scelta UTF-16 è intenzionale per l'interoperabilità .NET: un singolo
carattere Unicode fuori dal Basic Multilingual Plane occupa due `char`.

Il nucleo 0.12 non contiene `dict`. Il nome storico viene rimosso dal catalogo
dei tipi: un dizionario senza tipi di chiave e valore non sarebbe verificabile.
Una futura collezione associativa richiederà sintassi parametrica, iterazione e
semantica d'uguaglianza proprie prima di essere ammessa.

## Compatibilità .NET e Roslyn

Il backend principale genera C# dalla IR e lo compila tramite Roslyn. Il mapping
normativo del nucleo è:

| Tipo DDF | Tipo CLR | Forma C# generata | Nota |
| --- | --- | --- | --- |
| `int` | `System.Int32` | `int` | operazioni emesse in contesto `checked` |
| `float` | `System.Double` | `double` | non corrisponde a `System.Single`/`float` di C# |
| `char` | `System.Char` | `char` | una code unit UTF-16 |
| `bool` | `System.Boolean` | `bool` | nessuna conversione numerica |
| `string` | `System.String` | `string` | valore DDF sempre non nullo |
| `void` | `System.Void` | `void` | ammesso soltanto come risultato |
| `T[n]` | `T[]` SZArray | `T[]` | lunghezza controllata dalla semantica DDF |
| struttura DDF | classe CLR generata | `sealed class` interna | conserva identità condivisa e non-null |

Il futuro tipo integrato `error`, accettato per DDF `0.14` ma non ancora
attivo, sarà un valore immutabile backend-neutral e non una `System.Exception`.
Le identità qualificate, per esempio `operation.invalidInput`, selezioneranno
esattamente un errore recuperabile; non introdurranno ereditarietà o cast CLR.
Il contratto di progetto è `ddf-error-handling-design.json`.

La parola DDF `struct` non impone quindi `System.ValueType`: descrive un record
nominale DDF con semantica di riferimento. Questa scelta evita che VM e backend
divergano per copie implicite dei valori composti.

La compatibilità Roslyn è un vincolo necessario ma non sufficiente. Il codice
generato deve aggiungere i controlli DDF per overflow, valori floating non
finiti, bounds, non-null e capacità host; non può adottare silenziosamente il
comportamento C# quando è diverso. Le stringhe e i nomi generati devono usare
confronti ordinali coerenti con la specifica.

Un futuro tipo aggiunto dal nucleo o da un'estensione eseguibile deve dichiarare
un mapping backend versionato verso un tipo CLR accessibile a Roslyn e un adapter
per VM, host, debugger e serializzazione. In assenza di tutti questi elementi il
pacchetto può offrire soltanto una diversa grafia di tipi DDF esistenti e deve
essere rifiutato se dichiara un nuovo tipo. Nessun mapping autorizza reflection,
caricamento di assembly o accesso a membri non esposti dal catalogo host.

## Letterali

- Un intero senza punto ha tipo `int` e deve rientrare nell'intervallo
  `-2147483648..2147483647`, tenendo conto dell'eventuale negazione prefissa.
- Un numero con punto ha tipo `float`; deve essere finito e rappresentabile.
- `true` e `false` hanno tipo `bool` e valore dichiarato dal profilo equivalente.
- Una stringa tra doppi apici ha tipo `string` ed usa gli escape della specifica
  lessicale.
- Il nucleo non possiede ancora una grafia separata per i letterali `char`.
  Soltanto l'inizializzazione di un `char` può usare una stringa che, dopo la
  decodifica degli escape, contiene esattamente una code unit UTF-16.

Quest'ultima è una conversione contestuale, non rende `string` convertibile in
`char` nelle assegnazioni, nei parametri o nei ritorni.

Il disegno futuro `DDF-0034`, non ancora attivo, aggiunge `long` signed 64 bit
checked e `decimal` base 10 esatto con coefficiente a 96 bit e scala `0..28`.
Le sole nuove promozioni implicite saranno `int -> long`, `int -> decimal` e
`long -> decimal`; ogni passaggio con `float` richiederà una funzione esplicita.
La IR userà primitive DDF e non tipi CLR o Roslyn.

Il disegno futuro `DDF-0035`, non ancora attivo, conserva `string` come valore
non nullo e immutabile. DDF `0.20` aggiungerà `string + string`, operazioni
ordinali su code unit UTF-16, dati Unicode `15.1` versionati, ricerca con
risultato `maybe int`, split/join basati su `sequence<string>` e formattazione
di soli argomenti già convertiti. Nessun valore viene convertito implicitamente
in testo e la IR non espone operazioni CLR. Il contratto è
`ddf-string-library-design.json`.

Il disegno futuro `DDF-0036`, non ancora attivo, associa a ogni funzione
matematica firme DDF non sovraccaricate e risultati dello stesso tipo dichiarato.
Valore assoluto, min/max, clamp e arrotondamento coprono le famiglie numeriche
ammesse; radice, potenza generale e trascendenti operano su `float`. Domini,
zeri con segno e risultati non finiti sono normativi. `long` e `decimal`
richiedono prima DDF `0.19`; nessun tipo o overload CLR entra nella IR. Il
contratto è `ddf-math-library-design.json`.

Il disegno futuro `DDF-0037`, non ancora attivo, aggiunge i tipi nominalmente
distinti `date`, `time`, `datetime`, `instant` e `duration`. Nessuna conversione
implicita trasforma tempo civile in istante UTC; offset, range e precisione sono
parte del tipo e non del backend CLR. Le primitive IR conservano tick e identità
DDF, mentre clock e random sono effetti forniti da provider sostituibili. Il
contratto è `ddf-temporal-random-design.json`.

## Tipi composti

### Array

La forma 0.12 è `T[dimensione]`. Un array:

- è monodimensionale, di lunghezza fissa e non nullo;
- richiede una dimensione `int` non negativa, valutata una sola volta quando la
  dichiarazione viene eseguita;
- inizializza ricorsivamente ogni elemento al valore iniziale di `T`;
- è invariante: `A[]` è compatibile soltanto con `A[]`;
- consente lettura e scrittura per indice entro i limiti;
- ha identità condivisa: assegnazione e passaggio a funzione condividono lo
  stesso array, non ne creano una copia.

I suffissi concatenati riconosciuti dalla grammatica beta sono riservati e non
validi nel nucleo 0.12. Gli array multidimensionali o jagged richiederanno una
decisione successiva.

La futura iterazione `each`, definita ma non ancora attiva da `DDF-0032`, userà
il tipo elemento esatto dell'array e uno snapshot superficiale in ordine di
indice. Non renderà l'array implicitamente convertibile a un enumeratore CLR e
non introdurrà write-back attraverso il binding del ciclo.

Le future collezioni definite da `DDF-0033` sono famiglie di tipo integrate e
chiuse: `sequence<T>`, `map<K,V>`, `view<T>` ed `entry<K,V>`. Non costituiscono
un sistema di generici universali. Sequenze e mappe avranno identità condivisa e
ordine stabile; le viste saranno snapshot host in sola lettura. La loro sintassi
non è ancora valida nel DDF `0.13`.

### Strutture

Una struttura è un tipo nominale: due strutture con campi uguali ma nomi diversi
restano incompatibili. Contiene soltanto campi dichiarati, tutti inizializzati
quando viene creata l'istanza. Non possiede metodi, costruttori, ereditarietà o
campi aggiunti dinamicamente.

Le istanze sono riferimenti gestiti, sempre non nulli. Assegnazione e passaggio
a funzione condividono l'istanza; modificare un campo attraverso un alias rende
la modifica visibile agli altri alias. Riassegnare il parametro non riassegna la
variabile del chiamante.

Il grafo di contenimento fra strutture deve essere aciclico, considerando anche
i campi array: con valori sempre non nulli, una struttura che contiene se stessa
direttamente o indirettamente non avrebbe una costruzione finita. La ricorsione
dei dati richiederà in futuro un tipo opzionale o un riferimento esplicito.

### Enum nominali

`enum Nome { ValoreA, ValoreB }` dichiara un tipo nominale chiuso. I valori si
usano sempre in forma qualificata (`Nome.ValoreA`), sono immutabili e non sono
convertibili implicitamente in `int`, `string` o in un altro enum, anche quando
i nomi dei membri coincidono. Sono ammessi assegnazione, passaggio, ritorno e
confronto di uguaglianza fra valori dello stesso enum; gli operatori numerici e
relazionali sono vietati. Un enum deve contenere almeno un valore e il primo è
il valore iniziale deterministico.

### Selezione multipla

L'espressione di `select` determina il tipo di tutti i casi `when`. Ogni caso è
una espressione costante deterministica compatibile per uguaglianza con quel
tipo; casi incompatibili o duplicati sono errori. `otherwise` rende completa la
selezione e deve essere unico e ultimo. In sua assenza il type checker accetta
soltanto una copertura completa dei due valori `bool` o di tutti i membri dello
stesso enum nominale. Non viene applicata alcuna conversione dell'enum verso un
tipo numerico o testuale.

## Compatibilità e conversioni

L'identità di tipo è sempre ammessa. L'unica conversione implicita generale è:

```text
int -> float
```

Essa è applicata a inizializzazioni, assegnazioni, argomenti e ritorni prima di
memorizzare il valore. `char` non è numerico e non viene convertito in `int` o
`float`. `bool` non è un numero. Non esistono conversioni implicite da o verso
`string`, salvo l'inizializzatore `char` descritto sopra.

Le conversioni che possono fallire o perdere informazione saranno funzioni
esplicite e tipizzate della libreria standard (`toInt`, `toFloat` e analoghe),
definite nella `0.12.3`; non sono cast del linguaggio.

## Dichiarazioni, inizializzazione e mutabilità

Variabili, parametri e campi ordinari sono mutabili. Una dichiarazione senza
inizializzatore riceve il valore iniziale deterministico del tipo; non esiste uno
stato «non inizializzato». Gli enum assumono il primo valore dichiarato. Array e strutture vengono costruiti ricorsivamente e
non possono contenere riferimenti nulli.

`fixed T nome << espressione;` dichiara invece una costante tipizzata locale o
di modulo. L'inizializzatore è obbligatorio e il tipo deve essere primitivo
scalare oppure un enum nominale. Sono ammesse soltanto espressioni pure e
deterministiche composte da letterali, valori enum qualificati, parentesi,
operatori unari/binari e altre costanti già visibili; chiamate, output,
assegnazioni, accessi a campi e indici non sono ammessi. Un valore `fixed` non
può essere assegnato, incrementato o decrementato.
Il valutatore costante tipizzato materializza il valore nella IR comune prima
della VM e di Roslyn. Risolve dipendenze in avanti nello stesso modulo e valori
`fixed` esportati da dipendenze dirette; `DDF318` segnala i cicli e `DDF319`
overflow checked, divisione per zero o risultati `float` non finiti.
I valori enum sono materializzati con identità di tipo e membro, senza
conversione implicita a intero o dipendenza dal valore sottostante CLR.

`state T nome << espressione;` dichiara uno stato mutabile di modulo associato
all'istanza di esecuzione. La prima verticale richiede un tipo primitivo scalare
e l'inizializzatore. Senza `DdfStateStore` esplicito l'esecuzione usa uno store
effimero nuovo; riusare lo stesso store conserva il valore tra invocazioni,
mentre `Reset()` ripristina l'inizializzazione alla chiamata successiva. Lo store
non persiste su disco e impedisce l'uso concorrente della medesima istanza.
`DDF320` rifiuta campi e locali, `DDF321` l'inizializzatore mancante e `DDF322`
i tipi composti. Snapshot persistenti, migrazioni e serializzazione host restano
fuori dal contratto di questa verticale.

Nel servizio Editor SDK lo store committed è distinto per istanza e condiviso
fra Run e Debug. Ogni invocazione usa una copia: soltanto un'esecuzione
`HostEffects` riuscita la conferma atomicamente; Simulation, errore e
cancellazione la eliminano. Snapshot e reset sono espliciti e non implicano I/O.
Durante una pausa del debugger lo `state` è osservabile in sola lettura come
categoria distinta da globali e locali. Il valore appartiene alla transazione
del run sospeso; lo snapshot committed dell'istanza resta separato.
La persistenza opzionale usa uno snapshot SDK schema `1.0` vincolato all'identità
e alla versione del pacchetto linguaggio. Un ripristino incompatibile richiede
una migrazione esplicita dell'host e non modifica parzialmente lo store corrente.

Le destinazioni modificabili sono esclusivamente:

- il nome di una variabile o di un parametro;
- un elemento di array DDF modificabile;
- un campo DDF o una proprietà host esplicitamente esposta in scrittura.

L'assegnazione `destinazione << espressione` risolve prima la destinazione,
valuta poi l'espressione una sola volta, converte il risultato al tipo della
destinazione, lo memorizza e restituisce quel valore convertito. Questo permette
assegnazioni concatenate senza cambiare tipo alla cella dichiarata.

`++` e `--` richiedono una destinazione modificabile di tipo `int` o `float`,
applicano le stesse regole numeriche e restituiscono il valore precedente.

## Scope, nomi e durata

Esistono namespace distinti per tipi, funzioni e valori. I campi appartengono al
namespace della propria struttura. I confronti dei nomi sono case-sensitive.

Le funzioni sono sempre dichiarazioni top-level appartenenti a un modulo. Non
possiedono un ricevitore implicito, non sono membri di una struttura e non
richiedono un'istanza: sotto questo unico aspetto svolgono già il ruolo delle
funzioni statiche di altri linguaggi. DDF non introduce il modificatore
`static`; locali e parametri continuano comunque ad avere una nuova durata per
ogni chiamata, mentre soltanto `state` conserva dati nell'istanza esplicita.

- Un parametro è visibile nell'intero corpo della funzione.
- Un locale è visibile dalla dichiarazione alla fine del blocco che lo contiene.
- L'inizializzatore di un `for` ha uno scope che comprende condizione,
  incremento e corpo del ciclo.
- Un blocco annidato può leggere e modificare i nomi visibili esterni.
- Non è consentito dichiarare un valore con lo stesso nome di un altro valore
  già visibile. Parametri e globali non possono quindi essere nascosti da locali.
- Funzioni e strutture devono avere nomi univoci nel progetto; non esistono
  overload.

Locali e parametri vivono per la durata della chiamata e del relativo scope.
Array e strutture restano vivi finché sono raggiungibili da un valore DDF o da
un adapter host. La durata e l'ordine di inizializzazione dei globali multi-file
sono definiti dalla milestone moduli `0.12.3`.

## Espressioni e ordine di valutazione

Gli operandi sono valutati da sinistra a destra, una sola volta. Fanno eccezione
`&&` e `||`, che non valutano l'operando destro quando il risultato è già noto.
`^^` valuta sempre entrambi gli operandi. La destinazione di un'assegnazione è
risolta prima del valore destro.

Non esiste verità implicita: condizioni di `if`, `while`, `do while` e `for`
devono avere tipo `bool`.

### Aritmetica

- `+`, `-`, `*`, `/`, `^`, `+` e `-` unari accettano soltanto `int` e `float`.
- Due operandi `int` producono `int`; se almeno un operando è `float`, il
  risultato è `float` e l'altro viene promosso.
- La divisione fra `int` tronca verso zero. La divisione per zero è sempre un
  errore di runtime, anche per `float`.
- La potenza fra `int` richiede esponente non negativo e risultato `int`
  rappresentabile. Negli altri casi segue `float`.
- Overflow o underflow di `int` è un errore di runtime; non avviene wrap-around.
- Un risultato `float` NaN o infinito è un errore di runtime. L'underflow verso
  zero è ammesso.

### Confronto e uguaglianza

`<`, `<=`, `>` e `>=` accettano tipi numerici; l'eventuale `int` viene promosso
a `float`. `==` e `!=` accettano:

- due valori dello stesso tipo primitivo;
- una coppia `int`/`float`, dopo promozione dell'intero.

L'uguaglianza di `string` confronta ordinalmente le code unit UTF-16. Array e
strutture non supportano `==` o `!=`: identità e uguaglianza strutturale non
devono essere confuse. Eventuali confronti profondi saranno funzioni esplicite.

Gli operatori booleani accettano soltanto `bool`. `+` non concatena stringhe nel
nucleo: formattazione e composizione testuale appartengono alla libreria.

## Funzioni

La futura verticale DDF `0.23` aggiungerà tipi risorsa nominali e opachi soltanto
per il binding lessicale `use` definito da `DDF-0041`. Tali handle non saranno
copiabili, confrontabili, serializzabili, persistibili o ammessi come globali,
parametri, risultati, campi ed elementi. Le operazioni potranno riceverli solo
come prestiti dichiarati e non trattenibili. Nessun tipo CLR o handle nativo
entrerà nel sistema dei tipi DDF. Questa categoria è inattiva nella `0.13`.

L'asincronia cooperativa `DDF-0042` non introduce `Task<T>`, future, promise o
tipi CLR nel sistema DDF. Una funzione conserva il proprio tipo di risultato e
il descrittore dichiara separatamente l'effetto sospensivo. I valori catturati
dalla continuazione devono già essere rappresentabili dalla IR DDF; lease e
risorse restano opachi e governati dalle rispettive regole di ownership.

Una firma comprende nome, parametri nell'ordine dichiarato e tipo restituito.
Il nome è unico nell'intero progetto: overload, parametri opzionali, variadici e
generici non fanno parte del nucleo 0.12.

- Ogni chiamata deve fornire esattamente un argomento per parametro.
- Gli argomenti sono valutati da sinistra a destra e convertiti al tipo del
  parametro prima di entrare nella funzione.
- Primitivi e stringhe sono passati per valore; array e strutture condividono il
  riferimento gestito descritto sopra.
- Un parametro può essere riassegnato localmente senza cambiare la variabile del
  chiamante.
- Una funzione `void` può usare soltanto `ret;`.
- Una funzione non `void` deve usare `ret espressione;` su ogni percorso
  raggiungibile; il valore viene convertito al tipo restituito.
- Raggiungere la fine di una funzione non `void` è un errore semantico, non un
  ritorno del valore iniziale.

La funzione `main` è unica e resta il punto di ingresso del programma
standalone. Il suo contratto completo con moduli e processo è chiuso nella
`0.12.3`.

## Errori semantici e runtime

Il compilatore deve rifiutare prima dell'esecuzione almeno: tipo sconosciuto,
conversione non ammessa, operatore non applicabile, destinazione non
modificabile, nome duplicato o nascosto, indice non `int`, firma non rispettata,
ritorno errato e percorso non restituente.

Il runtime deve controllare almeno: overflow intero, risultato floating non
finito, divisione per zero, indice fuori intervallo, dimensione array negativa,
violazione di capacità host e limite di esecuzione. Gli errori devono indicare
codice diagnostico, documento e intervallo sorgente quando disponibili.

La VM di riferimento assegna codici stabili ai nuovi errori numerici: `DDF417`
per overflow `int`, `DDF418` per un risultato `float` NaN o infinito e `DDF419`
per una potenza intera con esponente negativo. La divisione per zero conserva
`DDF404`. Questi codici fanno parte del corpus differenziale destinato anche al
backend Roslyn.

Il tipo interno di errore usato dal checker è compatibile con gli altri tipi
soltanto per evitare diagnostiche a cascata; non è un tipo dichiarabile né un
valore eseguibile.

## Corrispondenza con la IR sicura

| Costrutto | Rappresentazione/operazione richiesta | Vincolo |
| --- | --- | --- |
| dichiarazione | `DdfIrVariableDeclaration` tipizzata | tipo risolto e valore iniziale |
| assegnazione | `Assign` | l-value valido e conversione prima dello store |
| aritmetica | ID numerici della VM | controllo overflow/non-finite |
| logica | ID booleani, short-circuit per AND/OR | nessuna truthiness |
| chiamata DDF | `DdfIrCall` verso firma unica | arità e tipi esatti dopo promozione |
| indice | `DdfIrIndex` | `int`, bounds check, array o stringa |
| membro | `DdfIrMemberAccess` | campo nominale o capacità host registrata |
| ritorno | `DdfIrReturn` | compatibile con la firma e completo sui percorsi |
| API host | catalogo e adapter tipizzati | deny-by-default, nessuna reflection implicita |

Non sono rappresentabili nel nucleo: puntatori, memoria non gestita, cast
arbitrari, `null`, reflection, concorrenza, eccezioni utente, overload, generici,
array multidimensionali e dizionari dinamici.

## Gap di conformità dell'implementazione beta

Questi punti devono essere corretti prima di dichiarare completata la `0.12.2`:

1. rimuovere `dict` dai tipi primitivi e rifiutarlo nel corpus;
2. rendere `char` non numerico ed eliminare la conversione `char -> float`;
3. limitare gli array al rango uno e diagnosticare i suffissi concatenati;
4. applicare la conversione `int -> float` anche al valore realmente memorizzato;
5. usare aritmetica intera checked senza passaggio intermedio da `double`;
6. diagnosticare risultati floating non finiti e potenze intere non valide;
7. vietare shadowing e duplicati invece di sovrascrivere le tabelle dei simboli;
8. verificare che ogni percorso di una funzione non `void` restituisca;
9. vietare uguaglianza di array e strutture invece del confronto di riferimento;
10. rifiutare l'uso dell'indice di `string` come destinazione modificabile;
11. diagnosticare cicli diretti o indiretti nel contenimento delle strutture;
12. estendere il contratto dei tipi con mapping CLR/Roslyn versionati e
    validarli durante il caricamento del pacchetto;
13. aggiungere un corpus normativo condiviso da checker, VM e futuro backend.

I tredici gap sono ora coperti per pacchetto, checker, servizi IDE e VM. Il
mapping `dotnet-roslyn-v1` è versionato e validato in `types.json`; il manifesto
del corpus registra diagnostiche, tipo CLR, valore o errore runtime atteso e
identifica la VM come oracolo. Il confronto con assembly Roslyn diventerà
eseguibile quando esisterà il backend verticale della `0.13`; fino ad allora la
specifica, non eventuali comportamenti beta residui, governa la migrazione.
