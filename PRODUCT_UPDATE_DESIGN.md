# Controllo aggiornamenti DDFLanguageEditor — disegno pianificato

Stato: **versionamento, provider GitHub, verifica crittografica e superficie
standalone implementati; verifica remota ufficiale non ancora attiva**. Il provider
consulta soltanto le release del repository ufficiale
`DDF-Technology/DDFLanguageEditor`, mentre il Core consuma contratti astratti e
non attribuisce fiducia ai metadati GitHub.

## Obiettivo

- rilevare in modo non bloccante una versione compatibile più recente;
- supportare canali `stable` e `beta` selezionabili esplicitamente;
- offrire lo stesso risultato immutabile a standalone e host embedded;
- non trasmettere sorgenti, workspace, diagnostiche o dati dell'host;
- distinguere aggiornamenti del prodotto, SDK, pacchetti lingua, moduli e plug-in.

Il controllo non coincide con l'installazione. La prima verticale presenta
versione, canale, note, data, compatibilità e link alla release. Download,
staging, installazione e rollback sono una fase successiva e separata.

## Provider GitHub Releases

`DdfGitHubReleaseProvider` implementa il primo trasporto senza attribuire
fiducia ai metadati remoti. L'endpoint è fissato a
`https://api.github.com/repos/DDF-Technology/DDFLanguageEditor/releases`; il
provider usa un client dedicato, disabilita i redirect e non invia body,
credenziali, sorgenti, workspace, diagnostiche o dati dell'host.

Il feed viene letto a flusso con limiti su byte, numero di release, asset e
lunghezza delle note. Timeout interno e cancellazione del chiamante rimangono
distinti. Offline, rate limit, HTTP inatteso, JSON malformato, URI fuori
repository o risposta oltre quota producono `Unknown` senza retry automatici.
La cache in memoria conserva soltanto il feed limitato e usa ETag e data di
ultima modifica per richieste condizionali; un `304` senza cache viene
rifiutato.

Il provider richiede sempre un `IDdfProductReleaseEvaluator`. GitHub resta
quindi una sorgente non fidata: il trasporto non può creare autonomamente un
risultato azionabile. `DdfReleaseManifestVerifier` e
`DdfReleaseUpdatePolicy` costituiscono il gate condiviso con cui un evaluator
seleziona il manifesto canonico, verifica firma e compatibilità e soltanto
allora restituisce una URI verificata. La costruzione del provider non effettua
rete e nessuna istanza viene ancora collegata automaticamente allo standalone o
all'embedded.

## Standalone

Lo standalone offre `Help > Controlla aggiornamenti...` e un controllo
automatico opzionale, disattivato per impostazione predefinita e ritardato di
cinque secondi dopo il completamento dell'avvio. Canale `stable`/`beta`, cadenza
da 1 a 30 giorni e ultimo tentativo sono persistiti nel profilo utente
`productUpdateSettings@1.0`. Il controllo è asincrono, annullabile alla chiusura
e impedisce richieste concorrenti; un errore registra comunque il tentativo per
non creare retry a ogni avvio.

Un risultato positivo produce una notifica non modale e accessibile nella barra
di stato. Il clic apre un popup centrato con versione, canale, data, note e il
pulsante esplicito `Apri release`; nessun collegamento viene aperto durante il
controllo. Il comando manuale mostra anche `up-to-date`, incompatibilità e stato
`unknown`, mentre i controlli automatici non aprono dialoghi. Offline, timeout,
rate limit o errore del provider non bloccano l'avvio e restano nell'Output.
Impostazioni, menu, timer e UI non vengono esposti dalla shell embedded.

La shell dipende esclusivamente da `IDdfProductUpdateProvider` ed è coperta con
un provider in memoria. Finché la chiave pubblica ufficiale non è provisionata,
la factory predefinita resta deliberatamente nulla e il comando restituisce
`Unknown` spiegando il motivo, senza effettuare rete: il frontend non può
aggirare il trust store o promuovere dati GitHub non verificati.

Nella prima verticale l'utente apre o scarica esplicitamente la release. Un
futuro updater dovrà usare processo separato, staging atomico, applicazione a
programma chiuso, verifica successiva e rollback; non richiederà privilegi in
modo silenzioso e non sostituirà file mentre l'editor è in esecuzione.

## Embedded

Il componente embedded non aggiorna, scarica o sostituisce mai autonomamente le
proprie DLL o l'applicazione host. Espone invece un servizio opzionale che
restituisce `currentVersion`, `availableVersion`, canale, compatibilità,
release notes e URI verificata. L'host decide:

- se abilitare il controllo automatico o solo quello manuale;
- quale provider, repository, canale e cadenza usare;
- se mostrare una notifica dentro l'editor o nella UI dell'applicazione;
- come distribuire e installare una nuova versione dell'SDK;
- se sopprimere completamente rete e UI.

Gli host compilati contro versioni precedenti continuano a funzionare. Nessuna
interfaccia esistente acquisisce membri obbligatori e nessuna chiamata parte per
il solo fatto di incorporare il controllo editor.

## Manifest e sicurezza

Ogni release aggiornabile pubblica un manifesto canonico con versione prodotto,
canale, versione minima/massima compatibile di SDK e dati persistiti, RID,
architettura, URI degli artefatti, dimensioni e SHA-256. Il manifesto è firmato
con una chiave pubblica fissata nel prodotto o fornita esplicitamente dall'host;
la fiducia non deriva soltanto dalla risposta HTTPS o dall'account GitHub.

Il provider accetta esclusivamente il repository configurato, segue soltanto
URI consentite, limita dimensioni e tempo, rifiuta downgrade, versioni ambigue,
asset duplicati, manifest non firmati o incompatibili e non interpreta testo
della release come istruzione. Un risultato non verificabile equivale a
`unknown`, mai a "aggiornamento sicuro disponibile".

Il formato `DdfReleaseManifest@1.0`, descritto anche da
`ddf-release-manifest.schema.json`, usa una serializzazione canonica indipendente
dall'indentazione. Firma e verifica adottano ECDSA P-256 con SHA-256 e firma DER;
il `keyId` viene risolto in un `DdfReleaseTrustStore` contenente esclusivamente
chiavi pubbliche SPKI esplicitamente attendibili. Il parser rifiuta proprietà
sconosciute, schemi futuri, repository diversi, URI non HTTPS o con credenziali,
hash malformati, versioni divergenti e identità asset duplicate. La policy usa
SemVer, canale, RID, architettura, intervallo del contratto SDK e tutti gli
schemi persistiti della matrice autorevole; un asset assente o ambiguo non è mai
azionabile.

`tools/publish-release.ps1` genera archivio e SHA-256, costruisce il manifesto,
lo firma usando esclusivamente il secret di processo
`DDF_RELEASE_SIGNING_KEY_PKCS8_BASE64` e ne esegue immediatamente una verifica
con la chiave pubblica derivata. Le build push/PR passano intenzionalmente
`-SkipSignedManifest` e producono candidati non distribuibili; il
`workflow_dispatch` fallisce se il secret ufficiale manca. La chiave privata
non viene salvata nel repository, negli artefatti o nei log. Prima della prima
release aggiornabile resta obbligatorio creare fuori dal repository la chiave
ufficiale, registrare la privata come secret GitHub e fissare la pubblica nel
trust store del prodotto.

Le credenziali GitHub non sono richieste per il repository pubblico. Un token
eventuale per installazioni private appartiene all'host o al credential store e
non entra in log, impostazioni esportabili, sorgenti o manifest di progetto.

## Versioni e compatibilità

Il confronto usa SemVer e non deduce l'ordine da nomi di tag arbitrari. Il canale
`stable` ignora prerelease; `beta` accetta stable e prerelease compatibili. Un
aggiornamento dell'app standalone può contenere editor e runtime coordinati. Un
aggiornamento SDK embedded deve dichiarare separatamente compatibilità binaria,
sorgente e degli snapshot persistiti.

La versione corrente non viene ricostruita interrogando assembly o costanti
discordanti. Una matrice machine-readable costituisce la sorgente autorevole per
prodotto/editor, linguaggio, contratto SDK, pacchetti NuGet Core/WinForms,
assembly e file version, Runtime ABI, protocollo worker, schemi e persistenza.
Questi domini possono avere numeri differenti, ma ogni relazione deve essere
esplicita. Build e pack falliscono se `ProductInfo`, `.csproj`, About, README,
manifesti, DLL o `.nupkg` divergono dalla matrice.

Il primo incremento della `0.13.6` aggiunge la sezione `productUpdate` alla
matrice e il catalogo pubblico immutabile `DdfProductVersionCatalog`. Il
catalogo legge la copia incorporata nel Core e fornisce senza I/O di rete:

- identità prodotto, repository ufficiale e canale predefinito;
- versione e stato correnti mostrati dalla shell;
- componente standalone e versioni dei package embedded Core/WinForms;
- contratto SDK corrente e baseline legacy compatibile;
- insieme versionato degli schemi persistiti.

`ProductInfo` non conserva più la versione come costante parallela. Il gate Core
confronta catalogo, matrice, progetti e attributi; `pack-sdk.ps1` confronta anche
gli SHA-256 della matrice e dello schema realmente inclusi nei due NuGet. I
numeri di Compiler, Runtime ABI, Runner, CLI e adapter non sono versioni prodotto
obsolete: restano domini indipendenti esplicitamente censiti. Nessun controllo
remoto viene eseguito da questo catalogo e l'embedded resta completamente
silenzioso finché un host non configurerà il futuro servizio opzionale.

## Risultato condiviso 1.0

`DdfProductUpdateResult` è il valore immutabile scambiato tra provider, shell
standalone e host embedded. Il contratto e lo schema
`ddf-product-update-result.schema.json` conservano:

- tipo e identità del componente, distinguendo prodotto standalone, SDK
  embedded, pacchetto lingua, modulo e plug-in;
- versione corrente ed eventuale versione disponibile in formato SemVer;
- canale richiesto, istante della verifica e data della release;
- stato della verifica e compatibilità come dimensioni separate;
- note, motivazioni di incompatibilità e URI HTTPS già verificata.

Gli stati sono `Unknown`, `UpToDate`, `UpdateAvailable` e `Incompatible`.
`Unknown` richiede un dettaglio diagnostico ma non contiene una release;
`Incompatible` richiede almeno una motivazione. Solo `UpdateAvailable` con
compatibilità positiva e URI verificata espone `IsActionable=true`. Il
contratto non scarica contenuti, non apre collegamenti e non considera una URI
verificata finché il futuro provider non ha completato i controlli previsti.
Serializzazione e lettura rifiutano schemi futuri, combinazioni incoerenti,
SemVer invalide, HTTP e URI contenenti credenziali.

Pacchetti linguaggio, moduli e plug-in conservano i propri cataloghi, permessi e
lockfile: il controllo prodotto può segnalarne requisiti, ma non li aggiorna
implicitamente né converte un progetto durante l'avvio.

## Test e criteri di consegna

- provider in memoria per `up-to-date`, update, downgrade, prerelease, timeout,
  offline, rate limit, risposta malformata e firma/hash errati;
- server HTTP locale per cache, risposte condizionali, redirect, limiti e
  cancellazione; GitHub reale soltanto come test opt-in;
- nessuna chiamata di rete nei test ordinari o nell'embedded non configurato;
- test UI per comando Help, notifica, accessibilità e avvio non bloccato;
- contract test per host legacy, host aggiornato e più istanze isolate;
- gate sui metadati reali dei package che impedisca la pubblicazione di versioni
  storiche o incoerenti; il precedente `0.11.2-beta` di WinForms è già stato
  eliminato e i NuGet correnti sono verificati contro la matrice;
- futura installazione verificata su package portable e installato, processo in
  uso, accesso negato, interruzione, rollback e riavvio.

La funzione è prevista nella milestone `0.13.6`. Non modifica la grammatica DDF
e non è accessibile agli script.
