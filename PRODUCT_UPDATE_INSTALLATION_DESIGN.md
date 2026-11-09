# Installazione aggiornamenti standalone — contratto di progetto

Stato: **fasi 1–4 implementate; accettazione su copia isolata avviata;
attivazione generale non attiva**
(`0.13.6`).

Questo documento definisce il confine di sicurezza dell'eventuale updater di
DDFLanguageEditor. Il download con consenso è disponibile nello standalone;
non sono abilitati installazione, elevazione, chiusura o riavvio
dell'applicazione. La chiave ufficiale è provisionata e la prima
release remota firmata è pubblicata; l'attivazione richiede ancora il
completamento dei gate descritti qui sotto. Il
Core comprende ora i contratti `DdfProductUpdateInstallationPlan@1.0` e
`DdfProductUpdateInstallationJournal@1.0`, la persistenza atomica, la macchina
a stati e il pianificatore deterministico di recovery. Il downloader/stager
esegue inoltre streaming limitato in `.partial`, verifica ripetuta del
manifesto firmato, SHA-256 e inventario, estrazione sicura e sigillo persistente
`DdfProductUpdateStagingSeal@1.0`. Il progetto separato
`DDFLanguageEditor.Updater` applica inoltre il solo piano locale autorizzato,
attende l'esatto processo editor, usa un lock esclusivo, esegue rename di
directory e conserva backup/quarantena per il rollback. Il worker esegue ora il
self-test fisso, completa o ripristina atomicamente, applica l'eventuale scelta
di riavvio e offre una pulizia differita separata. La shell ispeziona in modo
read-only piano e journal all'avvio e dispone dei dialoghi di consenso; il
flusso non è ancora attivato per l'utente finale. La release remota firmata
`v0.12.6` è stata verificata dal consumer `0.12.5`; download, staging e
installazione con health check sono riusciti su una copia isolata. Anche il
rollback di una nuova copia danneggiata e il recovery dopo un'interruzione tra
backup e promozione hanno ripristinato `0.12.5` funzionante. Restano gli altri
casi della matrice di accettazione. La release `0.12.6`
pubblicata non include la successiva correzione per il redirect CDN del download.

## Perimetro

L'updater riguarda esclusivamente l'applicazione **standalone** nelle
distribuzioni portable e per utente. Il componente embedded non scarica, non
installa e non sostituisce DLL dell'host: può soltanto restituire il risultato
verificato tramite il servizio SDK opzionale già definito.

Sono fuori perimetro:

- installazioni machine-wide o che richiedono privilegi amministrativi;
- aggiornamento implicito di pacchetti lingua, moduli, plug-in o progetti;
- esecuzione di script, hook o comandi provenienti dal manifesto remoto;
- migrazione silenziosa di workspace o impostazioni incompatibili;
- chiusura forzata dell'editor e riavvio senza consenso esplicito;
- sostituzione di file mentre l'editor è ancora in esecuzione.

Un'installazione protetta o machine-wide dovrà usare un installer esterno,
firmato e avviato consapevolmente dall'utente. L'updater integrato non tenta
fallback con elevazione UAC.

## Attori e confini di fiducia

1. **Editor in esecuzione** — verifica release, manifesto, firma e
   compatibilità; raccoglie separatamente il consenso al download e
   all'installazione.
2. **Downloader/stager** — scarica in un'area temporanea dedicata, calcola
   l'hash durante lo streaming ed estrae soltanto un inventario ammesso.
3. **Worker updater fuori processo** — attende la chiusura dell'esatta istanza
   dell'editor e applica un piano immutabile. Deve risiedere fuori dalla
   directory che sostituisce oppure essere consegnato come worker autonomo.
4. **Verifica di salute** — controlla inventario e avvio mediante un comando
   fisso del prodotto, mai mediante codice indicato dal manifesto.

GitHub e i metadati della release restano non fidati. Il worker accetta soltanto
un piano locale generato dopo la verifica di `DdfReleaseManifest@1.0`; non
accetta URL, eseguibili, argomenti arbitrari o hook aggiunti dal server.

## Consenso e macchina a stati

Controllo, download e installazione sono operazioni distinte. Un controllo
automatico può soltanto notificare. Il download richiede un gesto esplicito;
l'applicazione e il riavvio richiedono un secondo consenso che mostra versione,
origine, dimensione, destinazione e necessità di chiudere l'editor.

Gli stati persistiti sono:

`Checked → DownloadAuthorized → Downloading → Staged → InstallAuthorized →`
`WaitingForEditorExit → Applying → Verifying → Completed`.

Da `Checked` a `Staged` l'utente può annullare e il contenuto incompleto viene
isolato. Dopo l'inizio di `Applying` non si interrompe uno swap a metà: Stop,
crash o riavvio entrano nel recupero journalizzato e terminano in `Completed`,
`RolledBack` oppure `Failed`. `Cancelled` non equivale mai a successo.

## Piano immutabile e journal

Prima di avviare il worker, l'editor scrive atomicamente un piano versionato che
contiene almeno:

- identità installazione e versione corrente/obiettivo;
- RID e architettura;
- hash canonico del manifesto e identificatore della chiave attendibile;
- URI HTTPS già verificata, dimensione e SHA-256 dell'asset;
- directory target, staging e backup risolte e validate;
- inventario previsto dei file;
- PID e istante di avvio dell'editor, nonce monouso e scelta di riavvio;
- versione minima del protocollo updater.

Il piano non contiene segreti. Directory e file non vengono ricalcolati da
input relativi dopo la validazione. Il journal viene aggiornato con replace
atomico e flush durevole prima e dopo ogni operazione irreversibile; registra
soltanto metadati tecnici, mai sorgenti, workspace o credenziali.

## Download e staging

- Il payload viene scritto come file `.partial`, con limite di dimensione e
  hash SHA-256 calcolato durante lo streaming.
- Firma, manifesto, dimensione e hash vengono verificati prima
  dell'estrazione e nuovamente prima dell'applicazione.
- L'archivio viene estratto in una directory nuova sullo stesso volume del
  target, così che gli scambi di directory possano essere atomici.
- Sono rifiutati path assoluti o risalenti, ADS, device name, collisioni di
  maiuscole/minuscole, link simbolici, junction/reparse point, file duplicati,
  quote superate e contenuti non presenti nell'inventario.
- Nessun eseguibile estratto viene avviato durante lo staging.

La directory staged diventa sigillata soltanto dopo il confronto esatto con
l'inventario. Un payload incompleto o eccedente non può essere promosso.

## Applicazione fuori processo

Il worker verifica nonce, piano, manifesto, installazione corrente e identità
del processo padre. Attende la terminazione dell'esatto PID con lo stesso
istante di avvio; allo scadere del timeout non forza la chiusura.

Con un lock esclusivo sull'installazione esegue, sullo stesso volume:

1. rinomina il target corrente in una directory backup unica;
2. rinomina lo staging sigillato nel percorso target;
3. aggiorna il journal allo stato `Verifying`;
4. confronta nuovamente inventario, versione e file essenziali;
5. esegue in processo separato il comando fisso
   `--verify-installation`, con timeout e directory di lavoro neutra;
6. marca `Completed` e, soltanto se autorizzato, riavvia l'EXE noto.

Non vengono sovrascritti singoli file in uso. L'EXE da riavviare è derivato
dall'identità locale dell'installazione, non dal manifesto. Il worker non
eredita token, credenziali o handle non necessari dall'editor.

## Rollback e recupero dopo crash

Se inventario, self-test o primo avvio protetto falliscono, il worker sposta il
nuovo target in quarantena e ripristina il backup con lo stesso meccanismo di
rename. La versione precedente resta disponibile per un periodo di grazia e
viene eliminata soltanto da una successiva operazione controllata.

All'avvio, editor e worker leggono il journal prima di iniziare un nuovo
aggiornamento:

- prima dello swap: eliminano o riprendono soltanto lo staging verificabile;
- dopo il backup ma prima della promozione: ripristinano il backup;
- dopo la promozione: completano la verifica oppure eseguono rollback;
- dopo il completamento: non ripetono l'installazione e pianificano soltanto la
  pulizia differita.

Un journal mancante, futuro, ambiguo o corrotto blocca l'applicazione automatica
e mostra istruzioni di recupero; il sistema non indovina quale directory sia
valida e non cancella alcuna copia.

## Concorrenza, log e privacy

Un mutex per identità d'installazione impedisce updater concorrenti. Un secondo
avvio dell'editor durante `Applying` mostra lo stato di manutenzione e non apre
l'installazione parziale. I log hanno dimensione e conservazione limitate,
redigono query e credenziali e non includono codice DDF, nomi dei workspace o
dati dell'host.

## Piano di consegna

1. **Contratti e simulatore — completato** — schema del piano/journal, macchina
   a stati, validazione dei percorsi, scrittura atomica e simulazione crash
   senza modificare installazioni.
2. **Download e staging — completato** — streaming limitato, verifica ripetuta,
   estrazione sicura e inventario sigillato; nessuna applicazione.
3. **Worker e swap — completato** — processo esterno, lock, attesa dell'editor,
   rename atomici, verifica strutturale e rollback su directory temporanee
   controllate; l'esito positivo resta in `Verifying` fino al futuro health
   check.
4. **Health check e UX — completato** — self-test fisso con timeout e marker,
   ispezione fail-closed del recovery all'avvio, consenso accessibile distinto,
   restart opzionale e pulizia differita esplicita.
5. **Attivazione** — chiave ufficiale e release firmata reale completate;
   download, staging, swap e health check riusciti su una copia isolata.
   Rollback e recovery dei due punti critici dello swap sono riusciti con la
   release reale; restano gli altri casi della matrice e un nuovo
   pacchetto che includa la correzione del redirect CDN prima dell'abilitazione.

La prova ripetibile usa `tools/test-real-update-acceptance.ps1` con archivio e
manifesto della release firmata, una directory nuova sotto `.artifacts` e,
per l'applicazione completa, `-PreviousBuildRoot` puntato a una pubblicazione
`0.12.5` sotto `.artifacts/publish`. Lo script rifiuta una destinazione già
presente, valida la firma ufficiale e l'hash dell'archivio, confronta
l'inventario estratto, attesta che il worker attenda l'esatto processo di prova
prima dello swap e verifica versione, stato `Completed` e backup. Non pulisce
automaticamente target e backup, per lasciare l'evidenza ispezionabile.
`-RollbackProbe` corrompe soltanto l'EXE della nuova copia dopo lo swap e
verifica quarantena, hash e self-test del target ripristinato; `-CrashProbe`
simula l'interruzione dopo il rename target→backup e verifica il recovery e il
self-test della versione precedente. Ogni modalità usa una directory nuova.

`DdfProductUpdateArchiveInspector` costruisce l'inventario del piano da uno
ZIP già scaricato soltanto dopo firma, policy di compatibilità, hash e
dimensione; rifiuta path insicuri, link, duplicati e quote superate senza
estrarre né eseguire file. La prova reale con `v0.12.6` ha prodotto 373 voci;
l'installazione completa è riuscita anche in un percorso con spazi. Nello
script lo ZIP iniziale è fornito esplicitamente; lo stager può ora usare
`StageLocalAsync` per ricontrollarlo e sigillarlo senza un secondo download.
Il primo consenso al download è ora collegato alla UI standalone: prima di
mostrarlo, `InspectOfferAsync` rilegge il manifesto firmato e presenta versione,
origine e dimensione dell'asset. Annullare non richiede lo ZIP. Dopo conferma,
`DownloadAuthorizedAsync` verifica di nuovo il manifesto e scarica l'archivio
nella directory di stato, senza installarlo o eseguirlo. Il consenso separato
all'installazione, il piano persistito e l'avvio del worker restano da collegare.
L'embedded non adotta nessuna di queste azioni.

Il download verificato conserva anche il manifesto firmato come ricevuta
locale accanto allo ZIP. La riapertura verifica di nuovo firma, policy, hash e
inventario; il solo fatto che i file esistano non autorizza lo staging o
l'installazione. Il piano persistito nella UI resta una fase successiva.
La factory Core del piano può ora derivare i campi firmati soltanto da questa
riapertura. Un test isola piano e journal e completa `StageLocalAsync` senza
rete, fermandosi a `Staged`: non viene autorizzata l'installazione. Il binding
alla directory dell'editor in esecuzione, il consenso distinto e l'avvio del
worker rimangono esclusi dalla UI corrente.
Il preflight di destinazione legge soltanto il percorso dell'EXE in esecuzione
e controlla che sia una distribuzione standalone self-contained integra nei
componenti essenziali. Rifiuta copie nel repository, directory di sistema e
reparse point; prepara percorsi distinti sul medesimo volume ma non li crea.
La UI mostra l'esito dopo il download senza costruire o applicare un piano.
Il coordinatore Core `DdfProductUpdateLocalPreparation` può già persistire
piano e journal e sigillare lo staging da archivio locale verificato. Rifiuta
uno stato precedente e non concede `InstallAuthorized`; non viene chiamato
dalla UI. Il PID e l'istante dell'editor devono essere acquisiti nella stessa
sessione in cui si chiede il consenso all'installazione: creare il piano al
solo download lo renderebbe obsoleto dopo un riavvio dell'editor.
Il gate Core `DdfProductUpdateInstallAuthorization` separa il rifiuto (nessuna
transizione) dall'autorizzazione: prima di `InstallAuthorized` ricontrolla
sessione, manifesto firmato, archivio, piano/journal e sigillo staging.
Non avvia il worker e non è ancora invocato dalla UI.

`DdfProductUpdatePackageDownloader` copre ora il primo download dopo consenso
esplicito del chiamante: recupera dal repository ufficiale manifesto e ZIP con
al massimo un redirect HTTPS verso CDN GitHub ammessi, verifica nuovamente
firma, policy e corrispondenza con l'aggiornamento già controllato, scarica
entro quota in un file `.partial`, controlla SHA-256 e produce l'inventario
tramite l'inspector. In caso di errore rimuove soltanto i propri file
temporanei. Una prova online reale su `v0.12.6` ha prodotto 373 voci senza
installare. La UI standalone espone il download soltanto dopo consenso
esplicito; la costruzione del piano persistito e l'avvio del worker rimangono
aperti. Nessuna installazione automatica è disponibile nella UI o
nell'embedded.
La prova `-UseLocalStaging` ha poi installato la stessa copia verificata in
un target isolato `0.12.5`, con health check verde e backup conservato, senza
richiedere una seconda connessione a GitHub (`2026-11-06`).

## Matrice di verifica obbligatoria

- portable e installazione per utente, Debug/Release e percorsi con spazi;
- file in uso, accesso negato, spazio esaurito e volume non compatibile;
- archivio tronco/corrotto, firma o hash errati, inventario mancante/eccedente;
- path traversal, ADS, link, reparse point, duplicati e nomi riservati;
- interruzione simulata prima e dopo ogni transizione del journal;
- rollback per self-test fallito e ripristino della versione precedente;
- riavvio accettato, rifiutato e fallito senza falsi esiti positivi;
- due istanze editor/updater e PID riutilizzato con istante differente;
- incompatibilità fra protocollo worker, manifesto e schema persistito;
- assenza completa di download/installazione/elevazione nell'embedded.

Il gate Core verifica ora con un processo reale il PID riutilizzato e l'editor
ancora aperto; il test del lock conferma che un secondo worker non avanza e
non modifica il target. Restano le prove sui file bloccati da terzi e sugli
altri guasti ambientali prima di attivare la UI.

## Criteri di accettazione

L'installazione potrà essere dichiarata disponibile soltanto quando una suite
end-to-end dimostrerà che nessun asset non verificato viene promosso, nessun
file viene sostituito con l'editor attivo, ogni crash lascia almeno una copia
completa recuperabile e il componente embedded rimane estraneo al lifecycle di
installazione. Fino ad allora l'unica azione consentita resta aprire la release
verificata su richiesta dell'utente.
