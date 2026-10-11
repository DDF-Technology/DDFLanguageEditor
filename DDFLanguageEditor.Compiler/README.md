# DDFLanguageEditor.Compiler

Assembly riservato alla compilazione standalone e alla futura CLI DDF.

Il progetto è l'unico punto del repository autorizzato a dipendere da Roslyn.
Dipende dai contratti backend-neutral di `DDFLanguageEditor.Core`, mentre Core,
WinForms e l'SDK embedded non possono dipendere né da questo assembly né dai
pacchetti `Microsoft.CodeAnalysis`.

Il progetto consuma ora le unità backend-neutral del Core e possiede il contratto
persistente `.NET`: manifest `1.1`, source map `1.1`, descrittori DLL/PDB,
dipendenze e mappe verso
profilo e sorgente DDF. La policy di compatibilità verifica inoltre TFM, ABI,
RID, capability, dipendenze, hash e limiti senza caricare assembly; l'embedded è
sempre escluso. La milestone `0.13.1` dispone di un emitter reale e deterministico
per DLL, Portable PDB, source map e manifest. Il generatore di alberi C# è chiuso
e sicuro: usa soltanto `SyntaxFactory`,
la IR bound e i mapping CLR, rifiutando esplicitamente la IR non supportata.
Il secondo slice copre anche `for`, postfix e array e viene sottoposto a una
vera `CSharpCompilation` semantica prima dell'emit.
Strutture, globali puri e binding standard sono ora inclusi; una tabella link
qualifica le chiamate inter-modulo tramite identità bound. L'intero workspace
campione viene validato come tre compilazioni separate collegate in memoria.

La `0.13.1` dispone ora del piano multi-modulo `1.0`: una work item per unità,
dipendenze in ordine topologico e output relativi deterministici derivati da una
preparazione Core già valida. Ogni work item espone la IR bound `2.1` prodotta
dal Core, con simboli, tipi, conversioni, l-value e chiamate risolte. La runtime ABI standalone
`1.6.0` è ora isolata in `DDFLanguageEditor.Runtime`; il name map `1.0` deriva
namespace e identificatori esclusivamente dagli ID bound.
Il profilo C# 14/Roslyn e le opzioni riproducibili entrano nel fingerprint del
piano; il reference set esplicito registra identità, hash e dimensione e viene
risolto da una directory reference pack autorizzata, senza scandire assembly
caricati, GAC o directory casuali.

La suite Compiler esegue il workspace multi-modulo emesso soltanto in un runner
di verifica scaricabile e non pubblico. Il loader di prodotto resta assegnato
alla `0.13.2`; l'embedded non compila né carica assembly DDF.

`DdfDotNetArtifactCache` `1.0` conserva bundle completi in directory
content-addressed derivate dal fingerprint del piano. Il riuso avviene soltanto
dopo aver verificato ricevuta, manifest, source map, percorsi, dimensioni e hash
di ogni payload. Una voce corrotta viene invalidata e sostituita atomicamente;
la cache non carica assembly, non esegue codice e non appartiene all'SDK embedded.

La `0.13.2` introduce inoltre `DdfDotNetArtifactLinker`: collega manifest senza
caricare i PE, confronta esattamente dipendenze e import/export tipizzati e
produce un programma immutabile in ordine topologico con fingerprint stabile.
Il loader di prodotto rimane un componente successivo e separato dal linker.

`DdfDotNetPeVerifier` legge PE e metadati senza caricare codice e blocca identità
o riferimenti DDF incoerenti, immagini non IL-only, entry point, ModuleRef e
`P/Invoke`. `DdfDotNetLinkedBundleBuilder` produce quindi un bundle in memoria
deterministico e non modificabile dall'esterno. Il verifier richiede lo stesso
reference set della build, applica un'allowlist per identità assembly completa e
decodifica tutti i corpi IL verificando operandi, token, flusso ed exception
region entro budget configurabili. Il dataflow controlla inoltre l'altezza dello
stack, le confluenze, le firme delle chiamate e `MaxStack`.
Il verifier attraversa inoltre tutte le firme metadati, esclude pointer,
function pointer e `TypedReference` e confronta ogni indice argomento/locale
con la firma reale del metodo (`DDFC517`). Il bundle `1.2`
contiene il manifest package canonico con hash dei payload e allowlist gestita
bloccata per identità/hash/dimensione, senza percorsi locali. Pubblicazione
fisica del package e verifica tipizzata completa restano fasi successive.

`DdfDotNetStandaloneLoader` ricostruisce il programma dal solo bundle, ripete
package lock, compatibilità e verifica PE/IL e lo carica in un
`AssemblyLoadContext` collezionabile. La sessione è monouso, inizializza i moduli
in ordine topologico, trova `main` dalla source map e può creare il debugger
cooperativo senza sorgenti originali. Solo runtime ABI e riferimenti presenti
nell'allowlist possono essere risolti; l'embedded è rifiutato. Il caricamento
in-process non è una sandbox: package non fidati richiederanno il worker con
profilo di sicurezza del sistema operativo.

`DdfDotNetWorkerClient` e `DDFLanguageEditor.Runner` implementano ora il primo
confine fuori processo monouso. Il protocollo JSON `1.9` trasferisce il bundle su
pipe, lo ricostruisce e riverifica nel figlio e restituisce risultato scalare e
output con correlation ID. Può inoltre fornire una coda input immutabile e
limitata, consumata in ordine e attestata nella risposta; esaurimento e quote
producono `DDF408`/`DDFC542`. Un piano debug batch può inoltre predefinire
breakpoint e comandi Step/Continua e ricevere una traccia limitata con sole
posizioni, stack e variabili DDF (`DDFC543`). Timeout e cancellazione causano il kill dell'intero
albero. Su Windows il client applica inoltre un Job Object limitato al bootstrap
fidato e al worker, memoria/CPU configurabili e kill-on-close; ogni esecuzione usa una
directory temporanea dedicata. Il worker supporta soltanto input pre-bufferizzato
e broker espliciti. Le risposte di rete possono essere preacquisite dall'host e
passate come snapshot logici verificati oppure richieste durante l'esecuzione con
`brokerFetchResponse(nome)` sul canale duplex; URL e credenziali restano nell'host.
La console non viene esposta direttamente: `readLine` può richiedere input
interattivo soltanto tramite un grant duplex limitato. Il debug può ricevere
dinamicamente Continua, Step Into/Over/Out e Stop, scambiando soltanto snapshot
DDF limitati. Altre capability host restano escluse e il
contenimento delle risorse non è ancora una sandbox di sicurezza completa. Il
runner attesta un token Windows già ristretto oppure crea un figlio con token
filtrato, privilegi massimi rimossi e gruppo `INTERACTIVE` deny-only; downgrade
e attestazioni mancanti producono
`DDFC537`, anche senza mascherare gli errori DDF successivi alla verifica. Vedere
`DDF_DOTNET_WORKER_PROTOCOL.md`.

Ogni richiesta include una policy di accesso `1.3`: il profilo accetta filesystem
assente, lettura brokerizzata o lettura/scrittura brokerizzata e risposte di rete
preautorizzate; capability diverse da `brokerReadText`, `brokerWriteText`,
`brokerReadResponse` e `brokerFetchResponse` sono rifiutate con `DDFC536`.
Snapshot di rete alterati o
oltre quota producono `DDFC544`. L'ambiente del figlio è ricostruito da
zero e non eredita variabili applicative o credenziali dall'editor.
`DdfDotNetWorkerResourceLimits.RequireProtectedWorkingDirectory` rende inoltre
obbligatoria la DACL Windows dedicata della directory di sessione; se l'host non
autorizza la modifica, l'esecuzione viene rifiutata con `DDFC538` senza fallback.

`DdfDotNetWorkerFileSnapshot.FromFile` implementa il primo broker read-only:
legge lato client un file UTF-8 esplicitamente scelto e trasferisce al worker
soltanto nome logico, contenuto e SHA-256. Il provider del runner espone la sola
capability di lettura `brokerReadText`; traversal, duplicati, manomissioni, budget o
capability incoerenti vengono rifiutati con `DDFC539`. Il gate verifica anche
il broker `brokerWriteText`: le modifiche restano staged nel worker e il client
le applica soltanto dopo il successo, su target fisici espliciti mai serializzati,
con controllo concorrenza, quota, hash, sostituzione atomica e rollback
compensativo (`DDFC540`). I target scrivibili vengono aperti per attestare
l'accesso, devono avere un percorso privo di reparse point e, su Windows,
mantengono un'identità kernel ricontrollata contro sostituzioni anche a hash
invariato; fallimenti di questa protezione producono `DDFC541` e i successi
espongono ricevute prive di percorsi fisici. Il gate verifica anche
assenza del percorso originale nell'IPC, `DDF409` per una risorsa non concessa,
cinque sessioni reali consecutive e cleanup delle directory temporanee.

`DdfDotNetWorkerNetworkBroker` acquisisce lato host gli snapshot HTTP(S) prima
dell'avvio del worker. I grant fissano endpoint, media type, timeout e quota;
HTTPS è predefinito, proxy/cookie/redirect sono disabilitati e gli indirizzi DNS
vengono filtrati e fissati alla connessione per bloccare SSRF e rebinding. Un
provider credenziali non può cambiare metodo o destinazione. Nel payload IPC
entrano soltanto nome logico, contenuto, media type e hash; la ricevuta omette
URL, query e header. Errori di policy o trasporto producono `DDFC545`.

`DdfDotNetWorkerNetworkCapabilityHandler` associa i nomi logici live ai grant
host e usa lo stesso broker HTTP governato. `LiveNetworkResources` limita i nomi
ammessi nella singola esecuzione; incoerenze e richieste negate producono
`DDFC547`/`DDFC548`. Receipt, URL e autorizzazioni restano lato host.

`DdfDotNetWorkerInteractiveInputHandler` soddisfa le richieste tardive di
`readLine` senza consegnare stdin o la console al worker. La policy `1.3`
distingue input assente e `BrokeredInteractive`; numero di richieste e byte per
valore sono limitati e ricontrollati. Avvio legacy, grant incoerenti e risposte
non valide producono `DDFC549`.

`DdfDotNetWorkerInteractiveDebugHandler` riceve a ogni pausa posizione, stack e
variabili DDF e restituisce un solo comando verificato. `DebugPlan.Interactive`
esclude la lista batch preventiva; avvio legacy, snapshot/comandi non validi e
Stop controllato producono `DDFC550`. Nomi C#, token IL e oggetti CLR non
attraversano il canale.

Il framing JSONL `DdfDotNetWorkerDuplexProtocol` alimenta il loop bidirezionale
opzionale usato da `ExecuteDuplexAsync`. `Start`, richieste/risposte
capability e completamento sono correlati e sequenziali; una macchina a stati
limita a 256 chiamate, impedisce replay, risposte fuori ordine, completamento con
richieste pendenti e payload incoerenti (`DDFC546`). Il runner resta monouso;
il percorso legacy senza capability tardive continua a usare una sola
richiesta/risposta.

`DdfDotNetDebugAdapter` verifica e legge PE/PDB senza eseguirli, traducendo token
metodo, sequence point e scope IL in frame, breakpoint e simboli DDF. I nomi CLR
rimangono interni all'adapter; i valori runtime vengono acquisiti dalla ABI
cooperativa e proiettati tramite identità DDF.

`DdfDotNetCooperativeDebugger` usa l'observer opzionale della ABI per fermare il
runner di verifica su breakpoint DDF e applicare Continua, Step Into, Step Over,
Step Out e Stop in base allo stack bound. Parametri, locali, globali, array e
strutture sono copiati per pausa e proiettati tramite la source map con soli nomi
e tipi DDF. Le classi struttura generate descrivono esplicitamente i propri campi
tramite la ABI, senza reflection sui membri CLR.

`DdfDotNetDifferentialDebugTrace` confronta la proiezione osservabile di una
pausa VM e Roslyn: documento/span, Call Stack e albero dei valori. Conteggi
istruzione, storage CLR e dettagli interni non fanno parte del contratto. Il gate
multi-modulo copre breakpoint, Step Into, Step Over, Step Out e Continua.

La suite applica inoltre un primo gate differenziale generale a 11 programmi:
confronta VM e assembly su risultato, `Output`, terminazione, controllo di flusso,
array, strutture, `Input`, conversioni e diagnostiche runtime `DDF404`, `DDF405`,
`DDF408`, `DDF417`, `DDF418` e `DDF419`, inclusi percorso e offset sorgente.
Le funzioni host sincrone richiamate dal codice vengono tradotte in capability
canoniche `host.function.*` e invocate soltanto attraverso
`IDdfRuntimeCapabilityProvider`. Il manifest elenca esclusivamente quelle usate;
compatibilità e runtime sono deny-by-default. Il gate confronta con la VM sia il
risultato sia il numero di effetti. Letture e scritture di oggetti primitivi,
percorsi di proprietà annidati ed elementi host usano capability
`host.object.*`, `host.member.*` e `host.element.*` distinte, senza tipi host CLR
o reflection nel codice generato. Un provider asincrono è atteso dalla ABI con
timeout e cancellazione cooperativa; chiamate e risorse hanno budget, mentre le
risorse registrate vengono rilasciate LIFO. La IR bound distingue le funzioni
mutanti; ricevuta e policy transazionale governano commit/rollback anche per un
assembly emesso. L'SDK embedded rimane separato e non carica questi artefatti.

Le API esterne descritte da `DdfExternalApiDefinition` usano lo stesso confine:
`DdfDotNetExternalApiCapabilityProvider` associa le funzioni alle capability
canoniche e inoltra soltanto binding e valori DDF all'adapter tipizzato. Il
binding verifica ID, versione, fingerprint del lock e adapter prima
dell'esecuzione; i permessi vengono rivalutati a ogni chiamata. Il gate
cross-backend combina realmente SQLite, HTTP, filesystem e hardware simulato e
confronta `Validate`/`Build`, esecuzione e pausa debugger VM/Roslyn. Verifica
inoltre aggiornamento compatibile a fingerprint invariato, revoca e adapter
assente con `DDF409` e rifiuto preventivo di un aggiornamento incompatibile.

`DdfDotNetExecutablePublisher` può ora trasferire nel package self-contained le
sole `PackagedReferences` già verificate. Ogni identità richiede una
`DdfDotNetExecutableReferenceDistribution` esplicita con licenza obbligatoria:
il permesso di compilare una DLL non implica il diritto di redistribuirla.
`DDFC605` nega ricevute mancanti, duplicate o estranee; dopo la pubblicazione
dimensione e SHA-256 devono coincidere col bundle. Il manifest
`ddf-publish` `1.2` elenca le reference e i relativi file di licenza sotto
`support/THIRD-PARTY-LICENSES`; nella radice restano soltanto l'apphost EXE e
la directory `support`. L'apphost incorpora l'icona DDF o un `.ico`
personalizzato validato; il manifest registra origine, nome, SHA-256 e un
`publicationFingerprint` sensibile anche alla variazione dell'icona.
Il gate di distribuzione neutralizza la ricerca .NET globale, richiede il
runtime self-contained sotto `support` e ricontrolla l'intero inventario del
manifest, oltre alla protezione di destinazioni già esistenti.

Il gate legge inoltre direttamente `samples/language-corpus/corpus.json` e ne
esegue tutti gli oracoli runtime correnti: 6 programmi validi, 2 errori e 2
workspace multifile. Tra questi sono coperti valori predefiniti, precedenza e
logica, cicli, alias di strutture, array, globali, standard library, output,
file multipli e moduli installati. I locali primitivi privi di inizializzatore
sono materializzati con il default DDF, senza dipendere dalla definite assignment
del C# né introdurre punti di stepping negli initializer impliciti dei campi.

Vedere `DDF_DOTNET_ARTIFACT_FORMAT.md` nella radice del repository.
