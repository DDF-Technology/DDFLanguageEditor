# Operatori DDF 0.12

Stato: contratto normativo del pacchetto DDF `0.12.1`.

Questa specifica di area è consolidata dalla specifica autonoma
`DDF_LANGUAGE_SPEC.md`, che prevale in caso di divergenza accidentale.

Questa specifica definisce grafia, arità, precedenza e associatività degli
operatori del nucleo. Tipi, conversioni ed errori numerici sono stati chiusi
nella `0.12.2` e consolidati in `DDF_LANGUAGE_SPEC.md`.

## Tabella normativa

La precedenza cresce dal livello 1 al livello 11.

| Livello | Forma | Arity | Associatività | Operazione |
| --- | --- | --- | --- | --- |
| 1 | `<<` | binaria | destra | assegnazione |
| 1 | `>>` | binaria | destra | invio alla destinazione di output |
| 2 | `||` | binaria | sinistra | OR booleano con corto circuito |
| 3 | `^^` | binaria | sinistra | XOR booleano |
| 4 | `&&` | binaria | sinistra | AND booleano con corto circuito |
| 5 | `==`, `!=` | binaria | sinistra | uguaglianza e disuguaglianza |
| 6 | `<`, `<=`, `>`, `>=` | binaria | sinistra | confronto ordinato |
| 7 | `+`, `-` | binaria | sinistra | somma e sottrazione |
| 8 | `*`, `/` | binaria | sinistra | prodotto e divisione |
| 9 | `^` | binaria | destra | elevamento a potenza |
| 10 | `!`, `+`, `-` | prefissa | destra | NOT, identità e negazione |
| 11 | `++`, `--` | postfissa | sinistra | incremento e decremento |

Chiamata `()`, indice `[]` e accesso membro `.` appartengono allo stesso livello
postfix più alto e si concatenano da sinistra a destra.

## Regole di lettura

- `<<` è l'unica assegnazione del nucleo e può inizializzare una dichiarazione.
- `>>` non è uno shift: nel profilo DDF canonico invia il valore a una
  destinazione di output autorizzata.
- `^` significa sempre potenza; lo XOR booleano è sempre `^^`.
- soltanto `||` e `&&` applicano corto circuito; `^^` valuta entrambi gli operandi.
- `++` e `--` sono esclusivamente postfissi nel nucleo 0.12.
- non esistono operatori bitwise, di intervallo o di radice nel nucleo 0.12.

Esempio di raggruppamento implicito:

```ddf
bool accepted << enabled || fallback ^^ verified && score >= 10;
```

equivale a:

```ddf
bool accepted << enabled || (fallback ^^ (verified && (score >= 10)));
```

## Fonte eseguibile

Le grafie e le precedenze sono dichiarate in
`language-packages/ddf/lexicon.json`; il collegamento alle operazioni della VM è
in `language-packages/ddf/runtime.json`. Parser, formatter, classificatore, type
checker e runtime consumano queste definizioni: il type checker decide in base
all'ID semantico dell'operazione e non alla sua grafia. Ogni modifica richiede
una nuova decisione, l'aggiornamento del corpus e una nuova versione del
pacchetto.
