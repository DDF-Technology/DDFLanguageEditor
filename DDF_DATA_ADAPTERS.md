# Adapter dati DDF

## Stato

La `0.13.4` introduce `DDFLanguageEditor.Data`, primo adapter gestito della
famiglia `ddf.dotnet.*`. Il componente dipende soltanto da `System.Data.Common`
e dal Core DDF: il provider concreto (SQLite, SQL Server, MySQL, PostgreSQL o
altro provider ADO.NET) viene scelto e configurato dall'applicazione host.

Il gate iniziale usa `Microsoft.Data.Sqlite` 10.0.12 su .NET 10. La dipendenza
SQLite appartiene esclusivamente al progetto di test; non viene trascinata nel
Core, nell'editor embedded o nella libreria standard DDF.

## Confine di sicurezza

`DdfAdoNetDatabaseAdapter` non accetta SQL dallo script. L'host registra una
raccolta immutabile di `DdfAdoNetOperationDefinition`; ogni operazione associa:

- un nome funzione DDF e un binding/statement ID univoco;
- SQL scritto dall'host;
- parametri nominati che devono coincidere esattamente con i placeholder SQL;
- tipo di ritorno DDF, effetto query/comando, timeout e permessi richiesti;
- risultato scalare per le query oppure numero di righe modificate per i
  comandi.

Un binding non dichiarato, parametri mancanti o aggiuntivi e combinazioni
query/risultato incoerenti vengono rifiutati. Non sono disponibili reflection,
discovery del sistema, stringhe SQL dinamiche o accesso diretto alla
connessione.

La factory `Func<CancellationToken, ValueTask<DbConnection>>` resta
responsabilità dell'host. Stringa di connessione, percorso, account, password,
token e secret non entrano nello script, nel descrittore API o nei lockfile DDF.

## Runtime

Ogni invocazione senza transazione usa una connessione dedicata e la rilascia
anche in caso di errore. Le transazioni sono aperte dall'host con un ID logico,
poi associate all'esecuzione tramite `databaseTransactions`; commit e rollback
rimangono fuori dal controllo dello script. Le operazioni della stessa
transazione sono serializzate.

Timeout DDF, scadenza dell'esecuzione e cancellazione vengono combinati. Poiché
alcuni provider, incluso SQLite, possono eseguire in modo sincrono anche metodi
ADO.NET asincroni, il comando viene isolato e `DbCommand.Cancel()` è collegato
al token. `DisposeAsync` impedisce nuove chiamate, attende quelle già in corso,
esegue rollback delle transazioni residue e rilascia connessioni e transazioni.

La prima superficie accetta risultati scalari `bool`, `int`, `float` e
`string`; record, valori opzionali, streaming e set di righe tipizzati saranno
aggiunti solo insieme ai relativi contratti DDF.

## Analisi ed esecuzione

Lo stesso catalogo di operazioni può produrre:

- un `DdfHostDatabaseDefinition` per l'host catalog dell'editor embedded;
- un `DdfExternalApiDefinition` per analisi, completamento, documentazione,
  runtime e debugger tramite il resolver dei riferimenti esterni.

L'adapter implementa sia `IDdfHostDatabaseService` sia
`IDdfExternalApiRuntimeAdapter`; l'ID dell'adapter deve coincidere esattamente
con quello autorizzato dal resolver.

## Gate SQLite

`DDFLanguageEditor.Data.Tests` verifica su un database SQLite reale:

- inserimento con parametri, incluso un valore simile a SQL injection;
- query scalari e conversione nei tipi DDF;
- commit, rollback e visibilità all'interno della transazione;
- proiezione dello stesso contratto per analisi e runtime;
- rifiuto di SQL/binding non dichiarati;
- timeout, cancellazione e rilascio delle risorse native;
- rollback automatico delle transazioni ancora attive durante il dispose.

ADODB/COM non fa parte di questo adapter: resta una compatibilità Windows
legacy separata prevista dalla roadmap.
