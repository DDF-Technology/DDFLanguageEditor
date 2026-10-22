# Specifica autonoma del linguaggio DDF 0.13

Stato: **normativa** per il linguaggio DDF `0.13`, distribuito dal pacchetto
canonico `ddf` versione `0.13.0`.

Questo documento definisce in modo autosufficiente il nucleo del linguaggio
DDF. Non è necessario consultare bozze storiche, codice dell'editor o altri
documenti per stabilire se un programma appartiene al nucleo `0.13` e quale
comportamento deve avere. I file JSON del pacchetto sono la rappresentazione
eseguibile dello stesso contratto, non una fonte di semantica alternativa.

Tutto il testo è normativo salvo le sezioni marcate **non normativa**. Le parole
«deve», «non deve» e «può» esprimono rispettivamente obbligo, divieto e facoltà.

## 1. Scopo e conformità

DDF è un linguaggio imperativo, staticamente tipizzato, case-sensitive e
orientato a programmi modulari. Il linguaggio `0.13` definisce:

- testo sorgente e token;
- unità di compilazione e grammatica canonica;
- tipi, conversioni, scope e ordine di valutazione;
- funzioni e controllo di flusso;
- moduli sorgente, esportazioni e dipendenze esatte;
- semantica della VM di riferimento;
- libreria standard minima `ddf.standard@0.12.3`;
- confini fra linguaggio, applicazione host e futuro backend Roslyn.

Un'implementazione è conforme quando accetta ogni programma valido descritto
qui, rifiuta ogni violazione normativa prima dell'esecuzione quando richiesto e
produce gli stessi risultati o errori osservabili. Un parser può costruire un
albero parziale per assistere la digitazione, ma il recupero sintattico non rende
valido il programma. Editor, VM e futuro backend compilato devono condividere la
stessa semantica.

Non appartengono al nucleo: `null`, puntatori, eccezioni utente, concorrenza,
overload, generici, inferenza `var`, cast arbitrari, dizionari, array
multidimensionali, ereditarietà, metodi di struttura, reflection e accesso
implicito a .NET.

## 2. Testo sorgente e token

### 2.1 Codifica e confronto

Un sorgente DDF è testo UTF-8. Sono accettate terminazioni LF e CRLF. Gli spazi
Unicode separano i token. Identificatori e keyword vengono confrontati per valore
Unicode esatto, senza normalizzazione e distinguendo maiuscole e minuscole.

Quando più token possono iniziare nella stessa posizione, viene scelto quello
con la grafia più lunga.

### 2.2 Identificatori

Un identificatore inizia con una lettera Unicode oppure `_`. I caratteri
successivi possono essere lettere Unicode, cifre Unicode oppure `_`.

```ebnf
identifier = ( unicode-letter | "_" ),
             { unicode-letter | unicode-digit | "_" } ;
```

Un identificatore non può coincidere esattamente con una keyword o con un
letterale booleano canonico.

### 2.3 Keyword e punteggiatura

Le keyword canoniche sono:

| Categoria | Keyword |
| --- | --- |
| tipi | `int`, `float`, `char`, `bool`, `void`, `string` |
| dichiarazioni | `struct`, `out`, `fixed`, `state` |
| controllo | `if`, `else`, `while`, `do`, `for` |
| uscita | `ret`, `brk`, `continue`, `end` |
| booleani | `true`, `false` |

`dict` non è una keyword né un tipo del nucleo. La punteggiatura è
`{ } ( ) [ ] . , ;`.

### 2.4 Numeri

I numeri usano soltanto cifre ASCII. Un intero non contiene il segno; `-2` è la
negazione prefissa di `2`. Un decimale contiene `.` e almeno una cifra dopo il
punto.

```ebnf
integer-literal = digit, { digit } ;
decimal-literal = digit, { digit }, ".", digit, { digit } ;
digit = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
```

Basi alternative, separatori, suffissi ed esponenti non appartengono alla
grafia numerica del nucleo.

### 2.5 Booleani, stringhe e caratteri

Gli unici letterali booleani sono `true` e `false`. Una stringa è delimitata da
doppi apici, non attraversa una fine riga e ammette soltanto gli escape `\"`,
`\\`, `\n`, `\r` e `\t`.

Il nucleo non possiede una grafia autonoma per un letterale `char`. Soltanto
l'inizializzatore di una variabile o campo `char` può usare una stringa che,
dopo la decodifica, contiene esattamente una code unit UTF-16.

### 2.6 Commenti

`//` introduce un commento fino alla fine della riga. `/*` apre un commento a
blocchi chiuso dal primo `*/`; i commenti a blocchi non sono annidati. `///` è
un commento di riga che gli strumenti possono usare come documentazione.

### 2.7 Direttiva legacy

`@@'Nome'` è riconosciuta su una sola riga esclusivamente per compatibilità. È
una richiesta legacy deprecata di libreria o capacità: non include testo, non
importa file o moduli e non concede autorizzazioni. Le dipendenze reali sono nel
manifesto del modulo.

## 3. Grammatica sintattica

La notazione usa `[]` per una parte opzionale, `{}` per la ripetizione e
letterali fra doppi apici. I simboli fra parentesi angolari sono token prodotti
dal lessico.

<!-- ddf-canonical-grammar:start -->
```ebnf
compilation-unit = { library-directive }, { member } ;
member = struct-declaration | enum-declaration | function-declaration | variable-declaration ;
library-directive = <LibraryDirective> ;

struct-declaration = "struct", identifier, block ;
enum-declaration = "enum", identifier, "{", enum-member, { ",", enum-member }, [ "," ], "}" ;
enum-member = identifier ;
function-declaration = identifier, "(", [ parameter-list ], ")",
                       "out", return-type, block ;
return-type = type ;
parameter-list = parameter, { ",", parameter } ;
parameter = type, identifier ;

statement = block | if-statement | select-statement | while-statement | do-while-statement |
            for-statement | return-statement | break-statement | continue-statement |
            end-statement | variable-declaration | expression-statement ;
block = "{", { statement }, "}" ;
if-statement = "if", "(", expression, ")", block,
               [ "else", ( block | if-statement ) ] ;
select-statement = "select", "(", expression, ")", "{",
                   { when-clause }, [ otherwise-clause ], "}" ;
when-clause = "when", expression, block ;
otherwise-clause = "otherwise", block ;
while-statement = "while", "(", expression, ")", block ;
do-while-statement = "do", block, "while", "(", expression, ")", ";" ;
for-statement = "for", "(",
                ( variable-declaration | [ expression ], ";" ),
                [ expression ], ";", [ expression ], ")", block ;
return-statement = "ret", [ expression ], ";" ;
break-statement = "brk", ";" ;
continue-statement = "continue", ";" ;
end-statement = "end", ";" ;
variable-declaration = [ "fixed" | "state" ], type, identifier,
                       [ "<<", expression ], ";" ;
expression-statement = expression, ";" ;

expression = assignment-expression ;
assignment-expression = logical-or-expression,
                        [ ( "<<" | ">>" ), assignment-expression ] ;
logical-or-expression = logical-xor-expression,
                        { "||", logical-xor-expression } ;
logical-xor-expression = logical-and-expression,
                         { "^^", logical-and-expression } ;
logical-and-expression = equality-expression,
                         { "&&", equality-expression } ;
equality-expression = relational-expression,
                      { ( "==" | "!=" ), relational-expression } ;
relational-expression = additive-expression,
                        { ( "<" | "<=" | ">" | ">=" ), additive-expression } ;
additive-expression = multiplicative-expression,
                      { ( "+" | "-" ), multiplicative-expression } ;
multiplicative-expression = power-expression,
                            { ( "*" | "/" ), power-expression } ;
power-expression = prefix-expression, [ "^", power-expression ] ;
prefix-expression = { "!" | "+" | "-" }, postfix-expression ;
postfix-expression = primary-expression,
                     { "(", [ argument-list ], ")" |
                       "[", expression, "]" |
                       ".", identifier | "++" | "--" } ;
argument-list = expression, { ",", expression } ;
primary-expression = <NumberLiteral> | <StringLiteral> |
                     <BooleanLiteral> | identifier |
                     "(", expression, ")" ;
type = ( <DataTypeKeyword> | identifier ),
       { "[", [ expression ], "]" } ;
identifier = <Identifier> ;
```

La selezione multipla canonica usa `select`, uno o più rami `when` e un
eventuale ramo finale `otherwise`. Non esiste fall-through implicito: viene
eseguito al massimo un ramo e il valore selezionato viene valutato una volta.
<!-- ddf-canonical-grammar:end -->

La grammatica consente al parser di rappresentare suffissi array incompleti o
ripetuti per produrre diagnostiche precise. Un programma conforme deve però
rispettare le restrizioni semantiche della sezione 5: una dimensione è
obbligatoria e può esistere un solo suffisso array.

Al livello principale sono ammesse soltanto direttive, strutture, `enum`, funzioni e
globali. Gli enum sono nominali, richiedono almeno un valore e si usano in forma
qualificata (`Nome.Valore`), senza conversioni implicite numeriche o testuali.
Le direttive precedono ogni dichiarazione. Un `;` isolato non è
un'istruzione. I corpi di tutti i costrutti di controllo richiedono le graffe;
solo `else if (...) { ... }` può seguire direttamente `else`.

## 4. Dichiarazioni e nomi

Una dichiarazione di valore ha forma `tipo nome;` oppure
`tipo nome << espressione;`. L'inizializzatore usa l'operatore generale di
assegnazione ma viene verificato nel contesto del tipo dichiarato.

Una costante tipizzata ha forma `fixed tipo nome << espressione;`. È ammessa
come valore locale o globale di modulo, richiede sempre l'inizializzatore e
accetta tipi primitivi scalari oppure enum nominali. L'espressione deve essere
deterministica e priva di effetti: letterali, valori enum qualificati, altre
costanti già visibili, parentesi e operatori puri. Chiamate, Output,
assegnazioni, accessi a campi o indici e operatori di mutazione non sono
ammessi. Una costante non può
essere destinazione di `<<`, `++` o `--`.

L'inizializzatore viene valutato e convertito al tipo dichiarato prima dei
backend. Sono ammesse dipendenze in avanti fra costanti dello stesso modulo e
costanti esportate da dipendenze dirette; un ciclo produce `DDF318`, mentre
overflow, divisione per zero e risultato `float` non finito producono `DDF319`.
VM e backend compilato consumano il medesimo valore materializzato nella IR e
non rivalutano l'espressione con semantiche proprie.
Un valore enum materializzato conserva tipo nominale e membro; non viene
convertito nel numero sottostante scelto dal backend CLR.

La keyword `fixed` introduce esclusivamente questa dichiarazione immutabile.

Uno stato di istanza ha forma `state tipo nome << espressione;`. Nella prima
verticale è ammesso soltanto a livello di modulo, richiede un inizializzatore e
accetta tipi primitivi scalari. È mutabile, ma non è globale al processo: ogni
esecuzione senza store esplicito riceve uno stato effimero isolato; più
esecuzioni condividono il valore soltanto se l'host passa lo stesso
`DdfStateStore`. Lo store è process-local, non salva su disco, può essere
azzerato esplicitamente e rifiuta esecuzioni sovrapposte sulla stessa istanza.
La persistenza esterna, la migrazione e i tipi composti non fanno parte di
questa prima verticale.

Durante il debug il valore `state` della transazione sospesa è leggibile in
Watch e Variabili come categoria distinta, ma non è modificabile. Diventa
committed soltanto al completamento riuscito dell'esecuzione con effetti host.
Un host può persistere lo stato committed soltanto attraverso la capability SDK
esplicita. Il linguaggio non definisce percorsi, file o salvataggi automatici;
schema o pacchetto incompatibili richiedono una migrazione dichiarata dall'host.

Una struttura dichiara esclusivamente campi. Non può contenere istruzioni,
funzioni, costruttori o strutture annidate. Una funzione dichiara nome,
parametri ordinati, marcatore `out`, tipo restituito e corpo.

Esistono namespace distinti per tipi, funzioni e valori. I campi appartengono
al namespace della struttura. Tutti i confronti dei nomi sono case-sensitive.
Non esistono overload.

- un parametro è visibile nell'intero corpo della funzione;
- un locale è visibile dalla propria dichiarazione alla fine del blocco;
- l'inizializzatore di un `for` è visibile in condizione, incremento e corpo;
- un blocco annidato vede i nomi esterni;
- un valore non può nascondere un altro valore già visibile;
- funzioni e strutture devono avere nomi univoci nel modulo.

Variabili, parametri e campi ordinari sono mutabili. Una dichiarazione senza
inizializzatore riceve sempre il valore iniziale deterministico del proprio tipo.

## 5. Sistema dei tipi

### 5.1 Tipi primitivi

| Tipo | Dominio | Valore iniziale | Rappresentazione CLR prevista |
| --- | --- | --- | --- |
| `int` | intero con segno a 32 bit | `0` | `System.Int32` |
| `float` | IEEE 754 binary64 finito | `0.0` | `System.Double` |
| `char` | una code unit UTF-16 | `U+0000` | `System.Char` |
| `bool` | `true` o `false` | `false` | `System.Boolean` |
| `string` | sequenza immutabile e non nulla di code unit UTF-16 | `""` | `System.String` |
| `void` | assenza di risultato | nessuno | `System.Void` |

`void` può essere soltanto il risultato di una funzione. Non è valido per
variabili, campi, parametri o elementi di array. `char` non è numerico. `float`
DDF corrisponde a `double` C#, non a `System.Single`.

I numerici estesi non appartengono ancora al contratto eseguibile. Il disegno
accettato in `DDF-0034` e in `ddf-extended-numerics-design.json` punta a DDF
`0.19`: `long` sarà un intero signed 64 bit checked con letterali `i64`, mentre
`decimal` sarà un decimale esatto base 10 con letterali `dec`. Le regole sono
definite da DDF e non da Roslyn; nella `0.13` entrambi i nomi restano
identificatori ordinari.

### 5.2 Array

Un tipo array ha forma `T[n]`, con una sola dimensione `int` non negativa. La
dimensione viene valutata una volta quando si esegue la dichiarazione.

Un array è di lunghezza fissa, non nullo e invariante. Ogni elemento riceve il
valore iniziale di `T`. Lettura e scrittura richiedono un indice `int` compreso
fra zero incluso e lunghezza esclusa. Assegnazione e passaggio a funzione
condividono la stessa identità dell'array. Array jagged o multidimensionali non
sono validi.

Le collezioni dinamiche non appartengono ancora al contratto eseguibile. Il
disegno accettato in `DDF-0033` e in
`ddf-typed-collections-design.json` punta a DDF `0.18` con le famiglie chiuse
`sequence<T>`, `map<K,V>`, `view<T>` ed `entry<K,V>`. Non introduce generici
universali né tipi di collezione CLR nella IR; nel DDF `0.13` questi nomi
restano identificatori ordinari.

### 5.3 Strutture

Una struttura è nominale: due strutture con campi identici ma nomi differenti
sono tipi diversi. Le istanze sono riferimenti gestiti, non nulli, con campi
inizializzati ricorsivamente. Assegnazione e passaggio a funzione condividono
l'istanza.

Il grafo di contenimento fra strutture deve essere aciclico, inclusi i campi
array. Con riferimenti sempre non nulli, un ciclo non avrebbe costruzione finita.

### 5.4 Compatibilità e conversioni

L'identità di tipo è sempre compatibile. L'unica conversione implicita generale
è `int -> float`; si applica prima della memorizzazione in inizializzazioni,
assegnazioni, argomenti, campi e ritorni.

La stringa di una sola code unit usata per inizializzare un `char` è una
conversione contestuale e non abilita assegnazioni, argomenti o ritorni generali
da `string` a `char`. Non esistono altre conversioni implicite. Le conversioni
fallibili sono funzioni della libreria standard, non cast.

## 6. Espressioni e operatori

La precedenza cresce da 1 a 11.

| Livello | Forma | Associatività | Significato |
| --- | --- | --- | --- |
| 1 | `<<` | destra | assegnazione |
| 1 | `>>` | destra | invio alla destinazione Output |
| 2 | `||` | sinistra | OR booleano con corto circuito |
| 3 | `^^` | sinistra | XOR booleano |
| 4 | `&&` | sinistra | AND booleano con corto circuito |
| 5 | `==`, `!=` | sinistra | uguaglianza, disuguaglianza |
| 6 | `<`, `<=`, `>`, `>=` | sinistra | confronto numerico |
| 7 | `+`, `-` | sinistra | somma, sottrazione |
| 8 | `*`, `/` | sinistra | prodotto, divisione |
| 9 | `^` | destra | potenza |
| 10 | `!`, `+`, `-` | destra | NOT, identità, negazione prefissa |
| 11 | `++`, `--` | sinistra | incremento e decremento postfissi |

Chiamata `()`, indice `[]` e membro `.` appartengono al livello postfix più alto
e si concatenano da sinistra a destra. Non esistono operatori bitwise, di
intervallo, radice o concatenazione stringa.

Gli operandi sono valutati da sinistra a destra e una sola volta. `&&` e `||`
non valutano il lato destro quando il risultato è già determinato; `^^` valuta
sempre entrambi. La destinazione di `<<` viene risolta prima del valore destro.

### 6.1 Assegnazione e mutazione

Sono destinazioni modificabili soltanto una variabile o parametro, un elemento
di array, un campo DDF e una proprietà host esplicitamente scrivibile.
`destinazione << valore` converte il valore al tipo della destinazione, lo
memorizza e restituisce il valore convertito. Un indice di `string` è in sola
lettura.

`++` e `--` richiedono una destinazione modificabile `int` o `float`, applicano
le regole numeriche e restituiscono il valore precedente.

`valore >> Console` invia la rappresentazione del valore al canale Output e
restituisce il valore sinistro. `Console` è l'unica destinazione canonica di
questo operatore e non concede accesso al catalogo host.

### 6.2 Aritmetica

Gli operatori numerici accettano soltanto `int` e `float`. Due `int` producono
`int`; se almeno un operando è `float`, l'altro viene promosso e il risultato è
`float`.

- l'aritmetica `int` è checked e non può fare wrap-around;
- la divisione `int` tronca verso zero;
- la divisione per zero è sempre un errore, anche per `float`;
- la potenza `int` richiede esponente non negativo e risultato rappresentabile;
- un risultato `float` NaN o infinito è un errore; l'underflow a zero è ammesso.

### 6.3 Logica e confronti

Le condizioni e gli operatori logici richiedono `bool`; non esiste truthiness.
I confronti ordinati accettano numeri e promuovono `int` a `float` quando serve.

`==` e `!=` accettano primitivi dello stesso tipo oppure una coppia numerica
`int`/`float`. Le stringhe sono confrontate ordinalmente per code unit UTF-16.
Array e strutture non supportano uguaglianza o disuguaglianza.

Il disegno futuro `DDF-0035`, non ancora attivo, estende le stringhe in DDF
`0.20` senza cambiare questo modello: `+` accetterà soltanto due `string` e la
libreria standard fornirà operazioni ordinali, Unicode `15.1` versionato,
ricerca tramite `maybe int`, split/join tipizzati e formato posizionale sicuro.
Non sono previste conversioni implicite, dipendenze dalla cultura o primitive
CLR nella IR. Il contratto è `ddf-string-library-design.json`.

Il disegno futuro `DDF-0036`, non ancora attivo, aggiunge in DDF `0.21` una
libreria matematica pura con famiglie tipizzate, domini espliciti,
arrotondamento tie-to-even e trigonometria in radianti. Le trascendenti usano
un algoritmo DDF versionato e devono coincidere bit per bit in VM, Roslyn e
futura VM; NaN e infinito restano errori. Il contratto è
`ddf-math-library-design.json`.

Il disegno futuro `DDF-0037`, non ancora attivo, separa in DDF `0.22` data,
ora civile, data-ora senza fuso, istante UTC e durata. Usa tick da 100 ns,
offset UTC espliciti e testo invariant; timezone IANA richiedono dati versionati.
Ora corrente e casualità dipendono da provider espliciti, revocabili e
sostituibili, mai da stato globale del processo. Il contratto è
`ddf-temporal-random-design.json`.

## 7. Controllo di flusso

`if`, `while`, `do ... while` e la condizione di `for` richiedono `bool`.
L'assenza della condizione nel `for` equivale a una condizione sempre vera.
Inizializzazione, condizione e incremento del `for` vengono eseguiti nel consueto
ordine: inizializzazione una volta, poi condizione, corpo e incremento.

`brk;` termina il ciclo più interno e non è valido fuori da un ciclo.
`continue;` salta il resto dell'iterazione del ciclo più interno: in `while` e
`do ... while` passa alla verifica della condizione, mentre nel `for` esegue
prima l'incremento e poi rivaluta la condizione. Non è valido fuori da un ciclo. `ret;` o
`ret espressione;` termina la funzione corrente. `end;` termina l'intero
programma. Ogni funzione non `void` deve restituire su tutti i percorsi
raggiungibili; una funzione `void` può usare soltanto `ret;`.

L'iterazione tipizzata non appartiene ancora al contratto eseguibile corrente.
Il disegno accettato in `DDF-0032` e in
`ddf-typed-iteration-design.json` punta a DDF `0.17` con
`each(T elemento in raccolta)`, valutazione singola, binding non riassegnabile,
snapshot superficiale e ordine deterministico. Nel DDF `0.13`, `each` e `in`
restano identificatori ordinari.

## 8. Funzioni

Una firma comprende nome, parametri nell'ordine dichiarato e tipo restituito.
Ogni chiamata fornisce esattamente un argomento per parametro. Gli argomenti
sono valutati da sinistra a destra e convertiti prima dell'ingresso.

Ogni funzione è dichiarata top-level e appartiene al modulo che contiene il
sorgente, non a un file, a una struttura o a un'istanza. È quindi invocabile
senza ricevitore dalle altre funzioni dello stesso modulo e, quando esportata,
dai moduli dipendenti diretti. Questa semantica corrisponde al ruolo pratico di
una funzione statica, ma `static` non è una keyword né un modificatore DDF:
aggiungerlo non renderebbe la funzione più condivisa e importerebbe un modello
OOP che il linguaggio non possiede. Le strutture continuano a contenere soltanto
campi.

Primitivi e stringhe sono passati per valore. Array e strutture condividono il
riferimento gestito. Riassegnare un parametro non riassegna la variabile del
chiamante; modificare un elemento o campo condiviso resta invece osservabile.

Il manifesto sceglie per nome la funzione di ingresso, normalmente `main`, che
deve comparire una sola volta nel modulo radice. L'avvio automatico non fornisce
argomenti. Il valore restituito, se presente, è il risultato del programma.

## 9. Moduli e progetti

### 9.1 Identità

Un modulo è un manifesto `ddf-project.json` schema `1.1` più l'insieme ordinato
dei sorgenti selezionati. Non coincide con un file, una cartella o una scheda.
Possiede `id` minuscolo stabile, versione esatta ed elenco `exports`.

Tutti i sorgenti del modulo condividono namespace piatti e case-sensitive per
funzioni, strutture e globali. I percorsi non creano namespace. Il runtime
qualifica internamente ogni dichiarazione con `module.id@module.version`, così
moduli distinti possono avere nomi privati uguali.

Le funzioni top-level sono funzioni di modulo: non vengono duplicate per file o
per esecuzione e non catturano un'istanza implicita. Il loro stato locale nasce
a ogni chiamata; l'eventuale stato conservato fra invocazioni deve essere
dichiarato separatamente con `state` e associato allo store esplicito
dell'istanza di esecuzione.

### 9.2 Esportazioni e dipendenze

Ogni nome in `exports` è unico e deve corrispondere a una funzione, struttura o
globale top-level. Un consumatore vede soltanto gli export delle dipendenze
dirette; non esiste ri-esportazione transitiva implicita.

Una dipendenza indica una coppia esatta `id`/`version`. Auto-dipendenze, moduli
mancanti, identità risolte differenti, cicli, richieste di versioni incompatibili
e backend o pacchetti linguaggio differenti rendono il progetto non valido. Se
due dipendenze dirette esportano lo stesso nome usato dal consumatore, il
riferimento è ambiguo e non viene scelto in base all'ordine.

Lo store predefinito è `.ddf/modules/<id>/<version>`, ma il percorso non ha
significato semantico. Un archivio `.ddfmod` distribuisce sorgenti e manifesto;
installarlo non esegue codice e non concede capacità.

La qualificazione esplicita degli export non appartiene ancora al contratto
eseguibile corrente. Il disegno accettato in `DDF-0031` e nel contratto
`ddf-module-qualification-design.json` punta a DDF `0.16` e al manifesto `1.3`:
ogni dipendenza diretta avrà un alias confermato dal consumatore e i riferimenti
useranno `alias::simbolo`. Non saranno introdotti namespace derivati dai
percorsi, import wildcard o visibilità transitiva. Nel DDF `0.13` gli export
diretti continuano temporaneamente a essere risolti secondo il modello piatto
documentato in questa sezione e `::` non appartiene alla grammatica.

### 9.3 Inizializzazione globale

I moduli dipendenza vengono inizializzati prima dei consumatori. Dentro ogni
modulo, le globali seguono l'ordine deterministico dei sorgenti normalizzati e
poi l'ordine delle dichiarazioni. Una globale può leggere soltanto globali già
inizializzate; riferimenti in avanti e cicli impediscono l'avvio.

### 9.4 Manifesto minimo

Il manifesto deve selezionare esattamente il pacchetto linguaggio, sorgenti,
ingresso e backend. I buffer non salvati partecipano soltanto se l'opzione
`useUnsavedBuffers` lo permette. La build non salva mai implicitamente i file.

```json
{
  "schemaVersion": "1.1",
  "name": "example",
  "module": {
    "id": "example.application",
    "version": "1.0.0",
    "exports": [ "main" ]
  },
  "languageId": "ddf",
  "languagePackageVersion": "0.13.0",
  "sources": [ "**/*.ddf" ],
  "entrySource": "main.ddf",
  "entryPoint": "main",
  "dependencies": [],
  "backend": "ddf-vm-ir-v1",
  "output": {
    "directory": ".ddf-build",
    "artifact": "example.ddfbuild.json"
  },
  "options": {
    "useUnsavedBuffers": true,
    "maxInstructions": 100000
  }
}
```

## 10. Esecuzione e osservabilità

La pipeline conforme esegue lessico, sintassi, risoluzione, controllo dei tipi e
lowering prima di avviare il programma. Errori in queste fasi impediscono Run e
Debug. La VM di riferimento usa IR `ddf-ir-vm-1.0`, conserva documento e
intervallo sorgente e applica un limite di istruzioni configurabile.

Gli errori runtime sono diagnostiche DDF, non eccezioni CLR esposte allo script.
Devono includere posizione e stack DDF quando disponibili. Sono stabili almeno:

| Codice | Classe | Condizione |
| --- | --- | --- |
| `DDF401` | fault | funzione di ingresso assente dopo la validazione |
| `DDF402` | fault | funzione, binding o arità runtime non coerenti |
| `DDF403` | fault | controllo di flusso o valore runtime interno non valido |
| `DDF404` | recuperabile | divisione per zero |
| `DDF405` | recuperabile | indice, membro o destinazione non validi |
| `DDF406` | fault | simbolo validato non disponibile a runtime |
| `DDF407` | fault | limite di istruzioni superato |
| `DDF408` | recuperabile | input o conversione standard fallita |
| `DDF409` | fault | capability host assente, revocata o non autorizzata |
| `DDF410` | fault | adapter host fallito senza errore DDF dichiarato |
| `DDF411` | fault | scrittura host non autorizzata |
| `DDF412` | fault | simulazione richiesta ma non disponibile |
| `DDF413` | fault | budget di chiamate o risorse esaurito |
| `DDF414` | fault | timeout di una capability host |
| `DDF415` | fault | cleanup di una risorsa fallito |
| `DDF416` | fault | contratto degli argomenti host non rispettato |
| `DDF417` | recuperabile | overflow `int` checked |
| `DDF418` | recuperabile | risultato `float` non finito |
| `DDF419` | recuperabile | esponente intero negativo |
| `DDF420` | fault | lifecycle della transazione capability fallito |
| `DDF423` | fault | store `state` usato concorrentemente |
| `DDF424` | fault | esecuzione annullata dall'host |
| `DDF425` | fault | violazione di sicurezza o isolamento |
| `DDF426` | fault | artefatto runtime corrotto o incompatibile |
| `DDF499` | fault | errore interno non previsto del motore |

Un errore DDF possiede un'identità di tipo stabile distinta dal codice
diagnostico, oltre a messaggio, documento, intervallo sorgente e stack DDF. Gli
errori recuperabili possono essere dichiarati nel contratto di una funzione
standard o di una API host. I fault del motore, come esaurimento del budget di
istruzioni o revoca di una capability, non possono essere dichiarati
recuperabili né nascosti dallo script. La prima superficie normativa di questi
metadati è backend-neutral. La sintassi di recupero, propagazione e cleanup non
appartiene ancora al contratto eseguibile corrente e nessun costrutto implicito
di tipo `try/catch` è ammesso.

Il disegno futuro è stato accettato in `DDF-0029` e nel contratto
`ddf-error-handling-design.json`: punta a DDF `0.14` con
`attempt`/`recover`/`always`, dichiarazioni `raises` e ripropagazione `raise`.
Queste forme non appartengono al presente DDF `0.13` e devono restare
identificatori ordinari finché la verticale completa non verrà attivata.
`DDF424`–`DDF426` sono identità di normalizzazione riservate: cancellazione,
violazioni di sicurezza e rifiuto di artefatti possono oggi essere osservati da
canali host o diagnostiche backend specifiche, ma dovranno convergere su questi
fault quando attraverseranno il futuro modello unificato degli esiti.

Anche i valori opzionali sono esclusi dal contratto eseguibile corrente. Il
disegno accettato in `DDF-0030` e in `ddf-optional-values-design.json` punta a
DDF `0.15`: `maybe T` distingue presenza e assenza tramite `some(...)` e
`none`, con accesso soltanto attraverso una selezione esaustiva. Non introduce
`null`, conversioni implicite o tipi Roslyn nella IR. Nel DDF `0.13`, `maybe`,
`some` e `none` restano identificatori ordinari.

Breakpoint, stepping, Watch e stack devono usare gli stessi moduli, tipi e
regole di risoluzione dell'esecuzione normale. Le Watch sono in sola lettura:
non ammettono assegnazione, output o mutazioni postfix.

## 11. Libreria standard minima

Il catalogo normativo è `ddf.standard@0.12.3`. È parte del pacchetto linguaggio,
non è un modulo installabile e non abilita API host.

I futuri dialoghi `DDF-0039` non appartengono alla libreria standard: sono una
capability host opzionale, negata per impostazione predefinita nell'embedded e
priva di tipi UI nella IR. Il contratto è `ddf-host-dialog-design.json`.

La futura libreria `ddf.testing@1` di `DDF-0040` è disponibile soltanto durante
i test di progetto e non appartiene a `ddf.standard`. Funzioni e sorgenti test
non vengono esportati o pubblicati con il programma. Il contratto è
`ddf-project-testing-design.json`.

La gestione deterministica delle risorse di `DDF-0041` è destinata a DDF
`0.23`. La forma futura `use tipo nome << acquisizione { ... }` limiterà un
handle nominale opaco a uno scope e ne garantirà il cleanup LIFO. Non introduce
un `defer` generico né tipi CLR nella IR. Nel DDF `0.13`, `use` resta un
identificatore ordinario. Il contratto è `ddf-resource-lifetime-design.json`.

L'asincronia cooperativa di `DDF-0042` è destinata a DDF `0.24` e non aggiunge
keyword: una normale chiamata marcata sospensiva nel descrittore potrà rilasciare
il thread e riprendere la stessa continuazione. Ogni istanza manterrà una sola
esecuzione e sospensione pendente, senza concorrenza implicita su `state` e senza
esporre task, thread o eccezioni CLR. `async` e `await` restano identificatori
ordinari. Il contratto è `ddf-cooperative-async-design.json`.

| Funzione | Effetto | Comportamento | Errori |
| --- | --- | --- | --- |
| `print(string) out void` | output | invia al canale Output; se assente scarta il testo | nessuno |
| `readLine() out string` | input | legge una riga; un risultato nullo diventa `""` | `DDF408` se manca il canale |
| `length(string) out int` | puro | restituisce il numero di code unit UTF-16 | nessuno |
| `toInt(string) out int` | puro | parsing invariant in `System.Int32`, con segno e spazi esterni | `DDF408` |
| `toFloat(string) out float` | puro | parsing invariant in `System.Double`, risultato finito | `DDF408` |

Il futuro contratto `DDF-0038`, non ancora attivo, sostituirà i descrittori
minimi con lo schema `2.0`: ogni funzione avrà identità stabile, firma, effetti,
errori, determinismo, portabilità, costo e binding backend-neutral in un unico
snapshot fingerprintato. Parser e UI non potranno riconoscere funzioni tramite
nomi hardcoded. Il disegno è `ddf-standard-library-binding-design.json`.

Una funzione DDF top-level con lo stesso nome ha precedenza nel proprio modulo.
In assenza di essa viene risolta la funzione standard. Non esistono overload,
parametri opzionali o conversioni ulteriori.

## 12. Host, embedded e backend .NET

Oggetti, proprietà, eventi, funzioni e database dell'applicazione host non fanno
parte del linguaggio implicito. Sono visibili soltanto se registrati in un
catalogo tipizzato e autorizzati. Non viene eseguita reflection automatica. Le
proprietà non dichiarate scrivibili restano in sola lettura.

La modalità embedded interpreta lo script per espandere l'host e non compila né
carica assembly DDF. L'accesso embedded allo store moduli è negato in assenza di
una capability host esplicita e revocabile. Standalone e futura CLI saranno gli
unici contesti autorizzati a compilare e pubblicare.

Il mapping CLR stabilisce compatibilità, non accesso alla piattaforma. Il futuro
backend Roslyn deve compilare la semantica già risolta da DDF, aggiungendo i
controlli prescritti per overflow, valori non finiti, bounds, non-null e
capacità. Roslyn non può ridefinire conversioni, ordine di valutazione o errori.

## 13. Profili sintattici

Un profilo equivalente può rinominare keyword, booleani o operatori e cambiare
la presentazione didattica. Non può cambiare tipi, conversioni, ordine di
valutazione, effetti, diagnostiche runtime, moduli o libreria standard. Il
profilo canonico definito qui resta il fallback incorporato.

Un pacchetto denominato, per esempio, «familiare C» offre soltanto comodità di
sintassi: non rende DDF compatibile con C e non importa librerie o toolchain C.

## 14. Diagnostiche e versionamento

I prefissi distinguono famiglie stabili:

- `DDF0xx`: lessico e sintassi;
- `DDF3xx`: tipi e semantica statica;
- `DDF4xx`: errori causati dal programma a runtime;
- `DDFBxxx`: build, ingresso e risoluzione moduli;
- `DDFPxxx`: manifesto progetto;
- `DDFLxxx`: definizione del pacchetto linguaggio;
- `DDFExxx`: capacità mancanti del motore.

Una modifica a grafia o presentazione equivalente incrementa il pacchetto. Una
modifica osservabile a tipi, semantica, libreria standard, moduli o runtime
richiede una nuova versione del contratto DDF e una guida di migrazione. Un
comportamento accidentale dell'implementazione non modifica questa specifica.
La policy completa e verificabile è `DDF_LANGUAGE_VERSIONING.md`; la sua
rappresentazione macchina è `ddf-language-version-policy.json`. Essa distingue
versione del linguaggio, revisione del pacchetto, API motore, schemi, moduli e
libreria standard e non autorizza fallback verso versioni non richieste.

---

## Appendice A — Esempio completo non normativo

Questo esempio illustra le regole precedenti ma non ne aggiunge di nuove.

<!-- ddf-complete-example:start -->
```ddf
struct Point
{
    float x;
    float y;
}

distanceSquared(Point point) out float
{
    ret point.x * point.x + point.y * point.y;
}

main() out int
{
    Point point;
    point.x << 3;
    point.y << 4;

    float squared << distanceSquared(point);

    if(squared >= 25)
    {
        print("Distanza raggiunta");
    }
    else
    {
        print("Distanza breve");
    }

    int[3] values;
    int index << 0;

    while(index < 3)
    {
        values[index] << index ^ 2;
        index++;
    }

    ret values[2];
}
```
<!-- ddf-complete-example:end -->

## Appendice B — Esempi non validi non normativi

Gli esempi seguenti sono intenzionalmente non validi:

```ddf
dict values;              // dict non appartiene al nucleo
int[2][3] matrix;         // un solo rango array
bool enabled << 1;        // nessuna truthiness int -> bool
string name << "DDF";
name[0] << "X";           // gli indici stringa sono in sola lettura
```

```ddf
main() out int
{
    brk;                  // nessun ciclo da interrompere
    ret 0;
}
```

## Appendice C — Matrice delle fonti di conformità non normativa

Questa tabella aiuta i manutentori; non trasferisce norme fuori dal documento.

| Area | Rappresentazione eseguibile o prova |
| --- | --- |
| lessico e operatori | `language-packages/ddf/lexicon.json` |
| grammatica | `language-packages/ddf/grammar.ebnf` |
| tipi e mapping CLR | `language-packages/ddf/types.json` |
| operazioni VM | `language-packages/ddf/runtime.json` |
| libreria standard | `language-packages/ddf/standard-library.json` |
| progetto e moduli | `ddf-project.schema.json`, suite Core |
| esempi e regressioni | `samples/language-corpus/corpus.json`, casi `corpus:*` in `language-packages/ddf/tests.json`, suite Core |
| versionamento e migrazione | `ddf-language-version-policy.json`, `DDF_LANGUAGE_VERSIONING.md`, `DDF_MIGRATION_0_12.md` |

In caso di divergenza, il rilascio è bloccato finché documento, pacchetto e test
non vengono riallineati mediante una decisione di linguaggio versionata.
