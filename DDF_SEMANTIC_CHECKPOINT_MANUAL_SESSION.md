# Sessione manuale visuale e accessibile del checkpoint DDF

Questa sessione completa il gate automatico creato da
`tools/run-semantic-checkpoint.ps1`. La ricevuta generata resta intenzionalmente
`not-ready` finché una persona non conclude questa verifica e classifica ogni
anomalia. Un esito automatico verde non sostituisce il controllo dell'esperienza
reale.

## Preparazione

1. Eseguire il gate in Release e conservare il percorso della ricevuta mostrato
   al termine.
2. Avviare lo standalone Release massimizzato e aprire
   `samples/language-features-debug-workspace`.
3. Tenere visibili Outline/API, Diagnostica/Output, Breakpoint, Watch, Call Stack
   e Flowchart. Ripetere i passaggi essenziali a scala Windows 100% e 150%, se
   disponibili sul sistema di prova.

## Esperienza editor e accessibilità

- Verificare contrasto e leggibilità di codice, selezione, diagnostiche,
  breakpoint e riga corrente; nessun testo o controllo deve essere tagliato.
- Usare soltanto la tastiera per raggiungere menu, schede documento e palette:
  F6/Shift+F6 devono cambiare area, frecce e Ctrl+Tab devono muoversi dentro
  l'area attiva e il focus deve restare visibile.
- Controllare nome e tooltip delle icon bar, ordine di tabulazione, pulsanti
  predefiniti/annulla nei popup e apertura centrata di Trova, Sostituisci,
  Rinomina, Playground e About.
- Scrivere, selezionare in entrambe le direzioni, copiare/tagliare/incollare,
  annullare/ripetere e formattare. Numeri di riga, overview e scroll devono
  restare sincronizzati senza flicker o propagazioni errate del colore.

## Flussi semantici reali

- Richiamare completamento, Signature Help, hover, Vai alla definizione, Trova
  riferimenti, Rinomina e Quick Fix fra file diversi.
- Avviare Run e Debug dal `main`, entrare in una funzione esterna con F11,
  usare F10 e Shift+F11, controllare cambio scheda, riga corrente, Call Stack,
  Variabili e Watch, quindi completare l'esecuzione.
- Sul workspace `samples/language-features-debug-workspace`, lasciare il
  debugger fermo su un breakpoint per oltre cinque minuti; confermare che la
  riga evidenziata, Watch e comandi restino utilizzabili, fare un passo,
  cambiare un breakpoint durante la pausa e usare Stop. Verificare in Gestione
  attività che non resti alcun Runner della sessione. Lo smoke non visuale
  ripetibile è `dotnet tests/DDFLanguageEditor.Compiler.Tests/bin/Release/DDFLanguageEditor.Compiler.Tests.dll --debug-pause-endurance-only`.
- Aprire `samples/debug-large-workspace` e seguire la procedura nel relativo
  README: l'interfaccia deve restare reattiva lungo la catena profonda e i
  breakpoint non raggiungibili non devono interrompere il programma.
- Aprire la Flowchart, navigare nelle funzioni e tornare al chiamante; verificare
  zoom, pan, selezione sorgente ed export JPG senza intersezioni ambigue.
- Aprire il Playground linguaggio, introdurre e correggere un errore e verificare
  che token, albero, diagnostiche e anteprima si aggiornino senza modificare i
  documenti aperti.

## Standalone ed embedded

- Nello standalone verificare Convalida, Run, Debug e Build; l'artefatto deve
  nascere soltanto dal profilo standalone.
- Avviare DatabaseDemo: colorazione, formattazione, numeri di riga, palette e
  persistenza layout devono coincidere con l'editor; Run/Build manuali devono
  restare assenti e lo script deve partire esclusivamente dall'evento host.
- Durante uno script embedded lento, l'applicazione host deve restare reattiva;
  Stop, chiusura e dispose non devono lasciare Runner o callback tardivi.

## Chiusura e ricevuta

Annotare file, passaggio, risultato atteso/osservato, riproducibilità e immagini
per ogni anomalia. Solo con tutte le osservazioni chiuse o differite in modo
esplicito aggiornare una copia della ricevuta:

- `manual.status` a `passed`, con `completedBy`, `completedAt` e note;
- `readiness` a `ready` soltanto se anche `automated.status` è `passed`;
- `manual.status` a `failed` e `readiness` a `not-ready` se resta un blocco.

La ricevuta non deve mai essere promossa automaticamente dallo script.

## Sessione assistita 2026-10-23

Computer Use ha verificato sulla build Release il layout massimizzato, nomi e
stati accessibili dei controlli, F6 fra editor/palette, Ctrl+Tab nelle palette,
leggibilità del tema, gutter continuo, Playground centrato e popolato, About
centrato con icona nitida, Flowchart con controlli nominati e modalità didattica,
debug al breakpoint, evidenza gialla, disabilitazione dei comandi pericolosi,
Step Over, sincronizzazione Flowchart e Stop. Il breakpoint temporaneo è stato
rimosso e il sorgente non è stato modificato.

Il 2026-10-24 la stessa sessione ha coperto anche DatabaseDemo Release. Sono
risultati coerenti tema e colorazione embedded, numeri di riga, Flowchart,
palette `API host`, assenza di Run/Build manuali, esecuzione esclusiva dal
pulsante dell'host, inserimento nel database virtuale, pausa automatica su un
breakpoint, evidenza della riga corrente, reattività dell'host durante la pausa
e ripresa fino al risultato. Lo smoke mirato `--database-demo-only` è verde. Il
primo avvio ha correttamente rifiutato uno script utente persistito contenente
`False` maiuscolo; il sorgente canonico DDF, che usa `false`, è stato quindi
provato senza sovrascrivere lo stato persistito. Breakpoint e processi di prova
sono stati rimossi.

La sessione resta `pending`: il controllo remoto non può indirizzare come
finestra autonoma i dialoghi figli e la sua riattivazione dell'owner altera
temporaneamente lo stato massimizzato, quindi input da tastiera nei dialoghi e
persistenza della massimizzazione richiedono conferma umana. Resta inoltre da
provare la scala Windows 150% come indicato sopra. Un nuovo tentativo del
2026-10-24 ha confermato il limite dello strumento: Trova espone correttamente
campo, checkbox e pulsanti nell'albero accessibile, ma l'iniezione testo
riattiva l'editor proprietario. Il testo finito nel buffer è stato annullato e
la diagnostica è tornata pulita; il risultato non è stato classificato come
difetto applicativo. Anche Impostazioni Windows non ha esposto una finestra
indirizzabile, quindi la scala del sistema non è stata modificata.

La verifica umana successiva ha confermato Trova e Sostituisci: inserimento,
selezione del risultato, ordine Tab/Shift+Tab, Invio ed Escape sono corretti. La
prova di Rinomina sul riferimento `completedRuns` in `main.ddf` ha invece
individuato un difetto applicativo reale: F2 non apriva il popup e la voce nel
menu Modifica era disabilitata, perché il comando accettava soltanto occorrenze
risolte nel file corrente. Il comando usa ora la risoluzione semantica del
workspace, aggiorna in buffer dichiarazione e riferimenti negli altri file e
ripristina scheda e selezione originarie. Il gate Core è `281/281` e lo smoke
`--workspace-rename-only` è verde. La nuova build Release è stata quindi
avviata sul workspace reale e la verifica umana ha confermato l'apertura di
Rinomina tramite F2 sul riferimento esterno. Il difetto è chiuso; resta la prova
a scala Windows 150%.

La prova conclusiva è stata eseguita con scala Windows 150%. La prima build ha
mostrato una palette destra eccessivamente larga, icon bar scomposte e testi
Flowchart compressi: osservazioni classificate come difetti DPI reali. La build
corretta limita adattivamente la palette al 40% dell'area disponibile, ricompatta
le toolbar senza perdere gruppi, ordine o visibilità e scala la geometria
Flowchart separatamente dallo zoom logico e dall'export. L'utente ha confermato
editor e palette proporzionati, icon bar ordinate su tre righe e diagramma
leggibile. Build Release, smoke WinForms completo e matrice geometrica risultano
verdi. Tutti i punti manuali sono conclusi positivamente e la ricevuta può
essere promossa a `passed`/`ready`.

## Verifica debug prolungato 2026-11-06

Sulla build Release `4d11903`, nel workspace
`samples/language-features-debug-workspace`, la sessione UI è rimasta in
`DEBUG IN PAUSA` dal `09:19:58` al `09:25:24` UTC senza timeout. Durante la
pausa Watch ha mostrato `completedRuns = 0`; dopo F10 la riga corrente è
avanzata e Watch è diventato `1`. Un breakpoint aggiunto a debug avviato
alla riga 7 di `main.ddf` è stato raggiunto con F5. Proseguendo fino alla
chiamata in riga 15, F10 è entrato in `safeDivide` su `calculations.ddf`;
la scheda e la Flowchart si sono aggiornate e Call Stack ha mostrato
`safeDivide()` e `main()`. Shift+F5 ha terminato la sessione, svuotato lo
stack e riabilitato i comandi normali. Nessun processo
`DDFLanguageEditor.Runner` era attivo dopo Stop. Watch e i due breakpoint
temporanei sono stati rimossi; il breakpoint preesistente in riga 3 è
rimasto intatto e i sorgenti non sono stati modificati.

Questa osservazione UI riguarda il debugger VM dello standalone. La pausa
del worker compilato e la terminazione del relativo PID sono verificate
separatamente dallo smoke opt-in `--debug-pause-endurance-only`, già
registrato nella roadmap: non attribuire alla sessione UI l'avvio di un
Runner che non è avvenuto.
