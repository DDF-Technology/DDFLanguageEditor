# Registro delle decisioni del linguaggio DDF

Le decisioni sono append-only: una scelta superata viene marcata come tale e
collegata alla decisione sostitutiva, senza riscriverne retroattivamente la
motivazione. Gli stati ammessi sono `Accettata`, `Proposta`, `Superata` e
`Rifiutata`.

## DDF-0001 — Un'unica semantica, più profili sintattici

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Contesto:** DDF deve poter offrire una sintassi propria, profili familiari e
  profili didattici localizzati senza fingere di eseguire linguaggi differenti.
- **Decisione:** i profili condividono tipi, semantica, IR, API host e risultati.
  Se un profilo richiede una capacità semantica diversa, è un altro linguaggio o
  una nuova versione, non una semplice traduzione di keyword.
- **Alternative rifiutate:** dichiarare compatibilità C++/Java/C#/VB in base alla
  sola somiglianza sintattica; tradurre automaticamente il file quando si cambia
  pacchetto.
- **Conseguenze:** i test normativi devono poter verificare equivalenza fra
  profili; incompatibilità e conversioni saranno operazioni esplicite.

## DDF-0002 — Codice testuale come unica fonte autorevole

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** flowchart, Outline e modalità didattica sono proiezioni derivate
  dalla semantica del codice e non modificano automaticamente il sorgente.
- **Alternative rifiutate:** editing bidirezionale implicito fra diagramma e testo.
- **Conseguenze:** non esistono conflitti fra due rappresentazioni modificabili;
  la navigazione dal diagramma resta supportata.

## DDF-0003 — Accesso all'host tramite capacità esplicite

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** oggetti, proprietà, eventi, funzioni, database e librerie esterne
  sono utilizzabili soltanto tramite cataloghi e adapter tipizzati registrati
  dall'host, con permessi, limiti e modalità di effetto dichiarati.
- **Alternative rifiutate:** reflection arbitraria dal sorgente, enumerazione
  automatica di tutto l'host, caricamento diretto e non autorizzato di DLL/COM.
- **Conseguenze:** l'interoperabilità con assembly, COM e librerie di sistema
  resterà possibile tramite adapter futuri; l'in-process non è una sandbox.

## DDF-0004 — VM e backend .NET devono condividere la semantica

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** la VM resta l'implementazione di riferimento durante lo sviluppo
  del backend .NET + Roslyn. Risultati, errori, ordine di valutazione e chiamate
  host dovranno essere confrontabili tramite lo stesso corpus.
- **Alternative rifiutate:** generare C# direttamente dalla sintassi ignorando la
  IR; far divergere la modalità embedded da quella standalone.
- **Conseguenze:** la `0.12` deve chiudere le ambiguità semantiche prima della
  toolchain `0.13`.

## DDF-0005 — Originalità subordinata alla chiarezza

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** una grafia differente viene mantenuta quando rende DDF coerente
  e riconoscibile, non soltanto per distinguersi. Gli operatori storici `!!`,
  `>><<`, `<<>>`, `^/`, l'uso logico di `&` e la forma `out` devono superare una
  revisione di leggibilità nella `0.12.1`.
- **Alternative rifiutate:** copiare integralmente C; conservare ogni simbolo
  beta per evitare incompatibilità; cambiare simboli senza beneficio misurabile.
- **Conseguenze:** la beta può introdurre cambiamenti incompatibili documentati e
  accompagnati da una guida di migrazione.

## DDF-0006 — Nucleo lessicale 0.12

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** identificatori Unicode case-sensitive, numeri con cifre ASCII,
  booleani esclusivamente `true`/`false`, stringhe su una riga e insieme chiuso
  di escape `\"`, `\\`, `\n`, `\r`, `\t`. I commenti restano `//` e `/* */`.
- **Alternative rifiutate:** cifre Unicode non interpretabili uniformemente dal
  runtime; alias booleani con maiuscole; stringhe multilinea implicite; escape
  sconosciuti accettati silenziosamente.
- **Conseguenze:** `True` e `False` diventano identificatori, gli escape invalidi
  producono `DDF005` e i progetti dichiarano il pacchetto DDF `0.12.0`.

## DDF-0007 — Operatori del nucleo 0.12

- **Stato:** Accettata
- **Data:** 2026-09-26
- **Decisione:** DDF conserva `<<` per assegnazione, `>>` per output e `^` per
  potenza. Usa `!=`, `&&`, `||`, `^^` e `!` per confronto e logica. Rimuove dal
  pacchetto canonico `!!`, `&`, `|&|`, `>><<`, `<<>>` e `^/`.
- **Motivazione:** `!!` contraddice l'uso comune di `!`; `&` suggerisce
  un'operazione bitwise non disponibile; gli operatori di intervallo beta non
  codificavano due estremi e nella VM producevano risultati fuorvianti; `^/`
  era poco leggibile. `^^` mantiene distinta la potenza dallo XOR senza
  introdurre keyword speciali nel lexer.
- **Alternative rifiutate:** conservare ogni simbolo beta; copiare anche
  l'assegnazione `=`; introdurre ora operatori testuali che richiederebbero
  regole di confine lessicale ancora non definite.
- **Conseguenze:** il pacchetto passa a `0.12.1`; i sorgenti beta richiedono la
  migrazione esplicita descritta in `DDF_MIGRATION_0_12.md`. Intervalli e radici
  saranno funzioni di libreria leggibili se approvate nella `0.12.3`.
