# Adapter HTTP/JSON DDF

## Stato

La `0.13.4` introduce `DDFLanguageEditor.Network`, adapter gestito della
famiglia `ddf.dotnet.*` per servizi HTTP/JSON. Il progetto usa esclusivamente
le API HTTP e JSON incluse in .NET 10 e non aggiunge dipendenze al Core,
all'editor embedded o alla libreria standard DDF.

## Contratto deny-by-default

`DdfHttpJsonAdapter` opera su una sola origine HTTPS esatta. Lo script non può
fornire URL, host, metodo, header, credenziali, JSON grezzo o percorsi liberi.
L'host registra una raccolta immutabile di `DdfHttpOperationDefinition`; ogni
operazione dichiara:

- nome della funzione DDF e binding ID univoco;
- metodo ammesso tra GET, POST, PUT, PATCH e DELETE;
- percorso relativo senza schema, autorità o traversal;
- parametri scalari DDF collocati esplicitamente in route, query o body JSON;
- tipo e modalità della risposta: scalare JSON, testo oppure status code;
- percorso JSON autorizzato, timeout, limiti request/response e permessi.

I placeholder della route devono coincidere esattamente con i parametri
dichiarati. GET non accetta un body JSON. Le operazioni non dichiarate, i metodi
diversi e i percorsi assoluti vengono rifiutati prima di qualunque I/O.

L'origine viene ricontrollata prima dell'invio e sulla risposta finale. Un
`HttpClient` fornito dall'host deve disabilitare i redirect automatici: in
questo modo ogni redirect viene restituito come normale risposta e non può
inoltrare payload verso un'altra origine. Il controllo sulla risposta resta una
seconda difesa contro handler personalizzati non conformi.

## Credenziali

La callback `DdfHttpRequestAuthenticator` è configurata dall'host e riceve la
richiesta già costruita. Può aggiungere soltanto header, ad esempio
`Authorization`; se cambia metodo, URI o contenuto l'operazione fallisce. Token,
API key, certificati e secret non entrano nei parametri DDF, nel descrittore API,
nel sorgente o nei lockfile.

## Payload e tipi

La prima superficie supporta parametri e risultati scalari `bool`, `int`,
`float` e `string`. Il body viene serializzato con `System.Text.Json`; query e
route sono codificate come componenti URI. Le risposte vengono lette a flusso
con limite anche in assenza di `Content-Length`, profondità JSON massima e
conversione esatta nel tipo DDF dichiarato.

Le letture GET sono catalogate come pure. POST, PUT, PATCH e DELETE sono effetti
host mutanti e non vengono eseguiti automaticamente in modalità Simulation.
Timeout dell'operazione, timeout host, scadenza globale e cancellazione vengono
combinati nello stesso token.

## Analisi ed esecuzione

Lo stesso insieme di operazioni genera:

- `DdfHostFunctionLibraryDefinition` per l'editor embedded;
- `DdfExternalApiDefinition` per resolver, analisi, IntelliSense, runtime e
  debugger.

L'adapter implementa `IDdfExternalApiRuntimeAdapter`; il suo ID deve coincidere
con il binding autorizzato dal resolver.

## Gate

`DDFLanguageEditor.Network.Tests` usa un handler HTTP locale e deterministico,
senza accesso alla rete reale, per verificare:

- costruzione di route, query e body JSON tipizzati;
- credenziali aggiunte dall'host ma assenti dal contratto DDF;
- rifiuto di HTTP non cifrato, URL assoluti, metodi e binding non autorizzati;
- limiti di risposta anche con contenuto eccedente;
- cancellazione cooperativa e diagnostica DDF di timeout;
- proiezione coerente del contratto verso analisi ed esecuzione.

Streaming, upload/download di file, collezioni JSON e policy di retry non sono
impliciti: richiederanno contratti dedicati con quote e semantica verificabile.
