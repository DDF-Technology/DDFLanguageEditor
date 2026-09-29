# Inventario del linguaggio DDF corrente

Stato: inventario evolutivo della beta, aggiornato al contratto DDF `0.12.3`.

Questo inventario separa il comportamento realmente disponibile dalle scelte
ancora aperte. Le fonti eseguibili sono i file in `language-packages/ddf`; la
vecchia `DDF - Program Language Spec.txt` è soltanto una bozza storica.

## Legenda

- **Implementato**: riconosciuto e coperto dal motore corrente.
- **Compatibilità beta**: mantenuto per sorgenti sperimentali, non approvato per
  la sintassi definitiva.
- **Parziale**: esiste nel lessico o nei tipi ma non ha ancora un contratto
  completo in analisi, runtime e build.
- **Aperto 0.12**: richiede una decisione esplicita.

## Lessico

| Area | Comportamento corrente | Stato | Decisione 0.12 |
| --- | --- | --- | --- |
| Identificatori | lettera Unicode o `_`, poi lettere, cifre o `_` | Implementato | decidere normalizzazione, maiuscole e parole riservate |
| Numeri | interi o decimali con `.` e cifre ASCII dopo il separatore | Definito 0.12 | segno, range e overflow passano alla semantica; basi/esponenti richiedono una nuova decisione |
| Stringhe | `"..."` su una riga; escape `\"`, `\\`, `\n`, `\r`, `\t` | Definito 0.12 | multilinea e interpolazione escluse dal nucleo corrente |
| Commenti | `//` e `/* ... */` | Implementato | decidere annidamento e commenti documentali |
| Direttiva libreria | `@@'nome'` | Parziale | ridefinire come dipendenza/capacità o sostituirla |
| Booleani | `true`, `false` | Definito 0.12 | confronto case-sensitive; alias maiuscoli rimossi |
| Terminatore | `;` | Implementato | confermare obbligatorietà e casi di omissione |
| Blocchi | `{ ... }` | Implementato | confermare come forma DDF o introdurre delimitatori propri |

## Parole chiave e tipi

| Gruppo | Forme correnti | Stato / questione aperta |
| --- | --- | --- |
| Tipi primitivi | `int`, `float`, `char`, `bool`, `void`, `string` | Contratto normativo in `DDF_TYPE_SYSTEM_SPEC.md`; allineamento del motore in corso |
| Tipo dizionario | `dict` | Residuo beta da rimuovere; escluso dal nucleo normativo 0.12 |
| Strutture | `struct` | Contratto nominale non nullo definito; allineamento del motore in corso |
| Funzioni | `out`, `ret` | Conservate come forma distintiva DDF nella `0.12.1` |
| Controllo | `if`, `else`, `while`, `do`, `for` | Implementato con struttura prevalentemente C-like |
| Salti controllati | `brk`, `end` | Implementati; chiarire scope di `end` e future alternative |

## Operatori

| Famiglia | Forme correnti | Stato / rischio |
| --- | --- | --- |
| Assegnazione | `<<` | Confermato e distintivo; non rappresenta uno shift |
| Output | `>>` verso `Console` | Confermato nel nucleo; destinazioni e capacità saranno formalizzate nella semantica |
| Aritmetica | `+`, `-`, `*`, `/`, `^`, `++`, `--` | Contratto 0.12.1; `^` significa potenza |
| Confronto | `<`, `<=`, `>`, `>=`, `==`, `!=` | Contratto 0.12.1 |
| Intervallo | nessun operatore | Le forme beta difettose sono state eliminate; usare confronti espliciti |
| Logica | `!`, `&&`, `||`, `^^` | Contratto 0.12.1; `^^` è XOR booleano |
| Accesso | `.`, `[]`, chiamata `()` | Implementato per membri, indici e invocazioni |

La precedenza corrente, dalla più bassa alla più alta, è: assegnazione/output,
OR, XOR, AND, uguaglianza, confronto ordinato, somma, prodotto, potenza,
prefissi e postfix. Assegnazione e potenza sono associative a destra; le altre
forme binarie sono associative a sinistra.

## Costrutti sintattici

| Costrutto | Forma corrente | Stato / decisione |
| --- | --- | --- |
| Unità | direttive seguite da strutture, funzioni o variabili globali | Contratto 0.12.1; istruzioni top-level eliminate |
| Funzione | `nome(parametri) out tipo { ... }` | Contratto 0.12.1; ordine e marcatore `out` confermati |
| Ingresso | unica funzione `main` scelta dal manifest/runtime | Implementato; firma valida da specificare |
| Variabile | `tipo nome [<< espressione];` | Implementata; inizializzazione obbligatoria da decidere |
| Array | tipo con suffisso `[dimensione]`, accesso `[indice]` | Implementato; dimensione, durata e collezioni dinamiche aperte |
| Struttura | `struct Nome { dichiarazioni }` | Implementata; costruttori e valori iniziali assenti |
| Condizione | `if (condizione) { ... } [else { ... } | else if ...]` | Contratto 0.12.1; blocchi obbligatori |
| Cicli | `while`, `do ... while`, `for` con corpo `{ ... }` | Contratto sintattico 0.12.1; semantica del `for` da chiudere |
| Ritorno | `ret [espressione];` | Implementato |
| Interruzione | `brk;` | Implementato soltanto nel ciclo corrente |
| Fine programma | `end;` | Implementato come terminazione esplicita |

## Semantica beta corrente

La lista seguente fotografa ancora l'implementazione e non sostituisce il
contratto normativo di `DDF_TYPE_SYSTEM_SPEC.md`. In particolare conversione
numerica di `char`, shadowing e `dict` sono gap da rimuovere.

- scope separati per funzioni, blocchi e inizializzatori `for`;
- parametri, variabili e campi mutabili; shadowing consentito;
- array invarianti e tipi utente nominali;
- arità esatta, ordine dei parametri dichiarato e ritorno obbligatorio per
  funzioni non `void`;
- conversioni implicite `int → float`, `char → float` e stringa di un carattere
  verso `char` soltanto nell'inizializzazione;
- condizioni obbligatoriamente booleane;
- funzioni, strutture e globali condivise fra i sorgenti del manifest;
- inizializzazione globale deterministica, con dipendenze in avanti o cicliche
  rifiutate;
- `main` è unico; Run e Debug usano lo snapshot coerente di tutti i buffer;
- limite predefinito di 100.000 istruzioni della VM.

## Confronto mirato con C

| Tema | C | DDF beta | Direzione proposta |
| --- | --- | --- | --- |
| Assegnazione | `=` | `<<` | conservare un'identità distinta, verificandone l'ambiguità visiva |
| Tipo di ritorno | prima del nome | dopo `out` | valutare e documentare la lettura da sinistra a destra |
| Ritorno | `return` | `ret` | mantenere una forma breve solo se resta immediata |
| Disuguaglianza | `!=` | `!=` | adottato per chiarezza e coerenza con `!` |
| Potenza | funzione di libreria | `^` | mantenere semantica matematica esplicita, evitando falsa compatibilità |
| AND | `&&` logico, `&` bitwise | `&&` logico | adottato; DDF non espone `&` bitwise nel nucleo |
| Controllo | parentesi e graffe | forma simile | differenziare dove migliora la leggibilità, non per originalità artificiale |
| Preprocessore | macro e `#include` | assente, `@@` parziale | usare dipendenze strutturate, non macro testuali |
| Memoria | puntatori e gestione esplicita | assenti | esclusi dal nucleo DDF 1.0 |
| API esterne | header, ABI e librerie | catalogo host autorizzato | mantenere il modello a capacità tipizzate |
| Multi-file | unità e linker | manifest di progetto condiviso | formalizzare moduli/import nella `0.12.3` |

## Funzioni volutamente non assunte

Non sono parte del linguaggio soltanto perché comuni altrove: `switch`, `goto`,
eccezioni, classi, ereditarietà, generics, lambda, overload, valori nulli,
puntatori, macro, concorrenza e gestione diretta della memoria. Ognuna richiede
un caso d'uso, una decisione e operazioni sicure nella IR.

## Confronto con altri modelli affini

| Modello | Aspetto utile | Confine DDF |
| --- | --- | --- |
| C/C# | tipi espliciti, blocchi e tooling maturo | nessuna compatibilità ABI o semantica implicita; niente puntatori nel nucleo |
| Python | ciclo di prova rapido e leggibilità | DDF conserva tipi e capacità host espliciti, senza lookup dinamico globale |
| BASIC/VB | sintassi verbosa e localizzabile | i profili possono essere più discorsivi, ma condividono la stessa IR |
| Structured Text / pseudocodice | vicinanza ad automazione e didattica | DDF non dichiara conformità IEC né certificazioni industriali |
| Linguaggi di scripting embedded | estensione dell'applicazione host | l'host espone un catalogo tipizzato, non l'intero object model per reflection |

## Disposizione delle forme storiche

| Forma o comportamento | Provenienza | Disposizione 0.12.0 |
| --- | --- | --- |
| `//` e `/* ... */` | implementazione corrente | conservare come baseline, dettagli ancora aperti |
| `..` e `0.Text.0` per i commenti | prima bozza testuale | eliminare; mai supportati dal pacchetto corrente |
| `*` per la moltiplicazione | implementazione corrente | conservare come baseline |
| `x` per la moltiplicazione | prima bozza testuale | eliminare perché indistinguibile da un identificatore |
| `ret valore;` | implementazione corrente | conservare come candidata, da validare nella `0.12.1` |
| `nomeFunzione << valore` come ritorno | prima bozza testuale | eliminare; confligge con l'assegnazione corrente |
| `do { ... } while (...);` | implementazione corrente | conservare come baseline |
| `do while (...) { ... }` | prima bozza testuale | eliminare salvo futuro profilo esplicito |
| `++(valore)` / `--(valore)` | prima bozza testuale | eliminare; mai formalizzati |
| `True` / `False` | compatibilità beta | eliminare come letterali; restano identificatori ordinari |
| istruzioni al livello principale | parser beta | eliminate dal programma valido; il parser storico può recuperarle soltanto per tooling interno |
| `@@'Library'` | lexer e parser beta | modificare: sostituire il significato provvisorio con dipendenze formali |
| `dict` | lessico e tipi beta | eliminare dal nucleo 0.12; una futura mappa dovrà essere parametrica e tipizzata |

## Debito documentale rilevato

- `LANGUAGE_REFERENCE.md` fotografa un sottoinsieme precedente e contiene ancora
  formulazioni superate dal runtime multi-file;
- `DDF_LANGUAGE_CATALOG.md` è utile come catalogo beta, non come specifica finale;
- `DDF_GRAMMAR.md` riassume l'EBNF ma non deve diventare una seconda fonte;
- `DDF - Program Language Spec.txt` usa commenti, moltiplicazione e ritorni non
  coerenti con l'implementazione ed è conservato solo come documento storico.
