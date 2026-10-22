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

La IR espone membri di programma, librerie, strutture, enum nominali, funzioni, parametri,
tipi, dichiarazioni, blocchi, condizioni, cicli, salti ed espressioni. Ogni nodo
conserva `Start`, `Length` ed `End`. Queste coordinate sono usate direttamente
per breakpoint, pausa, diagnostiche runtime e stack trace navigabile.

I salti di ciclo sono nodi distinti: `DdfIrBreak` punta semanticamente
all'uscita del ciclo più interno, mentre `DdfIrContinue` punta alla sua prossima
verifica; nel `for` la destinazione attraversa prima l'incremento. VM, backend
Roslyn e Flowchart consumano la stessa distinzione e non la ricostruiscono dal
testo della keyword.

Gli enum usano `DdfIrEnum` e `DdfIrEnumMember`; la IR conserva identità e membri
simbolici, non un valore numerico Roslyn. La IR bound distingue i simboli
`Enumeration` e `EnumerationMember`, marca questi ultimi in sola lettura e
permette ai backend di generare la propria rappresentazione senza cambiare la
semantica nominale.

La selezione multipla usa `DdfIrSelect` e `DdfIrSelectClause`. Il valore
selezionato compare una sola volta nella IR, ogni clausola conserva valore,
corpo e natura `otherwise`, e non esiste un arco di fall-through. VM e Roslyn
consumano la stessa struttura; il backend compilato materializza un temporaneo
per garantire la singola valutazione dell'espressione.

`DdfIrBuildResult` contiene programma e diagnostiche di lowering. Il flusso di
analisi dell'editor unisce tali diagnostiche a lexer, parser, risoluzione e type
checker. Run viene bloccato se una fase non riesce a rappresentare il sorgente.

`DdfIrModule` associa inoltre ogni programma sorgente a identità/versione del
modulo, export e dipendenze dirette. La VM usa questi metadati per qualificare
funzioni, strutture e globali: più moduli possono conservare gli stessi nomi
privati, mentre il lookup attraversa soltanto gli export dichiarati. Questa è
qualificazione runtime deterministica; la IR bound `2.1` assegna inoltre
un'identità simbolica stabile a ogni riferimento già risolto e conserva se una
funzione host è pura o mutante.

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

La futura gestione risorse `DDF-0041` richiederà regioni cleanup esplicite e le
operazioni backend-neutral `resource.acquire`, `resource.scope.enter`,
`resource.borrow` e `resource.release`. La IR dovrà conservare ownership e ordine
LIFO senza incorporare `IDisposable`, `using`, `try/finally` C# o handle nativi;
VM e Roslyn consumeranno lo stesso piano già verificato.

La sospensione cooperativa `DDF-0042` aggiungerà le operazioni
`suspend.request`, `suspend.resume`, `suspend.cancel` e `suspend.discard`. Un
punto di ripresa conserverà identità sorgente e soli valori DDF o lease opachi,
mai `Task`, `ValueTask`, delegate, eccezioni o frame CLR. La continuazione sarà
legata alla generazione dell'esecuzione, così una risposta tardiva o duplicata
non potrà riattivare un programma terminato. VM e Roslyn dovranno attraversare
gli stessi sequence point prima e dopo l'attesa.

La prima implementazione usa un lowerer DDF dalla proiezione AST legacy per
garantire equivalenza completa con il runtime esistente. Il contratto pubblico
della IR non contiene riferimenti a `CompilationUnitSyntax`, `StatementSyntax`
o `ExpressionSyntax`, e `DdfVirtualMachine` opera esclusivamente su nodi `DdfIr*`.

Il backend sostituibile `ddf-vm-ir-v1` usa già questo confine per produrre un
artefatto in memoria e una ricevuta di build deterministica. La successiva
evoluzione sostituirà gradualmente il ponte legacy con il lowering diretto delle
operazioni semantiche pianificate; eventuali estensioni native resteranno
plug-in espliciti e autorizzabili.

Dal contratto compilatore `1.0`, ogni modulo validato viene inoltre proiettato in
una `DdfCompilationUnit`: snapshot sorgenti, documenti IR e interfaccia esportata
restano livelli immutabili distinti. Le unità sono descritte in
[DDF_COMPILATION_UNIT_CONTRACT.md](DDF_COMPILATION_UNIT_CONTRACT.md) e non sono
di per sé un formato persistente.

L'audit Roslyn ha inoltre fissato un secondo confine: la IR strutturale `1.0`
non viene passata direttamente al generatore C#. La IR bound `2.1`, prodotta
dalla stessa preparazione Core, assegna identità simboliche deterministiche,
annota il tipo e la categoria di valore di ogni espressione, materializza la
conversione contestuale, risolve le chiamate e conserva l'esito del flusso dei
ritorni e la natura degli effetti host prima che inizi il backend.

`DdfBoundCompilationUnit` contiene una vista per documento sopra l'albero
strutturale e una tabella di simboli immutabile. `DdfBoundExpression` distingue
valori, destinazioni modificabili, destinazioni in sola lettura, tipi e callable;
conserva inoltre il simbolo referenziato, la destinazione esatta di una chiamata
e la conversione `Identity` o `Implicit` richiesta dal contesto. Le identità non
dipendono dal percorso assoluto del workspace e il fingerprint bound partecipa
al fingerprint dell'unità di compilazione.

`DdfBoundFunctionFlow` registra inoltre se una funzione restituisce sempre un
valore, può raggiungere la fine o contiene un arresto `end`; il backend non deve
ricalcolare queste proprietà attraverso la semantica accidentale del C#.

I simboli privati delle dipendenze non entrano nella vista del chiamante; quelli
esportati, le funzioni standard e le capability host autorizzate ricevono invece
identità esplicite. I callable standard conservano inoltre il loro
`SemanticBindingId`, separato dal nome visibile e quindi stabile anche nei
profili sintattici tradotti. Il generatore C# consuma esclusivamente questa vista,
così Roslyn convaliderà ed emetterà il programma senza diventare accidentalmente
il definitore della semantica DDF. I gate completi sono in
`ROSLYN_COMPATIBILITY_AUDIT.md`.
