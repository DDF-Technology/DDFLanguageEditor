# Contratto plug-in eseguibili (bozza 0.15.0)

Un plug-in eseguibile non è un pacchetto lingua dichiarativo `.ddflang`, né un
modulo DDF. Aprire codice, progetti o pacchetti lingua non deve installare o
attivare plug-in. Questo documento definisce soltanto metadati e decisioni di
autorizzazione: **nessun loader è abilitato** in questa verticale.

`ddf-extension-plugin.schema.json` descrive il manifest `1.0`. `id` e
`publisher` sono identificatori distinti; `version` è SemVer; l'intervallo
`minimumSdkContract`–`maximumSdkContract` include gli estremi. `assemblyFile`
è un nome locale, non un percorso. `assemblySha256` blocca esattamente i byte
consentiti. `dependencies` contiene ID di plug-in, non riferimenti impliciti a
DLL di sistema. Il loader futuro dovrà risolvere versioni e hash delle
dipendenze tramite lockfile verificato; il solo ID non basta a caricarle.

Le capability sono nomi granulari nei domini `file`, `network`, `process`,
`device`, `ui` e `native`. Il manifest dichiara ciò che richiede; il consenso
dell'utente/host è separato e vincolato a ID, editore, versione, hash e
capability. Senza grant, con grant revocato, identità diversa o capability
mancanti, la decisione è negativa. Un cambio di binario, versione o editore
richiede quindi un nuovo consenso. Il Core non memorizza ancora grant né
effettua richieste di consenso: persistenza, audit e UI arriveranno in una
verticale successiva. Il confine di processo per plug-in non fidati è previsto
dalla 0.15.2; un controllo di manifest **non** è una sandbox.

La decisione del contratto è distinta da errori del linguaggio DDF, del
progetto e del motore. La futura diagnostica operativa aggiungerà codici
dedicati per formato, compatibilità, autorizzazione, caricamento ed esecuzione.
Fino ad allora `DdfExtensionPluginDecision` rappresenta solo il gate di
compatibilità e autorizzazione, non prova autenticità dell'editore né idoneità
del binario. Prima di eseguire qualsiasi codice, il loader dovrà verificare
SHA-256 sul file reale, provenienza/firma secondo policy, grafo dipendenze e
isolamento adeguato.
