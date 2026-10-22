# Runtime ABI DDF per .NET

Stato: contratto standalone `1.11.0`, introdotto come `1.0.0` e ampliato nella
milestone `0.13.1` con osservabilità cooperativa, valori del debugger compilato
e capability governate per tipo, indice, tempo, cancellazione e risorse.

`DDFLanguageEditor.Runtime` è la sola dipendenza DDF prevista per gli assembly
generati. Non dipende da Core, WinForms, Editor SDK, Compiler o Roslyn e non viene
referenziata dall'editor embedded. Il suo scopo è impedire che il C# generato
ridefinisca accidentalmente la semantica runtime del linguaggio.

## Superficie

- `DdfRuntimeContext` governa budget, cancellazione, Input, Output, `end` e
  capability esplicite;
- `DdfRuntimeNumeric` applica aritmetica `int` checked, divisione per zero,
  potenza intera, finitezza dei `float`, parsing invariant e controllo indici;
- `DdfRuntimeValues` applica non-null, default stringa, creazione e accesso
  checked agli array monodimensionali e indicizzazione delle stringhe;
- `DdfRuntimeNumeric` conserva anche il valore precedente dei postfix checked
  `++` e `--` per `int` e `float`;
- `DdfRuntimeException` conserva codice DDF, coordinate nel sorgente originale e
  frame bound immutabili catturati prima dell'unwind;
- `DdfRuntimeErrorFrame` trasporta identità funzione, dichiarazione e ultima
  posizione nota senza nomi CLR; `DdfRuntimeContext.CaptureError` è invocato dal
  `catch` generato prima del `finally` che bilancia lo stack;
- `DdfRuntimeSourceLocation` trasporta documento, span e identità simbolica;
- `IDdfRuntimeCapabilityProvider` è deny-by-default e non contiene riferimenti al
  catalogo dell'applicazione host.
- `IDdfRuntimeAsyncCapabilityProvider` riceve un
  `DdfRuntimeCapabilityInvocationContext` per token cooperativo, identità della
  singola esecuzione, istante iniziale, scadenza e registrazione di risorse;
- ogni chiamata ha timeout e budget espliciti; le risorse `IDisposable` e
  `IAsyncDisposable` sono limitate, deduplicate per riferimento e rilasciate in
  ordine LIFO anche quando il programma fallisce;
- `DdfRuntimeCapabilityEffectReceipt` registra in ordine soltanto funzioni
  dichiarate mutanti e scritture host, con stato, capability e disposizione
  finale; è disponibile sul risultato e, anche dopo un errore, sul contesto;
- la policy `RollbackOnFailure` richiede una factory esplicita: la transazione
  viene aperta prima dello script, il suo ID raggiunge ogni invocation context,
  quindi viene eseguito commit o rollback e infine `DisposeAsync` entro timeout;
- `InvokeCapabilityValue<T>` verifica il tipo CLR previsto dal mapping DDF e
  converte una risposta incompatibile del provider in `DDF409` sulla chiamata;
- `ExecuteAsync`, `InvokeCapabilityAsync` e le varianti tipizzate consentono al
  backend compilato di sospendere realmente capability, transazioni e cleanup;
  le API sincrone restano disponibili per compatibilità con gli artefatti già
  emessi;
- `WriteCapabilityValue<T>` conserva il valore dell'assegnazione senza imporre
  al provider di restituirlo; `ReadCapabilityElement<T>` e
  `WriteCapabilityElement<T>` confinano gli array host e traducono gli indici
  fuori intervallo in `DDF405` sulla posizione DDF;
- `IDdfRuntimeDebugObserver` riceve snapshot immutabili a ogni sequence point;
  `EnterFunction`/`ExitFunction` mantengono uno stack di soli ID bound e
  coordinate DDF, sempre bilanciato tramite `try/finally` generato;
- `EnterDebugScope`/`ExitDebugScope` delimitano la visibilità lessicale; parametri,
  locali e globali sono registrati tramite lettori tipizzati generati;
- `DdfRuntimeDebugSnapshot` copia posizione, conteggio istruzioni, frame e valori
  DDF. Array e strutture sono espansi senza reflection tramite
  `IDdfRuntimeDebugStructure`; ogni ramo è limitato a 256 figli, profondità 8 e
  1024 valori complessivi. Cicli e troncamenti sono marcati esplicitamente. Non
  sono esposti nomi CLR, riferimenti mutabili o oggetti interni del runtime.
- `AtCall` aggiorna temporaneamente il frame chiamante con lo span DDF preciso
  della chiamata e lo ripristina in `finally`, anche per chiamate annidate.
- `DdfPersistedStateEntry` e `DdfPersistedStateSnapshot` trasportano esclusivamente
  scalari DDF portabili, chiavi stabili e identità esatta del pacchetto
  linguaggio. Non espongono simboli, tipi o serializer Roslyn.

Gli errori prodotti rispettano i codici normativi `DDF403`, `DDF404`, `DDF405`,
`DDF407`, `DDF408`, `DDF409`, `DDF413`, `DDF414`, `DDF415`, `DDF417`, `DDF418`,
`DDF419` e `DDF420`.

## Classificazione degli errori

Il Core definisce identità e classificazione backend-neutral degli errori. In
questa ABI `DdfRuntimeException` rimane esclusivamente il trasporto interno del
backend compilato: non è un valore CLR esposto allo script e non costituisce
ancora un meccanismo di recupero DDF. Compilatore e host devono mappare codice,
tipo, posizione e stack nel contratto DDF senza inserire dipendenze da Core in
`DDFLanguageEditor.Runtime`. La sintassi di recupero DDF resta futura, ma il
trasporto backend è ora completo: il worker converte codice, posizione e frame
nel contratto Core senza far uscire `DdfRuntimeException` dal processo. Questa
estensione ha portato la ABI a `1.8.0`; la successiva fondazione cooperativa
porta il contratto a `1.9.0` senza cambiare la classificazione.
La ABI `1.10.0` aggiunge ai valori di debug i metadati `IsReadOnly` e
`IsFixed`, conservando le registrazioni a tre argomenti per gli assembly
generati in precedenza.

Il catalogo Core classifica come recuperabili soltanto `DDF404`, `DDF405`,
`DDF408`, `DDF417`, `DDF418` e `DDF419`. Errori di contratto interno, capability,
permessi, budget, timeout, cleanup e transazione sono fault non intercettabili.
Le identità `DDF424` (cancellazione), `DDF425` (sicurezza) e `DDF426` (artefatto
invalido) sono riservate per la futura normalizzazione degli esiti fra VM,
worker e backend compilato; non modificano ancora i codici nativi `DDFC...`
prodotti prima dell'avvio né il canale `WasCancelled` corrente.

## Confini

Lo standalone o la futura CLI possono fornire I/O e capability autorizzate dal
manifesto. L'host embedded continua invece a eseguire sorgente tramite la VM e i
propri adapter, permessi, timeout e transazioni: non carica assembly DDF e non
attraversa questa ABI.

Il backend Roslyn usa `IDdfRuntimeCapabilityProvider` per funzioni sincrone,
oggetti primitivi, percorsi di proprietà annidati ed elementi di array host
dichiarati nel manifest compilato.
Un provider asincrono mantiene invariata la semantica DDF. Nel percorso
`ExecuteAsync` la ABI attende la `ValueTask` senza bloccare il chiamante,
propaga la cancellazione e produce `DDF414` allo scadere del timeout. Il percorso
sincrono compatibile continua invece a bloccare il thread che lo invoca.
Il provider sincrono viene isolato in un task, ma un'operazione che ignora il
token può continuare oltre il timeout: per codice non fidato o nativo resta
obbligatorio un processo worker separato. `DDF413` governa i budget di chiamate e
risorse; `DDF415` segnala un cleanup fallito senza saltare le risorse successive.
L'assenza del provider o di una capability produce `DDF409` sulla posizione della
chiamata o dell'accesso DDF. Il provider non concede automaticamente permessi e
non abilita reflection o discovery: è il runner standalone a costruire una
tabella esplicita coerente con le capability già verificate prima del caricamento.

Il registro corrente conserva ownership fino alla fine dell'esecuzione. La
decisione futura `DDF-0041` aggiungerà scope di lease per `use`, trasferendo
atomicamente una risorsa registrata allo scope lessicale e rimuovendola dal
fallback quando viene rilasciata. Questa estensione dovrà impedire il doppio
dispose e preservare ordine ed errori correnti; non è attiva nella ABI `1.10.0`.

Gli assembly emessi usano ora il percorso asincrono per funzioni DDF,
inizializzatori e capability host: le state machine C# attendono `ValueTask`
senza occupare il chiamante e il percorso sincrono resta soltanto wrapper di
compatibilità. Un gate su assembly reale prova sospensione e ripresa senza
richiamare `IDdfRuntimeCapabilityProvider.Invoke`. Non è ancora la verticale
cooperativa DDF completa: `DDF-0042` richiede anche il protocollo correlato
`ddf.async-capability-provider.v1`: il motore salverà una continuazione DDF,
rilascerà il thread e accetterà un solo risultato valido per generazione.
Risposte tardive, duplicate o successive a Stop/dispose non potranno produrre
effetti. La prima versione consentirà una sola sospensione per istanza e non
esporrà task, delegate, token o eccezioni CLR. Questa estensione è destinata a
DDF `0.24`; non è attiva integralmente nella ABI `1.10.0`.

La ABI `1.10.0` osserva inoltre in modo fail-safe l'eventuale fault di una
operazione provider che termina dopo timeout o Stop. Il risultato tardivo non
viene convertito, non completa effetti e non riapre l'esecuzione; una risorsa
registrata dopo la chiusura viene rifiutata e rilasciata immediatamente.

La ABI non cerca automaticamente assembly, GAC, COM, OCX o DLL native. Queste
integrazioni richiederanno in futuro riferimenti manifesti, policy di fiducia e
loader isolati; non sono conseguenza implicita della compilazione Roslyn.

## Versionamento

Il compilatore accetta esclusivamente la versione esatta che implementa. La
versione ABI entra nel piano e nel manifest dell'artefatto; un loader deve
rifiutare versioni incompatibili prima di caricare la DLL.

Gli incrementi sono intenzionali: `1.1.0` ha aggiunto ingresso/uscita dalle
funzioni, `1.2.0` scope e registrazione dei valori, `1.3.0` risultati capability
tipizzati, `1.4.0` scritture indipendenti dal valore restituito e accessi checked
agli elementi host, `1.5.0` provider asincroni, timeout, budget e cleanup LIFO e
`1.6.0` ricevute degli effetti e transazioni commit/rollback.

`1.7.0` identità debugger distinta per lo `state` transazionale in sola lettura;
`1.8.0` posizione e stack tipizzato degli errori catturati prima dell'unwind;
`1.9.0` esecuzione, capability, transazioni e cleanup awaitable con compatibilità
sincrona conservata.

`1.10.0` metadati immutabili `IsReadOnly`/`IsFixed` negli snapshot debug e
overload additivi di registrazione, senza rimuovere le firme precedenti.

`1.11.0` aggiunge lo snapshot state immutabile e backend-neutral. Il loader
standalone applica uno snapshot validato dopo gli inizializzatori e prima di
`main`; rende catturabile il nuovo snapshot soltanto dopo un'esecuzione
completata con successo, lasciando al chiamante il commit atomico.

Gli assembly nuovi non sono dichiarati compatibili con runtime
precedenti; il planner registra e richiede la versione esatta.
