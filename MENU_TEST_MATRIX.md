# Matrice smoke dei menu

Questa matrice documenta la copertura automatica delle funzioni esposte dalla
barra dei menu. I test invocano le vere voci `ToolStripMenuItem` del form
WinForms e vengono eseguiti da
`tests/DDFLanguageEditor.EditorSmokeTests`.

## File

| Comando | Verifica dinamica |
| --- | --- |
| Nuovo | crea un documento vuoto e ripristina il titolo senza percorso |
| Apri | carica un file DDF temporaneo tramite il normale handler |
| Apri cartella | indicizza ricorsivamente un workspace temporaneo e popola l'explorer |
| Chiudi cartella | rimuove explorer e indice condiviso senza chiudere il documento corrente |
| Salva | aggiorna il file corrente e verifica il contenuto su disco |
| Salva con nome | salva in un secondo percorso temporaneo e aggiorna il titolo |
| File recenti | espone e riapre il percorso appena salvato |
| Cartelle recenti | registra e riapre l'ultimo workspace valido separatamente dai documenti recenti |
| Esci | chiude una finestra con documento pulito senza eccezioni residue |

I selettori Apri/Salva sono sostituiti nello smoke da risposte deterministiche;
nell'applicazione continuano a usare i dialoghi Windows. I file sono creati in
una directory temporanea dedicata e rimossi al termine. Le impostazioni dei file
e delle cartelle recenti dell'utente non vengono salvate durante il test. Lo smoke
trascina inoltre un sorgente `.ddf` e una cartella sulle superfici dell'IDE,
verificando apertura in scheda, aggiornamento del workspace e rifiuto dei file
con estensione non supportata.

Lo smoke modifica inoltre dall'esterno un file aperto e verifica il confronto
affiancato, la ricarica, la conservazione del buffer, il rinvio senza popup
ripetuti, il blocco prima del salvataggio e il caso di file eliminato. Verifica
anche che contenuti identici con terminazioni LF/CRLF differenti non generino un
falso conflitto.

## Modifica

| Comando | Verifica dinamica |
| --- | --- |
| Annulla | ripristina il testo rimosso da Taglia |
| Ripristina | riapplica il taglio annullato |
| Taglia | rimuove la selezione corrente |
| Copia | copia la selezione, anche dalla proiezione compressa |
| Incolla | sostituisce la selezione con il testo negli appunti |
| Seleziona tutto | opera sul sorgente e sulla proiezione compressa |
| Trova | apre la finestra, precompila la selezione e trova la ricorrenza seguente |
| Sostituisci | verifica sostituzione corrente, sostituzione globale e chiusura |
| Trova nel workspace | cerca testo e dichiarazioni nei file e nei buffer non salvati, poi naviga dal risultato |
| Completamento | ordina suggerimenti per contesto/tipo/scope/frequenza/prossimità e mostra categoria, tipo e origine |
| Correzione rapida | propone azioni contestuali da menu, `Ctrl+.` e toolbar, applicandole in un singolo Undo |
| Formatta documento | normalizza il sorgente, conserva il cursore ed è annullabile in un passo |
| Vai alla definizione | seleziona la dichiarazione risolta dal riferimento corrente |
| Rinomina simbolo | modifica solo dichiarazione e riferimenti nello stesso ambito ed è annullabile |
| Espandi selezione sintattica | attraversa token, espressione, istruzione, blocco, funzione e documento |
| Riduci selezione sintattica | ripercorre i livelli precedenti usando la cronologia della scheda attiva |
| Vai al delimitatore corrispondente | salta tra parentesi, quadre e graffe ignorando stringhe e commenti |
| Seleziona occorrenza successiva | aggiunge con `Ctrl+D` la prossima occorrenza tokenizzata |
| Seleziona tutte le occorrenze | crea selezioni simultanee nel codice senza includere commenti o stringhe omonime |
| Sostituisci nel workspace | genera l'anteprima, seleziona una sola occorrenza tra più file, modifica un buffer non salvato e lo ripristina con un Undo |
| Vai a file | filtra e apre un file del workspace mantenendo i buffer non salvati |
| Vai a simbolo | raggiunge una dichiarazione selezionata anche in un altro file |
| Trova riferimenti | distingue dichiarazione e utilizzi semantici, ignorando commenti, stringhe e omonimi locali |
| Vai a riga/colonna | interpreta `riga:colonna` e posiziona il cursore senza modificare il testo |
| Vai all'ultima modifica | torna all'ultimo punto modificato della scheda attiva |
| Navigazione indietro | ripristina file, selezione e posizione precedenti conservando il buffer non salvato |
| Navigazione avanti | riapplica la destinazione successiva e viene invalidata da un nuovo salto |

Lo smoke conserva e ripristina il contenuto precedente degli appunti Windows.
Verifica inoltre gli stati abilitati/disabilitati all'apertura del menu e che
`Ctrl+X`, `Ctrl+C` e `Ctrl+V` usino lo stato corrente dell'editor anche quando
le corrispondenti voci di menu erano rimaste disabilitate.
Lo smoke enumera inoltre tutte le caselle della form principale e dei popup:
le scorciatoie standard devono agire sul controllo focalizzato, ogni casella
deve avere il menu contestuale di modifica e il gutter deve restare escluso.

## Esegui

| Comando | Verifica dinamica |
| --- | --- |
| Convalida progetto | usa tutti i sorgenti e i buffer non salvati senza creare l'output |
| Compila progetto | crea una ricevuta deterministica con fingerprint senza salvare implicitamente i sorgenti |
| Avvia senza debug | esegue l'artefatto in memoria con `Ctrl+F5` senza una sessione debugger |
| Avvia debug | esegue `main` con `F5`, seleziona Output e verifica valore e completamento; un file attivo esterno al workspace viene eseguito in isolamento |
| Entra nella funzione | con il debugger in pausa, `F11` entra nella prima istruzione della funzione DDF chiamata e aggiorna Call Stack, Variabili e Watch |
| Passo successivo | con il debugger in pausa, `F10` esegue una sola istruzione senza entrare nelle funzioni chiamate e aggiorna Watch e riga corrente |
| Esci dalla funzione | con il debugger in pausa, `Shift+F11` completa il frame corrente, si ferma nel chiamante e aggiorna Call Stack e Variabili |
| Breakpoint | attiva con `F9` o dal gutter, marca la riga, sospende sullo statement, evidenzia in giallo l'intera riga e continua con `F5` |
| Arresta | espone `Shift+F5` e viene abilitato soltanto durante un'esecuzione |

Il core verifica inoltre cancellazione cooperativa, limite anti-loop ed errori
runtime posizionati nel sorgente.

## Visualizza

| Comando | Verifica dinamica |
| --- | --- |
| Comprimi blocco | comprime più blocchi dalla firma nella stessa proiezione di sola lettura |
| Espandi tutto | ripristina il sorgente originale senza alterarne il contenuto |
| Evidenzia riga corrente | alterna l'overlay della riga del cursore senza spostare selezione o modificare il sorgente |
| Mostra caratteri invisibili | disegna marker per spazi, tab e fine riga e sincronizza menu, toolbar e impostazioni |
| Mostra guide di indentazione | disegna guide per i livelli configurati senza contaminare RTF o Undo |
| A capo automatico | applica il word wrap alle schede e alla vista compressa mantenendo coerente il gutter |
| Mostra overview diagnostica | alterna la barra persistente, verifica errori e breakpoint reali e naviga alla riga cliccata |
| Barre degli strumenti | mostra o nasconde singolarmente le sette barre mobili per categoria e ne persiste visibilità, ordine, riga e posizione |
| Command Palette | filtra azioni, categorie e scorciatoie, mostra gli stati non disponibili ed esegue l'handler originale |
| Impostazioni | applica editor, scorciatoie, autosave, Hot Exit, sessione e recovery locale, con preset predefinito |

Sono controllati anche testo e disponibilità contestuale dei due comandi, i
colori, la colonna dei numeri di riga, l'espansione selettiva di un singolo
blocco e il blocco delle modifiche nella vista compressa.

## Help

| Comando | Verifica dinamica |
| --- | --- |
| Pacchetto linguaggio attivo | apre il popup centrato e verifica stato, versione, origine, sorgente e diagnostiche di caricamento |
| About | apre il popup e verifica icona ad alta risoluzione, autore, sito, licenza MIT, versione e stato beta |

Lo smoke controlla inoltre che la finestra principale sia configurata per
l'avvio massimizzato, che la diagnostica resti visibile anche senza problemi e
che la precedente barra beta non sia più presente nel layout. Verifica anche il
gutter non selezionabile, l'allineamento a sinistra dei numeri di riga, la
larghezza iniziale dell'Outline e le transizioni pinned/auto-hide delle due
palette. Entrambe vengono ridimensionate tramite i rispettivi splitter; larghezza,
altezza e stato pin vengono serializzati per il ripristino al successivo avvio.
Il breadcrumb viene verificato sul form reale con file, funzione e blocchi
annidati; il click su un segmento deve selezionare il costrutto e consentire il
ritorno alla posizione precedente tramite Navigazione indietro.

## Barre degli strumenti

Le sette barre mobili raggruppano per categoria 51 pulsanti a icona con tooltip: File, Modifica, Codice e formattazione,
Navigazione e ricerca, Visualizzazione, Progetto e linguaggio, Esecuzione e debug. Possono essere riordinate tramite la
maniglia e mostrate o nascoste da **Visualizza > Barre degli strumenti**. Lo smoke verifica disposizione iniziale compatta,
persistenza, accessibilità e condivisione degli handler Convalida/Build/Run/Debug/Breakpoint/Stop con il menu Esegui.

Lo stesso gate verifica che tutte le superfici applicative usino la palette
chiara, oppure i colori di sistema in modalità ad alto contrasto, e che soltanto
editor, folding e gutter conservino lo sfondo scuro. Verifica inoltre che i controlli
raggiungibili espongano nomi accessibili e che `F6`/`Shift+F6` attraversino
editor, Outline e Diagnostica in entrambe le direzioni. `Ctrl+Tab`,
`Ctrl+Shift+Tab` e `Ctrl+PagSu/PagGiù` cambiano inoltre la scheda della palette
che contiene il focus. Le dimensioni persistite
delle palette vengono inoltre verificate come pixel logici reversibili tra DPI.
Verifica inoltre che le scorciatoie personalizzate rilevino i conflitti, siano
persistenti e aggiornino menu, toolbar, Command Palette ed esecuzione reale.
Autosave viene provato con un orologio controllato su un file temporaneo; Hot
Exit deve serializzare e ripristinare contenuto, selezione e stato dirty senza
accedere ai dati reali del profilo utente.
Lo snapshot di sessione viene verificato ricaricando realmente un file temporaneo
dal disco e recuperando selezione e breakpoint, separatamente dal contenuto Hot Exit.
Il recovery simula una chiusura inattesa, controlla il popup con le tre scelte e
ripristina il buffer come modificato verificando che il file originale non cambi.
Verifica inoltre la nuova icona incorporata, la centratura delle form secondarie
e il docking senza sovrapposizioni tra palette destra e pannello inferiore.
Il popup Trova/Sostituisci viene inoltre controllato per allineamento della
checkbox, ordine dei pulsanti, dimensioni uniformi e visibilità contestuale.

## Scorciatoie e comando di esecuzione

La scheda **Espressioni di controllo** nella palette inferiore espone Aggiungi
(anche Invio nel campo) e Rimuovi (anche Canc nell'elenco). Non aggiunge shortcut
globali né pulsanti alla toolbar in questo primo incremento; si raggiunge tramite
le schede e la navigazione tastiera esistente. Lo smoke verifica lettura alla
pausa, deduplicazione, rimozione e azzeramento dei valori a fine debug. Verifica
inoltre espressioni aritmetiche pure, rifiuto delle assegnazioni e persistenza
separata passando fuori da un progetto e tornando nello stesso workspace.

La scheda **Variabili** elenca automaticamente parametri, locali e globali alla
pausa, mostra tipo dichiarato e valore e permette di espandere array e strutture.
Lo smoke verifica anche la rimozione dei valori alla fine del debug.

La scheda **Call Stack** espone frame corrente e chiamanti con posizione e
documento. Doppio clic e Invio navigano all'intervallo sorgente del frame; lo
stack viene svuotato alla fine della sessione. Lo smoke entra inoltre in una
funzione annidata, seleziona il chiamante e verifica che Variabili mostri soltanto
lo scope corrispondente.

Le regressioni del 2026-09-06 verificano anche che Chiudi disponga realmente il
Playground modeless, che il gestore pacchetti mantenga Chiudi sulla riga degli
altri comandi anche alla dimensione minima e che l'icona pacchetti sia distinta
da Impostazioni. Il debug viene provato su una scheda non salvata e con cambio
scheda durante la pausa: i breakpoint degli altri documenti non devono sostituire
quelli della sessione in esecuzione. Un workspace implicito viene inoltre avviato
dall'unica `main` anche se la scheda attiva contiene un breakpoint in una funzione
mai richiamata.

Lo smoke verifica le 51 scorciatoie dichiarate, incluse `Ctrl+,`, `Ctrl+Shift+P`, `Alt+Sinistra`, `Alt+Destra`, `Ctrl+P`, `Ctrl+Shift+O`, `Shift+F12`, `Ctrl+G`, `Ctrl+Shift+Backspace`, `Ctrl+Alt+F` per la ricerca workspace, `Ctrl+Alt+H` per la sostituzione workspace, `Ctrl+.` per le correzioni, `F12` per la definizione,
`F2` per la rinomina, `Ctrl+Shift+B` per Build, `Ctrl+F5` per Run, `F9` per i breakpoint, `F5` per Debug/Continua, `F11` per Entra nella funzione, `F10` per Passo successivo, `Shift+F11` per Esci dalla funzione e `Shift+F5` per Stop. Il gate completo si avvia con:

```powershell
.\tests\run-dynamic-smoke.ps1
```

Usare `-Visible` per mostrare i form durante l'esecuzione.
