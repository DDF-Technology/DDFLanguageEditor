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

| ID | Area | Gravità | Classificazione | Stato | Evidenza |
| --- | --- | --- | --- | --- | --- |
| — | — | — | — | Nessun problema classificato all'apertura | — |

Ogni nuova voce deve indicare un caso riproducibile del corpus o una procedura
manuale. La chiusura richiede il riferimento a test, decisione o documento che
dimostra l'esito.

## Gate del ciclo

| Gate | Stato iniziale | Evidenza richiesta |
| --- | --- | --- |
| Baseline normativa immutata | Attivo | test delle impronte SHA-256 |
| Analisi e diagnostiche | Da verificare | corpus valido, non valido e incompleto |
| IntelliSense e formatter | Da verificare | prove automatiche e smoke UI |
| Debugger e runtime VM | Da verificare | corpus eseguibile, multi-file e debug |
| Build di preparazione corrente | Da verificare | ricevuta riproducibile e nessuna compilazione embedded |
| Uso reale | Da verificare | sessioni registrate e problemi classificati |

Il runner Roslyn non appartiene a questo ciclo: verrà collegato allo stesso corpus
nella `0.13`. La parola “Build” nella `0.12.5` indica ancora validazione e
preparazione IR, non emissione di assembly DDF.

## Uscita dal ciclo

Il candidato può essere dichiarato pronto per la futura stabilizzazione soltanto
quando tutti i gate risultano verificati, non restano divergenze normative note e
ogni problema emerso è stato chiuso o differito esplicitamente con motivazione.
Solo allora `stability` potrà passare da `beta` a `candidate` in un cambiamento
separato e verificato.
