# Contratto plug-in eseguibili (bozza 0.15.0)

Un plug-in eseguibile non è un pacchetto lingua dichiarativo `.ddflang`, né un
modulo DDF. Aprire codice, progetti o pacchetti lingua non deve installare o
attivare plug-in. Questo documento definisce soltanto metadati e decisioni di
autorizzazione: **nessun loader è abilitato** in questa verticale.

Le diagnostiche plug-in usano esclusivamente il prefisso `DDFX`, distinto da
`DDFP` del manifest di progetto, dai codici del sorgente DDF e dal motore.
`DdfExtensionPluginPreflight` restituisce risultati tipizzati per parsing di
manifest/firma/lock, verifica di firma/hash/SDK, grafo, consenso e operazione.
Le famiglie `DDFX0xx`–`DDFX4xx` coprono i controlli attuali; `DDFX9xx` indica
problemi di audit. I domini `Loading` ed `Execution` sono riservati al futuro
host e non simulano funzionalità già attive. I vecchi journal di sviluppo
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
