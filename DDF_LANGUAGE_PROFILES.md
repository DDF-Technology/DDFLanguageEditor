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

Nell'applicazione standalone il DDF è l'unico linguaggio incluso e costituisce
anche il fallback incorporato nell'assembly Core. Il profilo italiano e quello
familiare C sono distribuiti come pacchetti utente iniziali: al primo avvio
vengono installati nel catalogo locale e da quel momento sono selezionati,
convalidati e aggiornati esclusivamente da `Linguaggio > Gestisci pacchetti`.
Il profilo italiano crea per impostazione predefinita file `.ddfit` e quello
familiare C file `.ddfc`; entrambi accettano anche `.ddf`, così un workspace
esistente rimane visibile durante il confronto fra sintassi.

Il profilo `ddf-c-familiar` è soltanto una sintassi DDF familiare: non compila
C, non importa header C, non adotta il modello di memoria C e non promette
compatibilità con C, C++, C# o Java.

I nomi dei tipi primitivi e della libreria standard restano per ora canonici
in tutti e tre i profili. Un futuro livello dichiarativo di alias potrà
presentare nomi localizzati senza cambiare gli identificativi semantici usati
dalla IR e dalle API host.

## Presentazione localizzata

Ogni profilo dichiara in `intellisense.json` anche la propria presentazione
delle diagnostiche e della Flowchart. Il profilo didattico può quindi usare
messaggi più esplicativi, mentre quello familiare C mostra `Yes`, `No` ed
`End`; il cambio è immediato e non altera AST, IR o comportamento runtime.

Una diagnostica conserva sempre il messaggio tecnico prodotto dal motore e gli
affianca il testo destinato all'utente. Palette, hover e Output mostrano il
secondo; Quick Fix e logica automatica continuano a usare codice e messaggio
tecnico stabili. Se un codice non è presente nella mappa del pacchetto viene
mostrato il messaggio originale, senza perdere informazioni.

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

Nella shell standalone il cambio di profilo converte immediatamente tutte le
schede aperte. La conversione opera sui token del profilo precedente e usa i
ruoli semantici dichiarati dal pacchetto: keyword di controllo, marker di
ritorno, booleani e operatore di inizializzazione vengono proiettati nella
grafia del profilo di destinazione. Commenti, stringhe, direttive libreria e
identificatori restano invariati.

Ogni buffer convertito viene marcato come modificato ma non viene salvato
automaticamente; un singolo Undo ripristina la sintassi precedente e Redo
riapplica la conversione. I file del workspace non aperti non vengono scritti
né modificati implicitamente: vanno aperti prima del cambio profilo oppure
convertiti in una futura operazione workspace con anteprima.
