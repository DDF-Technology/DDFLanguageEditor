# Sintassi DDF 0.12

Stato: contratto normativo del nucleo sintattico del pacchetto DDF `0.12.1`.

La fonte eseguibile è `language-packages/ddf/grammar.ebnf`. Questo documento ne
spiega le scelte senza sostituirla. Lessico e operatori sono definiti in
`DDF_LEXICAL_SPEC.md` e `DDF_OPERATOR_SPEC.md`.

## Unità di compilazione

Un file contiene, in quest'ordine:

1. zero o più direttive `@@'Nome'`;
2. zero o più dichiarazioni di struttura, funzione o variabile globale.

Le istruzioni eseguibili non sono ammesse direttamente al livello principale.
Una variabile globale usa la stessa sintassi di una variabile locale; il suo
ordine di inizializzazione appartiene alla semantica della `0.12.2` e al modello
dei moduli della `0.12.3`.

La grafia `@@'Nome'` resta la sola direttiva riconosciuta nella linea 0.12.1,
ma il significato di import, dipendenza o capacità verrà deciso nella `0.12.3`.
Non è un preprocessore e non esegue sostituzioni testuali.

## Parole riservate

| Gruppo | Parole |
| --- | --- |
| Tipi | `int`, `float`, `char`, `bool`, `void`, `string`, `dict` |
| Dichiarazioni | `struct`, `out` |
| Controllo | `if`, `else`, `while`, `do`, `for` |
| Uscita dal flusso | `ret`, `brk`, `end` |
| Letterali riservati | `true`, `false` |

`dict` è riservato per evitare incompatibilità, ma la sua forma composta e la
semantica restano da approvare nella `0.12.2`. Non esistono alias con
maiuscole o forme estese delle keyword nel profilo DDF canonico.

## Delimitatori

Il nucleo usa esclusivamente `{ } ( ) [ ] . , ;`. Il punto e virgola è
obbligatorio per dichiarazioni di variabile, espressioni, `ret`, `brk`, `end` e
la clausola finale del `do ... while`. Un `;` isolato non è un'istruzione valida.

## Dichiarazioni

```ddf
int counter;
float ratio << 1.5;
int[10] values;
Point current;
```

La forma generale è `tipo nome [<< espressione];`. I suffissi array fanno parte
del tipo e possono essere concatenati. Dimensioni obbligatorie, inizializzazione
e durata dei valori sono decisioni semantiche della `0.12.2`.

Una struttura contiene soltanto dichiarazioni di campo:

```ddf
struct Point
{
    float x;
    float y;
}
```

Metodi, costruttori ed ereditarietà non fanno parte del nucleo 0.12.1.

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

`main` non è una keyword: è il nome del punto di ingresso scelto dal runtime.
Unicità e firma valida appartengono al contratto di build.

## Blocchi e controllo di flusso

I corpi di `if`, `while`, `do` e `for` richiedono sempre `{ ... }`. Questa regola
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
```

`else if` è l'unica eccezione alla regola “dopo `else` viene un blocco”: crea
una catena condizionale senza introdurre una keyword aggiuntiva. `brk;` interrompe
il ciclo corrente; `end;` termina il programma; `ret` termina la funzione. Le
restrizioni contestuali sono semantiche.

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

