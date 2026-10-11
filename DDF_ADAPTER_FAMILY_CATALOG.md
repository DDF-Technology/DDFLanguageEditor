# Catalogo delle famiglie adapter DDF

Il catalogo [adapter-families/catalog.json](adapter-families/catalog.json) è
l'inventario governato delle integrazioni esterne previste o già verificate.
Non è un package manager, non effettua discovery, non carica assembly e non
concede capability al runtime. La risoluzione e l'esecuzione restano affidate
ai riferimenti esatti, ai descrittori API e agli adapter autorizzati.

Lo schema è `ddf-adapter-family-catalog.schema.json`, formato `1.0`. Il loader
Core è rigoroso: rifiuta proprietà sconosciute, enum non riconosciuti, ID o
capability duplicate e confini incompleti.

## Classificazione

- `ddf.dotnet.*`: adapter basati su API o provider gestiti .NET;
- `ddf.platform.*`: integrazioni legate a sistema operativo, driver o hardware;
- `host.*`: capacità definite e implementate dall'applicazione incorporante.

Una famiglia adapter dichiara sempre `standardLibrary: false`. Il catalogo non
può quindi promuovere accidentalmente un wrapper, un provider o un servizio
esterno dentro `ddf.standard`.

## Stadi

- `available`: il percorso verticale esiste ed è coperto da evidenze;
- `candidate`: esiste un caso d'uso, ma almeno una responsabilità o un gate è
  ancora aperto;
- `deferred`: la famiglia è intenzionale ma una scelta immediata produrrebbe
  architettura o dipendenze premature.

`available` è uno stato vincolante. Richiede maintainer, almeno un caso d'uso,
almeno una capability, nessun blocker e tutti i gate `complete` oppure
`notApplicable` con una motivazione. Una voce `candidate` o `deferred` non deve
essere presentata come funzionalità supportata nella UI o nella documentazione
commerciale.

Il record `ddf.platform.hardware.core` rende disponibile soltanto il contratto
governato e il loopback simulato. Il supporto seriale è dichiarato separatamente
da `ddf.platform.hardware.serial`. RS422/RS485 hanno a loro volta la famiglia
`ddf.platform.hardware.differential-serial`: essa rende disponibile il contratto
di profilo e direzione, non garantisce che una qualunque porta COM disponga del
transceiver, driver o controllo elettrico necessario. La famiglia MIDI è
`candidate`: il contratto UMP/legacy e i gate simulati esistono, ma il bridge
fisico C#/WinRT e i relativi gate di portabilità/licenza non sono ancora chiusi.
Anche `ddf.platform.hardware.parallel` è `candidate`: il contratto LPT/IEEE 1284
è verificato senza accesso diretto ai registri, ma driver, SDK vendor e matrice
fisica devono ancora essere scelti. `ddf.platform.hardware.iot` applica lo
stesso stato `candidate` a GPIO/I²C/SPI/PWM: profili, transazioni e bridge
`System.Device.Gpio` sono coperti, mentre la matrice pubblicata non contiene
ancora risultati fisici sufficienti alla promozione.
Gli altri provider fisici conservano gate e roadmap propri.

## Gate obbligatori

| Gate | Domanda a cui deve rispondere |
| --- | --- |
| `security` | Quali autorità, segreti, input, quote e lifecycle sono governati? |
| `portability` | Dove funziona realmente e quali provider o componenti la limitano? |
| `licensing` | Quali dipendenze si possono usare e redistribuire? |
| `testing` | Quali test deterministici, reali opt-in e confronti VM/Roslyn la provano? |

L'evidenza non può essere vuota nemmeno per `required` o `notApplicable`: nel
primo caso descrive il lavoro che blocca l'ammissione, nel secondo spiega perché
il gate non si applica.

## Processo di ammissione

1. Registrare un caso d'uso integratore concreto e assegnare un maintainer.
2. Stabilire classificazione, portabilità, dipendenze e capability minime.
3. Scrivere il contratto tipizzato senza tipi Roslyn, reflection o discovery
   implicita.
4. Completare sicurezza, portabilità e licenze prima della distribuzione.
5. Aggiungere fake deterministici, test reali opt-in quando servono e un gate
   equivalente su VM e backend compilato.
6. Rimuovere tutti i blocker e portare la voce ad `available` nella stessa
   modifica che consegna documentazione ed evidenze.

Il fingerprint SHA-256 prodotto da `DdfAdapterFamilyCatalogLoader` è canonico
rispetto all'ordine delle famiglie. Può identificare l'istantanea di governance,
ma non sostituisce il lockfile dei riferimenti né autorizza un adapter.
