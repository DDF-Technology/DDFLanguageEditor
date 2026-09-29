# Specifica autonoma del linguaggio DDF 0.12.3

Stato: **normativa** per il profilo canonico `ddf` versione `0.12.3`.

Questo documento definisce in modo autosufficiente il nucleo del linguaggio
DDF. Non è necessario consultare bozze storiche, codice dell'editor o altri
documenti per stabilire se un programma appartiene al nucleo `0.12.3` e quale
comportamento deve avere. I file JSON del pacchetto sono la rappresentazione
eseguibile dello stesso contratto, non una fonte di semantica alternativa.

Tutto il testo è normativo salvo le sezioni marcate **non normativa**. Le parole
«deve», «non deve» e «può» esprimono rispettivamente obbligo, divieto e facoltà.

## 1. Scopo e conformità

DDF è un linguaggio imperativo, staticamente tipizzato, case-sensitive e
orientato a programmi modulari. La versione `0.12.3` definisce:

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
| dichiarazioni | `struct`, `out` |
| controllo | `if`, `else`, `while`, `do`, `for` |
| uscita | `ret`, `brk`, `end` |
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
member = struct-declaration | function-declaration | variable-declaration ;
library-directive = <LibraryDirective> ;

struct-declaration = "struct", identifier, block ;
function-declaration = identifier, "(", [ parameter-list ], ")",
                       "out", return-type, block ;
return-type = type ;
parameter-list = parameter, { ",", parameter } ;
parameter = type, identifier ;

statement = block | if-statement | while-statement | do-while-statement |
            for-statement | return-statement | break-statement |
            end-statement | variable-declaration | expression-statement ;
block = "{", { statement }, "}" ;
if-statement = "if", "(", expression, ")", block,
               [ "else", ( block | if-statement ) ] ;
while-statement = "while", "(", expression, ")", block ;
do-while-statement = "do", block, "while", "(", expression, ")", ";" ;
for-statement = "for", "(",
                ( variable-declaration | [ expression ], ";" ),
                [ expression ], ";", [ expression ], ")", block ;
return-statement = "ret", [ expression ], ";" ;
break-statement = "brk", ";" ;
end-statement = "end", ";" ;
variable-declaration = type, identifier, [ "<<", expression ], ";" ;
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
<!-- ddf-canonical-grammar:end -->

La grammatica consente al parser di rappresentare suffissi array incompleti o
ripetuti per produrre diagnostiche precise. Un programma conforme deve però
rispettare le restrizioni semantiche della sezione 5: una dimensione è
obbligatoria e può esistere un solo suffisso array.

Al livello principale sono ammesse soltanto direttive, strutture, funzioni e
globali. Le direttive precedono ogni dichiarazione. Un `;` isolato non è
un'istruzione. I corpi di tutti i costrutti di controllo richiedono le graffe;
solo `else if (...) { ... }` può seguire direttamente `else`.

## 4. Dichiarazioni e nomi

Una dichiarazione di valore ha forma `tipo nome;` oppure
`tipo nome << espressione;`. L'inizializzatore usa l'operatore generale di
assegnazione ma viene verificato nel contesto del tipo dichiarato.

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

Variabili, parametri e campi sono mutabili. Una dichiarazione senza
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

### 5.2 Array

Un tipo array ha forma `T[n]`, con una sola dimensione `int` non negativa. La
dimensione viene valutata una volta quando si esegue la dichiarazione.

Un array è di lunghezza fissa, non nullo e invariante. Ogni elemento riceve il
valore iniziale di `T`. Lettura e scrittura richiedono un indice `int` compreso
fra zero incluso e lunghezza esclusa. Assegnazione e passaggio a funzione
condividono la stessa identità dell'array. Array jagged o multidimensionali non
sono validi.

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

## 7. Controllo di flusso

`if`, `while`, `do ... while` e la condizione di `for` richiedono `bool`.
L'assenza della condizione nel `for` equivale a una condizione sempre vera.
Inizializzazione, condizione e incremento del `for` vengono eseguiti nel consueto
ordine: inizializzazione una volta, poi condizione, corpo e incremento.

`brk;` termina il ciclo più interno e non è valido fuori da un ciclo. `ret;` o
`ret espressione;` termina la funzione corrente. `end;` termina l'intero
programma. Ogni funzione non `void` deve restituire su tutti i percorsi
raggiungibili; una funzione `void` può usare soltanto `ret;`.

## 8. Funzioni

Una firma comprende nome, parametri nell'ordine dichiarato e tipo restituito.
Ogni chiamata fornisce esattamente un argomento per parametro. Gli argomenti
sono valutati da sinistra a destra e convertiti prima dell'ingresso.

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
  "languagePackageVersion": "0.12.3",
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

| Codice | Condizione |
| --- | --- |
| `DDF401` | funzione di ingresso assente |
| `DDF402` | funzione o arità runtime non valida |
| `DDF403` | controllo di flusso o valore runtime non valido |
| `DDF404` | divisione per zero |
| `DDF405` | indice, membro o destinazione non validi |
| `DDF407` | limite di istruzioni superato |
| `DDF408` | input o conversione standard fallita |
| `DDF409` | capacità host mancante |
| `DDF417` | overflow `int` checked |
| `DDF418` | risultato `float` non finito |
| `DDF419` | esponente intero negativo |

Breakpoint, stepping, Watch e stack devono usare gli stessi moduli, tipi e
regole di risoluzione dell'esecuzione normale. Le Watch sono in sola lettura:
non ammettono assegnazione, output o mutazioni postfix.

## 11. Libreria standard minima

Il catalogo normativo è `ddf.standard@0.12.3`. È parte del pacchetto linguaggio,
non è un modulo installabile e non abilita API host.

| Funzione | Effetto | Comportamento | Errori |
| --- | --- | --- | --- |
| `print(string) out void` | output | invia al canale Output; se assente scarta il testo | nessuno |
| `readLine() out string` | input | legge una riga; un risultato nullo diventa `""` | `DDF408` se manca il canale |
| `length(string) out int` | puro | restituisce il numero di code unit UTF-16 | nessuno |
| `toInt(string) out int` | puro | parsing invariant in `System.Int32`, con segno e spazi esterni | `DDF408` |
| `toFloat(string) out float` | puro | parsing invariant in `System.Double`, risultato finito | `DDF408` |

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
| esempi e regressioni | corpus e test dichiarativi del pacchetto |

In caso di divergenza, il rilascio è bloccato finché documento, pacchetto e test
non vengono riallineati mediante una decisione di linguaggio versionata.
