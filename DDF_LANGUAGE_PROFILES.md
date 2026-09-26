# Profili sintattici DDF 0.12

I profili sintattici cambiano il modo in cui si scrive un programma, non ciò
che il programma può fare. Tutti i profili dichiarati in questo documento
producono la stessa IR, usano lo stesso runtime, vedono lo stesso catalogo API
host e devono restituire risultati osservabili equivalenti.

## Profili di riferimento

| Pacchetto | Scopo | Esempi distintivi |
| --- | --- | --- |
| `ddf` | Sintassi canonica e identità del linguaggio | `out`, `ret`, `<<`, `true` |
| `ddf-it-didactic` | Lettura didattica in italiano | `restituisce`, `restituisci`, `se`, `altrimenti`, `mentre`, `vero`, `falso` |
| `ddf-c-familiar` | Ingresso più familiare a chi conosce linguaggi della famiglia C | `returns`, `return`, `break`, `=`, `true` |

Il profilo `ddf-c-familiar` è soltanto una sintassi DDF familiare: non compila
C, non importa header C, non adotta il modello di memoria C e non promette
compatibilità con C, C++, C# o Java.

I nomi dei tipi primitivi e della libreria standard restano per ora canonici
in tutti e tre i profili. Un futuro livello dichiarativo di alias potrà
presentare nomi localizzati senza cambiare gli identificativi semantici usati
dalla IR e dalle API host.

## Contratto dei booleani

`lexicon.json` elenca le grafie in `booleanLiterals` e ne dichiara il valore in
`booleanLiteralValues`. Il parser conserva tipo e valore del letterale fino
alla IR; type checker, Watch e VM non deducono più il valore dalla parola
`true` o `false`. I vecchi pacchetti che usano esattamente queste due grafie
restano caricabili anche senza la nuova mappa.

## Verifica di equivalenza

La suite Core carica i tre pacchetti reali, analizza tre sorgenti equivalenti,
controlla il type checking con la stessa funzione host, confronta la forma
semantica della IR ignorando la grafia dei token ed esegue i programmi. La
prova deve produrre lo stesso valore finale in ogni profilo.

Il cambio di pacchetto non converte il testo del documento. Una futura
conversione sarà un comando esplicito, con anteprima e diagnostiche, separato
dalla semplice selezione del profilo.
