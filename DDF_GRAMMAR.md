# DDF grammar — parser and semantic subset 0.6

Questa grammatica descrive il sottoinsieme sintattico riconosciuto dal parser di
DDFLanguageEditor 0.6. È deliberatamente piccola e conserva la natura
sperimentale del linguaggio. Dalla 0.7.0 questo sottoinsieme può essere eseguito
dal primo interprete AST interno, ancora privo di libreria standard completa.

## Notazione

- i terminali sono racchiusi tra virgolette;
- `[]` indica una parte opzionale;
- `{}` indica zero o più ripetizioni;
- `|` separa alternative;
- `identifier`, `number` e `string` sono token prodotti dal lexer.

## Unità di compilazione

```ebnf
compilation-unit = { library-directive }, { member } ;

member = struct-declaration
       | function-declaration
       | statement ;

library-directive = "@@'" identifier "'" ;

struct-declaration = "struct", identifier, "{",
                     { variable-declaration }, "}" ;

function-declaration = identifier, "(", [ parameter-list ], ")",
                       "out", type, block ;

parameter-list = parameter, { ",", parameter } ;
parameter = type, identifier ;
```

Le istruzioni al livello principale sono accettate per compatibilità con i
sorgenti sperimentali, anche se il punto di ingresso consigliato è `main`.

## Tipi e dichiarazioni

```ebnf
type = ( built-in-type | identifier ), { array-suffix } ;

built-in-type = "int" | "float" | "char" | "bool"
              | "void" | "string" | "dict" ;

array-suffix = "[", [ expression ], "]" ;

variable-declaration = type, identifier,
                       [ "<<", expression ], ";" ;
```

Un identificatore usato come tipo rappresenta un tipo definito dall'utente. La
risoluzione del simbolo verrà introdotta con l'analisi semantica.

## Istruzioni

```ebnf
statement = block
          | variable-declaration
          | if-statement
          | while-statement
          | do-while-statement
          | for-statement
          | return-statement
          | break-statement
          | end-statement
          | expression-statement ;

block = "{", { statement }, "}" ;

if-statement = "if", "(", expression, ")", statement ;

while-statement = "while", "(", expression, ")", statement ;

do-while-statement = "do", statement,
                     "while", "(", expression, ")", ";" ;

for-statement = "for", "(",
                [ variable-declaration | expression, ";" ],
                [ expression ], ";",
                [ expression ], ")", statement ;

return-statement = "ret", [ expression ], ";" ;
break-statement = "brk", ";" ;
end-statement = "end", ";" ;
expression-statement = expression, ";" ;
```

`else`, `switch`, eccezioni e altre forme di controllo non appartengono ancora
al sottoinsieme 0.4.

## Espressioni

La precedenza, dalla più bassa alla più alta, è:

| Livello | Operatori | Associatività |
| --- | --- | --- |
| 1 | `<<`, `>>` | destra |
| 2 | `||` | sinistra |
| 3 | `|&|` | sinistra |
| 4 | `&` | sinistra |
| 5 | `<`, `<=`, `>`, `>=`, `==`, `!!`, `>><<`, `<<>>` | sinistra |
| 6 | `+`, `-` | sinistra |
| 7 | `*`, `/` | sinistra |
| 8 | `^`, `^/` | destra |
| 9 | prefissi `!`, `+`, `-` | destra |
| 10 | postfix `++`, `--`, chiamata e indice | sinistra |

```ebnf
expression = assignment-expression ;

primary-expression = identifier
                   | number
                   | string
                   | boolean
                   | "(", expression, ")" ;

postfix-expression = primary-expression,
                     { "++" | "--"
                     | "(", [ argument-list ], ")"
                     | "[", expression, "]"
                     | ".", identifier } ;

argument-list = expression, { ",", expression } ;
```

## Recupero dagli errori

Il parser continua dopo un errore sincronizzandosi sui delimitatori `;`, `{` e
`}` o avanzando almeno di un token. I nodi incompleti restano nell'AST con un
intervallo valido, permettendo all'editor di mostrare più diagnostiche nella
stessa sessione senza interrompere la digitazione.

## Diagnostiche sintattiche

| Codice | Significato |
| --- | --- |
| `DDF101` | Token inatteso |
| `DDF102` | Token o delimitatore atteso |
| `DDF103` | Espressione attesa |
| `DDF104` | Tipo atteso |
| `DDF105` | Identificatore atteso |

## Diagnostiche semantiche

Il type checker verifica tipi primitivi, strutture, array, operatori, condizioni,
chiamate e ritorni. Usa i codici `DDF301`–`DDF308` rispettivamente per
incompatibilità, operatori, argomenti, ritorni, tipi/membri/indici, condizioni,
ritorni mancanti e destinazioni di assegnazione non modificabili.
