# Adapter filesystem governato DDF

## Stato

La `0.13.4` introduce `DDFLanguageEditor.Storage`, adapter gestito della
famiglia `ddf.dotnet.*` per file di testo UTF-8, JSON e CSV. Dipende soltanto
dal Core e dalle API .NET 10: non rende `System.IO` parte della libreria
standard DDF e non abilita accesso generale al filesystem.

## Contratto deny-by-default

L'host sceglie una radice assoluta privata e dichiara ogni
`DdfLogicalFileDefinition` con:

- ID logico univoco;
- percorso relativo host, mai proiettato nel descrittore DDF;
- formato `Utf8Text`, `Json` o `Csv`;
- accesso read-only oppure read-write;
- quota massima in byte.

Ogni `DdfLogicalFileOperationDefinition` associa una singola funzione DDF a
una risorsa e a `Read` oppure `Write`. Lo script non passa un nome file: una
lettura non ha argomenti e una scrittura riceve soltanto il contenuto. Non
esistono enumerazione, directory corrente, glob, path assoluti, traversal,
rename o delete impliciti. Binding, funzione e percorso host duplicati vengono
rifiutati in fase di configurazione; un'operazione di scrittura non può essere
collegata a una risorsa read-only.

La proprietà `RelativePath` serve esclusivamente alla configurazione host.
`DdfExternalApiDefinition`, IntelliSense, runtime e debugger ricevono nomi,
binding, firme, effetti e permessi, mai la radice o il percorso fisico.

## Formati e quote

Tutto il testo usa UTF-8 rigoroso senza sostituzione silenziosa dei byte non
validi. La quota viene controllata sui byte effettivi, sia in lettura sia prima
della scrittura.

- JSON viene analizzato senza commenti o virgole finali, con profondità
  massima 32, e serializzato in forma canonica compatta;
- CSV supporta campi quotati, virgole e newline nei campi, escape `""` e
  terminazioni LF o CRLF; tutte le righe devono avere lo stesso numero di
  colonne;
- testo UTF-8 resta invariato.

Una seconda quota limita il totale staged da una singola transazione. Timeout,
scadenza globale e cancellazione dell'esecuzione vengono propagati alle API
file asincrone.

## Scritture e transazioni

Senza una transazione host, il contenuto validato viene scritto in un file
temporaneo nella directory di destinazione e reso visibile tramite sostituzione
atomica sullo stesso volume.

Con `DdfHostEffectPolicy.RollbackOnFailure`, l'host passa
`adapter.CreateTransactionFactory()` alla richiesta di esecuzione. Le
scritture restano in staging e le letture della stessa esecuzione osservano il
valore staged. Al successo, il commit serializzato conserva backup, applica le
sostituzioni e ripristina quanto già applicato se una sostituzione successiva
fallisce; in caso di errore DDF, stop o dispose avviene il rollback e lo staging
viene eliminato.

Il commit multi-file fornisce atomicità logica ai consumatori che passano dallo
stesso adapter. Un processo esterno che legge direttamente i percorsi durante
il breve commit potrebbe osservare sostituzioni intermedie: per vera atomicità
fisica multi-file serve un contenitore o database transazionale dedicato.

## Analisi ed esecuzione

Lo stesso catalogo genera:

- `DdfHostFunctionLibraryDefinition` per l'editor embedded;
- `DdfExternalApiDefinition` per resolver, analisi, IntelliSense, runtime e
  debugger.

Le letture sono pure; le scritture sono effetti host mutanti. L'adapter
implementa `IDdfExternalApiRuntimeAdapter` e richiede l'identità esatta del
binding autorizzato.

## Gate

`DDFLanguageEditor.Storage.Tests` verifica quattro aree:

- descrittore API e catalogo senza radice o percorso fisico;
- round-trip e rifiuto di UTF-8, JSON e CSV non validi, oltre alle quote;
- commit e rollback reali con cleanup dello staging;
- diniego di traversal, percorsi assoluti, binding ignoti e scrittura su
  risorse read-only.

Delete, append concorrente, directory, watch del filesystem, file binari e
streaming richiederanno contratti separati: non vengono inferiti da questo
primo adapter.
