# Versionamento, compatibilità e migrazione DDF

Stato: **normativo** per DDF `0.12`, pacchetto canonico `0.12.3`.

La rappresentazione eseguibile di questa policy è
`ddf-language-version-policy.json`; il relativo schema è
`ddf-language-version-policy.schema.json`. Questo documento definisce il
significato dei campi. Non rende disponibile un migratore che non sia ancora
implementato.

## 1. Domini di versione

Le versioni non sono intercambiabili:

| Dominio | Significato | Regola corrente |
| --- | --- | --- |
| prodotto | editor standalone e SDK | non determina la semantica del sorgente |
| `languageVersion` | grammatica e comportamento osservabile DDF | `MAJOR.MINOR`; DDF corrente `0.12` |
| `packageVersion` | revisione riproducibile di un pacchetto | `MAJOR.MINOR.PATCH`; selezione esatta |
| `engineApi` | contratto richiesto al loader/motore | versione non supportata rifiutata prima dell'attivazione |
| `schemaVersion` | forma di ciascun documento JSON | validata indipendentemente per sezione |
| versione modulo | API sorgente esportata da `id@version` | risoluzione esatta, nessun intervallo implicito |
| libreria standard | firme, effetti, binding ed errori | parte del contratto semantico |

Due pacchetti possono condividere `languageVersion` soltanto se producono lo
stesso significato osservabile: tipi, conversioni, ordine di valutazione,
diagnostiche normative, IR, runtime, moduli e libreria standard coincidono. Una
traduzione di keyword o una presentazione didattica può quindi cambiare soltanto
`packageVersion`; non crea un nuovo linguaggio.

## 2. Incrementi

Durante la beta `0.x`, una modifica semantica additiva o incompatibile incrementa
il `MINOR` del linguaggio e deve includere decisione, specifica, corpus e guida di
migrazione. Dopo `1.0`, una modifica incompatibile incrementa `MAJOR`; una
estensione retrocompatibile incrementa `MINOR`.

Correzioni d'implementazione, prestazioni, documentazione e presentazione che non
cambiano alcun risultato normativo incrementano soltanto la patch del pacchetto
o la versione del prodotto. Modificare JSON o capacità del motore richiede anche
l'incremento dello `schemaVersion` o di `engineApi` interessato.

Non si riutilizza mai la stessa `packageVersion` per contenuti semanticamente
diversi in una distribuzione pubblicata. Il progetto e la ricevuta di build
conservano la revisione esatta.

## 3. Compatibilità

- `ddf-project.json` richiede `languageId` e `languagePackageVersion` esatti;
- le dipendenze modulo richiedono `id@version` esatto;
- un profilo è equivalente solo con stessa versione del linguaggio e stesso
  contratto semantico; la somiglianza grafica non basta;
- versioni di schema, adapter o motore sconosciute vengono rifiutate
  atomicamente, senza fallback silenzioso;
- un pacchetto più recente non viene scelto automaticamente al posto di quello
  richiesto;
- VM e futuro backend Roslyn devono superare lo stesso corpus prima di essere
  dichiarati compatibili.

## 4. Deprecazioni

Una deprecazione conserva temporaneamente una forma valida ma scoraggiata. Deve
registrare introduzione, sostituzione, diagnostica prevista, finestra di
supporto e versione di rimozione oppure dichiarare esplicitamente che la
rimozione non è pianificata.

La rimozione ordinaria avviene non prima della successiva versione del linguaggio
che può contenere cambiamenti incompatibili. Eccezioni per sicurezza o forme mai
realmente supportate richiedono una decisione DDF esplicita.

La sola deprecazione corrente è `@@'Nome'`: resta un suggerimento legacy di
libreria/capacità, non include sorgenti e non risolve moduli. La sostituzione è
una dipendenza nel manifesto o una capability host esplicita. La rimozione non è
ancora pianificata e la conversione non può essere automatica senza conoscere
l'intento.

## 5. Migrazioni automatiche

Una migrazione può essere candidata all'automazione soltanto quando è:

1. basata su token o struttura, mai su sostituzione testuale globale;
2. semanticamente equivalente per ogni sorgente accettato;
3. idempotente e verificabile rieseguendo parser, checker e corpus;
4. mostrata in anteprima con elenco dei file e differenze;
5. applicata soltanto dopo conferma, con backup/checkpoint e Undo dove possibile;
6. incapace di modificare commenti, stringhe o identificatori omonimi;
7. priva di salvataggi impliciti durante il semplice cambio profilo.

Sono candidate, ma **non ancora implementate**, le conversioni token-aware
`!! -> !=`, `& -> &&`, `|&| -> ^^` e l'aggiornamento guidato del manifesto
progetto `1.0 -> 1.1`. Il convertitore di profilo già presente è una funzione
distinta: opera sui buffer aperti fra sintassi equivalenti e non dichiara una
migrazione di versione.

Richiedono invece revisione umana gli operatori beta di intervallo e radice,
`dict`, array multidimensionali, dichiarazioni in shadowing e qualsiasi modifica
che imponga una scelta di struttura dati, algoritmo, API o comportamento.

## 6. Processo di rilascio

Un cambiamento del linguaggio è accettabile soltanto se, nella stessa revisione:

1. viene registrata una decisione DDF;
2. vengono aggiornate specifica autonoma e policy macchina;
3. pacchetto, schemi e versione sono coerenti;
4. corpus e test dichiarativi coprono comportamento precedente e nuovo;
5. incompatibilità e migrazioni sono documentate;
6. editor, VM e ogni backend dichiarato superano i gate di conformità.

Una divergenza blocca il rilascio. Il comportamento accidentale del codice non
costituisce compatibilità garantita finché non è presente nella specifica e nel
corpus normativo.
