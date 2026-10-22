# Pipeline di compilazione DDF .NET

Stato: milestone `0.13.1` in corso; pianificazione, IR bound `2.1`, profilo,
generazione, emit PE/PDB, source map, cache verificata e pubblicazione atomica
implementati. Load ed esecuzione di prodotto restano disattivati.

## Obiettivo

La compilazione .NET non ripete parsing, type checking o risoluzione dei moduli.
Parte esclusivamente da un `DdfBuildResult` riuscito prodotto dalla pipeline
Core condivisa. In questo modo VM e backend compilato ricevono gli stessi
sorgenti, pacchetto, export, dipendenze, diagnostiche e IR strutturale.

`DdfDotNetCompilationPlanner` trasforma quella preparazione in un piano
deterministico senza invocare Roslyn. Il successivo generatore produce alberi
soltanto per il sottoinsieme esplicitamente supportato e non esegue emit.

## Piano `1.0`

Il piano contiene:

- Target Framework, runtime ABI e Runtime Identifier richiesti;
- fingerprint della preparazione Core;
- identità esatta del modulo applicativo;
- una `DdfDotNetCompilationWorkItem` per ogni modulo risolto;
- dipendenze dirette esatte per ogni work item;
- nome logico dell'assembly e percorsi relativi di DLL, PDB, source map e manifest;
- fingerprint complessivo indipendente dall'ordine del filesystem.

Le work item sono ordinate topologicamente: una libreria precede sempre i propri
consumatori e il modulo di ingresso viene dopo tutte le sue dipendenze. Unità
mancanti, duplicate o cicliche bloccano il piano con `DDFC300`–`DDFC306`.

## Gate obbligatori

Il planner rifiuta una preparazione Core fallita. Non offre scorciatoie per
compilare singoli file saltando diagnostica, export, controllo dei tipi o
risoluzione dei moduli. Il piano è solamente una descrizione di lavoro: non è un
artefatto e non implica che il modulo sia già compilabile.

La sequenza effettiva della `0.13.1` è:

1. pianificazione per unità dalla pipeline condivisa — completata;
2. IR bound `2.1` con simboli, tipi, conversioni, l-value, chiamate risolte ed effetti host — completata;
3. runtime ABI `1.9.0` e name map deterministico `1.0` — completati;
4. slice chiuso di alberi C# con controllo semantico in memoria — completato per funzioni, controllo di flusso, postfix e array;
5. risoluzione esplicita del reference pack autorizzato e collegamento al piano — completata;
6. strutture, globali, standard library e chiamate inter-modulo con validazione topologica in memoria — completati;
7. inizializzatore esplicito di modulo per inizializzazioni con chiamate — completato;
8. emit Roslyn deterministico, manifest `1.1` e pubblicazione atomica del bundle
   con Portable PDB e source map DDF — completati, senza load o esecuzione;
9. cache content-addressed `1.0`, con verifica completa degli hit e sostituzione
   atomica delle voci incompatibili o corrotte — completata;
10. confronto differenziale con la VM — completato per il protocollo debugger
   (breakpoint, Step Into/Over/Out, Continua, stack e valori) e per una prima
   slice generale di 11 programmi su risultati, `Output`, terminazione, flusso,
   array, strutture, input, conversioni e sei errori runtime con posizione
   esatta; completato anche sul corpus normativo eseguibile versionato (6 validi,
   2 errori runtime e 2 workspace multifile). Funzioni, oggetti primitivi e
   proprietà host, percorsi annidati ed elementi di array usati sono ora
   capability esplicite del runner
   standalone e vengono confrontati con la VM per risultato, stato finale e
   cardinalità dell'effetto. Funzioni, inizializzatori, factory delle strutture
   e chiamate inter-modulo vengono emessi come state machine C#; le capability
   usano la ABI asincrona e un gate su assembly reale verifica sospensione e
   ripresa senza fallback sincrono. Anche una chiamata DDF usata come argomento
   di un'altra chiamata viene attesa dentro una lambda `async` tipizzata per
   `AtCallAsync`, così nessun `await` può finire in una lambda sincrona generata.
   Timeout, budget e cleanup LIFO sono governati
   dalla ABI; ricevute e transazioni commit/rollback distinguono gli effetti
   reali dalle letture. Restano il confronto worker su Stop/timeout/risposte
   tardive e l'ampliamento futuro del corpus.

Non verranno emessi assembly placeholder o stub che simulino falsamente la
semantica DDF. La prima DLL prodotta dovrà già rispettare la IR bound e i gate di
equivalenza previsti dalla roadmap.

Ogni work item espone la propria `BoundUnit`. Il fingerprint dell'unità bound è
incluso nel fingerprint di compilazione: cambiare binding, conversioni o
mutabilità invalida quindi il piano anche quando il testo sorgente resta uguale.

Ogni work item espone anche il proprio `GeneratedNames`; namespace, contenitore
e identificatori derivano soltanto dagli ID bound. Il planner accetta unicamente
la ABI realmente implementata da `DDFLanguageEditor.Runtime` e segnala
`DDFC307` per richieste incompatibili.

Il fingerprint del piano include anche versione effettiva di Roslyn, versione
C# e opzioni nullable/overflow/unsafe/ottimizzazione/determinismo/piattaforma.
Il reference set esplicito viene risolto soltanto da una directory reference
pack indicata dal chiamante e viene incorporato nel piano e nelle work item. Il
sottoinsieme sintattico e i rifiuti intenzionali sono descritti in
`DDF_DOTNET_COMPILER_PROFILE.md`.

L'allocazione dei campi e dei globali è separata dalla loro inizializzazione.
Ogni struttura generata riceve `DdfRuntimeContext` nel costruttore e valuta lì i
propri initializer. Ogni documento fornisce invece una parte privata del modulo;
il coordinatore pubblico del modulo richiama prima i coordinatori delle
dipendenze dirette, quindi le parti documentali in ordine stabile e imposta il
proprio guard soltanto dopo il completamento. Un errore lascia quindi il modulo
ritentabile e nessuna semantica dipende dall'ordine degli initializer CLR. Il
futuro entry wrapper dovrà chiamare questo coordinatore prima di `main`.

`DdfDotNetArtifactEmitter` riusa lo stesso grafo-compilazione già validato,
emette una DLL gestita per work item in ordine topologico e crea il manifest
soltanto dopo il successo. Due emissioni dello stesso piano sono verificate
byte-per-byte; la ricevuta registra piano, profilo, Roslyn, C# e reference set.
Un errore `DDFC407`/`DDFC408` invalida l'intero risultato e non restituisce un
insieme parziale. `DdfDotNetArtifactPublisher` controlla nuovamente coerenza e
hash, pubblica tramite staging e rinomina atomica e rifiuta destinazioni già
esistenti (`DDFC409`–`DDFC411`). Portable PDB e source map sono parte dello
stesso bundle; i sequence point visibili del PDB sono già rimappati sui file DDF
e il C# generato è nascosto. La source map `1.1` collega inoltre ogni simbolo
utente alla sua identità bound, dichiarazione DDF, tipo e storage CLR privato,
così l'adapter non deve mostrare nomi generati.

`DdfDotNetArtifactCache` usa una chiave SHA-256 versionata derivata dagli schemi
manifest/source map e dal fingerprint completo del piano. Un hit non è mai considerato attendibile per la
sola presenza della directory: manifest, ricevuta, source map, payload, path,
dimensioni e hash devono corrispondere esattamente al piano richiesto. Cambiare
sorgenti, IR bound, pacchetto, opzioni, Roslyn, reference set, TFM/RID o ABI crea
una nuova chiave; una voce con la stessa chiave ma corrotta viene spostata fuori
linea, rigenerata e sostituita tramite pubblicazione atomica. La cache non carica
né esegue assembly e non è esposta all'embedded (`DDFC412`–`DDFC414`).

Nel bridge dell'editor standalone la cache risiede esclusivamente sotto
`obj\ddf\cache\<Configurazione>\<target>`; non è l'output mostrato all'utente.
`DdfDotNetStableArtifactOutput` materializza la stessa emissione verificata in
`bin\<Configurazione>\<target>\artifacts` e sostituisce l'intero bundle tramite
una directory candidata e un backup compensativo. Una pubblicazione incompleta
o un ripristino non riuscito produce `DDFC417`; il percorso `publish` rimane un
esito distinto e non viene rimosso da una nuova build.

Il primo stadio della `0.13.2` è `DdfDotNetArtifactLinker`: consuma soltanto
manifest `1.1`, risolve import ed export tipizzati, chiude il grafo a partire dal
modulo di ingresso e produce un `DdfDotNetLinkedProgram` immutabile. L'ordine è
topologico e il fingerprint non dipende dall'ordine dei file ricevuti. Questo è
ancora link statico di metadati: non carica PE, non usa reflection e non espone
un runner di prodotto.

`DdfDotNetPeVerifier` riceve il programma collegato e i payload in memoria,
apre i PE soltanto con `PEReader` e controlla metadati CLR, flag IL-only,
assenza di entry point, ModuleRef e `P/Invoke`, identità assembly e riferimenti
DDF diretti completi di versione, cultura e chiave pubblica.
`DdfDotNetLinkedBundleBuilder` crea quindi il bundle `1.2` nello
stesso ordine topologico, copia tutti i byte e calcola un fingerprint globale.
I getter dei payload restituiscono copie. Il verifier riceve inoltre il reference
set esplicito, ne confronta il fingerprint con la ricevuta e autorizza ogni
riferimento gestito esterno soltanto se coincidono nome, versione, cultura e
public key token. Tutti i corpi vengono decodificati: opcode/operandi, token,
branch, switch, firme locali e regioni di eccezione devono essere strutturalmente
validi; opcode indiretti o unsafe restano vietati. Un secondo passaggio propaga
l'altezza dello stack lungo il grafo di controllo, comprende gli ingressi dei
gestori di eccezione e le firme delle chiamate, e rifiuta underflow, merge
incompatibili, `leave`/`ret` incoerenti o `MaxStack` insufficiente (`DDFC513`).
Tutte le firme di metodi, campi, proprietà, riferimenti, generici, tipi e locali
vengono decodificate tramite `System.Reflection.Metadata`; puntatori, function
pointer e `TypedReference` sono esclusi dal profilo, mentre ogni indice
argomento/locale usato dall'IL deve rientrare nella firma effettiva (`DDFC517`).
Budget configurabili limitano
metadati, dimensione IL, istruzioni, switch ed exception region sia per assembly
sia per programma. Il bundle incorpora ora i byte canonici del manifest package
`ddf-dotnet-linked-package` `1.1`: il lock registra identità completa, SHA-256,
dimensione e classificazione `packaged`/`platform` di ogni reference
autorizzata, non i percorsi della macchina di build, e lega l'allowlist agli
hash degli artefatti. Le sole DLL esterne risolte vengono copiate nel bundle;
il worker `2.0` ne ricostruisce identità e reference set dai byte e il loader le
risolve in memoria nell'`AssemblyLoadContext` collezionabile.

La pubblicazione EXE usa gli stessi byte packaged e non riapre le DLL dalla
macchina di build. L'inclusione richiede una ricevuta di redistribuzione esatta
per ciascuna identità, con licenza obbligatoria e avviso opzionale; `DDFC605`
nega per default. Il publisher ricontrolla il payload finale e il manifest
`ddf-publish` `1.2` registra identità, file, dimensione, SHA-256, directory
`support` e percorsi degli avvisi relativi alla radice. Un apphost dedicato è
l'unico file ammesso nella radice e carica l'assembly applicativo e il runtime
self-contained da `support`, indipendentemente dalla directory di lavoro.
L'icona DDF incorporata o un `.ico` personalizzato validato viene trasferito
nelle risorse PE dell'apphost; origine, nome e SHA-256 sono registrati nel
manifest e l'hash partecipa al fingerprint della pubblicazione.
Autorizzazione all'uso, risoluzione, packaging runtime e diritto di
redistribuzione rimangono quindi quattro decisioni distinte.

`DdfDotNetDiagnosticMapper` applica la stessa regola deny-by-default alle
diagnostiche Roslyn: percorso, riga e colonna vengono esposti soltanto quando il
mapped span corrisponde esattamente a uno snapshot DDF dell'unità; in caso
contrario la diagnostica resta classificata come errore interno del backend.

`DdfDotNetStandaloneLoader` costituisce il primo loader di prodotto della
`0.13.2`. Riceve soltanto bundle, reference set e ambiente standalone/CLI:
ricostruisce e ricollega i manifest dai byte, ricalcola il package lock, ripete
compatibilità e verifica PE/IL, poi carica DLL e PDB in un
`AssemblyLoadContext` collezionabile. La risoluzione è deny-by-default, i moduli
vengono inizializzati in ordine topologico e `main` è individuato dalla source
map verificata. Ogni sessione è monouso e rimuove i riferimenti forti prima di
`Unload`; può inoltre creare il debugger cooperativo dalle mappe conservate.
L'API vive soltanto nell'assembly Compiler e rifiuta esplicitamente l'embedded.
Package non fidati richiederanno il completamento del worker con una sandbox del
sistema operativo: il loader in-process non è un confine di sicurezza.

La Runtime ABI `1.11.0` aggiunge un confine state backend-neutral. Ogni modulo
emette helper di cattura/applicazione per i soli globali dichiarati `state`, con
la stessa chiave stabile della VM. Una nuova sessione può importare lo snapshot
prima di `main`; dopo il successo può esportarne uno nuovo. Identità linguaggio,
versione, chiavi e tipi sono verificati fail-closed e un fallimento non produce
stato committibile. Il contratto non serializza nomi CLR, reflection o tipi
Roslyn.

Il primo `DDFLanguageEditor.Runner` separa già il caricamento dal processo
dell'editor. `DdfDotNetWorkerClient` invia su pipe una richiesta JSON `1.1`
correlata; il figlio ricostruisce reference set, artefatti, link e bundle e
richiede che il package lock rigenerato coincida byte-per-byte prima di usare il
loader. Output e risultato scalare tornano in una risposta dedicata, mentre
timeout e cancellazione terminano l'intero albero. Su Windows un Job Object
ammette soltanto bootstrap fidato e worker, impone memoria e tempo CPU
configurabili e kill-on-close;
ovunque viene usata una directory di lavoro temporanea dedicata. È contenimento
di lifecycle e risorse. Il runner attesta un token già ristretto oppure crea un
figlio con token ridotto e rifiuta downgrade con `DDFC537`. Non è ancora una
sandbox completa: oltre al broker file snapshot-only mancano scrittura, rete,
capability host ulteriori, input interattivo, debug remoto ed equivalenti
non-Windows. Il contratto è
descritto in `DDF_DOTNET_WORKER_PROTOCOL.md`.

La richiesta include inoltre una policy di accesso versionata e deny-by-default.
Il profilo corrente ammette filesystem assente o `BrokeredRead`, rete assente e
soltanto `brokerReadText` quando dichiarata in modo identico da policy, ambiente
e bundle; modalità non implementate vengono respinte con `DDFC536`. Il processo
riceve un ambiente minimo ricostruito da zero, senza
inoltrare variabili applicative o credenziali del processo editor.
Su Windows può inoltre essere richiesta una DACL dedicata sulla directory di
sessione: la mancata applicazione viene rifiutata fail-closed con `DDFC538`,
mentre il risultato espone sempre se la protezione è effettivamente attiva.

Il primo broker file è intenzionalmente snapshot-only: il client acquisisce un
file UTF-8 preautorizzato, ne blocca nome logico/contenuto/SHA-256 e il worker lo
espone esclusivamente tramite `brokerReadText`. Nessun percorso del file entra
nel protocollo o viene aperto dal codice eseguito; limiti, hash e corrispondenza
esatta fra capability di policy, ambiente e bundle sono verificati con
`DDFC539`. Scrittura e rete restano negate.

Il gate ostile/lifecycle serializza davvero la richiesta per escludere il
percorso originale, verifica `DDF409` su una risorsa logica non concessa ed
esegue cinque worker consecutivi controllando risultato stabile e rimozione
delle directory temporanee. Anche le risposte d'errore conservano
l'attestazione del token ridotto, evitando falsi `DDFC537` lato client.

Il backend espone inoltre `DdfDotNetDebugAdapter`, che non carica codice: dopo
aver verificato hash e dimensioni usa PE, PDB e source map per produrre frame,
sequence point, breakpoint legati e simboli DDF visibili allo specifico offset
IL. Questa separazione mantiene la traduzione dei metadati testabile prima di
aggiungere controllo della pausa e lettura dei valori nel runner.
