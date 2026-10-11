# Riferimenti esterni DDF

Stato: contratto incrementale della `0.13.4`. Il manifest progetto `1.2` può
dichiarare dipendenze esterne logiche, ma non può indicare percorsi arbitrari,
CLSID, ProgID, entry point nativi o nomi da caricare direttamente. Un resolver
autorizzato dovrà trasformare in seguito la dichiarazione in un adapter
tipizzato e in una ricevuta bloccata.

## Dichiarazione

```json
"references": [
  {
    "id": "microsoft.data.sqlclient",
    "kind": "managed-dotnet",
    "version": "6.1.0",
    "architecture": "any",
    "origin": "configured",
    "capabilities": [ "database.read" ]
  }
]
```

`id` è un'identità logica stabile e non un nome file. `version` è esatta.
`kind` ammette `managed-dotnet`, `com`, `native` e `host-service`; `origin`
ammette `project`, `configured`, `system` e `host`. L'architettura è `any`,
`x86`, `x64` oppure `arm64`; COM e native richiedono un'architettura esplicita.
Un `host-service` deve avere origine `host`. Le capacità sono ID logici,
univoci, che il contesto dovrà approvare separatamente.

## Confine di sicurezza iniziale

- il manifest non risolve e non carica nulla;
- nessuna scansione del sistema o reflection viene avviata dalla dichiarazione;
- una build contenente riferimenti senza resolver termina con `DDFB018`;
- raccolta assente, riferimenti incompleti/incoerenti o duplicati producono
  rispettivamente `DDFP020`, `DDFP018` e `DDFP019` durante il caricamento;
- lo stesso contratto è leggibile da standalone ed embedded, ma l'embedded non
  riceverà assembly: l'host soddisferà esclusivamente API adattate e capacità
  esplicite.

## Resolver autorizzato

`IDdfExternalReferenceResolver` riceve radice progetto, dichiarazione e contesto
`Standalone`/`Embedded`. Il primo resolver concreto usa esclusivamente un
catalogo fornito dall'applicazione e una callback di autorizzazione obbligatoria:
la corrispondenza deve essere esatta per tutti i campi e le capacità.

In standalone una voce `managed-dotnet` può produrre una ricevuta
`ManagedAssembly`: il file deve esistere, essere un assembly .NET gestito e
viene identificato con percorso assoluto, dimensione e SHA-256. In embedded e
per le altre integrazioni il risultato è un `TypedAdapter` identificato
logicamente, senza consegnare assembly allo script o al compilatore. Un assembly
gestito richiesto in embedded viene rifiutato con `DDFR003`; catalogo ambiguo,
permesso negato, file assente, adapter invalido e immagine non gestita producono
`DDFR001`–`DDFR007`, comprese immagini gestite o dipendenze transitive non valide.

La risoluzione non sceglie mai una variante approssimativa. `DDFR001` indica
una voce assente o più corrispondenze esatte, `DDFR012` una versione esatta non
catalogata, `DDFR013` un'architettura non disponibile e `DDFR014` una divergenza
di tipo, origine o capability. Ogni diagnostica conserva la posizione della
proprietà pertinente (`id`, `version`, `architecture`, `kind`, `origin` o
`capabilities`) nel `ddf-project.json` proprietario, compresi i manifest dei
moduli. Il doppio clic sull'Output apre il manifest e seleziona quel valore.
Anche `DDFB018`/`DDFB019` per resolver assente o fallito sono emessi per singola
reference e sono navigabili.

La dichiarazione entra nel fingerprint della build. Il descrittore API descritto
di seguito entra inoltre nel lockfile e alimenta i servizi del linguaggio.

## Descrittore API tipizzato

`DdfExternalApiDefinition` `1.0` è un contratto immutabile privo di codice
eseguibile e tipi CLR. Associa all'identità/versione del riferimento:

- permessi documentati;
- tipi DDF con campi, proprietà ed eventi espliciti;
- oggetti con nome visibile, ID di binding, tipo, accesso e lifetime;
- funzioni con ID di binding, parametri, risultato, effetto e documentazione.

Il descrittore rifiuta nomi o binding duplicati, permessi non dichiarati e tipi
non appartenenti ai primitivi DDF o alla propria tabella. Un SHA-256 canonico
identifica esattamente la superficie esposta. Identità o versione discordanti
dal riferimento risolto producono `DDFR008`.

`DdfExternalApiCatalogProjector` proietta lo stesso descrittore nel catalogo
condiviso già consumato da type checker, completamento, documentazione, runtime
VM e debugger. La proiezione per analisi non può eseguire operazioni. Quella di
runtime richiede un `IDdfExternalApiRuntimeAdapter` il cui ID deve coincidere
esattamente col binding autorizzato; le chiamate ricevono soltanto ID e valori
DDF, senza reflection, oggetti assembly o `P/Invoke` scoperti automaticamente.
Ogni `DdfBuildResult` espone inoltre la raccolta immutabile
`ResolvedExternalReferences`, permettendo alla shell standalone o all'host di
proiettare soltanto le API effettivamente risolte per quella build.

Per il backend Roslyn, `DdfDotNetResolvedReferenceSetBuilder` trasforma solo i
binding `ManagedAssembly` già presenti in quella raccolta in reference di
compilazione. Assembly principale e dipendenze transitive vengono ricontrollati
immediatamente prima di creare i `MetadataReference`: percorso DLL assoluto,
esistenza, dimensione e SHA-256 devono coincidere con la ricevuta Core. Una
sostituzione fra risoluzione e compilazione fallisce con `DDFC415`; collisioni
di identità o un reference set non costruibile falliscono con `DDFC416`. CLI e
debugger compilato standalone usano lo stesso builder.

La ricevuta Core porta con sé anche la provenienza della dichiarazione. Per
questo `DDFC415` e `DDFC416` puntano alla reference del manifest, non al file
DLL binario che è stato ricontrollato. Una funzione o un valore usati nello
script ma assenti dal descrittore API continuano invece a produrre rispettivamente
`DDF312` o `DDF313` sul sorgente DDF: non vengono cercati tramite reflection,
registro, GAC o scansione del filesystem.

Il bundle `1.2` marca ogni reference come `platform` oppure `packaged`. Solo i
percorsi restituiti dal resolver gestito diventano payload: byte, identità
assembly, dimensione e hash vengono legati al manifest package `1.1`. Il worker
`1.9` non riceve né riapre i percorsi originali, ricostruisce le reference in
memoria e rifiuta payload alterati, duplicati o oltre quota con `DDFC552`.
L'`AssemblyLoadContext` standalone risolve le DLL packaged esclusivamente dai
byte verificati e continua a essere deny-by-default.

Questo passaggio non rende pubblica automaticamente la superficie CLR della
DLL: le funzioni DDF restano quelle dichiarate da `DdfExternalApiDefinition` e
inoltrate da un adapter tipizzato. Configurazione e approvazione del catalogo,
proiezione API e packaging dell'EXE distribuibile restano passi distinti; non
esiste reflection arbitraria come conseguenza dell'inclusione della DLL.

La pubblicazione self-contained applica un secondo consenso, distinto
dall'autorizzazione a compilare o eseguire. Per ogni identità presente in
`PackagedReferences`, `DdfDotNetExecutablePublisher` richiede una
`DdfDotNetExecutableReferenceDistribution` esatta con licenza esistente e
avviso opzionale. Una ricevuta assente, duplicata o riferita a un assembly non
presente fallisce con `DDFC605`: una DLL installata e utilizzabile non viene
mai considerata automaticamente redistribuibile. I byte vengono presi dal
bundle verificato, non dal percorso originale, e dopo `dotnet publish` devono
conservare identità, dimensione e SHA-256. Il manifest `ddf-publish` `1.2`
registra l'elenco `externalReferences` e la directory `support`; licenze e
avvisi sono copiati sotto
`support/THIRD-PARTY-LICENSES/<assembly>/`. Le dipendenze fornite dal sistema o
dall'host e le DLL con divieto di redistribuzione richiederanno profili di
deployment distinti e non possono usare questa ricevuta.

Più descrittori possono essere registrati esplicitamente nello stesso
`DdfEditorHostCatalog` tramite `RegisterAnalysisCatalog` o
`RegisterRuntimeCatalog`; permessi e nomi restano soggetti ai normali controlli
di unicità del catalogo. Un catalogo di sola analisi che venga usato per errore
in esecuzione fallisce con `DDF409`, come una capability compilata priva del
provider corrispondente.

## Bridge del backend compilato

Il backend Roslyn non richiama direttamente gli adapter e non incorpora tipi
CLR esterni nel codice generato. `DdfDotNetExternalApiCapabilityProvider`
traduce le capability canoniche `host.function.*` già presenti nel manifest
nelle funzioni del descrittore e usa `DdfExternalApiRuntimeInvoker` per
inoltrare binding e soli valori DDF all'adapter.

Ogni `DdfDotNetExternalApiBinding` richiede la corrispondenza esatta fra:

- ID e versione del riferimento attesi;
- fingerprint API conservato dal lock;
- descrittore disponibile al momento dell'esecuzione;
- ID dell'adapter autorizzato.

I permessi vengono rivalutati a ogni chiamata: la loro revoca non richiede di
ricompilare e produce `DDF409`. Un provider assente produce lo stesso codice;
un descrittore aggiornato con fingerprint diverso viene invece rifiutato prima
di eseguire l'assembly e richiede l'aggiornamento esplicito del lock. Contesto,
cancellazione, scadenza, ID transazione e risorse registrate mantengono le
garanzie della Runtime ABI senza introdurre una dipendenza della Runtime dal
Core o dagli adapter concreti.

Il gate trasversale usa lo stesso descrittore per `Validate` embedded e `Build`
standalone e richiede un fingerprint identico, lasciando la scrittura della
ricevuta alla sola build. Esecuzione e pausa debugger vengono confrontate fra
VM e Roslyn prima di attraversare gli adapter. La sostituzione
dell'implementazione è ammessa senza ricompilazione soltanto quando il
fingerprint pubblico rimane invariato; revoca e adapter assente producono
`DDF409`, mentre un descrittore divergente viene respinto dal lock.

## Lockfile riproducibile

Una build scrive atomicamente
`<output.directory>/ddf-references.lock.json`, conforme a
`ddf-references-lock.schema.json`. La ricevuta `1.1` registra contesto,
proprietario modulare, dichiarazione completa, binding, percorso autorizzato,
dimensione, SHA-256, fingerprint del descrittore API e dipendenze transitive
fornite dal catalogo; gli adapter registrano l'ID logico e non un assembly.

Le convalide e build successive ricalcolano la ricevuta. Un lock illeggibile o
diverso produce `DDFR010` e non viene sovrascritto: l'aggiornamento deve essere
esplicito, inizialmente rimuovendo il file e ricostruendo in un ambiente
autorizzato. Una scrittura atomica fallita produce `DDFR011`. Il percorso resta
nel lock locale e non viene trasferito nel manifest portabile.

Il passaggio da `1.0` a `1.1` aggiunge il fingerprint API ed è intenzionalmente
fail-closed: un vecchio lock deve essere rigenerato esplicitamente dopo aver
verificato e autorizzato il descrittore, non viene aggiornato in silenzio.

I manifest `1.1` già esistenti vengono migrati soltanto in memoria a `1.2` con
una raccolta vuota e diagnostica `DDFP021`; il loader non modifica il file.
