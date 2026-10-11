# Profilo del compilatore DDF .NET

Stato: contratto `1.0` implementato per il primo slice della milestone `0.13.1`.
Il primo emitter produce DLL, Portable PDB, source map e manifest deterministici;
un publisher separato li persiste atomicamente. Load ed esecuzione restano
disattivati.

## Configurazione fissata

`DdfDotNetCompilerProfile` impedisce che la compilazione dipenda dalle
impostazioni della macchina o dai default variabili di Roslyn:

- Roslyn `Microsoft.CodeAnalysis.CSharp` `5.9.0` e C# 14;
- output `DynamicallyLinkedLibrary`, `Release`, `AnyCpu`;
- nullable abilitato;
- overflow intero checked;
- codice unsafe vietato;
- build deterministica e non concorrente;
- importazione dei soli metadati pubblici.

La versione effettiva di Roslyn e tutte queste opzioni entrano nel fingerprint
del piano. Profilo, piano, Roslyn, C# e reference set vengono inoltre registrati
nella ricevuta del manifest prodotto dall'emitter.

## Reference assembly chiuse

`DdfDotNetReferenceSet` accetta soltanto percorsi assoluti espliciti verso
assembly .NET validi. Per ogni riferimento registra identità assembly, SHA-256,
dimensione e percorso normalizzato, ordina deterministicamente l'insieme e ne
calcola il fingerprint. Riferimenti vuoti, relativi o duplicati sono rifiutati.

Questa API non scandisce assembly già caricati, GAC, directory corrente, COM o
DLL trovate casualmente nel sistema. `DdfDotNetReferencePackResolver` accetta
invece una directory reference pack assoluta fornita dal chiamante e aggiunge
soltanto la runtime DDF e gli assembly autorizzati esplicitamente. Il reference
set risultante entra nelle work item, nel fingerprint del piano e nella ricevuta
persistibile, ed è l'unico insieme usato dal primo emit.

## Generazione sintattica sicura

`DdfDotNetSyntaxTreeGenerator` implementa un primo sottoinsieme verticale
chiuso. Consuma solo IR bound e mapping CLR versionati e costruisce nodi, token,
identificatori e literal con `SyntaxFactory`; non usa `ParseExpression`,
`ParseStatement`, `ParseCompilationUnit` o interpolazione di sorgente C#.

Il sottoinsieme corrente comprende funzioni, parametri e variabili primitive,
blocchi, espressioni e ritorni, `if`, `while`, `do`, `for`, `break`, `end`,
literal, nomi bound, gruppi, operatori fondamentali, postfix checked, array
monodimensionali, indicizzazione sicura, strutture locali, campi, globali,
funzioni standard e chiamate risolte anche tra moduli. Campi e globali non
dipendono dagli initializer CLR: i primi vengono assegnati dal costruttore
contestuale della struttura; i secondi da un coordinatore di modulo idempotente
che inizializza prima le dipendenze e supporta anche chiamate DDF. Elementi non
ancora rappresentabili sono rifiutati con `DDFC400`, senza produrre alberi
parziali o placeholder.

`DdfDotNetCompilationBuilder` crea quindi una `CSharpCompilation` immutabile con
il profilo e il reference set del piano e traduce gli errori semantici generati
in `DDFC403`. Il builder non espone un percorso di emissione e non invoca
`Compilation.Emit`.

`DdfDotNetPlanCompilationBuilder` valida le work item in ordine topologico come
compilazioni separate. Le dipendenze sono `CompilationReference` mantenute in
memoria; la destinazione di ogni chiamata deriva da modulo e ID bound tramite
`DdfDotNetLinkNameTable`. Non viene scritto né caricato alcun assembly.

`DdfDotNetArtifactEmitter` usa lo stesso grafo validato per emettere PE gestiti
deterministici in memoria. Non espone `CSharpCompilation`, non restituisce
artefatti parziali e accompagna ogni DLL con Portable PDB, source map DDF e un
manifest `1.1` contenente la ricevuta del profilo, dei riferimenti e degli import
tipizzati. La
persistenza è responsabilità di
`DdfDotNetArtifactPublisher`, che non accede a Roslyn e non sostituisce mai una
destinazione esistente.

Lo stesso `DdfDotNetReferenceSet` è ora richiesto anche dal verifier pre-load:
il fingerprint deve coincidere con la ricevuta e ogni AssemblyRef esterno deve
corrispondere esattamente a nome, versione, cultura e public key token. Il
reference pack usato in compilazione è quindi anche l'allowlist di verifica, non
una semplice sorgente di file per Roslyn.

Il generatore usa esclusivamente direttive `#line` costruite con `SyntaxFactory`:
ogni avanzamento eseguibile viene associato alla riga DDF originale e tutto il
codice helper torna immediatamente `hidden`. Il corpus ostile accetta soltanto
queste direttive note e verifica che testo, stringhe o percorsi DDF non possano
diventare direttive arbitrarie. Le diagnostiche Roslyn vengono rimappate solo se
il percorso risultante appartiene agli snapshot dell'unità.

Il corpus contrattuale verifica inoltre che testo simile a direttive o codice
C# resti un singolo literal e che nessun nome DDF diventi un identificatore C#.
