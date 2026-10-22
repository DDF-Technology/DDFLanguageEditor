# Controllo aggiornamenti DDFLanguageEditor — disegno pianificato

Stato: **pianificato, non ancora implementato**. Il primo provider consulterà le
release del repository ufficiale `DDF-Technology/DDFLanguageEditor`, ma il Core
consumerà un contratto astratto e non dipenderà da GitHub.

## Obiettivo

- rilevare in modo non bloccante una versione compatibile più recente;
- supportare canali `stable` e `beta` selezionabili esplicitamente;
- offrire lo stesso risultato immutabile a standalone e host embedded;
- non trasmettere sorgenti, workspace, diagnostiche o dati dell'host;
- distinguere aggiornamenti del prodotto, SDK, pacchetti lingua, moduli e plug-in.

Il controllo non coincide con l'installazione. La prima verticale presenta
versione, canale, note, data, compatibilità e link alla release. Download,
staging, installazione e rollback sono una fase successiva e separata.

## Standalone

Lo standalone offrirà `Help > Controlla aggiornamenti...` e un controllo
automatico opzionale, ritardato fino al completamento dell'avvio. Un risultato
positivo produce una notifica non modale; assenza di rete, timeout, rate limit o
errore del provider non bloccano l'avvio e restano consultabili nella
diagnostica. Il controllo rispetta una frequenza configurabile, cache, risposta
condizionale e proxy di sistema senza eseguire retry aggressivi.

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
  storiche o incoerenti, incluso l'attuale `0.11.2-beta` di WinForms;
- futura installazione verificata su package portable e installato, processo in
  uso, accesso negato, interruzione, rollback e riavvio.

La funzione è prevista nella milestone `0.13.6`. Non modifica la grammatica DDF
e non è accessibile agli script.
