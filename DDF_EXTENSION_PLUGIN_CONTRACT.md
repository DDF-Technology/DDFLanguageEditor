# Contratto plug-in eseguibili (bozza 0.15.0)

Un plug-in eseguibile non è un pacchetto lingua dichiarativo `.ddflang`, né un
modulo DDF. Aprire codice, progetti o pacchetti lingua non deve installare o
attivare plug-in. Questo documento definisce soltanto metadati e decisioni di
autorizzazione: **nessun loader è abilitato** in questa verticale.

La prima verticale `0.15.1` introduce `DdfExtensionPointDescriptor` e
`DdfExtensionPointCatalog`: quattro categorie versionate (`SemanticOperation`,
`RuntimeOperation`, `HostFunction`, `BuildBackend`), ID sotto il namespace
esatto del plug-in e capability richieste contenute nel manifest. Il catalogo
è immutabile, limitato a 64 voci ed esportabile come JSON `1.0` secondo
`ddf-extension-points.schema.json`. L'API del punto (`1.0`) è distinta dalla
versione SDK accettata dal manifest. Questa è **solo una dichiarazione**:
non registra delegate, non espone AST/IR mutabili, editor o VM, non concede
permessi e non collega ancora queste voci alle pipeline di analisi, runtime o
build. L'host futuro dovrà verificare firma, lock, consenso e capability prima
di attivare un punto e introdurre contratti tipizzati di input/output.
`DdfExtensionPointCatalogCodec.Parse` accetta il JSON solo insieme al manifest
atteso: schema, ID e versione del plug-in devono coincidere esattamente.
Campi sconosciuti/duplicati, categorie o versioni API future, capability non
dichiarate, commenti, trailing comma e input oltre quota vengono rifiutati.
Il parsing **non autentica** il catalogo. `DdfExtensionPointAttestation` offre
una firma staccata ECDSA P-256 con dominio proprio: il payload comprende il
digest SHA-256 del manifest canonico e il JSON canonico del catalogo. La firma
usa il medesimo formato a tre campi della firma del manifest e una chiave
pubblica esplicitamente attendibile per quell'editore. `VerifyCatalog`
richiede prima la firma valida del manifest e poi quella del catalogo;
alterazione dei punti, del manifest o revoca della chiave negano l'esito.
Il file JSON non deve necessariamente avere gli stessi byte o la stessa
spaziatura usati dal firmatario: il parser ricostruisce la forma canonica.
L'esito non prova ancora i byte dell'assembly, il grafo delle dipendenze,
il consenso o i grant delle risorse, e **non autorizza l'esecuzione**.

Come primo segmento delle API `0.15.1`, `DdfExtensionValue@1.0` rappresenta
solo `null`, booleani, interi `Int32`, float `Double` finiti, stringhe e array
immutabili. `DdfExtensionCallRequest` collega uno snapshot di argomenti a un
punto `RuntimeOperation` o `HostFunction`, con massimo 64 argomenti, 1024 nodi
complessivi, profondità 8, 256 elementi per array e 4096 caratteri per
stringa. Sono vietati riferimenti a controlli WinForms, oggetti host, AST/IR,
delegate e tipi Roslyn. Questo è un contenitore tipizzato **non eseguibile**;
transazioni, autorizzazione sul confine e contratti di semantica/build restano
da definire.
`DdfExtensionCallCodec` aggiunge ora il formato wire JSON `1.0` per la sola
richiesta, descritto da `ddf-extension-call.schema.json`: ogni valore ha un
tag `kind` esplicito e gli argomenti sono ordinati nella serializzazione.
Il parser è chiuso, limita l'input UTF-8 a 128 KiB, controlla profondità,
numero di nodi, range `Int32` e float finiti, e risolve `pointId` solo nel
catalogo ricevuto. Non verifica da solo la firma del catalogo: l'host deve
passargli uno snapshot ottenuto da `VerifyCatalog` e completare anche i gate
su artifact, dipendenze, consenso e risorse. Né il parsing né lo schema JSON
autorizzano il dispatch.
`DdfExtensionCallResponse@1.0` aggiunge una risposta dati separata, descritta
da `ddf-extension-call-response.schema.json`: `Succeeded` contiene esattamente
un valore tipizzato, `Failed` contiene solo un `errorId` con prefisso
`<pointId>.error.`. Il codec rifiuta campi misti, duplicati, status sconosciuti,
quote superate e `pointId` diverso dalla richiesta. L'errore dichiarato da un
plug-in non può usare la famiglia diagnostica host `DDFX` e non equivale a una
decisione di sicurezza. Il payload `1.0` non porta un ID univoco della
singola invocazione: `DdfExtensionCallFrameCodec` aggiunge un frame esterno
`1.0` con `callId` GUID casuale e request/response tipizzate, descritto
da `ddf-extension-call-frame.schema.json`.
`DdfExtensionCallCorrelationLedger` conserva fino a 128 chiamate pendenti in
memoria, usa un orologio monotono per la scadenza, permette la cancellazione e
consuma l'ID alla prima risposta, anche se il payload è malformato. Un frame
con ID ignoto o già usato è rifiutato; la risposta deve inoltre riferirsi al
punto della richiesta originale. È soltanto correlazione locale: il `callId`
non autentica il worker o il canale e non prova che il codice sia isolato.
`DdfExtensionCallAssessment` assegna diagnostiche host nel dominio
`Execution` senza fidarsi dei codici proposti dal plug-in: `DDFX601` per
`errorId` dichiarato, `DDFX602` per risposta non valida, `DDFX603` per
scadenza e `DDFX604` per ID sconosciuto o già consumato. I messaggi non
riportano eccezioni grezze o dati del worker. Queste diagnostiche descrivono
il protocollo, non un plug-in realmente eseguito. Autorizzazione sul confine,
autenticazione del trasporto e isolamento del worker restano prerequisiti
del dispatch.
`DdfExtensionCallSignature@1.0` aggiunge una firma in memoria per i punti
`RuntimeOperation` e `HostFunction`: nomi e tipi esatti dei parametri, tipo
del risultato, effetto dichiarato (`Pure`, `HostMutation`, `ExternalEffect`) e
ID degli errori ammessi. I tipi del protocollo sono scalari o array omogenei
ricorsivi; `Null` rappresenta esplicitamente l'assenza di valore. Il registro
può emettere una richiesta solo se conforme alla firma e controlla la
risposta prima di restituirla; un risultato del tipo sbagliato o un errore non
dichiarato viene consumato e classificato `DDFX602`.
`DdfExtensionCallSignatureCatalog@1.0` rende le firme machine-readable secondo
`ddf-extension-call-signatures.schema.json`: richiede una firma per ogni punto
runtime/host, nessuna per semantica/build, e include il digest del catalogo
dei punti. Il parser rifiuta versioni, campi, tipi, effetti o errori non validi,
assenze e duplicati. La firma staccata ECDSA P-256 ha un dominio proprio e
copre il catalogo delle firme canonico più il digest dei punti; la verifica
richiede anche la firma valida dei punti e quella del manifest, con editore
attendibile e chiavi non revocate. `VerifyCallSignatures` espone un preflight
`DDFX006`/`DDFX007`/`DDFX105`. Questo non verifica da solo il lock del grafo,
i byte dell'assembly, il consenso o le risorse: nessun plug-in è eseguibile.

Per `SemanticOperation` e `BuildBackend`, il primo confine `1.0` è solo un
input sorgente: `DdfExtensionSourceSnapshot` contiene testo UTF-8 valido,
percorso logico relativo e SHA-256, senza path assoluti o oggetti AST/IR.
La quota è 1 MiB per documento. `DdfExtensionSemanticRequest` identifica una
regola e un intervallo interno al documento; il punto deve dichiarare
`file.read`. `DdfExtensionBuildRequest` accetta fino a 128 documenti univoci
per percorso (confronto case-insensitive), massimo 8 MiB complessivi,
sorgente principale presente nell'insieme e target eseguibile/libreria; il
punto deve dichiarare `file.read` e `file.write`. Questi nomi sono logici:
non devono essere usati direttamente per aprire file.
`DdfExtensionSourceRequestCodec` definisce il wire format JSON `1.0` di tali
richieste (`ddf-extension-source-request.schema.json`). Il parser accetta solo
campi noti, ricostruisce gli snapshot e ne verifica l'hash SHA-256 sul testo
UTF-8, gli intervalli, i percorsi relativi, il punto nel catalogo e le quote
(1 MiB per sorgente, 8 MiB per build, 128 sorgenti, 32 MiB di payload).
Il catalogo passato al parser deve essere già verificato dall'host: il parser
non autentica la provenienza. I grant per risorsa esatta, la modalità
standalone per la build e l'attestazione di questi input rimangono da definire
prima di un dispatcher. I formati delle risposte sono descritti sotto.

Per le sole operazioni semantiche, `DdfExtensionSemanticResponse@1.0`
trasporta fino a 128 diagnostiche (`Info`, `Warning`, `Error`) con ID nel
namespace `<pointId>.diagnostic.`, messaggio e intervallo interno al documento,
oppure un errore `<pointId>.error.`. Il wire format è descritto da
`ddf-extension-semantic-response.schema.json` e limita il payload a 256 KiB.
Il digest SHA-256 della richiesta canonica vincola la risposta all'esatto
snapshot, regola e intervallo; non autentica il worker né sostituisce un ID
monouso di correlazione. Le diagnostiche fornite dal plug-in non sono codici
host `DDFX` e non diventano automaticamente errori del compilatore.
Isolamento, autorizzazione e collegamento alla pipeline restano aperti.

`DdfExtensionBuildResponse@1.0` trasporta un assembly obbligatorio e, se
presenti, simboli e manifest. Ogni artefatto conserva in memoria una copia
immutabile dei byte, un percorso **logico** relativo, il tipo e SHA-256: non è
una destinazione di scrittura. La quota è 8 MiB per artefatto, 16 MiB
complessivi e 24 MiB di wire JSON Base64; i ruoli e i percorsi non possono
essere duplicati. Il parser di `ddf-extension-build-response.schema.json`
verifica hash, formato canonico Base64 e digest della richiesta build, oppure
accetta un errore namespaced senza artefatti. Il digest serve solo a legare i
dati all'input: non autentica il worker. Un payload `Assembly` non è ancora
un PE valido o sicuro. Validazione dei byte, grant per risorsa, isolamento,
policy standalone, staging e pubblicazione rimangono prerequisiti separati.

`DdfExtensionSourceAdmission` è un gate descrittivo prima di qualunque uso
delle richieste: richiede il medesimo oggetto punto dello snapshot del catalogo,
rivaluta firma/hash/SDK/consenso con la policy plug-in e verifica un grant
`file.read` su ID di risorsa opaco. Per la build richiede anche `file.write`
su un secondo ID e `Standalone`; `Embedded` è sempre negato. Un punto che
richiede capability ulteriori non viene ammesso da questo gate. Il chiamante
deve fornire un catalogo previamente attestato e i binding delle risorse;
questa API non verifica la firma del catalogo, il lock dell'intero grafo o
l'audit durevole, non apre file e non concede permessi persistenti. Ogni
accesso fisico dovrà rifare il controllo sui grant correnti per evitare revoche
o cambi di stato fra ammissione e uso.

`DdfExtensionSourceFrameCodec@1.0` incapsula richiesta o risposta semantica/build
con `callId` casuale e tipo esplicito; lo schema è
`ddf-extension-source-frame.schema.json`. Il registro host
`DdfExtensionSourceCorrelationLedger` mantiene al massimo 128 richieste
pendenti, con scadenza basata su orologio monotono, cancellazione e consumo
monouso prima di validare il payload di una risposta strutturalmente leggibile.
Risposte fuori tipo, alterate, scadute o riutilizzate non producono un risultato.
Il digest della richiesta nel payload resta un controllo di associazione,
non una firma; neppure `callId` autentica worker o canale. L'host futuro dovrà
legare il frame al trasporto isolato e registrare le decisioni di sicurezza.
`DdfExtensionSourceAssessment` classifica gli esiti nel dominio host
`Execution`: `DDFX601` per errore del plug-in, `DDFX602` per risposta invalida,
`DDFX603` per scadenza e `DDFX604` per ID sconosciuto o consumato. Il testo
dell'errore del plug-in non viene riproposto nella diagnostica host. Una
risposta semanticamente valida conserva comunque diagnostiche e artefatti
non attendibili; nessuno dei due viene automaticamente applicato al progetto.

`ddf-extension-protocol.json` pubblica l'inventario `1.0` dei protocolli per
`SemanticOperation`, `RuntimeOperation`, `HostFunction` e `BuildBackend`:
schemi di richiesta/risposta/frame, modalità host consentite e capability
minime. `DdfExtensionProtocolCatalog` espone lo stesso inventario in Core e
rifiuta un manifesto divergente o che dichiari uno stato diverso da
`contracts-only`. Il file è incluso nel NuGet Core, insieme al suo schema.
La versione `1.0` del protocollo dei punti non è la versione `0.42` dell'SDK
dell'editor incorporabile. Il manifesto è documentazione verificabile, non
un modo per registrare o invocare codice esterno.
Il demo `samples/DDFLanguageEditor.ExtensionContractDemo/` esercita tutti e
quattro i protocolli con dati costruiti localmente, mostrando frame e
classificazione degli esiti senza un assembly eseguibile. Non costituisce un
template installabile: firma, consenso, trasporto isolato e confine delle
risorse sono volutamente fuori dal demo.

Le diagnostiche plug-in usano esclusivamente il prefisso `DDFX`, distinto da
`DDFP` del manifest di progetto, dai codici del sorgente DDF e dal motore.
`DdfExtensionPluginPreflight` restituisce risultati tipizzati per parsing di
manifest/firma/lock, verifica di firma/hash/SDK, grafo, consenso e operazione.
Le famiglie `DDFX0xx`–`DDFX4xx` coprono i controlli di preflight e policy;
`DDFX6xx` copre l'assessment del protocollo di chiamata e `DDFX9xx` indica
problemi di audit. Il dominio `Loading` è riservato al futuro host; il dominio
`Execution` oggi classifica solo esiti simulati del protocollo. I vecchi journal di sviluppo
possono contenere codici `DDFP` per eventi plug-in: non vengono riscritti né
utilizzati come permesso; le nuove decisioni usano `DDFX`.

`ddf-extension-plugin.schema.json` descrive il manifest `1.0`. `id` e
`publisher` sono identificatori distinti; `version` è SemVer; l'intervallo
`minimumSdkContract`–`maximumSdkContract` include gli estremi. `assemblyFile`
è un nome locale, non un percorso. `assemblySha256` blocca esattamente i byte
consentiti. `dependencies` contiene ID di plug-in, non riferimenti impliciti a
DLL di sistema. Il loader futuro dovrà risolvere versioni e hash delle
dipendenze tramite lockfile verificato; il solo ID non basta a caricarle.
Il formato `ddf-extension-plugin-lock.schema.json` fissa per ogni ID editore,
versione, SHA-256 dell'assembly e SHA-256 del payload canonico del manifest.
`DdfExtensionPluginDependencyResolver` verifica il grafo transitivo completo,
le firme di tutti i manifest, i byte degli assembly e la compatibilità SDK;
rifiuta assenze, duplicati, cicli, pin divergenti e voci non raggiungibili.
L'ordine restituito pone sempre le dipendenze prima del plug-in radice.
Il lockfile appartiene al progetto/host e non costituisce consenso a eseguire:
ciascun plug-in richiederà comunque autorizzazioni proprie e il futuro loader
dovrà usare esattamente i byte verificati, senza race sui file.
`DdfExtensionPluginGraphAdmission` esegue il gate successivo, ancora senza
caricare codice: dopo la verifica del lock richiede un grant distinto per ogni
nodo raggiungibile, compresa la radice. Ogni grant deve coincidere con ID,
editore, versione, hash e insieme esatto delle capability dichiarate;
concessioni mancanti, revocate, duplicate o estranee negano l'intero grafo.
Nessun permesso del plug-in principale si trasmette alle dipendenze. Un esito
positivo non autorizza di per sé l'uso di risorse: ogni operazione rimane
soggetta alla coppia capability/risorsa logica e all'audit.

La firma è un documento staccato descritto da
`ddf-extension-plugin-signature.schema.json`: algoritmo fisso
`ECDSA-P256-SHA256`, `keyId` e valore DER codificato Base64. Il payload
canonico è UTF-8, preceduto da `DDF-EXTENSION-PLUGIN-MANIFEST/1.0` e newline;
le proprietà hanno ordine fisso e dipendenze/capability sono ordinate
ordinalmente. La chiave pubblica SPKI P-256 deve essere associata dall'host
all'esatto editore e key ID, e può essere revocata. Non esiste una chiave
ufficiale implicita né una fiducia automatica nel campo `publisher`.
`EvaluateAdmission` richiede firma attendibile, SHA-256 dei byte forniti e
consenso esatto prima di restituire `Authorized`. Il futuro loader dovrà
garantire che i byte verificati siano gli stessi poi eseguiti, senza race sui
file, e risolvere il grafo delle dipendenze prima dell'attivazione.

Le capability sono definite dal catalogo Core versionato `1.0` e consultabili
come JSON tramite `DdfExtensionPluginCapabilityCatalog.ToJson()`: `file`
(`list`, `read`, `write`, `delete`), `network` (`connect`, `listen`, `publish`,
`subscribe`), `process` (`inspect`, `signal`, `start`), `device` (`read`, `write`,
`configure`), `ui` (`command`, `dialog`, `panel`) e `native` (`invoke`, `load`).
Il manifest e i grant rifiutano nomi non presenti nel catalogo; lo schema JSON
elenca gli stessi valori e un test blocca divergenze tra schema e Core.
`RequiresDedicatedWorker` segnala operazioni che richiederanno anche un worker
dedicato; `false` **non** autorizza l'esecuzione di plug-in non fidati nel
processo host. Nessuna capability abilita già un adapter di sistema.
Il manifest dichiara ciò che richiede; il consenso
dell'utente/host è separato e vincolato a ID, editore, versione, hash e
capability. Senza grant, con grant revocato, identità diversa o capability
mancanti, la decisione è negativa. Un cambio di binario, versione o editore
richiede quindi un nuovo consenso. Il Core non mostra una UI di consenso né
persiste grant automaticamente; il registro per sessione e l'audit sono
descritti sotto. Il confine di processo per plug-in non fidati è previsto
dalla 0.15.2; un controllo di manifest **non** è una sandbox.

Una singola capability non autorizza tutte le risorse del dominio. La policy
`DdfExtensionPluginResourcePolicy` richiede anche un grant per la coppia esatta
`capability` + `resourceId`, vincolato alla stessa identità e allo stesso hash
del plug-in. Il `resourceId` è un nome logico assegnato dall'host, ad esempio
`project.sources` o `service.crm`: non è un path, URL, porta, handle o nome di
dispositivo fornito dal plug-in. Non esistono wildcard o permessi impliciti;
grant mancanti, duplicati o revocati negano l'operazione. La policy rivalida
firma, hash e consenso a ogni decisione. Il futuro adapter deve applicare
questa decisione **sul confine effettivo della risorsa** e associare il nome
logico a configurazione host protetta; qui non viene ancora aperta alcuna
risorsa. `DdfExtensionPluginSecurityLedger` conserva in memoria, in ordine,
le decisioni con codici `DDFX` e dominio separato da sorgente/linguaggio/motore.
Il registro è limitato: quando è pieno, `EvaluateAndRecord` nega ulteriori
operazioni invece di autorizzarle senza traccia. È disponibile un backend
locale opzionale `DdfExtensionPluginFileAuditStore`: l'host crea una volta il
journal con `createNew`, gli assegna una chiave HMAC-SHA-256 esterna al
repository e lo colloca in una directory protetta; agli avvii successivi lo
riapre senza ricrearlo. Ogni append è durabile (`Flush(true)`), autenticato e
collegato alla voce precedente; chiave errata, modifica, troncamento, file
mancante, quota o errore I/O negano l'operazione. Non vengono salvati percorsi,
URL o segreti dell'host, solo gli ID logici e l'identità del plug-in.

Il solo journal non impedisce il rollback a una vecchia copia *valida*.
`IDdfExtensionPluginAuditAnchor` consente all'host di fornire un checkpoint
monotono (sequenza e ultimo HMAC) conservato fuori dalla directory del journal
e protetto contro il rollback. Con un'ancora configurata, ogni append confronta
il checkpoint, scrive e sincronizza il journal, poi avanza l'ancora tramite
compare-and-swap durevole. Una copia vecchia, una discrepanza o un avanzamento
fallito negano l'operazione; un arresto tra scrittura e avanzamento lascia il
sistema chiuso e richiede recupero manuale verificato. L'implementazione Core
non include un'ancora persistente: il test usa solo una doppia in memoria,
che **non** è sicura per produzione. L'host dovrà fornire storage realmente
monotono, protetto e coordinato tra processi prima di abilitare codice plug-in.

`DdfExtensionPluginConsentLedger`
gestisce una sessione per un solo plug-in: `Approve` verifica firma, hash e
compatibilità, poi scrive un evento `Granted` autenticato prima di attivare
la coppia capability/risorsa. `Revoke` rimuove subito il permesso e registra
`Revoked`; se il journal non è disponibile, il permesso rimane comunque
revocato. Entrambe le azioni riportano un ID attore fornito dall'host: il
Core non autentica da solo la persona o il processo dietro quell'ID.
Le decisioni di autorizzazione e gli eventi di consenso condividono sequenza e
catena HMAC. Il registro non ricostruisce permessi dal journal al riavvio:
una nuova sessione parte senza grant e richiede un nuovo consenso esplicito.
Restano aperti l'implementazione protetta dell'ancora, la verifica dell'identità dell'attore,
la persistenza governata dei grant, l'UI di consenso e l'applicazione della
policy negli adapter.

La decisione del contratto è distinta da errori del linguaggio DDF, del
progetto e del motore. La futura diagnostica operativa aggiungerà codici
dedicati per formato, compatibilità, autorizzazione, caricamento ed esecuzione.
`DdfExtensionPluginPolicy.Evaluate` rappresenta solo il consenso, senza prova
di provenienza. `EvaluateAdmission` aggiunge firma e hash, ma non è un loader,
una sandbox o una garanzia sulla sicurezza del binario. Prima di eseguire
qualsiasi codice servono risoluzione delle dipendenze, isolamento adeguato e
verifica dei byte effettivamente caricati.
