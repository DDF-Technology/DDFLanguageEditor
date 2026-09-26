# DDF grammar — declarative grammar candidate 0.12

La fonte canonica della sintassi è ora
[`language-packages/ddf/grammar.ebnf`](language-packages/ddf/grammar.ebnf),
completata dai metadati dei nodi in `grammar.json`. Questo documento rimane una
guida leggibile e non deve duplicare nuove produzioni.

Il contratto dei token è definito in `DDF_LEXICAL_SPEC.md`.

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
       | variable-declaration ;

library-directive = "@@'" identifier "'" ;

struct-declaration = "struct", identifier, "{",
                     { variable-declaration }, "}" ;

function-declaration = identifier, "(", [ parameter-list ], ")",
                       "out", type, block ;

parameter-list = parameter, { ",", parameter } ;
parameter = type, identifier ;
```

Le direttive precedono le dichiarazioni. Al livello principale sono ammesse
soltanto dichiarazioni; le istruzioni eseguibili beta sono rifiutate dalla
validazione normativa.

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

if-statement = "if", "(", expression, ")", block,
               [ "else", ( block | if-statement ) ] ;

while-statement = "while", "(", expression, ")", block ;

do-while-statement = "do", block,
                     "while", "(", expression, ")", ";" ;

for-statement = "for", "(",
                [ variable-declaration | expression, ";" ],
                [ expression ], ";",
                [ expression ], ")", block ;

return-statement = "ret", [ expression ], ";" ;
break-statement = "brk", ";" ;
end-statement = "end", ";" ;
expression-statement = expression, ";" ;
```

I corpi dei costrutti di controllo richiedono sempre un blocco; `else if` è
ammesso come concatenazione esplicita. Un punto e virgola isolato non è una
istruzione valida.

`switch`, eccezioni e altre forme di controllo non appartengono ancora al
sottoinsieme corrente.

## Espressioni

La precedenza, dalla più bassa alla più alta, è:

| Livello | Operatori | Associatività |
| --- | --- | --- |
| 1 | `<<`, `>>` | destra |
| 2 | `||` | sinistra |
| 3 | `^^` | sinistra |
| 4 | `&&` | sinistra |
| 5 | `==`, `!=` | sinistra |
| 6 | `<`, `<=`, `>`, `>=` | sinistra |
| 7 | `+`, `-` | sinistra |
| 8 | `*`, `/` | sinistra |
| 9 | `^` | destra |
| 10 | prefissi `!`, `+`, `-` | destra |
| 11 | postfix `++`, `--`, chiamata e indice | sinistra |

`^^` è XOR booleano e `^` è elevamento a potenza. Le vecchie grafie `!!`,
`&`, `|&|`, `>><<`, `<<>>` e `^/` non appartengono al pacchetto DDF `0.12.1`;
la migrazione è descritta in `DDF_MIGRATION_0_12.md`.

```ebnf
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

Il parser generico della grammatica EBNF emette inoltre `MissingSyntax` e la
diagnostica `DDFG101` nel punto sorgente più avanzato raggiunto. Le diagnostiche
normative sono esposte tramite `ValidationDiagnostics`; il vecchio AST DDF resta
temporaneamente una proiezione recuperabile per i servizi IDE.

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
