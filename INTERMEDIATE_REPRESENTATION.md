# Rappresentazione intermedia DDF

Direzione futura: la stessa IR alimenterà il backend .NET + Roslyn (generazione
C# e compilazione in assembly), la flowchart e le mappe di debug sul sorgente
originale. Il backend è pianificato nella `0.13.x`; quanto segue descrive la base
attuale. Vedere [ECOSYSTEM_ARCHITECTURE.md](ECOSYSTEM_ARCHITECTURE.md).

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
5. gli operatori concreti vengono risolti negli ID stabili dichiarati da `runtime.json`;
6. diagnostica, Build, Run e debugger consumano la IR e i suoi intervalli sorgente;
7. VM e futuri backend ricevono lo stesso modello, senza dipendere dai
   token o dalle classi sintattiche del parser.

## Modello 1.0

La IR espone membri di programma, librerie, strutture, funzioni, parametri,
tipi, dichiarazioni, blocchi, condizioni, cicli, salti ed espressioni. Ogni nodo
conserva `Start`, `Length` ed `End`. Queste coordinate sono usate direttamente
per breakpoint, pausa, diagnostiche runtime e stack trace navigabile.

`DdfIrBuildResult` contiene programma e diagnostiche di lowering. Il flusso di
analisi dell'editor unisce tali diagnostiche a lexer, parser, risoluzione e type
checker. Run viene bloccato se una fase non riesce a rappresentare il sorgente.

`DdfIrModule` associa inoltre ogni programma sorgente a identità/versione del
modulo, export e dipendenze dirette. La VM usa questi metadati per qualificare
funzioni, strutture e globali: più moduli possono conservare gli stessi nomi
privati, mentre il lookup attraversa soltanto gli export dichiarati. Questa è
qualificazione runtime deterministica; il futuro IR bound `2.0` assegnerà anche
un'identità simbolica stabile a ogni riferimento già risolto.

Ogni diagnostica dichiara anche il proprio dominio: `Codice sorgente`,
`Definizione linguaggio`, `Funzionalità motore` oppure `Runtime`. Il lowerer usa
il dominio del motore quando incontra un nodo non rappresentabile; l'interprete
riserva `DDFE00x` alle operazioni IR non implementate e mantiene `DDF4xx` per i
fallimenti prodotti dal programma in esecuzione. Palette, hover e output mostrano
la categoria senza dedurla dal prefisso del codice.

`DdfIrProgram.SemanticOperations` conserva inoltre il piano derivato dal
pacchetto. Un backend può quindi ispezionare sia la struttura normalizzata sia
le intenzioni semantiche dichiarate, senza eseguire codice proveniente dal JSON.
Le espressioni unarie, binarie e postfix conservano il token soltanto per
diagnostica, ma la VM esegue esclusivamente il relativo `OperationId`.

## Compatibilità e passo successivo

La prima implementazione usa un lowerer DDF dalla proiezione AST legacy per
garantire equivalenza completa con il runtime esistente. Il contratto pubblico
della IR non contiene riferimenti a `CompilationUnitSyntax`, `StatementSyntax`
o `ExpressionSyntax`, e `DdfVirtualMachine` opera esclusivamente su nodi `DdfIr*`.

Il backend sostituibile `ddf-vm-ir-v1` usa già questo confine per produrre un
artefatto in memoria e una ricevuta di build deterministica. La successiva
evoluzione sostituirà gradualmente il ponte legacy con il lowering diretto delle
operazioni semantiche pianificate; eventuali estensioni native resteranno
plug-in espliciti e autorizzabili.

L'audit Roslyn ha inoltre fissato il successivo confine: la IR strutturale `1.0`
non verrà passata direttamente al generatore C#. Una IR bound `2.0` dovrà
assegnare identità stabile ai simboli, annotare il tipo di ogni espressione,
materializzare conversioni e destinazioni e risolvere ogni chiamata. In questo
modo Roslyn convalida ed emette il programma senza diventare accidentalmente il
definitore della semantica DDF. I gate completi sono in
`ROSLYN_COMPATIBILITY_AUDIT.md`.
