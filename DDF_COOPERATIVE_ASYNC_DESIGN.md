# Asincronia cooperativa DDF 0.24 — disegno accettato

Stato: **accettato, infrastruttura in implementazione**. Questo documento definisce
il contratto della futura verticale DDF `0.24`; il pacchetto canonico DDF `0.13.0`
non abilita ancora la semantica sospensiva come feature linguistica. Il contratto machine-readable è
`ddf-cooperative-async-design.json`.

Audit del 2026-10-16: l'infrastruttura generale di esecuzione è completa e
operativa in VM, SDK embedded, backend Roslyn e worker. Sono presenti scheduler,
continuazioni correlate e versionate, deadline, timeout, budget, cancellazione,
cleanup, stato pubblico, debugger e dispatcher UI, con una sola esecuzione per
istanza. Rimane volutamente aperta l'attivazione linguistica DDF `0.24`: il
descrittore dichiarativo e fingerprintato `ddf-cooperative-suspension@1.0` non è
ancora associato alle firme e agli artefatti, quindi le API asincrone correnti
restano un meccanismo di adapter e non una promessa semantica del pacchetto
linguaggio `0.13.0`.

## Scelta linguistica

La prima versione non introduce `async`, `await`, task, future o thread nel
linguaggio. Una chiamata DDF resta una normale espressione; è il descrittore
semantico della funzione o capability a dichiarare l'effetto
`cooperative-suspension`. VM, backend Roslyn e host possono quindi sospendere e
riprendere la medesima continuazione senza creare due modelli di chiamata e
senza far trapelare tipi CLR nella IR.

```ddf
main() out int raises database.openFailed, database.queryFailed
{
    use databaseConnection connection << databaseOpen("customers")
    {
        int total << databaseCount(connection);
        ret total;
    }
}
```

`databaseOpen` e `databaseCount` potranno sospendere, ma il sorgente non cambia.
`async` e `await` restano identificatori ordinari in DDF `0.24`. Un eventuale
modello di concorrenza esplicita richiederà una decisione successiva e distinta.

## Macchina a stati e continuazione

Ogni istanza di script ammette una sola esecuzione attiva e, durante essa, al
massimo una sospensione pendente. Gli stati osservabili sono `idle`, `running`,
`awaiting-capability`, `cancelling` e `cleaning-up`. L'attesa non occupa un
thread e non trasferisce istruzioni DDF al dispatcher UI.

La richiesta sospensiva è uno snapshot immutabile contenente almeno:

- identità e generazione dell'esecuzione;
- correlation id, capability e operazione risolte;
- argomenti DDF già valutati, identità sorgente e transazione;
- deadline effettiva e token di cancellazione;
- limiti di payload e descrittore semantico fingerprintato.

La continuazione appartiene alla generazione che l'ha creata. Risposte tardive,
duplicate, provenienti da un provider diverso o ricevute dopo Stop/dispose sono
ignorate o rifiutate atomicamente e non possono modificare `state`, output,
risorse o UI. Un provider non può rientrare sincronicamente nella stessa
istanza; eventi host concorrenti ricevono un esito `busy` nella prima versione.

## Timeout, cancellazione e budget

La deadline effettiva è la più vicina fra limite dell'esecuzione, limite
dell'operazione e policy host. Il budget di istruzioni si arresta mentre non
vengono eseguite istruzioni DDF; deadline, durata sospesa, numero di sospensioni,
payload e quote capability continuano invece a essere conteggiati.

Stop, dispose, revoca e scadenza richiedono cancellazione al provider e passano
allo stato `cancelling`. La cancellazione del motore è un esito terminale e non
è intercettabile come errore applicativo. Soltanto un timeout dichiarato
esplicitamente recuperabile dal descrittore può diventare un errore DDF
tipizzato. Nessun thread .NET viene abortito: provider nativi o non cooperativi
devono vivere in un worker terminabile fuori processo.

Il cleanup DDF-0041 usa un contesto indipendente, limitato e non già cancellato.
Tutte le risorse vengono tentate una volta in LIFO; le risposte tardive della
capability sospesa non possono riattivare la continuazione. Commit di `state` e
transazioni avvengono soltanto dopo ripresa e cleanup riusciti.

## Stato, thread e host embedded

L'istanza rimane occupata per l'intera sequenza, compresi attesa e cleanup.
Nessuna seconda procedura può accedere implicitamente allo stesso `state`.
Istanze distinte possono essere eseguite in parallelo se usano store e provider
isolati o coordinati esplicitamente dall'host.

Lo scheduler di esecuzione conserva la continuazione. Un dispatcher UI fornito
dall'host esegue soltanto la parte di presentazione delle capability grafiche e
restituisce un valore tipizzato; non esegue codice DDF. Callback pubbliche,
output e completamento vengono consegnati sul dispatcher configurato e sono
vietati dopo dispose. L'embedded non ottiene capability aggiuntive e resta
deny-by-default.

## IR, backend e protocollo

La IR comune aggiunge operazioni backend-neutral `suspend.request`,
`suspend.resume`, `suspend.cancel` e `suspend.discard`. Il punto di ripresa
contiene soltanto valori DDF serializzabili o lease opachi governati; non
contiene `Task`, `ValueTask`, delegate, eccezioni o stack CLR.

La VM embedded conserva la continuazione nel proprio scheduler. Il backend
Roslyn e il runner standalone usano lo stesso protocollo correlato
richiesta/risposta e non bloccano il thread UI o il canale IPC. Lo stesso
programma deve produrre valore, output, errori, effetti, ordine di cleanup e
punti di debug equivalenti. Le durate reali non entrano nei fingerprint o negli
oracoli deterministici.

Il contratto descrittore è `ddf-cooperative-suspension@1.0`; il provider runtime
è `ddf.async-capability-provider.v1`. L'attuale `ValueTask` dell'ABI è un dettaglio
di adapter e non diventa un tipo DDF.

## Debugger, strumenti e Flowchart

Durante l'attesa il debugger conserva la riga della chiamata e mostra lo stato
sintetico `awaiting-capability`, identità sanificata della capability e deadline.
Non mostra frame provider, task CLR, token o temporanei generati. Watch, Variabili
e Call Stack rimangono snapshot in sola lettura. Dopo la risposta, Step Into,
Over e Out riprendono al successivo sequence point DDF coerente.

Il Flowchart evidenzia la chiamata sospesa senza inventare rami asincroni. Stop,
timeout e cleanup restano eventi sintetici con collegamenti alla sorgente. La
barra di stato e gli host embedded possono osservare il cambio di stato tramite
snapshot immutabili, non tramite callback rientranti.

## Diagnostiche e ordine di implementazione

Sono riservate `DDF370`–`DDF379` per descrittore sospensivo incoerente, provider
assente, limite superato, reentrancy, risposta invalida o duplicata, timeout,
cancellazione non cooperativa e continuazione non valida. I fault continuano a
usare il catalogo unificato e non espongono testo o stack del provider.

Ordine di implementazione:

1. schema descrittore, snapshot immutabili e state machine, ancora inattivi;
2. scheduler VM con una continuazione, cancellazione e provider in memoria;
3. SDK opzionale `0.41`, dispatcher, eventi e dispose asincrono osservabile;
4. protocollo correlato Roslyn/worker e confronto differenziale;
5. transazioni `state`, cleanup DDF-0041 e risposte tardive/duplicate;
6. debugger, Watch, Call Stack, Flowchart, status e strumenti;
7. test di reattività standalone/embedded con heartbeat UI, più istanze isolate,
   timeout, Stop, fault, cleanup e provider non cooperativo nel worker.

La prima fondazione interna è presente in Core: modelli profondamente
immutabili, generazione e correlation id, calcolo della deadline più vicina e
una state machine thread-safe impediscono doppia ripresa e risultati tardivi.
I modelli di coordinamento completi restano interni, mentre l'SDK pubblico `0.41`
ne espone una proiezione backend-neutral tramite la capability opzionale
`IDdfEditorCooperativeExecutionService`. Snapshot ed eventi riportano identità,
generazione, stato e richiesta sospesa immutabile, ma non token, task, thread,
continuation, eccezioni o dettagli Roslyn. Le implementazioni legacy di
`IDdfEditorExecutionService` restano valide. La VM
Core dispone ora di una catena `ExecuteAsync` completa: una capability host
incompleta sospende funzioni, statement ed espressioni tramite la continuazione
.NET generata, rilascia il worker e riprende soltanto dopo la validazione del
risultato correlato. Il vecchio `Execute` resta un wrapper sincrono compatibile.
Il backend compilato dispone ora della fondazione Runtime ABI `1.9.0`:
`ExecuteAsync`, invocazioni capability tipizzate asincrone, transazioni e cleanup
possono sospendersi senza occupare il chiamante. La sessione standalone sa
attendere metodi generati che restituiscono `Task`; il Runner percorre questa
API e il canale duplex serializza una richiesta pendente usando I/O asincrono e
cancellabile. Il vecchio percorso sincrono resta disponibile per gli artefatti
già emessi.

Il generatore Roslyn abbassa ora funzioni DDF, inizializzatori di documento e
modulo, factory delle strutture e chiamate intra/inter-modulo a metodi C#
`async Task`/`Task<T>`. Accessi e funzioni host usano esclusivamente le API
asincrone della ABI; il launcher EXE attende l'intera catena e il debug adapter
riconduce `MoveNext` alla funzione DDF originaria conservando il token IL reale.
Un gate su assembly emesso mantiene intenzionalmente incompleta una capability,
verifica che il chiamante venga rilasciato senza fallback sincrono e controlla
la ripresa con risultato deterministico.

La matrice VM/assembly/worker copre ora anche timeout, Stop, risposta tardiva e
cleanup durante la sospensione. L'assembly traduce la scadenza in `DDF414`,
propaga Stop come cancellazione e rilascia una risorsa registrata esattamente una
volta prima di ignorare la completion tardiva. Il client worker applica un drain
limitato dopo il kill: un callback host non cooperativo non può trattenere il
chiamante e la sua eventuale completion viene soltanto osservata, mai inviata a
un transcript già chiuso.

L'input console standard rimane sul binding sincrono dedicato finché non verrà
unificato con il dispatcher sospensivo. Il debugger VM embedded consuma ora lo
snapshot pubblico: durante l'attesa
mantiene posizione, Watch e Call Stack DDF, segnala `AwaitingCapability` e
aggiorna evidenziazione, Flowchart e stato grafico senza abilitare i comandi di
step. Il debugger Roslyn/worker usa lo stesso stato: il frame duplex `1.1`
allega alla richiesta capability l'ultimo snapshot DDF, il client produce
notifiche correlate di attesa/ripresa e il servizio standalone le proietta nel
contratto SDK `0.41`, senza inventare una pausa o un frame provider.

Il dispatcher host è ora operativo: `IDdfEditorEventDispatcher` separa il worker
dal contesto che riceve stato, output, pausa e completamento; la superficie
WinForms installa il proprio adapter e il debugger Roslyn usa lo stesso confine.
Il default inline conserva gli host headless precedenti. Uno smoke embedded
blocca intenzionalmente il worker su input, verifica che l'heartbeat UI continui
a scorrere e controlla che tutti i callback osservabili tornino sul thread UI.
Anche lo scheduler è ora sostituibile tramite
`IDdfEditorExecutionScheduler`: il default conserva il worker `LongRunning`,
mentre il motore rifiuta un'implementazione che tenti di eseguire DDF inline sul
thread chiamante/UI e normalizza i fault senza lasciare la sessione occupata.
Il confine UI è ora selettivo: una funzione host resta sul worker per default e
può usare `DdfHostFunctionDispatchTarget.Ui` soltanto con una singola capability
`ui.*`. `IDdfEditorUiCapabilityInvoker` accoda la sola presentazione sul
dispatcher, rifiuta richieste UI concorrenti e lascia la continuazione VM in
`AwaitingCapability`; namespace non UI e presentazioni cancellate prima
dell'avvio vengono respinti senza invocare il provider.

Il lifecycle asincrono è ora osservabile tramite
`IDdfEditorAsyncLifecycleService`, implementato sia da Run sia da Debug senza
modificare le interfacce legacy. `StopAsync` e `DisposeAsync` cancellano la
sessione e attendono completion e cleanup entro la scadenza fornita; dispose
sopprime immediatamente qualunque callback già accodato. L'esito tipizzato
mantiene generazione e completion e segnala un provider non cooperativo con
`TimedOut`/`DDFH005`. Non viene tentata la terminazione di thread CLR: il confine
di sicurezza per codice non cooperativo resta il worker fuori processo.

La matrice WinForms embedded è ora automatizzata anche separatamente tramite
`--embedded-reactivity-only`: controlla heartbeat e thread UI con carico
CPU-bound, capability UI sospesa assimilabile a un popup, provider sincrono
lento e breakpoint; esercita inoltre Stop asincrono, dispose senza callback
tardivi e due istanze contemporanee arrestate senza deadlock o stato condiviso.

L'attivazione richiede l'intera verticale DDF `0.24`. Fino ad allora le API
asincrone esistenti possono essere attese dagli adapter, ma non costituiscono
ancora la sospensione cooperativa qui definita.
