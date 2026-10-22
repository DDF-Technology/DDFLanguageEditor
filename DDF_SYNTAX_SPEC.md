# Sintassi DDF 0.13

Stato: contratto normativo del nucleo sintattico del pacchetto DDF `0.13.0`.

Questa specifica di area è consolidata dalla specifica autonoma
`DDF_LANGUAGE_SPEC.md`, che prevale in caso di divergenza accidentale.

La fonte eseguibile è `language-packages/ddf/grammar.ebnf`. Questo documento ne
spiega le scelte senza sostituirla. Lessico e operatori sono definiti in
`DDF_LEXICAL_SPEC.md` e `DDF_OPERATOR_SPEC.md`.

## Unità di compilazione

Un file contiene, in quest'ordine:

1. zero o più direttive `@@'Nome'`;
2. zero o più dichiarazioni di struttura, funzione o variabile globale.

Le istruzioni eseguibili non sono ammesse direttamente al livello principale.
Una variabile globale usa la stessa sintassi di una variabile locale; il suo
ordine di inizializzazione è definito dal modello dei moduli della `0.12.3`.

La grafia `@@'Nome'` resta la sola direttiva riconosciuta nella linea 0.12.1,
ma dalla `0.12.3` è soltanto una richiesta legacy deprecata di libreria o
capacità. Non è un preprocessore, non importa moduli e non concede permessi.

## Parole riservate

| Gruppo | Parole |
| --- | --- |
| Tipi | `int`, `float`, `char`, `bool`, `void`, `string` |
| Dichiarazioni | `struct`, `enum`, `out`, `fixed`, `state` |
| Controllo | `if`, `else`, `select`, `when`, `otherwise`, `while`, `do`, `for` |
| Uscita dal flusso | `ret`, `brk`, `continue`, `end` |
| Letterali riservati | `true`, `false` |

`dict` non appartiene al nucleo. Non esistono alias con maiuscole o forme estese
delle keyword nel profilo DDF canonico.

Il disegno accettato per DDF `0.14` riserva in futuro `attempt`, `recover`,
`always`, `raises`, `raise` ed `error`, ma tali parole non sono ancora keyword
del pacchetto `0.13.0`. Sintassi e semantica pianificate sono isolate in
`DDF_ERROR_HANDLING_DESIGN.md` per evitare una dichiarazione prematura di
supporto.

## Delimitatori

Il nucleo usa esclusivamente `{ } ( ) [ ] . , ;`. Il punto e virgola è
obbligatorio per dichiarazioni di variabile, espressioni, `ret`, `brk`, `continue`, `end` e
la clausola finale del `do ... while`. Un `;` isolato non è un'istruzione valida.

## Dichiarazioni

```ddf
int counter;
float ratio << 1.5;
fixed int maximum << 100;
state int invocations << 0;
int[10] values;
Point current;
```

La forma generale mutabile è `tipo nome [<< espressione];`. Una costante usa
`fixed tipo nome << espressione;`: il tipo resta esplicito e l'inizializzatore
è obbligatorio. `fixed` è ammesso per valori primitivi scalari o enum nominali,
locali o di modulo. Un solo suffisso array
`[dimensione]` può far parte del tipo; la dimensione è obbligatoria. Il parser
può recuperare suffissi incompleti o concatenati, ma il type checker li rifiuta.

La forma `state tipo nome << espressione;` dichiara invece stato mutabile
dell'istanza. In questa prima verticale è valida soltanto a livello di modulo,
ha inizializzatore obbligatorio e accetta esclusivamente primitivi scalari.

Una struttura contiene soltanto dichiarazioni di campo:

```ddf
struct Point
{
    float x;
    float y;
}
```

Metodi, costruttori ed ereditarietà non fanno parte del nucleo 0.12.1.

Un enum dichiara almeno un valore simbolico e può terminare l'elenco con una
virgola opzionale:

```ddf
enum Color
{
    Red,
    Green,
    Blue
}
```

I valori si usano in forma qualificata, per esempio `Color.Green`.

## Funzioni

```ddf
sum(int left, int right) out int
{
    ret left + right;
}
```

Nome, parametri tipizzati, marcatore `out`, tipo restituito e blocco sono tutti
obbligatori. `ret;` e `ret espressione;` sono entrambe forme sintattiche; la
compatibilità con il tipo restituito viene verificata semanticamente.

Una funzione può essere dichiarata soltanto al livello principale dell'unità
di compilazione ed è una funzione del modulo, indipendentemente dal file che la
contiene. Non esistono metodi di struttura o funzioni annidate. `static` non è
una keyword né un modificatore: le funzioni di modulo sono già richiamabili
senza istanza.

`main` non è una keyword: è il nome predefinito del punto di ingresso scelto dal
manifesto. La funzione selezionata deve essere unica e l'avvio automatico non
fornisce argomenti.

## Blocchi e controllo di flusso

I corpi di `if`, `select`, `when`, `otherwise`, `while`, `do` e `for` richiedono sempre `{ ... }`. Questa regola
elimina il dangling `else`, rende sicure le modifiche successive e mantiene
stabili formatter, folding, breakpoint e flowchart.

```ddf
if(condition)
{
    action();
}
else if(otherCondition)
{
    alternative();
}
else
{
    fallback();
}

while(condition)
{
    action();
}

do
{
    action();
}
while(condition);

for(int index << 0; index < limit; index++)
{
    action(index);
}

select(color)
{
    when Color.Red
    {
        action();
    }

    when Color.Green
    {
        alternative();
    }

    otherwise
    {
        fallback();
    }
}
```

`else if` è l'unica eccezione alla regola “dopo `else` viene un blocco”: crea
una catena condizionale senza introdurre una keyword aggiuntiva. `brk;` interrompe
il ciclo corrente; `continue;` salta alla verifica dell'iterazione successiva
(attraversando prima l'incremento nel `for`); `end;` termina il programma; `ret` termina la funzione. Le
restrizioni contestuali sono semantiche.

`select` valuta una sola volta la propria espressione ed esegue al massimo il
primo `when` corrispondente. Non esiste fall-through. I casi devono essere
costanti e compatibili, non possono essere duplicati e `otherwise`, se presente,
deve essere unico e ultimo. Senza `otherwise`, la selezione è valida soltanto
quando copre tutti i valori di un dominio chiuso (`bool` o enum nominale).

## Espressioni

Le espressioni comprendono letterali, nomi, raggruppamento, chiamate, accesso a
membri, indici e gli operatori normativi. Precedenza e associatività sono in
`DDF_OPERATOR_SPEC.md`. Assegnazioni e mutazioni restano espressioni, ma la
validità della destinazione viene controllata dal type checker.

## Diagnostica e recupero

L'EBNF del pacchetto è il validatore normativo. `ValidationDiagnostics` unisce
diagnostiche lessicali, parser recuperabile e grammatica dichiarativa ed è usato
da analisi, Build e test del pacchetto. L'AST storico continua temporaneamente a
recuperare anche forme incomplete per non interrompere digitazione, Watch,
Quick Fix e servizi IDE; il loro recupero non rende valido il programma.

