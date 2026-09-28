# Audit di compatibilità DDF con .NET e Roslyn

Stato: baseline verificata il 2026-09-28, prima del backend `0.13.x`.

## Esito

L'architettura realizzata è **compatibile come direzione** con un backend
.NET/Roslyn: Core e SDK sono già .NET 10, la sintassi concreta viene separata
dalla IR, le operazioni hanno ID stabili, il sistema di build ammette backend
sostituibili e il confine host usa tipi e delegate CLR espliciti.

Il progetto non è ancora **conforme end-to-end**: oggi Roslyn compila
l'ecosistema C#, non i programmi DDF. Prima di generare assembly è obbligatorio
completare i gate elencati sotto. Nessuna funzione corrente viene dichiarata
compatibile con il backend compilato finché il corpus differenziale VM/Roslyn
non produce gli stessi valori, errori, effetti host e punti di debug.

## Verifica eseguita

- SDK selezionato da `global.json`: .NET `10.0.101`, prerelease disabilitate.
- Build Release dell'intera soluzione con il compilatore dello SDK:
  **0 warning, 0 errori**.
- Suite Core Release: **209/209 test superati**.
- Progetti compilati: Core, SDK WinForms, launcher standalone, test, Host Demo,
  Database Demo e Minimal Host.
- Audit statico di IR, type checker, VM, manifest, pacchetti linguaggio,
  standard library, debugger e catalogo host.

Queste prove attestano la compatibilità del codice dell'ecosistema con .NET 10;
non sostituiscono il futuro test di compilazione DDF -> IR -> C# -> IL.

## Matrice delle componenti

| Area | Stato Roslyn | Evidenza / azione necessaria |
| --- | --- | --- |
| soluzione e SDK | pronta | tutti i progetti targettizzano `net10.0`; build deterministica riuscita |
| lessico e profili | pronta con lowering | le grafie locali spariscono dietro ruoli e ID semantici; non vanno copiate nel C# |
| parser e AST | pronta come front-end | Roslyn non deve analizzare DDF; riceve C# generato dalla IR |
| IR strutturale 1.0 | base valida | nodi immutabili, moduli, operazioni e span sono presenti |
| IR semantica | bloccante | mancano simboli risolti, tipi di ogni espressione e conversioni esplicite |
| tipi primitivi | mapping definito | tabella DDF -> CLR nella specifica; manca il contratto eseguibile nel pacchetto |
| array | compatibile con vincoli | mapping a SZArray `T[]`; rango uno, allocazione e default non-null da imporre |
| strutture | compatibile con lowering | generare classi sigillate; vietare cicli di contenimento non-null |
| operatori | compatibili con helper | potenza, non-finite, output e `end` richiedono lowering/runtime DDF |
| funzioni e scope | compatibili | contenitore statico; nomi e chiamate devono usare ID di simbolo, non testo grezzo |
| multi-file e globali | compatibili con piano | ordine già deterministico; moduli/import restano da chiudere nella `0.12.3` |
| standard library | compatibile via ABI | binding stabili esistono; serve una runtime library compilata e versionata |
| API host | base molto favorevole | delegate, adapter, permessi e snapshot CLR; usare un bridge, mai reflection implicita |
| effetti e database | compatibili via bridge | conservare cancellazione, timeout, simulazione e transazioni correnti |
| embedded | compatibile | VM/JIT resta opzionale; niente compilazione o load senza consenso dell'host |
| standalone EXE | pianificato | mancano emit PE/PDB, runtimeconfig, dipendenze, publish e firma |
| debugger | parzialmente pronto | gli span DDF esistono; servono sequence point e Portable PDB sul sorgente DDF |
| flowchart/IDE | indipendenti | consumano IR e analisi, non devono dipendere dal C# generato |
| sicurezza | compatibile come modello | deny-by-default presente; load/unload dell'assembly va isolato |

## Gate obbligatori prima del generatore C#

### R1 — IR bound e tipizzata

La IR destinata ai backend deve contenere:

- un ID stabile per ogni simbolo dichiarato e ogni riferimento;
- il tipo DDF risolto di ogni espressione;
- conversioni implicite rappresentate come nodi espliciti;
- distinzione fra locale, parametro, globale, campo, funzione standard e capacità host;
- l-value già validati e firma risolta per ogni chiamata;
- esito dell'analisi dei flussi e dei ritorni.

Il generatore non deve chiedere a Roslyn di reinterpretare la semantica DDF.
Roslyn deve convalidare il C# prodotto e trasformarlo in IL.

### R2 — Mapping tipi eseguibile

`types.json` e il relativo schema devono dichiarare mapping CLR versionati. Il
loader deve rifiutare mapping mancanti, duplicati, incompatibili con la categoria
DDF o non supportati dal backend. I profili sintattici equivalenti devono usare
esattamente gli stessi mapping.

Tipi nuovi richiedono contemporaneamente adapter per checker, VM, Roslyn, host,
debugger e serializzazione. Una stringa contenente un nome CLR non è sufficiente
e non autorizza il caricamento del relativo assembly.

### R3 — Semantica numerica identica

Il C# generato deve usare contesto `checked` per gli interi e helper DDF per:

- potenza intera e floating;
- rifiuto di NaN e infinito;
- divisione per zero e caso `int.MinValue / -1`;
- conversioni esplicite della libreria con cultura invariant;
- traduzione delle eccezioni CLR in diagnostiche runtime DDF stabili.

Il `checked` C# non controlla l'overflow di `double`; il controllo DDF deve quindi
essere emesso esplicitamente dopo ogni operazione che può produrre non-finite.

### R4 — Nomi generati sicuri

I nomi DDF non devono essere concatenati direttamente nel sorgente C#. Il
backend deve assegnare nomi interni deterministici basati sugli ID di simbolo,
gestendo keyword C#, identificatori Unicode, normalizzazione, `_`, nomi riservati
e collisioni con helper generati. Il nome originale resta nei metadati di debug.

Il C# deve essere costruito con le API sintattiche Roslyn o con token escapati e
validati; sorgente, stringhe, direttive e nomi DDF non possono diventare frammenti
C# arbitrari.

### R5 — Runtime ABI e host bridge

Creare una piccola libreria runtime versionata, indipendente dalla UI, che
fornisca controlli numerici, output, input, budget, cancellazione, `end`, errori
DDF e ponte alle capacità host. L'assembly generato dipende da questa ABI e non
da classi WinForms o dettagli interni della VM.

Le chiamate host compilate devono attraversare lo stesso catalogo, gli stessi
adapter, permessi, timeout, criteri di effetto e transazioni della VM. Nessun
delegate deve essere serializzato nell'assembly.

### R6 — Riferimenti e dipendenze riproducibili

Il backend deve compilare contro un insieme chiuso di reference assembly .NET,
la runtime DDF e le sole librerie autorizzate dal manifest. Versione SDK,
versione Roslyn, Target Framework, riferimenti, hash e opzioni entrano nella
ricevuta e nel fingerprint di build.

Non si devono usare implicitamente assembly caricati nel processo, GAC, COM,
cartelle correnti o reflection per “far compilare” un simbolo mancante.

### R7 — Emit deterministico e diagnostiche

Usare una `CSharpCompilation` immutabile con versione C# fissata, nullable e
overflow espliciti, ottimizzazione dichiarata, percorsi normalizzati ed emit PE
e Portable PDB deterministici. Le diagnostiche Roslyn vanno tradotte verso
documento e span DDF; gli errori del C# generato restano errori del backend
quando non esiste una mappa certa verso il sorgente.

### R8 — Debug e sorgenti

Ogni istruzione DDF eseguibile deve avere un sequence point riferito al documento
DDF originale. Helper, conversioni e controlli generati devono essere nascosti
o ricondotti al nodo responsabile. Breakpoint, stepping, call stack e variabili
devono usare le identità della IR bound.

Il formato previsto è Portable PDB, con hash dei documenti e sorgenti incorporati
o Source Link quando applicabile. Il debugger VM resta l'oracolo del corpus.

### R9 — Packaging e caricamento

Lo standalone deve produrre assembly, PDB, `.deps.json`, `.runtimeconfig.json` e
pubblicazione coerente col runtime scelto. Il percorso embedded deve restare
governato dall'host: compilazione, caricamento e persistenza sono opt-in.

Assembly dinamici o di terze parti vanno caricati in un confine esplicito e,
quando richiesto, scaricabile/isolato. La compatibilità Roslyn non equivale a
sandbox di sicurezza.

### R10 — Confronto differenziale

Prima di rendere Roslyn il backend predefinito, lo stesso corpus deve eseguire su
VM e assembly e confrontare valore restituito, output, errori e posizione,
ordine di valutazione, overflow, conversioni, array, strutture, globali,
multi-file, effetti host, breakpoint, stack e variabili. Ogni divergenza blocca
la release oppure richiede una decisione DDF versionata.

## Questioni rilevate durante l'audit

1. La IR corrente non è ancora bound o completamente tipizzata.
2. Il pacchetto non contiene ancora mapping CLR verificabili.
3. I gap della `0.12.2` produrrebbero divergenze fra VM e assembly.
4. Le strutture ricorsive non-null devono essere diagnosticate.
5. I nomi DDF richiedono mangling deterministico.
6. Non esiste ancora la runtime ABI usata dall'assembly generato.
7. Il backend e le dipendenze `Microsoft.CodeAnalysis` non sono ancora presenti:
   è corretto introdurli soltanto dopo la chiusura dei gate semantici.
8. Gli span sorgente esistono, ma non la mappa DDF/IL/PDB.
9. Opzioni, reference assemblies e packaging devono entrare nell'artefatto.
10. Le chiamate host asincrone richiedono un bridge che mantenga timeout e
    cancellazione senza cambiare la semantica sincrona DDF.

## Ordine di realizzazione approvato

1. completare `0.12.2` e il corpus dei tipi;
2. formalizzare moduli e libreria standard `0.12.3`;
3. produrre IR bound `2.0` e runtime ABI minima;
4. creare un backend Roslyn verticale su un sottoinsieme chiuso;
5. aggiungere PE/PDB e confronto differenziale;
6. estendere host bridge, packaging e debug;
7. rendere Roslyn selezionabile, poi predefinito soltanto dopo i gate.

## Riferimenti tecnici

- [Roslyn compiler API model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/compiler-api-model)
- [Roslyn semantic model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-semantics)
- [C# checked e unchecked](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked)
- [Regole degli identificatori C#](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [Simboli .NET e Portable PDB](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/symbols)
- [Roslyn Compilation.Emit](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.emit)
