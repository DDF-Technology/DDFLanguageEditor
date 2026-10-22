# Adapter hardware governati DDF

`DDFLanguageEditor.Hardware` definisce il confine comune per dispositivi reali,
simulati e futuri provider `ddf.platform.hardware.*`. Il contratto comune e il
loopback consentono verifiche senza hardware; i provider seriali aprono soltanto
endpoint configurati esplicitamente dall'host.

## Confine di autorità

Lo script non può enumerare dispositivi, porte o driver e non può fornire
percorsi, nomi COM, indirizzi I/O, VID/PID, CLSID o chiavi native. L'host crea
una `DdfHardwareDeviceIdentity`, la associa a un nome logico e inietta
esplicitamente un `IDdfHardwareDevice`. Identità, provider, chiave fisica,
architettura e driver devono coincidere esattamente con la definizione
autorizzata.

Ogni funzione visibile allo script è una
`DdfHardwareOperationDefinition` già collegata a:

- un solo dispositivo logico;
- una delle autorità `probe`, `read`, `write` o `configure`;
- almeno un permesso DDF;
- un binding e una firma tipizzati;
- per `configure`, un profilo host predefinito e non parametri arbitrari.

Il descrittore `DdfExternalApiDefinition` non contiene la chiave fisica o il
driver. Analisi, VM e futuro bridge compilato vedono quindi la stessa superficie
logica senza ottenere discovery o reflection.

## Lifecycle e concorrenza

La prima operazione di un'esecuzione acquisisce un lease. Il lease viene
registrato nelle risorse del runtime e rilasciato automaticamente, anche in caso
di errore o cancellazione. Il dispositivo viene aperto al primo lease e chiuso
quando termina l'ultimo.

`Exclusive` rifiuta una seconda esecuzione concorrente. `Shared` permette più
lease, ma deve essere scelto soltanto quando il provider garantisce una
semantica concorrente sicura. Timeout del dispositivo, deadline globale e
cancellazione vengono combinati; frame oltre quota vengono respinti prima della
scrittura e dopo la lettura.

Il provider pubblica variazioni di disponibilità con identità logica e
generazione monotona. DDF può interrogare un'operazione `IsAvailable`; callback
script e stream asincroni verranno aggiunti solo insieme alla semantica eventi
del linguaggio. Uno scollegamento durante un'operazione produce un errore DDF e
non autorizza ricerca automatica di un dispositivo sostitutivo.

## Loopback

`DdfLoopbackHardwareDevice` non accede al sistema. Mantiene una coda di frame,
può riecheggiare le scritture, applica profili autorizzati, simula hot-plug e
latenza ed espone contatori di apertura/chiusura per i test. È destinato a:

- gate automatici senza periferiche;
- modalità didattica;
- prototipi di applicazioni host;
- confronto futuro tra VM e backend Roslyn.

Non è un emulatore elettrico di RS232, RS485, MIDI, GPIO o altri bus. Ogni
provider reale dovrà aggiungere un proprio caso d'uso, maintainer e gate di
sicurezza, portabilità, licenza e test prima di essere dichiarato disponibile.

## Segnale acustico pianificato

La futura famiglia `ddf.platform.hardware.audio` esporrà un segnale acustico
come API tipizzata di piattaforma, non come sintassi del linguaggio. La prima
forma prevista è:

```ddf
beep();
```

`beep() out void` userà frequenza e durata predefinite dal profilo host. Dopo
l'attivazione del tipo `duration`, una funzione distinta
`tone(int frequencyHz, duration length) out void` consentirà toni espliciti
entro i limiti autorizzati. Non sono previsti overload né accesso a driver,
registri, porte I/O o mixer di sistema dal sorgente.

L'host registrerà un endpoint logico `internalSpeaker` e la capability
`hardware.audio.signal`. Un provider PC speaker, un provider audio di sistema e
un simulatore sono implementazioni differenti: se il provider richiesto non è
disponibile, l'operazione fallisce con un errore DDF tipizzato invece di usare
silenziosamente un'altra uscita. Frequenza, durata, numero di segnali, duty/rate
e volume eventualmente disponibile sono soggetti a policy e quote host.

Il provider deve eseguire e attendere il segnale in modo cooperativo fuori dal
thread UI, osservare cancellazione e timeout e arrestare il tono quando la
piattaforma lo consente. Un'API o un driver non cooperativo richiede isolamento
nel worker. Prima della promozione da `candidate` serviranno simulatore
deterministico, confronto VM/Roslyn, matrice Windows/hardware e test fisico
opt-in su una macchina dotata dell'uscita dichiarata.

## Provider seriale

`DdfSerialHardwareDevice` realizza il primo provider fisico sopra
`System.IO.Ports` 10.0.12. L'host costruisce un endpoint esplicito; il provider
non chiama `SerialPort.GetPortNames` e lo script non riceve mai il nome della
porta.

`DdfSerialCommunicationProfile` definisce in modo tipizzato baud rate, bit dati,
parità, stop bit, handshake, DTR e RTS. `DdfSerialEndpointOptions` conserva:

- porta fisica e profilo iniziale;
- profili alternativi nominati autorizzati;
- buffer di lettura e scrittura;
- framing `Line` oppure `RawUtf8Chunk`;
- delimitatore, append in scrittura e quota massima in byte.

I frame sono decodificati con UTF-8 rigoroso. Il framing line conserva i byte
successivi al delimitatore per la lettura seguente; payload incompleti o oltre
quota non vengono restituiti. Le operazioni sullo stream usano cancellazione e
la deadline viene applicata dal contratto hardware comune. L'apertura sincrona
richiesta da `SerialPort.Open()` verifica la cancellazione prima della chiamata;
un driver che blocca in modo non interrompibile dovrà essere confinato in un
worker, non mascherato aumentando il timeout.

`DataAvailable` e `Faulted` sono eventi host osservabili. Finché DDF non avrà
una semantica asincrona stabile, lo script legge esplicitamente tramite la
funzione autorizzata; non vengono create callback implicite nel codice DDF.
Scollegamento e fault non attivano discovery o selezione automatica di un'altra
porta.

I gate automatici usano `IDdfSerialPortConnection`, quindi verificano profili,
framing, segnali, cancellazione e cleanup senza dipendere dalla macchina. Le
prove reali sono opt-in e devono dichiarare sistema operativo, driver,
adattatore e collegamento loopback.

## Profili RS422 e RS485

`DdfDifferentialSerialHardwareDevice` aggiunge semantica differenziale esplicita
sopra lo stesso provider seriale. `DdfDifferentialSerialProfile` distingue:

- RS422 full-duplex;
- RS485 full-duplex;
- RS485 half-duplex;
- direzione gestita dal driver, RTS alto/basso durante la trasmissione oppure
  adapter vendor nominato;
- ritardi limitati prima e dopo la trasmissione per il turnaround.

Il controllo RTS manuale è ammesso soltanto in half-duplex. Prima di una
lettura e dopo ogni scrittura, cancellazione o errore, il provider riporta la
linea in ricezione. Apertura e cambio profilo sono transazionali: se la
configurazione differenziale fallisce, la porta viene chiusa oppure viene
ripristinato il profilo precedente.

`System.IO.Ports` copre la direzione gestita dal driver e le due polarità RTS,
ma rifiuta un profilo `VendorAdapter`: quel percorso richiede una specifica
implementazione di `IDdfDifferentialSerialPortConnection`. Questa astrazione
non trasforma una porta COM in RS422/RS485 e non certifica terminazione, bias,
isolamento o livelli elettrici. Porta, transceiver, cablaggio, driver e SDK del
produttore restano responsabilità dichiarate dall'host.

Quattro gate con connessione iniettata verificano profili validi, full/half
duplex, sequenza direzione/turnaround, ripristino dopo cancellazione e rifiuto
dei profili vendor non supportati. Le prove fisiche restano opt-in e devono
documentare adattatore, driver, cablaggio e strumentazione.

## Contratto MIDI

`DdfMidiHardwareDevice` fornisce il contratto provider-neutral per MIDI 1.0/2.0.
L'endpoint fisico viene scelto dall'host e rimane nella
`DdfHardwareDeviceIdentity`; lo script non enumera endpoint e non riceve ID
WinRT, nomi porta o dettagli del driver.

`DdfMidiEndpointProfile` separa obbligatoriamente:

- `WindowsMidiServices` con pacchetti UMP;
- `LegacyMidi1` con byte stream MIDI 1.0;
- flusso input, output o bidirezionale;
- gruppi UMP autorizzati;
- scheduling in uscita, SysEx e relativa quota;
- numero massimo di pacchetti segnalati in coda.

`DdfMidiPacket` valida il numero di word richiesto dal Message Type UMP e usa
una rappresentazione testuale canonica, ad esempio
`ump:0:20903C7F` oppure `midi1:0:90,3C,7F`. Il timestamp `0` richiede invio
immediato; un timestamp non nullo conserva il dominio di clock del provider.
I timestamp ricevuti sono sempre osservabili, mentre lo scheduling in uscita è
consentito soltanto dal profilo. SysEx è deny-by-default e soggetto a quota.

`IDdfMidiEndpointConnection` è il solo confine autorizzato verso Windows MIDI
Services o il percorso legacy. Espone apertura, profilo, lettura, invio,
`PacketAvailable` e `Faulted`; apertura e cambio profilo hanno cleanup/rollback,
mentre uno scollegamento aggiorna la disponibilità senza cercare un endpoint
sostitutivo. Quattro gate iniettati coprono isolamento degli identificatori,
UMP canonico, gruppi, timestamp, SysEx, legacy, hot-plug e cleanup.

Al 10 ottobre 2026 il bridge fisico C#/WinRT non viene ancora distribuito dal
progetto: Windows MIDI Services usa `Windows.Devices.Midi2`, ma API e pacchetti
sono ancora pre-release e soggetti a condizioni di distribuzione/versione. Il
catalogo mantiene quindi `ddf.platform.hardware.midi` come `candidate`; la
famiglia diventerà `available` soltanto dopo aver fissato versione, matrice
Windows/API mode, licenza, packaging e test opt-in su endpoint reali. Il fallback
legacy resterà un provider distinto, mai una selezione silenziosa.

Le callback DDF asincrone verranno collegate quando la semantica eventi del
linguaggio sarà stabilizzata; per ora gli eventi sono osservabili dall'host e
dal debugger dell'integrazione, mentre lo script usa letture esplicite.

## Contratto LPT e IEEE 1284

`DdfParallelHardwareDevice` governa dispositivi paralleli legacy senza esporre
indirizzi I/O, registri, handle o codici di controllo arbitrari. La porta e il
driver sono selezionati dall'host tramite `DdfHardwareDeviceIdentity`; il codice
DDF vede soltanto il dispositivo logico e le operazioni autorizzate.

`DdfParallelPortProfile` dichiara:

- provider Windows IEEE 1284, SDK vendor oppure simulatore;
- modi `Compatibility`, `Nibble`, `Byte`, `Epp` ed `Ecp`;
- flusso solo output o bidirezionale;
- quota massima per trasferimento;
- eventuale indirizzo IEEE 1284.3 daisy-chain;
- autorizzazione esplicita al reset;
- profilo vendor nominato, obbligatorio soltanto per un SDK vendor.

I dati usano il formato canonico `data:XX,YY`. Letture bidirezionali e
scritture sono limitate prima e dopo il bridge; stato `Busy`, `PaperEmpty`,
`Selected`, `Offline`, `PowerOff`, `NotConnected` ed `Error` è tipizzato e
osservabile dall'host. Apertura e cambio profilo eseguono cleanup o rollback;
fault di disconnessione aggiornano l'hot-plug senza cercare una porta
sostitutiva.

`IDdfParallelPortConnection` è l'unico confine verso un driver firmato,
l'interfaccia di dispositivo Windows o un SDK del produttore. Non comprende
alcuna operazione per leggere/scrivere un indirizzo come `0x378`, inviare IOCTL
arbitrari o caricare driver. Quattro gate simulati coprono isolamento, modi,
quote, stato, reset, hot-plug, rollback e cleanup.

Windows documenta `GUID_DEVINTERFACE_PARALLEL`, i modi IEEE 1284 e gli IOCTL del
driver di classe, ma molti callback e controlli operano in kernel mode. Per
questo il progetto non effettua P/Invoke diretto né dichiara compatibile un
adattatore USB-parallelo generico. La famiglia resta `candidate` finché un
bridge concreto non supera firma, licenza, architettura, esclusività e prove
fisiche opt-in.

## Contratto GPIO, I²C, SPI e PWM

`DdfIotHardwareDevice` usa profili fortemente tipizzati per quattro trasporti:

- `DdfGpioProfile`: pin, input/output, pull-up/down, active-low, debounce e
  livello iniziale deterministico delle uscite;
- `DdfI2cProfile`: bus, indirizzo a 7 bit, quota e lunghezza di lettura;
- `DdfSpiProfile`: bus, chip-select, clock, modo 0–3, word length e quota;
- `DdfPwmProfile`: chip, canale, frequenza minima/massima, frequenza e duty
  iniziali e autorizzazione alle variazioni dinamiche.

Questi valori appartengono all'host e non vengono proiettati nel descrittore
DDF. Lo script usa soltanto frame canonici `level:0|1`, `data:XX,YY` e
`pwm:frequenza:duty`. Tipo, direzione e quote vengono verificati prima e dopo
il provider.

Il contratto hardware comune include ora `Exchange`, che richiede insieme i
permessi `read` e `write`, accetta un frame e restituisce un frame nella stessa
operazione governata. I²C può così eseguire write/read ripetuto e SPI mantiene
la transazione full-duplex e il chip-select senza spezzare l'operazione in due
chiamate concorrenti. GPIO e PWM rifiutano `Exchange`.

`IDdfIotEndpointConnection` è il confine iniettabile per `.NET IoT`, bridge USB
o SDK vendor. Apertura e cambio profilo hanno cleanup/rollback; eventi input e
hot-plug sono host-side e non attivano discovery. Sette gate simulati coprono
topologia nascosta, direzione GPIO, segnali, scambi I²C/SPI atomici, quote, PWM,
failure cleanup e isolamento/lifecycle del bridge, portando la suite hardware
a 27 test.

`System.Device.Gpio` 4.2.0 è stabile, MIT e supporta GPIO, I²C, SPI e PWM su
piattaforme e bridge compatibili. La famiglia resta tuttavia `candidate` fino a
quando il bridge concreto, i binding scelti e una matrice board/OS/architettura
non saranno verificati su hardware reale; la disponibilità del package non
implica che ogni PC possieda controller o pin utilizzabili.

## Bridge fisico .NET IoT

`DdfSystemDeviceIotConnection` collega i contratti a `System.Device.Gpio`
4.2.0 senza inserire oggetti o tipi del provider nelle API DDF. Il bridge crea
soltanto il pin/bus/canale selezionato dal profilo host, mantiene atomici
`I2cDevice.WriteRead` e `SpiDevice.TransferFullDuplex`, inoltra gli eventi GPIO,
serializza cambio profilo e I/O e conserva l'endpoint precedente se la nuova
configurazione non può essere aperta. PWM viene arrestato e ogni handle viene
rilasciato alla chiusura.

La disponibilità NuGet non certifica l'hardware. La matrice e il comando di
prova fisica volontaria sono descritti in
[`DDF_IOT_PHYSICAL_TESTS.md`](DDF_IOT_PHYSICAL_TESTS.md). Finché almeno una riga
per trasporto non viene eseguita e registrata su hardware reale, la famiglia
resta `candidate`.
