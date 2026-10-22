# Test di progetto DDF — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
futuro piano `ddf-test-plan@1.0`, il test adapter `ddf-project-tests-v1` e il
report `ddf-test-report@1.0`. Non modifica i test dichiarativi del pacchetto
linguaggio `ddf-tests-v1`. Il contratto machine-readable è
`ddf-project-testing-design.json`.

## Confine fra test di linguaggio e test di progetto

`language-packages/<id>/tests.json` continua a essere un gate di installazione
del pacchetto: verifica lessico, grammatica e semantica dichiarata. Il nuovo
`ddf-test-plan.json`, collocato nel progetto utente, registra invece funzioni
DDF da eseguire come casi applicativi. I due formati non si includono, non si
sostituiscono e producono report distinti.

## Modello sorgente

La prima verticale non aggiunge una keyword `test`. Un caso richiama una
funzione DDF ordinaria dichiarata nei source set di test e identificata nel
piano tramite modulo, documento, funzione e firma esatti. Le sorgenti di test:

- possono dipendere dal codice di produzione e dalle sue dipendenze dirette;
- non vengono esportate, collegate o pubblicate negli artefatti di produzione;
- non possono essere dipendenza del codice di produzione;
- usano lo stesso parser, type checker, IR e backend del programma.

Il piano assegna a suite e casi ID stabili, tag, stato abilitato e budget. La
selezione per ID o tag non cambia isolamento e semantica.

## Libreria disponibile soltanto nei test

`ddf.testing@1` viene proiettata solo durante la preparazione di un test e usa
il futuro descrittore standard `DDF-0038`. La prima API comprende:

- `assertTrue`, `assertFalse` e `fail`;
- `assertIntEqual`, `assertLongEqual`, `assertFloatEqual`,
  `assertFloatNear`, `assertDecimalEqual`;
- `assertBoolEqual`, `assertCharEqual` e `assertStringEqual`.

Le uguaglianze seguono la semantica DDF del tipo. `assertFloatNear` richiede una
tolleranza finita e non negativa ed è l'unica asserzione approssimata. Ogni
fallimento registra valore atteso/effettivo tipizzato, messaggio e posizione
DDF senza lanciare un'eccezione CLR. Un caso continua per raccogliere al massimo
128 fallimenti; raggiunto il limite viene interrotto in modo controllato.

I nuovi tipi aggiungeranno asserzioni nominate senza introdurre overload o
generici universali. Le funzioni di test sono vietate in esecuzioni normali,
`fixed` e artefatti pubblicati.

## Casi e risultati attesi

Ogni caso dichiara esattamente uno degli esiti:

- `success`: ritorno tipizzato opzionale, Output e ricevute attese;
- `diagnostic`: fase e codici diagnostici esatti durante preparazione;
- `recoverable-error`: identità `error`, fase runtime e posizione opzionale.

Messaggi localizzati, stack CLR e durata non sono oracoli. Un engine fault,
crash, timeout infrastrutturale o contratto corrotto non può essere trasformato
in test superato. Il confronto di Output è ordinal e distingue righe e ordine.

Un caso senza asserzioni può essere valido quando possiede un esito dichiarato.
Un caso privo sia di asserzioni sia di aspettative è rifiutato per evitare falsi
positivi.

## Fixture dichiarative e isolamento

Ogni caso parte da un ambiente nuovo. Una fixture può dichiarare:

- snapshot iniziale di `state`, mai condiviso o committed nel progetto;
- coda Input e Output atteso;
- sequenze clock e random deterministiche;
- simulatori di capability host/external API con richieste e risposte esatte;
- limiti di istruzioni, tempo, memoria, chiamate ed effetti;
- variabili e oggetti host tipizzati, con permessi minimi.

La fixture è immutabile e fingerprintata. Capability non elencate sono negate;
rete, database, filesystem, UI e hardware reali non sono mai usati per default.
Gli effetti simulati entrano in una transazione sempre annullata al termine del
caso. Setup e teardown DDF arbitrari sono rinviati finché ownership e cleanup
delle risorse non avranno un contratto comune.

Casi e suite non osservano ordine, stato o Output di altri test. Possono essere
eseguiti in parallelo solo quando il runner dimostra isolamento; il report resta
ordinato per ID stabile.

## Debug e backend

Un test può essere eseguito o debuggato come una normale funzione DDF. Editor e
CLI possono scegliere VM, Roslyn o entrambi. Il gate differenziale confronta
risultato, asserzioni, Output, errori, effetti e posizioni; una divergenza rende
il caso `infrastructure-error`, mai `passed`.

Breakpoint, Step Into/Over/Out, Watch e Call Stack attraversano codice di test e
produzione, ma nascondono frame dell'assert adapter. Il Flowchart mostra la
funzione sotto test e può marcare il nodo dell'asserzione fallita.

## Report e strumenti

`ddf-test-report@1.0` contiene identità e fingerprint di progetto, linguaggio,
piano, backend e fixture; per ogni caso riporta stato `passed`, `failed`,
`error`, `skipped` o `infrastructure-error`, fallimenti tipizzati, diagnostiche,
errori, Output, ricevute e posizioni navigabili. Durata e timestamp sono
telemetria non deterministica e non entrano nell'esito o fingerprint.

Lo skip è ammesso soltanto se dichiarato nel piano con motivo non vuoto; un
provider mancante produce errore, non skip automatico. Editor e futura CLI
consumano lo stesso report. I comandi pianificati sono Test All, Test Selected,
Debug Test, Rerun Failed e Stop; l'editor offre Test Explorer e navigazione.

## Diagnostiche del test runner

| Codice | Significato futuro |
| --- | --- |
| `DDFT001` | piano, suite o caso non valido/duplicato |
| `DDFT002` | funzione o firma del caso non risolta |
| `DDFT003` | fixture, valore o capability simulata non valida |
| `DDFT004` | caso privo di asserzioni e aspettative |
| `DDFT005` | aspettativa diagnostica/errore incoerente |
| `DDFT006` | limite delle asserzioni o budget del caso superato |
| `DDFT007` | capability reale richiesta in modalità test isolata |
| `DDFT008` | divergenza fra backend |
| `DDFT009` | sorgente di test inclusa nell'artefatto di produzione |
| `DDFT010` | report o fingerprint incompatibile |

## Ordine di implementazione

1. schema del piano, modello immutabile, validazione e fingerprint;
2. source set separato e risoluzione esatta delle funzioni;
3. `ddf.testing@1`, collector delle asserzioni e provider fixture in memoria;
4. runner VM isolato e report `1.0`;
5. runner Roslyn e gate differenziale;
6. servizi SDK opzionali `0.39`, Test Explorer e futura CLI;
7. debug test, navigazione, smoke e documentazione.

Fino alla verticale completa, `ddf-tests-v1` resta il solo adapter di test
attivo e riguarda esclusivamente la convalida dei pacchetti linguaggio.
