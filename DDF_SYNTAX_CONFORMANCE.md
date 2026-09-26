# Conformità sintattica DDF 0.12.1

Stato dell’audit: completato il 2026-09-27 per il nucleo che precede moduli e
collezioni. Direttive, array e `dict` restano intenzionalmente aperti per
`0.12.2` e `0.12.3`.

## Fonti controllate

| Area | Fonte eseguibile | Verifica |
| --- | --- | --- |
| Lessico | `lexicon.json` | token, alias rimossi e longest match |
| Sintassi | `grammar.ebnf` | `ValidationDiagnostics` e corpus valido/non valido |
| AST recuperabile | parser tipizzato | test di compatibilità per IDE, Quick Fix e Watch |
| Formattazione | `formatting.json` | idempotenza e nuova validazione del risultato |
| IntelliSense | `intellisense.json` | ogni snippet marcato `requiresKeyword` usa una keyword reale del profilo |
| Esecuzione | `runtime.json` e IR | stessa semantica e stesso risultato nei tre profili |

## Correzione dell’EBNF

La precedente produzione `expression = prefix-expression, { <Operator>,
prefix-expression }` era deliberatamente provvisoria ma troppo permissiva:
permetteva operatori postfix in posizione binaria e non distingueva precedenza
o associatività. Anche l’inizializzatore accettava genericamente `<Operator>`.

Le grammatiche ora dichiarano livelli distinti per assegnazione/output, logica,
uguaglianza, relazione, somma, prodotto, potenza, prefisso e postfix. La potenza
e l’assegnazione sono ricorsive a destra; gli altri livelli sono associativi a
sinistra. L’inizializzatore ammette soltanto `<<` nei profili DDF e italiano e
`=` nel profilo familiare C.

## Corpus normativo

`samples/language-profiles/` contiene, per ogni profilo:

- `valid-core`: funzioni, chiamate, dichiarazioni, `for`, `if` e precedenza;
- `invalid-unbraced`: corpo di controllo privo del blocco obbligatorio;
- `invalid-operator-position`: operatore aritmetico usato illegalmente come
  inizializzatore.

La suite carica i pacchetti distribuiti, richiede zero diagnostiche per i casi
validi, verifica type checker, formattazione idempotente e risultato runtime,
e richiede `DDFG101` per i casi non validi. Controlla inoltre che la Quick Fix
inserisca la grafia corretta di `out`, `restituisce` o `returns` e che gli alias
beta rimossi non ricompaiano nel catalogo.

## Confini dell’audit

Il file `DDF - Program Language Spec.txt` è una testimonianza storica e non una
fonte normativa. Le sue grafie non vengono accettate implicitamente. Il cambio
di profilo non converte i sorgenti; un futuro comando di conversione dovrà
essere esplicito, mostrare un’anteprima e lasciare invariato l’originale finché
l’utente non conferma.
