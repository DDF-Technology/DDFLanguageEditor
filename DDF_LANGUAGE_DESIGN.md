# DDF language design baseline

Stato: baseline di progetto `0.12.0`, 2026-09-26.

Questo documento definisce l'identità del linguaggio DDF prima del congelamento
della sua sintassi. Non è ancora la specifica normativa: descrive obiettivi,
principi e confini ai quali lessico, grammatica e semantica della `0.12.x`
dovranno conformarsi.

## Scopo

DDF è un linguaggio semplice per scrivere automazioni comprensibili che possono:

1. estendere in modo controllato un'applicazione host tramite API esplicitamente
   pubblicate;
2. essere eseguite, osservate e sottoposte a debug nello stesso modo in modalità
   embedded e standalone;
3. diventare, con il futuro backend .NET, applicazioni autonome distribuibili;
4. rendere visibile il flusso del programma mediante flowchart e strumenti
   didattici derivati dal codice.

I casi d'uso prioritari sono scripting applicativo, elaborazione di dati,
calcoli matematici e logici, regole personalizzate e automazione di oggetti
dell'host. La didattica è un'esperienza trasversale, non una semantica diversa.

## Elementi distintivi

- Un'unica semantica può essere espressa da profili sintattici differenti. Un
  profilo ispirato a C o localizzato in italiano non dichiara compatibilità con
  C, Java, C# o altri ecosistemi.
- Le capacità dell'applicazione host sono tipizzate, documentate e autorizzate
  esplicitamente. Il sorgente non ottiene reflection o accesso globale al sistema.
- Editor, analisi, VM, debugger, flowchart e futuro compilatore consumano la
  stessa rappresentazione intermedia e gli stessi contratti semantici.
- Il codice testuale è la fonte autorevole. Flowchart e modalità didattica lo
  spiegano, ma non lo riscrivono automaticamente.
- Operazioni riconoscibili come `<<`, `out`, `ret`, `brk` ed `end` danno a DDF
  un'identità propria; la loro forma definitiva sarà decisa solo dopo averne
  verificato leggibilità e assenza di ambiguità.

## Principi vincolanti

### Leggibilità

Un costrutto deve avere un significato principale, essere riconoscibile senza
conoscere dettagli dell'editor e produrre formattazione deterministica. Le forme
brevi sono preferibili soltanto quando restano chiare.

### Semplicità progressiva

Il programma minimo deve richiedere pochi concetti. Funzioni avanzate, API host,
moduli e librerie devono aggiungersi senza cambiare il significato del nucleo.

### Esplicitezza

Conversioni potenzialmente distruttive, effetti sull'host, dipendenze e confini
di modulo non devono essere impliciti. La sintassi non deve fingere di offrire
capacità che il pacchetto o l'host non possiedono.

### Sicurezza per capacità

Uno script embedded vede soltanto tipi, valori, proprietà, funzioni, eventi e
database registrati dall'host. Il runtime in-process non viene descritto come
sandbox; il codice non fidato richiede un processo separato.

### Prevedibilità

Ordine di valutazione, scope, inizializzazione, mutabilità, conversioni, errori
e limiti runtime devono essere specificati e testabili. Lo stesso programma e
lo stesso catalogo host devono avere risultati osservabili equivalenti in VM e
nel futuro backend .NET.

### Strumenti derivati dalla semantica

Colorazione, IntelliSense, formatter, diagnostica, debugger e flowchart devono
dipendere da lessico, grammatica, modello semantico e IR, non da euristiche sul
testo o liste duplicate nel codice dell'editor.

### Evoluzione dichiarata

Ogni cambiamento incompatibile richiede versione del linguaggio, decisione
registrata, corpus aggiornato e indicazioni di migrazione. Caricare un profilo
non traduce né modifica automaticamente un sorgente esistente.

## Non obiettivi della 1.0

- essere un sostituto o un'implementazione completa di C, C++, C#, Java o VB;
- caricare librerie native, assembly o componenti COM senza una policy e un
  adapter espliciti;
- offrire puntatori, aritmetica della memoria, preprocessore testuale o
  reflection arbitraria;
- usare la flowchart come seconda fonte modificabile del programma;
- promettere hard real-time, isolamento di sicurezza in-process o certificazioni
  proprie di uno specifico settore industriale;
- mantenere per compatibilità ogni forma sperimentale delle beta.

## Gerarchia delle fonti durante la 0.12

Finché non esiste la specifica candidata, il comportamento verificabile è dato,
nell'ordine, dal pacchetto `language-packages/ddf`, dai test automatici e dagli
adapter sicuri del Core. `DDF_CURRENT_LANGUAGE_INVENTORY.md` fotografa tale
comportamento; i documenti precedenti restano guide o materiale storico.

Alla chiusura della `0.12.4`, specifica normativa, EBNF, pacchetto e corpus
dovranno descrivere lo stesso linguaggio. Una divergenza sarà un errore di
release, non una variante implicita.

## Passaggio successivo

La `0.12.1` deciderà il lessico e la sintassi definitiva partendo dalle voci
aperte dell'inventario. Nessuna grafia provvisoria diventa definitiva soltanto
perché è già implementata.
