# Gestione deterministica delle risorse DDF 0.23 — disegno accettato

Stato: **disegno accettato; fondazione SDK implementata, sintassi inattiva**.
Questo documento definisce il contratto della futura verticale DDF `0.23`; il
pacchetto canonico DDF `0.13.0` non riconosce ancora la forma descritta. Il
contratto machine-readable è `ddf-resource-lifetime-design.json`.

La fondazione corrente pubblica descrittori, richieste, decisioni, snapshot e
lease opachi tramite `DdfHostResourceDescriptor`,
`IDdfHostResourceLeaseAuthority` e `DdfHostResourceLease`. Un'operazione host
può usare `DdfHostInvocationContext.RegisterResourceLeaseAsync(...)`: l'autorità
nega o concede prima della registrazione, osserva soltanto metadati sanificati e
può revocare il lease; il registro execution-wide garantisce il fallback LIFO e
il cleanup singolo. L'oggetto nativo resta privato nel lease. Questa fondazione
non introduce ancora handle nel codice DDF, scope lessicali, AST, IR o lowering
Roslyn.

## Obiettivi e confine

- rendere espliciti ownership e durata di database, file, stream, sessioni di
  rete e risorse fornite dall'host;
- garantire un solo tentativo di cleanup, in ordine LIFO, per ogni risorsa
  acquisita con successo;
- mantenere identica la semantica in IR, VM, backend Roslyn ed embedded;
- non esporre `IDisposable`, `IAsyncDisposable`, handle nativi o oggetti CLR;
- integrare errori tipizzati, cancellazione e future sospensioni cooperative.

Il registro LIFO di capability già presente nella Runtime ABI resta il fallback
di sicurezza a durata di esecuzione. Il nuovo costrutto aggiungerà scope
lessicali e rilascio anticipato; una risorsa passata a uno scope `use` viene
rimossa atomicamente dal registro globale e diventa proprietà esclusiva dello
scope. Non può essere rilasciata due volte.

## Sintassi scelta

```ddf
main() out int raises database.openFailed, database.queryFailed
{
    use databaseConnection connection << databaseOpen("customers")
    {
        use databaseReader rows << databaseQuery(connection, "active")
        {
            ret databaseCount(rows);
        }
    }
}
```

La forma è:

```text
use resource-type identifier << acquisition-expression block
```

`use` è preferito a un `defer` generico. Il compilatore può verificare che
l'espressione acquisisca esattamente una risorsa, che il binding non sfugga allo
scope e che il cleanup sia quello dichiarato dal descrittore. `defer` avrebbe
permesso azioni arbitrarie, catture nascoste e differenze fra backend; non fa
parte della prima versione.

L'acquisizione viene valutata una volta prima di entrare nel blocco. Se fallisce,
lo scope non nasce e nessun cleanup viene eseguito. Il binding è immutabile e
visibile soltanto nel blocco. Più risorse richiedono `use` annidati: questa
scelta rende sempre evidente l'ordine inverso di rilascio.

## Tipi, ownership e API

Un tipo risorsa è un tipo nominale opaco dichiarato da un descrittore standard,
di modulo o host. La descrizione contiene identità stabile, capability,
operazione di acquisizione, operazioni ammesse, cleanup, errori e limiti. Il
valore DDF contiene soltanto un token non falsificabile legato a esecuzione,
provider e scope.

Nella prima verticale un handle di risorsa:

- non è copiabile, confrontabile, serializzabile o persistibile;
- non può essere `fixed`, `state`, globale, parametro, risultato, campo, elemento
  di array o collezione;
- non può essere assegnato, restituito, catturato o conservato dall'host oltre
  lo scope;
- può essere passato soltanto come argomento preso in prestito a operazioni che
  dichiarano esattamente quel tipo e non ne trattengono il riferimento;
- dopo l'uscita dallo scope non è più valido, anche se il provider conserva per
  errore l'oggetto nativo.

I descrittori usano il futuro schema di binding `2.0` di DDF-0038. Nessun nome
di tipo o metodo CLR entra nella firma DDF, nella IR o nel manifesto pubblico.
L'host concede separatamente capability di acquisizione e operazioni; possedere
un handle non amplia i permessi.

## Uscite, cleanup ed errori

Il cleanup viene tentato esattamente una volta su uscita normale, `ret`, `brk`,
`continue`, `end`, errore recuperabile, fault del motore, timeout, Stop o dispose
cooperativo. Gli scope annidati vengono chiusi dal più interno al più esterno.
Un arresto forzato del processo, un guasto del sistema operativo o la perdita di
alimentazione non possono offrire questa garanzia.

Tutti i cleanup ancora dovuti vengono tentati anche se uno fallisce. Se non
esiste un esito in propagazione, il primo errore di cleanup diventa primario; gli
altri sono soppressi in ordine stabile. Se esiste già un errore, fault o
cancellazione, quello rimane primario e tutti gli errori di cleanup diventano
soppressi. Il testo del provider e gli stack CLR non diventano osservabili.

La cancellazione ordinaria non interrompe i cleanup: essi ricevono un contesto
separato, limitato da quota e timeout dell'host. Un cleanup che supera il limite
produce un fault stabile e non impedisce i tentativi successivi. Codice nativo o
non cooperativo deve restare in un worker terminabile; nessun thread .NET viene
abortito.

`always` di DDF-0029 e `use` seguono l'annidamento lessicale. Uscendo da un
blocco protetto si chiudono prima gli scope `use` interni, poi si esegue il suo
`always`; le risorse possedute da scope esterni restano vive. Commit di `state`
e transazioni capability avvengono soltanto dopo la chiusura riuscita di tutti
gli scope dell'invocazione.

## IR, debugger e Flowchart

La IR comune rappresenta acquisizione, ingresso nello scope, prestito e rilascio
con operazioni backend-neutral e regioni di cleanup esplicite. La VM usa uno
stack di lease per scope. Il backend Roslyn può abbassare la regione a
`try/finally`, ma non può affidarsi alla semantica accidentale di `using` né
esporla nelle source map.

Debugger e Watch mostrano tipo, stato `active`/`released` e una descrizione
sanificata fornita dal descrittore; non mostrano token, connection string,
percorsi segreti o oggetti nativi. Step Into/Over/Out conserva i punti DDF e il
cleanup appare come evento sintetico non arrestabile, salvo una futura modalità
diagnostica. Il Flowchart delimita lo scope e il relativo rilascio senza creare
rami CLR artificiali.

## Diagnostiche e implementazione

Sono riservate `DDF360`–`DDF369` per uso di `use` con tipo non risorsa,
acquisizione incompatibile, fuga o copia dell'handle, prestito non consentito,
uso fuori scope, descrittore incoerente, limite di risorse e cleanup non valido.
I fault runtime di provider, timeout e cleanup continuano a convergere nel
catalogo unificato degli errori anziché duplicare codici backend.

Ordine di implementazione:

1. schema dei descrittori risorsa e catalogo tipizzato, senza attivare `use`;
2. lessico, grammatica, AST, ownership checker e diagnostiche;
3. regioni cleanup nella IR e stack di lease nella VM;
4. ABI SDK opzionale `0.40`, provider in memoria e migrazione del registro
   execution-wide corrente; la fondazione additiva è ora disponibile nel
   contratto SDK corrente `0.41`, mentre la migrazione agli scope lessicali resta
   da completare;
5. lowering Roslyn e confronto differenziale di esito e ordine di cleanup;
6. formatter, IntelliSense, debugger, Flowchart, Playground e profili;
7. corpus combinatorio e smoke standalone/embedded, inclusi errori multipli,
   timeout, cancellazione e provider non cooperativi isolati.

L'attivazione richiederà DDF `0.23` e tutte le parti della verticale. Fino ad
allora `use` resta un identificatore ordinario nella `0.13`; nessun pacchetto
sintattico può attribuirgli la nuova semantica in anticipo.
