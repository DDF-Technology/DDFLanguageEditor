# DDF language catalog — 0.4.1

Questo documento inventaria il vocabolario attualmente riconosciuto. Non pretende
di essere completo: è la base controllabile che verrà confrontata con il
linguaggio C e ampliata insieme alla grammatica.

La fonte eseguibile è `DDFLanguageEditor.Core/DdfLanguageCatalog.cs`. Lexer,
parser, completamento contestuale e formatter consumano `DdfLanguageDefinition`
e non mantengono copie proprie delle liste.

## Parole riservate

| Categoria | Termini | Ruolo sintattico |
| --- | --- | --- |
| Tipi primitivi | `int`, `float`, `char`, `bool`, `void`, `string`, `dict` | dichiarazioni e riferimenti di tipo |
| Strutture | `struct` | dichiarazione di struttura |
| Funzioni | `out`, `ret`, `brk`, `end` | tipo restituito, ritorno, interruzione, fine |
| Controllo | `if`, `while`, `do`, `for` | istruzioni di controllo |
| Booleani | `true`, `false`, `True`, `False` | letterali booleani |

I nomi non presenti nel catalogo rimangono identificatori. Questo consente di
aggiungere in futuro funzioni di libreria e simboli utente senza trasformarli
automaticamente in parole riservate.

## Operatori

| Precedenza | Operatori | Associatività/uso |
| --- | --- | --- |
| 1 | `<<`, `>>` | destra; `<<` inizializza anche le dichiarazioni |
| 2 | `||` | sinistra |
| 3 | `|&|` | sinistra |
| 4 | `&` | sinistra |
| 5 | `<`, `<=`, `>`, `>=`, `==`, `!!`, `>><<`, `<<>>` | sinistra |
| 6 | `+`, `-` | sinistra; anche prefissi |
| 7 | `*`, `/` | sinistra |
| 8 | `^`, `^/` | destra |
| 9 | `!`, `+`, `-` | prefissi |
| 10 | `++`, `--` | postfissi |

Il lexer ordina automaticamente gli operatori per lunghezza, quindi aggiungere
un operatore composto non richiede di modificare l'algoritmo longest-match.

## Delimitatori e costrutti lessicali

- delimitatori: `{ } ( ) [ ] . , ;`;
- commenti: `//` e `/* ... */`;
- stringhe: `"..."` con escape tramite barra inversa;
- inclusioni di libreria: `@@'Library'`;
- numeri interi e decimali;
- identificatori composti da lettere, cifre e `_`, non iniziati da cifra.

## Come estendere il catalogo

1. Registrare il termine in `DdfLanguageCatalog.CreateDefault` con categoria e,
   quando necessario, ruolo sintattico.
2. Per un operatore indicare precedenza, associatività e uso prefisso/postfisso.
3. Aggiornare questo inventario, `DDF_GRAMMAR.md` e il corpus interessato.
4. Aggiungere almeno un test lessicale e verificare completamento e formattazione;
   aggiungere un test del parser se cambia la grammatica.

Un sinonimo che usa un ruolo già esistente non richiede modifiche al parser. Un
costrutto realmente nuovo richiederà invece un nuovo ruolo, il relativo nodo AST
e la regola di parsing: questa distinzione impedisce che il catalogo lessicale e
la grammatica vengano confusi.

## Futuro confronto con C

Il confronto verrà registrato come matrice con almeno queste colonne:

| Elemento C | Equivalente DDF | Stato | Decisione |
| --- | --- | --- | --- |
| parola chiave, operatore o costrutto | termine attuale o proposto | presente, assente, diverso | adottare, adattare o escludere |

La matrice dovrà distinguere il semplice vocabolario dalle differenze
grammaticali e semantiche. In questo modo il confronto non introdurrà
accidentalmente comportamento C nel linguaggio DDF.
