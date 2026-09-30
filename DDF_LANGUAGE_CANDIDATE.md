# DDF 0.12 — ciclo Language Candidate

Stato: **baseline congelata in valutazione**  
Inizio del ciclo: 2026-09-29  
Baseline: `ddf-0.12-candidate-cycle-1`

## Scopo

Questo ciclo verifica con utilizzo reale che specifica, pacchetto canonico,
strumenti IDE e VM descrivano lo stesso DDF `0.12`. Non dichiara ancora il
linguaggio stabile né cambia lo stato pubblico `beta`: congela temporaneamente
il contratto per rendere significativi i risultati delle prove della `0.12.5`.

La rappresentazione verificabile del congelamento è
`ddf-language-candidate-baseline.json`. Le impronte sono SHA-256 calcolate sul
testo UTF-8 con terminatori normalizzati a LF, quindi non dipendono dal checkout
CRLF di Windows.

## Perimetro congelato

Sono congelati:

- specifica autonoma e specifiche normative di area;
- lessico, EBNF, tipi, semantica, runtime e libreria standard del pacchetto DDF;
- identità e versioni del pacchetto canonico;
- policy di versionamento e manifesto del corpus normativo.

Non sono congelati UI, prestazioni, documentazione non normativa, test aggiuntivi
e correzioni che rendono l'implementazione conforme al contratto già scritto.
Queste modifiche non possono però alterare il significato osservabile di un
programma DDF valido.

## Regola di modifica

Una modifica a un artefatto congelato deve essere intenzionale e non può limitarsi
ad aggiornare l'impronta per far passare il test. Prima della modifica occorre:

1. registrare il problema nella sezione seguente;
2. classificare il cambiamento come correzione editoriale, conformità
   dell'implementazione, aggiunta compatibile o incompatibilità;
3. aggiungere una decisione DDF quando cambia il contratto;
4. applicare le regole di versione di `DDF_LANGUAGE_VERSIONING.md`;
5. aggiornare insieme specifica, pacchetto, corpus e guida di migrazione coinvolti;
6. rigenerare consapevolmente la baseline e riavviare i gate interessati.

Durante il ciclo non sono ammesse correzioni semantiche implicite basate soltanto
sul comportamento storico del parser o della VM.

## Registro dei problemi del candidato

Il registro autorevole è `ddf-language-candidate-issues.json`, vincolato da
`ddf-language-candidate-issues.schema.json`. La tabella seguente è una sintesi
leggibile e non sostituisce il registro.

| ID | Area | Gravità | Classificazione | Stato | Impatto normativo |
| --- | --- | --- | --- | --- | --- |
| CAND-0001 | infrastruttura test | nota | test-infrastructure | risolto | nessuno |
| CAND-0002 | infrastruttura test | nota | test-infrastructure | risolto | nessuno |
| CAND-0003 | infrastruttura test | minore | test-infrastructure | risolto | nessuno |
| CAND-0004 | prestazioni | minore | implementation-conformance | aperto | nessuno |

Ogni nuova voce deve indicare un caso riproducibile del corpus o una procedura
manuale. La chiusura richiede il riferimento a test, decisione o documento che
dimostra l'esito. Il registro iniziale contiene due osservazioni emerse mentre si
costruivano i gate: entrambe riguardavano assunzioni errate dei test, sono state
risolte senza modificare il DDF e non costituiscono difetti del prodotto.

Una voce con `changesContract: true` è invalida se non specifica un ID decisione
`DDF-nnnn` e la nuova `targetLanguageVersion`. Il test del registro verifica
questa regola, i valori ammessi, gli ID univoci e la coerenza dei contatori.

La policy `ddf-language-candidate-change-control.json` irrigidisce il caso
incompatibile: una voce `breaking-contract` deve dichiarare l'impatto normativo,
la versione obiettivo deve essere maggiore della `0.12` e la decisione accettata
deve esistere in `DDF_LANGUAGE_DECISIONS.md` e citare sia l'ID `CAND-nnnn` sia la
versione. I test negativi verificano separatamente omissione dell'impatto,
decisione mancante o inesistente, versione invariata e decisione non collegata.

## Gate del ciclo

| Gate | Stato iniziale | Evidenza richiesta |
| --- | --- | --- |
| Baseline normativa immutata | Attivo | test delle impronte SHA-256 |
| Analisi e diagnostiche | Verificato automaticamente | 6 validi, 8 non validi, 4 incompleti e 3 workspace multi-file |
| IntelliSense e formatter | Verificato nel Core | snapshot di completamento sui validi/incompleti; formatter idempotente e semanticamente equivalente sui 6 validi |
| Debugger e runtime VM | Verificato nel Core | pausa/ripresa sulla `ret` raggiungibile e risultato invariato per ognuno dei 6 programmi validi; oracoli runtime e multi-file |
| Build di preparazione corrente | Verificato nel Core | doppia validazione con fingerprint riproducibile dei 6 validi e Build dei 3 workspace; backend `ddf-vm-ir-v1` |
| Uso reale | In corso | smoke WinForms e 76 comandi superati; sessione umana `DDF_CANDIDATE_MANUAL_SESSION.md` ancora da completare |

La regressione `verifies the DDF candidate toolchain against the normative corpus`
costituisce il gate trasversale. Riusa tutti i casi dichiarati dal manifesto e
verifica esplicitamente che la formattazione non cambi output o valore restituito,
che il debugger raggiunga una pausa reale e che due preparazioni Build producano
la stessa impronta. Gli smoke WinForms restano un gate complementare per la UI,
non una nuova definizione della semantica.

Il runner Roslyn non appartiene a questo ciclo: verrà collegato allo stesso corpus
nella `0.13`. La parola “Build” nella `0.12.5` indica ancora validazione e
preparazione IR, non emissione di assembly DDF.

La ricevuta `ddf-language-candidate-evaluation.json` mantiene separati esito
automatico e manuale. Il primo è verde; il secondo resta `pending`, quindi la
readiness è correttamente `not-ready` e lo stato pubblico rimane `beta`.
Inoltre CAND-0004 conserva come osservazione aperta la variabilità del p95 di
input su workspace realistico (`12,5 ms` e `24,5 ms` in due run, target `16 ms`):
non cambia il DDF, ma deve essere ripetuta e chiusa o differita esplicitamente.

## Uscita dal ciclo

Il candidato può essere dichiarato pronto per la futura stabilizzazione soltanto
quando tutti i gate risultano verificati, non restano divergenze normative note e
ogni problema emerso è stato chiuso o differito esplicitamente con motivazione.
Solo allora `stability` potrà passare da `beta` a `candidate` in un cambiamento
separato e verificato.
