# Sessione manuale DDF 0.12 Candidate

Questa prova completa il gate di uso reale della `0.12.5`. Non serve cercare di
“far passare” il candidato: qualsiasi comportamento inatteso va annotato prima di
decidere se sia un difetto dell'implementazione, dell'esperienza utente o del
contratto DDF.

## Preparazione

1. Avviare la build Release standalone.
2. Verificare in **Help > Linguaggio** che sia attivo il DDF `0.12`, pacchetto
   `0.12.3`.
3. Aprire `samples/language-corpus/multi-file/flat-workspace` come progetto.
4. Tenere visibili Diagnostica, Output, Outline, Breakpoint e Flowchart.

## Percorso di prova

- Aprire entrambi i sorgenti, modificarli senza salvarli e passare più volte fra
  le schede; le modifiche, il cursore e Undo/Redo devono restare indipendenti.
- Richiamare completamento, Signature Help, hover, Vai alla definizione e Trova
  riferimenti su una funzione esterna.
- Formattare i documenti: il risultato deve restare valido, stabile a una seconda
  formattazione e senza propagazione errata del colore dei commenti.
- Introdurre temporaneamente un identificatore sconosciuto e un punto e virgola
  mancante; diagnostiche, sottolineature, navigazione e Quick Fix devono riferirsi
  al punto corretto. Annullare poi le modifiche.
- Impostare un breakpoint nel chiamante e uno nella funzione esterna. Avviare il
  debug, usare F11/F10/Shift+F11, controllare riga corrente, Call Stack, variabili
  e Watch, quindi continuare fino al risultato `42`.
- Aprire la Flowchart, navigare nella funzione chiamata e tornare al diagramma
  principale; provare zoom, pan, selezione del nodo ed esportazione JPG.
- Eseguire Convalida e Build di preparazione. Non deve essere prodotto alcun EXE
  DDF tramite l'embedded e non devono apparire errori sul workspace valido.
- Salvare, chiudere e riaprire il progetto; schede, layout delle palette e
  breakpoint devono essere ripristinati senza perdere modifiche confermate.
- Se installati, passare al profilo italiano o familiare C e tornare al DDF,
  verificando conversione delle sole schede aperte e Undo senza salvataggi
  impliciti.

## Esito

Annotare per ogni anomalia:

- passaggio esatto e file interessato;
- risultato atteso e osservato;
- riproducibilità;
- screenshot o messaggio diagnostico, se utile.

Le anomalie confluiscono in `ddf-language-candidate-issues.json` con un nuovo ID
`CAND-nnnn`. Solo dopo una sessione interamente superata, oppure dopo la chiusura
o il differimento motivato di tutte le osservazioni, si può impostare la ricevuta
manuale su `passed` e valutare `readiness: ready`.
