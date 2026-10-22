# Protocollo worker DDF .NET

Stato: protocollo worker `2.0`, framing duplex `1.1`, separazione di processo e
policy di accesso `1.3` della milestone `0.13.2`.

## Confine

`DDFLanguageEditor.Runner` è un processo console distinto dall'editor. Riceve
una richiesta monouso oppure una sessione JSONL duplex su standard input/output
e termina. Nessun assembly compilato viene caricato nel processo
dell'editor quando si usa `DdfDotNetWorkerClient`.

La richiesta `ddf-dotnet-worker-request` `2.0` contiene:

- correlation ID obbligatorio;
- manifest package canonico;
- DLL, Portable PDB, source map e manifest di ogni modulo;
- percorsi assoluti delle sole reference di piattaforma già bloccate, validi
  soltanto per l'IPC locale;
- byte, identità completa, dimensione e SHA-256 delle sole DLL esterne
  autorizzate incluse nel bundle, senza i loro percorsi originali;
- TFM, runtime ABI, RID e capability dichiarate;
- policy di accesso `ddf-dotnet-worker-access-policy` `1.3`;
- snapshot file logici, con autorizzazione e quota di scrittura esplicite;
- snapshot logici di risposte di rete preautorizzate, senza URL o credenziali;
- nomi logici delle risorse HTTP live che il worker può richiedere all'host;
- coda facoltativa di input UTF-8 preautorizzato;
- grant facoltativo per input interattivo brokerizzato con quote esplicite;
- piano debug batch facoltativo con breakpoint DDF e comandi preautorizzati;
- budget massimo di istruzioni.

Il worker non si fida dei dati ricevuti: ricrea il reference set dai file di
piattaforma e dalle immagini esterne dopo averne decodificato l'identità PE,
con limiti di 64 assembly e 32 MiB complessivi (`DDFC552`),
ricostruisce gli artefatti, ricollega il grafo, rigenera il bundle e richiede la
corrispondenza byte-per-byte del manifest package. Il normale loader ripete poi
compatibilità e verifica PE/IL prima del caricamento.

La risposta `ddf-dotnet-worker-response` `2.0` ripete il correlation ID e
restituisce soltanto valori scalari DDF, stato `end`, istruzioni, output catturato
o codice/messaggio di errore. Dopo un'esecuzione riuscita può includere scritture
file staged con nome logico, hash originale, testo e hash finale e attesta il
numero di valori input consumati. Testo prodotto dallo script non viene scritto
sul canale del protocollo. Quando richiesto include una traccia debug limitata
con sole posizioni, funzioni, simboli, tipi e valori DDF.

## Lifecycle e limiti

Il client usa pipe reindirizzate, nessuna shell e nessuna finestra. Timeout o
cancellazione terminano l'intero albero del processo. Ogni worker gestisce una
sola esecuzione e quindi non conserva globali o assembly fra due richieste.
La richiesta è limitata a 64 MiB di caratteri JSON, la risposta a 4 MiB e
l'output catturato a 10.000 elementi/1 MiB, prima della serializzazione.

Su Windows il client crea un Job Object prima di inviare la richiesta e vi
assegna immediatamente il worker. Il profilo predefinito ammette un solo processo
quando il token è già ristretto, oppure al massimo il bootstrap fidato e il
worker ridotto quando deve crearlo; limita la memoria di ciascun processo a
1 GiB e il tempo CPU alla scadenza
della richiesta e termina il processo alla chiusura del job. Una mancata
creazione, configurazione o assegnazione non produce un downgrade silenzioso:
l'esecuzione viene rifiutata con `DDFC535`. Su ogni sistema il worker usa una
directory di lavoro temporanea dedicata, rimossa al termine della sessione.

L'ambiente ereditato viene ricostruito da zero: directory temporanee e profilo
.NET puntano alla directory della sessione, telemetria e diagnostica .NET sono
disabilitate e non vengono inoltrate variabili applicative o credenziali. Sono
conservati soltanto `SystemRoot` e, quando indispensabile per risolvere un host
`dotnet` non assoluto, `PATH`.

Su Windows il client tenta anche di rimuovere l'ereditarietà ACL dalla directory
di sessione e di concedere il controllo completo soltanto all'identità corrente.
Gli host che richiedono questa garanzia impostano
`RequireProtectedWorkingDirectory`: se il sistema o il contenitore non consente
di modificare la DACL, il worker non parte e restituisce `DDFC538`. Senza tale
opzione l'esito resta osservabile in `WorkingDirectoryAccessControlApplied`,
senza dichiarare una protezione che non è stata applicata.

La policy `1.3` è obbligatoria e deny-by-default. Su Windows la richiesta
predefinita esige un token ridotto. Il runner attesta il token corrente se è già
ristretto; altrimenti un bootstrap fidato crea un token filtrato con privilegi
massimi disabilitati, modalità LUA e gruppo `INTERACTIVE` deny-only. Non vengono
usati restricting SID che applicherebbero un secondo controllo ACL incompatibile
con il caricamento di CoreCLR su installazioni Windows comuni. L'attestazione
legge i gruppi del token e non si fida del solo marcatore del processo figlio. Un
downgrade o un'attestazione mancante produce `DDFC537`; l'attestazione accompagna
anche le risposte d'errore successive alla verifica, così la diagnostica DDF
originale non viene mascherata. Il worker accetta filesystem `None`,
`BrokeredRead` oppure `BrokeredReadWrite` e rete `None` oppure
`BrokeredClient`; le sole capability ammesse sono `brokerReadText`,
`brokerWriteText`, `brokerReadResponse` e `brokerFetchResponse`, richieste
esattamente da policy, ambiente e bundle. La rete ammette snapshot già acquisiti
oppure richieste live verso nomi logici autorizzati; il worker non riceve endpoint,
credenziali e non apre connessioni. Le altre modalità vengono
rifiutate con `DDFC536`.

Il protocollo `2.0` non concede accesso diretto alla console o al debugger CLR;
non inoltra capability host diverse
dai broker file/rete o risultati composti. Un
input deve essere fornito integralmente prima dell'avvio. Un bundle che richiede
capability fuori dalla policy broker esatta viene rifiutato prima dell'avvio.

La separazione di processo e, su Windows, il contenimento delle risorse limitano
crash, blocchi, memoria, CPU e stato residuo, ma non costituiscono ancora una
sandbox di sicurezza completa: il token Windows è ridotto e il solo accesso file
disponibile usa snapshot o richieste live brokerizzate; mancano capability ulteriori ed
equivalenti del profilo e dell'identità file sugli altri
sistemi operativi. Questi confini restano necessari prima di classificare un
package arbitrario come non fidato.

## Broker file

In `BrokeredRead` il processo chiamante legge un file
esplicitamente autorizzato e costruisce uno snapshot con nome logico, testo UTF-8
e SHA-256 tramite `DdfDotNetWorkerFileSnapshot.FromFile`. Il protocollo non
trasporta il percorso originale e il worker non apre quel file: la capability
unica `brokerReadText` risolve soltanto nomi logici presenti nella richiesta.

Sono ammessi al massimo 32 snapshot, 1 MiB ciascuno e 4 MiB complessivi. Nomi
duplicati, traversal, hash alterati, capability aggiuntive o disallineamento fra
policy, ambiente e manifest del bundle producono `DDFC539`. Elenco di directory,
accesso arbitrario per percorso e accesso diretto alla rete restano non supportati.

`BrokeredReadWrite` aggiunge `brokerWriteText`. Ogni snapshot scrivibile dichiara
una quota massima; il provider modifica soltanto una copia in memoria e la
risposta contiene le scritture esclusivamente se lo script termina con successo.
Il percorso fisico rimane nel client tramite
`DdfDotNetWorkerFileCommitTarget` e non attraversa mai l'IPC. Prima del commit il
client verifica corrispondenza esatta fra target e snapshot, hash originale,
hash finale, quota e assenza di modifiche concorrenti. Prepara tutti i file nella
directory di destinazione, conserva backup compensativi e applica il gruppo;
un errore ripristina i file già sostituiti. Target assente, risposta manomessa,
conflitto o commit fallito producono `DDFC540`; senza target espliciti una
richiesta scrivibile non avvia neppure il worker.

Prima di avviare il worker, il client apre ogni target con accesso read/write
reale e rifiuta file o directory `ReparsePoint` lungo l'intero percorso. Su
Windows registra inoltre l'identità kernel `volume serial + file index` e la
ricontrolla prima e dopo lo staging: eliminare e ricreare il file con gli stessi
byte non elude quindi il controllo SHA-256. Accesso non attestabile, reparse
point o identità cambiata producono `DDFC541`. Un commit riuscito espone per ogni
nome logico una `DdfDotNetWorkerFileProtectionReceipt`; nessun percorso fisico
entra nella ricevuta o nell'IPC. L'equivalente forte dell'identità file sui
sistemi non-Windows resta parte dell'hardening multipiattaforma.

## Broker di risposte di rete

In `BrokeredClient` l'host acquisisce e convalida una risposta prima di creare la
richiesta. `DdfDotNetWorkerNetworkSnapshot` trasporta soltanto nome logico,
contenuto UTF-8, media type e SHA-256; URL, header, token, cookie, certificati e
credenziali non attraversano l'IPC. Lo script può usare esclusivamente
`brokerReadResponse(nome)` e non può scegliere una destinazione di rete.

Sono ammesse al massimo 32 risposte, 1 MiB ciascuna e 4 MiB complessivi. Nomi
duplicati o non validi, media type non sicuri, hash alterati, quote superate o
disallineamento fra policy e capability producono `DDFC544`/`DDFC536`. Questo è
il confine deterministico per dati di rete preacquisiti.

`DdfDotNetWorkerNetworkBroker` implementa l'acquisizione HTTP(S) preflight lato
host. Ogni grant associa un nome logico a un endpoint esatto, media type, timeout
e quota; HTTPS è obbligatorio salvo opt-in esplicito dell'host. Il resolver viene
eseguito una sola volta, rifiuta l'intera risposta DNS se contiene indirizzi
locali, privati, link-local, multicast, condivisi o riservati e il trasporto
connette direttamente uno degli IP già approvati tramite `ConnectCallback`,
evitando una seconda risoluzione e il DNS rebinding. Proxy, cookie e redirect
sono disabilitati. Un provider credenziali fidato può configurare la richiesta,
ma non cambiare metodo o destinazione.

La risposta deve essere `2xx`, UTF-8, del media type autorizzato e sotto quota.
La ricevuta conserva soltanto nome logico, schema, porta, hash dell'host, stato,
media type, dimensione e hash del contenuto: query, header e credenziali non
entrano né nella ricevuta né nello snapshot IPC. Violazioni e fallimenti di
acquisizione producono `DDFC545`.

Con `brokerFetchResponse(nome)` il protocollo `2.0` consente anche
l'acquisizione durante l'esecuzione. La richiesta iniziale contiene al massimo
32 nomi in `LiveNetworkResources`; endpoint e credenziali restano esclusivamente
nell'handler host `DdfDotNetWorkerNetworkCapabilityHandler`. Il worker invia una
richiesta capability JSONL e resta sospeso; il client verifica capability, nome
logico e grant, esegue lo stesso preflight HTTP governato e restituisce soltanto
il contenuto UTF-8. Ogni acquisizione riuscita produce una receipt lato host.
Risorse incoerenti sono rifiutate con `DDFC547`, mentre richieste o risposte live
non autorizzate/non valide producono `DDFC548` e non raggiungono il trasporto.

Il gate ostile verifica inoltre che il percorso originale non compaia nella
richiesta serializzata, che un nome logico assente produca `DDF409` e che cinque
sessioni reali consecutive restino monouso, restituiscano lo stesso risultato e
non lascino directory `ddf-worker-*` dopo il cleanup.

Il gate di scrittura verifica inoltre deny-by-default senza target, link
simbolico quando consentito dall'host di test, sostituzione con contenuto
identico, conflitto ottimistico che conserva la modifica esterna e commit
riuscito con ricevute di commit e protezione.

## Input bufferizzato

`BufferedInput` contiene esclusivamente stringhe fornite dall'host prima
dell'avvio. Il worker le consuma in ordine tramite `readLine`; una richiesta senza
coda mantiene `DDF408` per canale non disponibile, mentre l'esaurimento della
coda durante lo script produce lo stesso errore nella posizione sorgente della
chiamata. La risposta espone soltanto `ConsumedInputCount`, non una copia dei
valori consumati.

Sono ammessi al massimo 1.024 valori, 64 KiB UTF-8 per valore e 1 MiB totale.
Valori null o quote superate producono `DDFC542` prima del caricamento del bundle.
Senza un grant interattivo non esistono prompt tardivi, letture arbitrarie da
stdin del processo figlio o accesso alla console dell'host: l'input utente resta
interamente pre-bufferizzato.

La policy `ConsoleInputAccess.BrokeredInteractive` e
`InteractiveInput` abilitano invece un prompt tardivo governato. Quando la coda
pre-bufferizzata è esaurita, `readLine` invia la capability interna
`worker.console.readLine` sul canale JSONL e sospende lo script. L'handler host
`DdfDotNetWorkerInteractiveInputHandler` restituisce soltanto una stringa JSON;
stdin del worker resta riservato ai frame del protocollo e non viene mai esposto
al codice DDF. Sono autorizzabili da 1 a 256 richieste e da 1 byte a 256 KiB per
valore; conteggio e dimensione vengono ricontrollati sia dal client sia dal
runner. Policy incoerenti, quota invalida, risposta non stringa o avvio senza
canale duplex producono `DDFC549`. Il percorso pre-bufferizzato resta preferito
e deterministico; se entrambi sono presenti viene consumato per primo.

## Debug batch

`DebugPlan` consente all'host di inviare prima dell'avvio breakpoint espressi
come documento e intervallo DDF, seguiti da una sequenza deterministica di
`Continue`, `StepInto`, `StepOver` e `StepOut`. Il runner usa le source map e
l'observer cooperativo della runtime ABI: non ispeziona né restituisce nomi,
metodi o percorsi sorgente CLR generati. Ogni pausa restituisce posizione DDF,
conteggio istruzioni, call stack e snapshot ricorsivo delle variabili già
limitato dalla ABI.

Sono ammessi al massimo 128 breakpoint, 256 comandi e 256 pause; il timeout di
attesa di una pausa è compreso tra 1 e 30.000 ms. `Stop`, percorsi assoluti o con
traversal e piani oltre quota vengono rifiutati con `DDFC543`. Se i comandi
terminano, il runner continua; raggiunto `MaxPauses`, disattiva i breakpoint e
porta a termine l'esecuzione. Anche timeout o nessun breakpoint raggiunto
producono `DDFC543`. Questo contratto resta adatto a gate riproducibili e
strumenti automatici ed è separato dal controllo interattivo seguente.

Con `DebugPlan.Interactive = true` la lista preventiva dei comandi deve essere
vuota. A ogni pausa il runner proietta lo stesso snapshot DDF limitato del piano
batch e lo invia all'host tramite `worker.debug.nextCommand`; non attraversano
l'IPC nomi C#, token IL, oggetti CLR o sorgenti generati. L'handler
`DdfDotNetWorkerInteractiveDebugHandler` restituisce dinamicamente `Continue`,
`StepInto`, `StepOver`, `StepOut` oppure `Stop`. Sequenza, correlation ID,
numero massimo di pause e timeout globale del worker continuano a essere
applicati. Snapshot o comandi non validi, avvio senza duplex e arresto richiesto
dall'host producono `DDFC550`; `Stop` termina in modo controllato l'esecuzione
invece di lasciare il thread sospeso. Il debug batch resta disponibile per CI e
test riproducibili.

Lo standalone può registrare sullo stesso canale debug, input e rete tramite
`DdfDotNetWorkerDuplexCapabilityRouter`. Il router usa identificatori esatti,
rifiuta registrazioni duplicate e risponde `DDFC551` a ogni capability non
presente nel catalogo costruito dall'host; non prova gli handler in sequenza e
non consente quindi a un provider di osservare richieste destinate a un altro.

`DdfDotNetEditorDebugService` è l'adapter usato esclusivamente dal launcher
standalone. Implementa `IDdfEditorDebugService`, converte i breakpoint di riga
negli intervalli degli snapshot compilati e proietta le pause ricevute nelle
strutture backend-neutral già usate da riga corrente, Call Stack, Variabili e
Watch. I comandi della UI completano la richiesta `nextCommand` pendente. Core,
WinForms e l'SDK embedded non referenziano Compiler o Runner e continuano a
usare la VM in-process; l'host embedded non può attivare questo adapter.

## Framing duplex

`DdfDotNetWorkerDuplexProtocol` definisce l'involucro JSONL `1.1` del canale
bidirezionale opzionale, separato dal payload worker `2.0`. I frame ammessi
sono `Start`, `CapabilityRequest`, `CapabilityResponse`, `Complete` e `Abort`;
il Runner usa ora un gate asincrono cancellabile per mantenere una sola
richiesta pendente, scrive e legge i frame senza occupare il chiamante e valida
sempre sequence, correlation ID e request ID prima della ripresa. La Runtime ABI
`1.9.0`, la sessione standalone e gli assembly Roslyn attendono ora l'intera
catena asincrona fino alla capability host.
ognuno porta correlation ID e sequenza monotona. Una richiesta capability ha un
ID monouso e argomenti JSON limitati, mentre la risposta deve riferirsi
esattamente all'unica richiesta pendente.

`DdfDotNetWorkerDuplexTranscript` applica il lifecycle fail-closed: `Start` è il
primo frame, è ammessa al massimo una capability pendente, gli ID non possono
essere riutilizzati, il limite è 256 chiamate e `Complete`/`Abort` sono terminali.
Frame multilinea, payload estranei al tipo, JSON non valido, sequenze saltate o
correlazioni differenti producono `DDFC546`. I frame ordinari sono limitati a
4 MiB; soltanto `Start` usa il limite della richiesta iniziale di 64 MiB.

Durante un debug interattivo una richiesta capability ordinaria può contenere
`cooperativeDebugSnapshot`: è l'ultimo sequence point DDF prodotto prima della
chiamata e comprende soltanto posizione, contatore, frame e valori DDF già
validati entro quote. Non viene mai allegato a `worker.debug.nextCommand`, che
resta una vera pausa del debugger distinta. Il client notifica a un observer
opzionale l'ingresso in `AwaitingCapability` prima di invocare l'handler host e
la ripresa in `finally`, usando correlation ID e request ID dello stesso frame.
Nessun `Task`, thread, nome generato C# o frame del provider attraversa l'IPC.

`ExecuteDuplexAsync` e il runner usano effettivamente questa macchina a stati:
`Start` trasporta la richiesta, il runner può emettere richieste capability e
`Complete` restituisce la risposta finale. Il percorso legacy monouso resta
disponibile e chiude stdin dopo la richiesta. La prima capability live collegata
sono `brokerFetchResponse`, l'input console brokerizzato e il controllo debug
interattivo esclusivamente in termini DDF.

Timeout e cancellazione terminano l'albero del worker e cancellano il token del
callback host. Dopo il kill il client attende gli stream soltanto per un
intervallo limitato: se un handler ignora il token, la chiamata torna comunque
al chiamante e la completion tardiva viene osservata senza scrivere una
`CapabilityResponse` né modificare un risultato già chiuso. Il gate reale copre
sia timeout sia Stop con un handler deliberatamente non cooperativo.
