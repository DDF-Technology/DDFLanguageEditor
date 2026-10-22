# Contratto di integrazione dichiarativa DDF

Stato: **normativo per il processo di integrazione**, versione `1.0`.

`ddf-language-integration.json` è l'inventario autorevole dei livelli che devono
essere valutati quando una funzione del linguaggio diventa attiva. Non è un
secondo lessico e non contiene le grafie concrete delle keyword: usa i ruoli
semantici stabili del pacchetto, mentre ogni profilo risolve autonomamente il
testo (`fixed`, `fisso`, ecc.) tramite il proprio `lexicon.json`.

## Regola di attivazione

Un incremento attivo deve dichiarare evidenze verificabili per specifica, EBNF,
lessico, grammatica, tipi, semantica, runtime, formatter, IntelliSense, libreria
standard, schemi, profili equivalenti e test. Il gate Core verifica che:

- versioni e percorsi coincidano con matrice e pacchetto canonico;
- ruoli, regole, operazioni, diagnostiche, snippet e casi validi, non validi,
  incompleti e multi-file dichiarati esistano davvero nei file del pacchetto;
- tutti i profili installabili espongano gli stessi ruoli e le stesse operazioni,
  pur potendo usare grafie differenti;
- ogni incremento richieda l'intero insieme dei livelli, così un nuovo costrutto
  non può essere dichiarato completato aggiornando soltanto parser o runtime.

Le funzionalità future restano nei documenti di design e nella roadmap: entrano
in `activeIncrements` soltanto quando sono realmente supportate. Il codice UI non
deve introdurre elenchi di keyword paralleli; colorazione, completamento,
formattazione e presentazione continuano a leggere il pacchetto attivo.

## Procedura per un incremento

1. Aggiornare i livelli applicabili e gli schemi senza cambiare silenziosamente
   la semantica di un ruolo esistente.
2. Aggiornare tutti i profili usando gli stessi ruoli e operazioni.
3. Aggiungere casi al pacchetto e al corpus normativo.
4. Registrare l'incremento nell'inventario e mantenere verdi i gate Core,
   profili, VM e Roslyn previsti dalla roadmap.

Il file JSON è distribuito con SDK e applicazione insieme al relativo schema,
così host e strumenti possono ispezionare il contratto senza dipendere dalla UI.
