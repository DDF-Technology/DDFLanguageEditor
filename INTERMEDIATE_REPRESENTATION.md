# Rappresentazione intermedia DDF

La IR `1.0` separa l'esecuzione dalla sintassi concreta del linguaggio. Il parser
continua temporaneamente a produrre anche l'AST DDF legacy, ma il lowerer lo
converte subito in nodi `DdfIr*`; l'interprete non legge più direttamente le
classi `*Syntax`.

## Pipeline corrente

1. il pacchetto attivo fornisce lessico, grammatica, tipi e collegamenti semantici;
2. il parser produce AST generico e proiezione DDF di compatibilità;
3. `DdfSemanticOperationPlanner` associa all'AST generico le operazioni sicure
   dichiarate in `semantics.json`;
4. `DdfIrLowerer` crea un `DdfIrProgram` immutabile e conserva il piano semantico;
5. diagnostica, Run e debugger consumano la IR e i suoi intervalli sorgente;
6. interprete e futuri backend ricevono lo stesso modello, senza dipendere dai
   token o dalle classi sintattiche del parser.

## Modello 1.0

La IR espone membri di programma, librerie, strutture, funzioni, parametri,
tipi, dichiarazioni, blocchi, condizioni, cicli, salti ed espressioni. Ogni nodo
conserva `Start`, `Length` ed `End`. Queste coordinate sono usate direttamente
per breakpoint, pausa, diagnostiche runtime e stack trace navigabile.

`DdfIrBuildResult` contiene programma e diagnostiche di lowering. Il flusso di
analisi dell'editor unisce tali diagnostiche a lexer, parser, risoluzione e type
checker. Run viene bloccato se una fase non riesce a rappresentare il sorgente.

Ogni diagnostica dichiara anche il proprio dominio: `Codice sorgente`,
`Definizione linguaggio`, `Funzionalità motore` oppure `Runtime`. Il lowerer usa
il dominio del motore quando incontra un nodo non rappresentabile; l'interprete
riserva `DDFE00x` alle operazioni IR non implementate e mantiene `DDF4xx` per i
fallimenti prodotti dal programma in esecuzione. Palette, hover e output mostrano
la categoria senza dedurla dal prefisso del codice.

`DdfIrProgram.SemanticOperations` conserva inoltre il piano derivato dal
pacchetto. Un backend può quindi ispezionare sia la struttura normalizzata sia
le intenzioni semantiche dichiarate, senza eseguire codice proveniente dal JSON.

## Compatibilità e passo successivo

La prima implementazione usa un lowerer DDF dalla proiezione AST legacy per
garantire equivalenza completa con il runtime esistente. Il contratto pubblico
della IR non contiene riferimenti a `CompilationUnitSyntax`, `StatementSyntax`
o `ExpressionSyntax`, e l'interprete opera esclusivamente su nodi `DdfIr*`.

La successiva evoluzione sostituirà gradualmente il ponte legacy con il lowering
diretto delle operazioni semantiche pianificate e definirà estensioni native
opzionali soltanto tramite plug-in espliciti e autorizzabili.
