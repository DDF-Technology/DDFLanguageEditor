# Runtime ABI DDF per .NET

Stato: contratto standalone `1.6.0`, introdotto come `1.0.0` e ampliato nella
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
- `DdfRuntimeException` conserva codice DDF e coordinate nel sorgente originale;
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

Gli errori prodotti rispettano i codici normativi `DDF403`, `DDF404`, `DDF405`,
`DDF407`, `DDF408`, `DDF409`, `DDF413`, `DDF414`, `DDF415`, `DDF417`, `DDF418`,
`DDF419` e `DDF420`.

## Confini

Lo standalone o la futura CLI possono fornire I/O e capability autorizzate dal
manifesto. L'host embedded continua invece a eseguire sorgente tramite la VM e i
propri adapter, permessi, timeout e transazioni: non carica assembly DDF e non
attraversa questa ABI.

Il backend Roslyn usa `IDdfRuntimeCapabilityProvider` per funzioni sincrone,
oggetti primitivi, percorsi di proprietà annidati ed elementi di array host
dichiarati nel manifest compilato.
Un provider asincrono mantiene sincrona la semantica DDF: la ABI attende la
`ValueTask`, propaga la cancellazione e produce `DDF414` allo scadere del timeout.
Il provider sincrono viene isolato in un task, ma un'operazione che ignora il
token può continuare oltre il timeout: per codice non fidato o nativo resta
obbligatorio un processo worker separato. `DDF413` governa i budget di chiamate e
risorse; `DDF415` segnala un cleanup fallito senza saltare le risorse successive.
L'assenza del provider o di una capability produce `DDF409` sulla posizione della
chiamata o dell'accesso DDF. Il provider non concede automaticamente permessi e
non abilita reflection o discovery: è il runner standalone a costruire una
tabella esplicita coerente con le capability già verificate prima del caricamento.

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
Gli assembly nuovi non sono dichiarati compatibili con runtime
precedenti; il planner registra e richiede la versione esatta.
