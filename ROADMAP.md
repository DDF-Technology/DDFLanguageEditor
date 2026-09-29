# DDFLanguageEditor roadmap

Questo documento guida lo sviluppo del progetto e viene aggiornato insieme al
codice. Le attività completate vengono marcate, mentre cambiamenti di direzione
e decisioni sul linguaggio vengono registrati nella sezione finale.

Schema dell'ecosistema e stato dei componenti: [ECOSYSTEM_ARCHITECTURE.md](ECOSYSTEM_ARCHITECTURE.md).

## Obiettivo del progetto

Costruire progressivamente un editor affidabile per il linguaggio sperimentale
DDF, mantenendo distinti tre livelli:

1. esperienza di editing e gestione dei documenti;
2. analisi lessicale, sintattica e semantica;
3. eventuale interprete, compilatore o altro modello di esecuzione.

Lo sviluppo del runtime è iniziato dopo la stabilizzazione della grammatica,
del parser e del primo sistema dei tipi; resta separato dall'interfaccia.

La direzione di prodotto comprende un editor incorporabile nelle applicazioni
di terzi per automatizzarne funzionalità ed espanderne le potenzialità: accesso
ai dati, calcoli matematici e logici, algoritmi su variabili personalizzate e
procedure sugli oggetti dell'host. CAD e SCADA sono esempi di applicazioni possibili,
non settori prioritari o percorsi di certificazione. L'applicazione standalone userà
gli stessi componenti pubblici. Una flowchart dinamica affiancherà il codice
per renderne comprensibile il controllo di flusso e, durante il debug, l'esecuzione.

La modalità didattica è un'opzione trasversale sempre attivabile e disattivabile
dall'utente nell'editor standalone e nel componente integrato. È indipendente dal
profilo sintattico e dalla lingua; non cambia semantica, permessi o output della build.

I pacchetti utente offriranno profili sintattici di una semantica comune:
didattico italiano o sintassi ispirate a C/C#/Java/VB non equivalgono al supporto
completo di quei linguaggi e delle loro librerie. Il cambio di profilo non
modifica la semantica; nell'editor standalone converte invece dinamicamente la
sintassi equivalente delle schede aperte. Il backend principale previsto
è .NET + Roslyn: dalla IR si genera C#, compilato in assembly .NET, con mappe
verso il sorgente originale, esclusivamente nella versione standalone e nella
relativa CLI. In modalità integrata il sorgente resta sempre uno script eseguito
dalla VM sulle API autorizzate dell'host; non vengono generati o caricati assembly
DDF. In modalità standalone si pubblica un EXE self-contained con dipendenze.

## Stato delle release

Ordine operativo aggiornato il 2026-09-26: completati il runtime multi-file
`0.11.0`, la flowchart dinamica `0.11.1` e l'Editor SDK `0.11.2`; è iniziata
la definizione completa del linguaggio nella `0.12.0`.
Il precedente identificativo provvisorio `0.10.2` è stato sostituito. Tutte le
milestone successive conservano ordine e ambito.

| Release | Obiettivo | Stato |
| --- | --- | --- |
| `0.1.0-beta.1` | Prima beta pubblica del prototipo | Completata |
| `0.1.1` | Stabilizzazione dell'editor e fondamenta testabili | Completata |
| `0.2.0` | Apertura, salvataggio e flusso documentale affidabile | Completata |
| `0.3.0` | Lexer DDF e diagnostica lessicale | Completata |
| `0.4.0` | Parser, AST e diagnostica sintattica | Completata |
| `0.4.1` | Catalogo del linguaggio estensibile | Completata |
| `0.5.0` | Funzioni da IDE | Completata |
| `0.5.1` | Indice dei simboli e outline navigabile | Completata |
| `0.5.2` | Delimitatori corrispondenti e folding | Completata |
| `0.5.2.x` | Stabilità interattiva e smoke dinamici | Completata |
| `0.5.3` | Completamento contestuale | Completata |
| `0.5.4` | Formattazione automatica | Completata |
| `0.5.4.x` | Rifinitura shell IDE e informazioni applicazione | Completata |
| `0.5.5` | Risoluzione, hover e rinomina dei simboli | Completata |
| `0.5.6` | Workspace DDF multi-file | Completata |
| `0.6.0` | Sistema dei tipi e diagnostica semantica | Completata |
| `0.7.0` | Primo interprete AST e comandi Run/Stop | Completata |
| `0.7.1` | Libreria standard minima, input e toolbar | Completata |
| `0.7.2` | Tema chiaro uniforme dell'interfaccia | Completata |
| `0.7.3` | Icona, dialoghi e layout delle palette | Completata |
| `0.7.4` | Stack runtime e navigazione dagli errori | Completata |
| `0.8.0` | Porting controllato a .NET 10 LTS | Completata |
| `0.8.1` | Distribuzione self-contained Windows x64 | Completata |
| `0.9.0` | Breakpoint e pausa/continua | Completata |
| `0.9.0.1` | Editing multidocumento e breakpoint per file | Completata |
| `0.9.1` | Comodità e gesti di scrittura | Completata (`0.9.1.0`–`0.9.1.3`) |
| `0.9.2` | IntelliSense, snippet e feedback inline | Completata (`0.9.2.0`–`0.9.2.10`) |
| `0.9.3` | Ricerca e navigazione del workspace | Completata (`0.9.3.0`–`0.9.3.7`) |
| `0.9.4` | Personalizzazione, sessione e recovery | Completata (`0.9.4.6`) |
| `0.9.5` | Fluidità, accessibilità e rifinitura | Completata (`0.9.5.6`) |
| `0.9.6` | Pacchetti di linguaggio, grammatica dichiarativa e build configurabile | Completata (`0.9.6.7`) |
| `0.10.0` | Variabili e call stack durante la pausa | Completata |
| `0.10.1` | Step Into, Step Over e Step Out | Completata |
| `0.11.0` | Runtime multi-file del workspace | Completata |
| `0.11.1` | Flowchart dinamica del codice e collegamento al debugger | Completata |
| `0.11.2` | Editor incorporabile e primo SDK per applicazioni host | Completata |
| `0.12.x` | Definizione completa e candidata alla stabilizzazione del linguaggio DDF | In corso (`0.12.0` completata) |
| `0.13.x` | Compilatore, linker, loader e CLI | Pianificata |
| `0.14.x` | DDF Language Package Studio integrato e riutilizzabile | Pianificata |
| `0.15.x` | Extension SDK e host sicuro per plug-in eseguibili | Pianificata |
| `0.16.x` | Assistente IA opzionale con fornitori locali e cloud | Pianificata |
| `Dopo 0.16` | Backend aggiuntivi, distribuzione estesa e preparazione della 1.0 | Differita |

## 0.1.1 — Stabilizzazione

### Ambito

- [x] Rendere esplicito il sottoinsieme sintattico riconosciuto dall'editor.
- [x] Separare classificazione sintattica e indentazione da Windows Forms.
- [x] Correggere commenti, stringhe, numeri, parole chiave e precedenze dei colori.
- [x] Conservare selezione e modifiche annullabili durante Tab e Invio.
- [x] Supportare indentazione e de-indentazione di selezioni multilinea.
- [x] Migliorare aggiornamento e sincronizzazione dei numeri di riga.
- [x] Evitare allocazioni grafiche ripetute e ridurre il lavoro a ogni battuta.
- [x] Aggiungere test automatici per classificazione e trasformazioni di testo.
- [x] Aggiornare versione, changelog, limiti e note di rilascio.

### Criteri di completamento

- soluzione compilabile in Debug e Release senza errori;
- tutti i test automatici superati;
- nessuna perdita della selezione nelle operazioni di editing coperte;
- classificazione corretta di commenti, stringhe e numeri nei casi di test;
- specifica e comportamento dell'highlighter privi delle contraddizioni note;
- worktree privo di artefatti generati non ignorati.

## 0.2.0 — Documenti persistenti

- [x] Nuovo, Apri, Salva e Salva con nome.
- [x] Estensione `.ddf` e codifica UTF-8 definite.
- [x] Stato modificato visibile nel titolo e nella barra di stato.
- [x] Conferma prima di perdere modifiche non salvate.
- [x] Scorciatoie standard e menu di modifica.
- [x] Trova, sostituisci e sostituisci tutto.
- [x] Barra di stato con file, riga, colonna e codifica.
- [x] File recenti persistenti.
- [x] Test di round-trip, codifica, stato documento, ricerca e file recenti.

Il criterio principale è che l'utente possa completare una sessione di editing
senza perdita silenziosa di dati.

## 0.3.0 — Analisi lessicale

- [x] Modello formale di token, posizione e intervallo nel sorgente.
- [x] Lexer incrementale derivato dalla specifica canonica.
- [x] Diagnostiche con codice, intervallo, riga e colonna.
- [x] Diagnostiche per caratteri, stringhe, commenti e librerie non terminati.
- [x] Evidenziazione alimentata dai token e aggiornata dal confine modificato.
- [x] Pannello diagnostico navigabile.
- [x] Corpus di sorgenti DDF validi e non validi.
- [x] Test di equivalenza fra lessicalizzazione completa e incrementale.

## 0.4.0 — Analisi sintattica

- [x] Grammatica formale e modello AST tipizzato.
- [x] Parser con precedenze, associatività e recupero dagli errori.
- [x] Diagnostiche sintattiche con codice, intervallo, riga e colonna.
- [x] Validazione sintattica di dichiarazioni, espressioni, blocchi e funzioni.
- [x] Pannello unificato per diagnostiche lessicali e sintattiche.
- [x] Corpus DDF valido e non valido con test di regressione.

## 0.4.1 — Catalogo del linguaggio

- [x] Unica fonte per parole riservate, booleani, operatori e delimitatori.
- [x] Ruoli sintattici indipendenti dal testo concreto delle parole chiave.
- [x] Precedenza, associatività e forme prefisse/postfisse definite come dati.
- [x] Lexer completo e incrementale configurabili tramite catalogo.
- [x] Parser configurabile tramite la stessa definizione del linguaggio.
- [x] Inventario documentato e procedura per il futuro confronto con C.
- [x] Test con un vocabolario alternativo e controllo dei duplicati.

## 0.5.0 — Esperienza IDE

- [x] completamento contestuale (`0.5.3`);
- [x] parentesi corrispondenti e folding (`0.5.2`);
- [x] outline e navigazione ai simboli (`0.5.1`);
- [x] formattazione automatica (`0.5.4`);
- [x] hover e rinomina simboli (`0.5.5`);
- [x] workspace DDF di piccole dimensioni (`0.5.6`).

### 0.5.1 — Indice dei simboli e outline

- [x] Intervalli esatti dei nomi dichiarati conservati nell'AST.
- [x] Indice indipendente dalla UI per librerie, strutture, campi e funzioni.
- [x] Indicizzazione di parametri e variabili anche in blocchi e cicli annidati.
- [x] Outline gerarchico aggiornato durante la digitazione.
- [x] Navigazione al nome tramite doppio clic.
- [x] Recupero utile anche in presenza di codice sintatticamente incompleto.
- [x] Test automatici e verifica visuale del layout e della navigazione.

### 0.5.2 — Delimitatori e folding

- [x] Matching di `()`, `[]` e `{}` vicino al cursore.
- [x] Esclusione automatica dei delimitatori dentro stringhe e commenti.
- [x] Intervalli comprimibili derivati dall'AST e validi con sorgenti incompleti.
- [x] Vista compressa di sola lettura senza modificare buffer, selezione o Undo.
- [x] Colori sintattici e diagnostici conservati sui segmenti visibili della proiezione.
- [x] Colonna dei numeri di riga mantenuta con numeri sorgente e indicatore `⋯`.
- [x] Compressione attivabile dalla firma o dall'interno del blocco, anche subito dopo una modifica.
- [x] Comandi `Ctrl+M` e `Ctrl+Shift+M` nel menu Visualizza.
- [x] Espansione automatica prima della navigazione dall'outline o della ricerca.
- [x] Test del core e verifica visuale di compressione ed espansione.

### 0.5.2.x — Stabilità interattiva e smoke dinamici

- [x] Ridisegno e notifiche del RichEdit sospesi durante la ricolorazione.
- [x] Matching dei delimitatori differito e sospeso durante selezioni mouse e doppio clic.
- [x] Operazioni di formato escluse dalla cronologia Undo dell'utente.
- [x] Confine incrementale corretto quando testo inserito condivide un prefisso con i token precedenti.
- [x] Matching dei delimitatori protetto da snapshot lessicali temporaneamente obsoleti.
- [x] Smoke deterministico con 400 inserimenti/tagli confrontati con il lexer completo.
- [x] Smoke WinForms sul form reale per selezione mouse, taglio libreria, Undo e modifiche rapide.
- [x] Smoke di compressione/espansione dalla firma con verifica di colori, numeri originali e sorgente invariato.
- [x] Smoke di tutti i 19 comandi dei menu File, Modifica, Visualizza e Help con file e impostazioni isolati.
- [x] Verifica delle 16 scorciatoie e degli stati contestuali dei menu.
- [x] Timer UI arrestati e protetti durante la chiusura del form.
- [x] Intervalli di folding obsoleti ignorati durante la sostituzione rapida del documento.
- [x] Comando unico `tests/run-dynamic-smoke.ps1`, estendibile nei prossimi step.

### 0.5.3 — Completamento contestuale

- [x] Motore indipendente dalla UI alimentato da `DdfLanguageDefinition`.
- [x] Suggerimenti per parole chiave, tipi, booleani e simboli del documento.
- [x] Simboli locali limitati alla funzione corrente e alle dichiarazioni precedenti.
- [x] Completamento dei nomi di libreria già noti nel documento.
- [x] Esclusione automatica di commenti e stringhe.
- [x] Popup vicino al cursore con attivazione automatica e `Ctrl+Spazio`.
- [x] Navigazione con frecce/Pagina, accettazione con `Tab` o `Invio` e chiusura con `Esc`.
- [x] Inserimento compatibile con Undo e senza interferenze con folding o formattazione.
- [x] Test con catalogo alternativo e smoke WinForms sul form reale.

### 0.5.4 — Formattazione automatica

- [x] Formatter indipendente dalla UI e configurabile tramite `DdfLanguageDefinition`.
- [x] Stile Allman, indentazione a quattro spazi e spaziatura uniforme dei token.
- [x] Gestione di dichiarazioni, blocchi, controlli, array, chiamate e operatori.
- [x] Contenuto di commenti, stringhe e direttive libreria conservato integralmente.
- [x] Sorgenti incompleti formattati senza tentare correzioni sintattiche.
- [x] Risultato idempotente e mappatura di cursore e selezione.
- [x] Comando Modifica → Formatta documento con `Ctrl+Shift+F`.
- [x] Applicazione come singola operazione Undo; nessuna modifica se già formattato.
- [x] Test con catalogo alternativo, smoke WinForms e verifica visuale reale.

### 0.5.4.x — Rifinitura shell IDE

- [x] Avvio della finestra principale sempre massimizzato.
- [x] Pannello diagnostico sempre visibile, anche in assenza di problemi.
- [x] Barra informativa beta rimossa dalla superficie di lavoro.
- [x] Icona applicazione multi-risoluzione con riquadro bianco arrotondato e anteprima About ad alta risoluzione.
- [x] Menu Help con popup About, autore, sito, licenza MIT e versione beta.
- [x] Palette scura da IDE con contrasto dedicato per stringhe, commenti, tipi e parole chiave.
- [x] Folding simultaneo di più blocchi con espansione selettiva.
- [x] Gutter dei numeri di riga allineato a sinistra e non selezionabile.
- [x] Outline compatto e palette Outline/Diagnostica pinnabili con auto-hide.
- [x] Finestra About centrata sullo schermo.
- [x] Smoke dinamico esteso al layout iniziale e a tutti i 19 comandi di menu.

### 0.5.5 — Informazioni e rinomina dei simboli

- [x] Modello semantico indipendente dalla UI e derivato dall'AST recuperabile.
- [x] Risoluzione di dichiarazioni e riferimenti con ambiti lessicali annidati.
- [x] Omonimi e simboli oscurati mantenuti in insiemi di riferimenti separati.
- [x] Diagnostiche `DDF201` per riferimenti non risolti e `DDF202` per duplicati nello stesso ambito.
- [x] Hover con categoria, tipo/firma e riga della dichiarazione.
- [x] Comando Vai alla definizione con `F12`.
- [x] Rinomina scoped con `F2`, validazione del nome e singolo Undo.
- [x] Test core e smoke WinForms sui comandi semantici.

### 0.5.6 — Workspace DDF multi-file

- [x] Apertura e chiusura di una cartella di lavoro dal menu File.
- [x] Indicizzazione ricorsiva dei sorgenti `.ddf` in un componente indipendente dalla UI.
- [x] Explorer compatto affiancato all'Outline con apertura tramite doppio clic.
- [x] Contenuto modificato in memoria reinserito nell'indice condiviso.
- [x] Simboli globali degli altri file disponibili nel completamento.
- [x] Riferimenti esterni riconosciuti senza falso diagnostico `DDF201`.
- [x] Navigazione `F12` alla definizione con apertura automatica del documento.
- [x] Test su filesystem temporaneo e smoke WinForms end-to-end.

## 0.6.0 — Sistema dei tipi

- [x] Modello indipendente dalla UI per primitivi, strutture e array.
- [x] Risoluzione dei tipi definiti nel documento e nel workspace.
- [x] Inferenza dei tipi di letterali, nomi, operatori, chiamate, indici e membri.
- [x] Verifica di inizializzazioni, assegnazioni e condizioni booleane.
- [x] Verifica di numero/tipo degli argomenti e valori restituiti.
- [x] Accesso ai campi di struttura tramite `.` aggiunto all'AST.
- [x] Diagnostiche stabili `DDF301`–`DDF308` nel pannello sorgente.
- [x] Hover arricchito con il tipo calcolato.
- [x] Test documentali, multi-file e smoke WinForms.

## 0.7.0 — Primo interprete AST

- [x] Interprete interno indipendente dalla UI basato sull'AST esistente.
- [x] Scope globali/locali, chiamate di funzione e punto di ingresso `main`.
- [x] Dichiarazioni, assegnazioni, operatori, condizioni e cicli.
- [x] Valori primitivi, array e istanze di strutture con accesso ai membri.
- [x] `ret`, `brk`, `end` e output tramite l'operatore `>>`.
- [x] Diagnostiche runtime `DDF401`–`DDF407` con posizione nel sorgente.
- [x] Cancellazione cooperativa e limite predefinito di 100.000 istruzioni.
- [x] Menu Esegui con Run (`F5`), Stop (`Shift+F5`) e palette Output.
- [x] Test core e smoke WinForms end-to-end.

## 0.7.1 — Libreria standard, input e toolbar

- [x] Catalogo centralizzato delle funzioni standard indipendente dalla UI.
- [x] `print`, `readLine`, `length`, `toInt` e `toFloat` tipizzate e completabili.
- [x] Input tramite callback host e dialogo WinForms sostituibile nei test.
- [x] Diagnostica `DDF408` per conversioni standard non valide.
- [x] Toolbar piatta a icone per 13 comandi principali con tooltip accessibili.
- [x] Run/Stop della toolbar sincronizzati con i corrispondenti comandi di menu.
- [x] Test Core e smoke WinForms per libreria standard, input e toolbar.

## 0.7.2 — Tema chiaro uniforme

- [x] Palette applicativa centralizzata per superfici, testo, bordi e stati.
- [x] Form principale, menu, toolbar, palette e status bar in tema chiaro.
- [x] Workspace, Outline, Diagnostica, Output e completamento in tema chiaro.
- [x] About, Trova/Sostituisci, Rinomina e Input runtime in tema chiaro.
- [x] Icone toolbar e pin con contrasto verificabile.
- [x] Tema scuro conservato esclusivamente per editor, folding e gutter.
- [x] Smoke WinForms di regressione sui colori di tutte le superfici principali.

## 0.7.3 — Icona, dialoghi e layout palette

- [x] Nuovo ICO multi-risoluzione incorporato nella Release con frame piccoli leggibili.
- [x] Icona caricata dalla risorsa assembly per form principale e About.
- [x] Trova/Sostituisci, Rinomina, Input e About sempre centrati sullo schermo.
- [x] Workspace/Outline resi come schede standard coerenti con Diagnostica/Output.
- [x] Palette destra estesa per tutta l'altezza utile della finestra.
- [x] Diagnostica/Output adattata alla larghezza residua senza passare sotto la palette destra.
- [x] Smoke su icona, centratura e geometria del docking.
- [x] Layout ordinato e adattivo per campi, checkbox e pulsanti di Trova/Sostituisci.

## 0.7.4 — Stack runtime navigabile

- [x] Conservare uno stack strutturato delle chiamate utente e standard nel Core.
- [x] Mostrare diagnostica e stack DDF nella palette Output.
- [x] Rendere navigabili con doppio clic l'istruzione fallita e ogni punto di chiamata.
- [x] Evidenziare nell'editor l'intervallo sorgente raggiunto dall'Output.
- [x] Aggiungere test Core e smoke WinForms per errori annidati e navigazione.

## 0.8.0 — Modernizzazione .NET

- [x] Consolidare la 0.7.4 in un checkpoint Git verificato.
- [x] Convertire separatamente i quattro progetti al formato SDK mantenendo `net48`.
- [x] Verificare il formato SDK su .NET Framework con test Core e smoke WinForms.
- [x] Portare Core e test logici a `net10.0`.
- [x] Portare applicazione e smoke WinForms a `net10.0-windows`.
- [x] Eliminare la dipendenza da `System.Configuration` per i file recenti.
- [x] Adottare bootstrap, metriche font e analizzatori WinForms moderni senza warning.
- [x] Migrare build locale e CI alla CLI .NET 10.

## 0.8.1 — Chiusura distributiva del porting

- [x] Adottare Windows x64 self-contained come formato ufficiale.
- [x] Aggiungere uno script ripetibile di pubblicazione e validazione.
- [x] Includere host .NET 10 e Windows Desktop senza dipendere dal runtime installato.
- [x] Generare lo ZIP distribuibile e il checksum SHA-256.
- [x] Produrre lo stesso pacchetto come artefatto della CI.

## 0.9.0 — Breakpoint e pausa/continua

- [x] Attivare e rimuovere breakpoint di riga dal gutter o con `F9`.
- [x] Marcare i breakpoint senza rendere selezionabile il gutter.
- [x] Sospendere il runtime prima dello statement eseguibile corrispondente.
- [x] Riutilizzare `F5` e Run come Continua durante la pausa.
- [x] Esporre Breakpoint nella icon bar e sincronizzare il pulsante Run con Continua.
- [x] Consentire a Stop di cancellare anche un runtime sospeso.
- [x] Coprire concorrenza del Core, gutter e flusso WinForms con test dinamici.

## 0.9.0.1 — Editing multidocumento e breakpoint per file

- [x] Introdurre un buffer indipendente per ogni documento aperto nel workspace.
- [x] Aggiungere schede documento con indicatore `*`, chiusura selettiva e ripristino di cursore, selezione e scroll.
- [x] Conservare modifiche, Undo/Redo e analisi dei documenti non attivi senza imporre il salvataggio a ogni cambio file.
- [x] Aggiornare completamento, diagnostica, tipi e indice workspace usando tutti i buffer in memoria.
- [x] Aggiungere Salva tutto e una conferma aggregata alla chiusura dell'applicazione.
- [x] Identificare ogni breakpoint tramite documento canonico e riga sorgente rimappabile, non come stato globale del solo editor attivo.
- [x] Conservare e mostrare breakpoint nei file non attivi, ripristinandoli nel gutter quando si cambia scheda.
- [x] Rimappare i breakpoint dopo inserimenti, eliminazioni e formattazione; segnalare quelli che non corrispondono più a uno statement eseguibile.
- [x] Aggiungere una palette Breakpoint con file, riga, stato abilitato e navigazione tramite doppio clic.
- [x] Coprire cambio scheda, buffer sporchi, Salva tutto e breakpoint multi-file con smoke dinamici.

## 0.9.x — Editor Experience

Prima di ampliare runtime, build o compilazione, questa linea deve rendere la
scrittura quotidiana comoda, interattiva e affidabile. Ogni incremento viene
valutato tramite flussi reali di editing, non soltanto con test statici.

## 0.9.1 — Comodità e gesti di scrittura

- [x] Chiudere automaticamente parentesi, graffe, quadre, virgolette e apostrofi senza duplicare il carattere già presente (`0.9.1.0`).
- [x] Gestire Enter, Backspace e Tab in modo contestuale tra coppie e blocchi (`0.9.1.0`).
- [x] Aggiungere commenta/decommenta selezione o riga (`0.9.1.0`).
- [x] Uniformare menu, tastiera e menu contestuale per Taglia/Copia/Incolla con stato sempre corrente (`0.9.1.0`).
- [x] Aggiungere duplica, sposta ed elimina riga mantenendo selezione e Undo prevedibili (`0.9.1.1`).
- [x] Introdurre selezione sintattica progressiva, riduzione con cronologia per scheda e navigazione tra delimitatori (`0.9.1.2`).
- [x] Supportare più cursori e selezioni con `Alt+Click`, occorrenza successiva/tutte e modifica simultanea annullabile in un passo (`0.9.1.3`).
- [x] Correggere automaticamente il rientro del testo incollato quando richiesto (`0.9.1.1`).
- [x] Esporre le nuove azioni tramite menu, scorciatoie e icon bar secondo frequenza d'uso (`0.9.1.0`).

## 0.9.2 — IntelliSense, snippet e feedback inline

- [x] Ordinare il completamento per contesto, tipo atteso, prossimità, frequenza e categoria; mostrare tipo e origine locale/workspace (`0.9.2.0`).
- [x] Correggere la colorazione completa dopo Formatta documento, impedendo al verde dei commenti di propagarsi al codice (`0.9.2.1`).
- [x] Separare visivamente istruzioni e contesti indipendenti durante la formattazione, senza spezzare `for` e `do/while` (`0.9.2.3`).
- [x] Mostrare firma, origine e parametro corrente durante la scrittura di chiamate locali, workspace e standard (`0.9.2.2`).
- [x] Aggiungere snippet contestuali e indentati con campi navigabili per funzioni, controlli, cicli e strutture (`0.9.2.4`).
- [x] Arricchire hover locale, standard e workspace con documentazione `///`, firma, tipo, origine, dichiarazione e riferimenti principali (`0.9.2.5`).
- [x] Mostrare diagnostiche inline ondulate, con hover sincronizzato alla palette, senza alterare testo, colori, selezione o cronologia Undo (`0.9.2.6`).
- [x] Definire provider di correzioni rapide estensibili, accessibili da menu contestuale, `Ctrl+.` e icon bar, per gli errori DDF più comuni (`0.9.2.7`).
- [x] Separare il token che segnala un errore dal punto reale di modifica, inserendo i token mancanti dopo l’ultima parte valida anche attraverso spazi e righe vuote (`0.9.2.8`).
- [x] Rendere strutturale il ripristino delle graffe usando contesto del parser, annidamento e indentazione, senza confondere la chiusura di un blocco interno con quella esterna (`0.9.2.9`).
- [x] Documentare che una graffa rimossa non è ripristinabile con certezza quando il sorgente restante costituisce ancora una forma sintatticamente valida: in assenza di diagnostica, l'intento originale resta ambiguo (`0.9.2.10`).
- [x] Eseguire analisi e completamento in background con cancellazione e scarto degli snapshot obsoleti (`0.9.2.10`).

## 0.9.3 — Ricerca e navigazione del workspace

- [x] Cercare testo e simboli in tutti i buffer e file del workspace, privilegiando le modifiche non salvate e navigando dai risultati (`0.9.3.0`).
- [x] Rendere ridimensionabile verticalmente la palette inferiore e conservarne l'altezza attraverso pin e auto-hide (`0.9.3.1`).
- [x] Sostituire nel workspace con anteprima selettiva e singola operazione annullabile per documento (`0.9.3.2`).
- [x] Aggiungere Vai a file, simbolo, riferimento, riga e ultima modifica (`0.9.3.3`).
- [x] Introdurre cronologia di navigazione Indietro/Avanti tra file e posizioni (`0.9.3.4`).
- [x] Mostrare breadcrumb cliccabile di file, funzione, struttura e blocco corrente (`0.9.3.5`).
- [x] Aggiungere una Command Palette ricercabile per tutte le azioni principali (`0.9.3.6`).
- [x] Supportare apertura mediante trascinamento di file e cartelle e workspace recenti (`0.9.3.7`).

## 0.9.4 — Personalizzazione, sessione e recovery

- [x] Salvare impostazioni per font, zoom, tema, tab/spazi, fine riga e formattazione (`0.9.4.0`).
- [x] Conservare dimensioni e stato pin/auto-hide delle palette destra e inferiore tra gli avvii (`0.9.4.1`).
- [x] Rendere configurabili le scorciatoie rilevando conflitti e mantenendo un preset ripristinabile (`0.9.4.2`).
- [x] Aggiungere autosave opzionale e Hot Exit per conservare buffer non salvati (`0.9.4.3`).
- [x] Ripristinare workspace, schede, layout, cursori e breakpoint della sessione precedente (`0.9.4.4`).
- [x] Creare recovery locale dopo arresti anomali senza sovrascrivere silenziosamente i file originali (`0.9.4.5`).
- [x] Rilevare modifiche esterne e offrire confronto, ricarica o conservazione del buffer (`0.9.4.6`).

## 0.9.5 — Fluidità, accessibilità e rifinitura

- [x] Definire budget misurabili per input, selezione, scroll, colorazione e apertura dei popup (`0.9.5.0`).
- [x] Evitare flickering e aggiornamenti completi durante le operazioni incrementali (`0.9.5.1`).
- [x] Mantenere reattivi documenti e workspace di dimensioni realistiche con lavoro cancellabile in background (`0.9.5.2`).
- [x] Aggiungere evidenza della riga corrente, spazi invisibili, guide di indentazione e word wrap configurabile (`0.9.5.3`).
- [x] Aggiungere un'overview disattivabile e interattiva per diagnostiche, ricerca, breakpoint e viewport; minimappa testuale completa valutata e rinviata come estensione opzionale (`0.9.5.4`).
- [x] Completare navigazione da tastiera, nomi accessibili, contrasto e comportamento DPI multi-monitor (`0.9.5.5`).
- [x] Creare smoke dinamici di sessioni prolungate, digitazione rapida e alternanza tra più comandi (`0.9.5.6`).

## 0.9.6 — Pacchetti di linguaggio e architettura dichiarativa

Trasformare DDFLanguageEditor da applicazione legata a una grammatica compilata
nel codice a piattaforma capace di caricare un pacchetto di linguaggio
versionato. DDF resterà il linguaggio predefinito e l'implementazione di
riferimento. Il pacchetto definirà sintassi, servizi IDE, semantica disponibile e
profilo di build senza consentire a un semplice file dati di eseguire codice
host arbitrario.

### 0.9.6.0 — Contratto e pacchetto DDF predefinito

- [x] Definire uno schema versionato per pacchetti `.ddflang`, rappresentabili come cartella o archivio (`0.9.6.0`).
- [x] Separare manifest, lessico, grammatica, tipi, semantica, formattazione, IntelliSense, runtime, build e libreria standard (`0.9.6.0`).
- [x] Trasferire il catalogo DDF corrente in un pacchetto esterno senza cambiare il comportamento osservabile (`0.9.6.0`).
- [x] Incorporare nell'applicazione una copia DDF valida come fallback, mantenendo espliciti origine e versione del pacchetto attivo (`0.9.6.0`).
- [x] Validare schema, duplicati, riferimenti, versioni e compatibilità con diagnostiche navigabili, senza lasciare l'editor in uno stato parziale (`0.9.6.0`).

### 0.9.6.1 — Lexer dichiarativo

- [x] Rendere configurabili identificatori, numeri, stringhe, escape, commenti, direttive, keyword, operatori, delimitatori e longest match (`0.9.6.1`).
- [x] Rilevare conflitti lessicali, regole irraggiungibili e costrutti non terminati già durante il caricamento del pacchetto (`0.9.6.1`).
- [x] Derivare classificazione, colori e completamento lessicale dalla stessa definizione (`0.9.6.1`).
- [x] Conservare token, intervalli sorgente, analisi incrementale e recovery necessari durante la digitazione incompleta (`0.9.6.1`).

### 0.9.6.2 — Grammatica caricabile e albero sintattico generico

- [x] Scegliere una notazione canonica EBNF con metadati JSON, evitando due rappresentazioni concorrenti della stessa regola (`0.9.6.2`).
- [x] Compilare e validare la grammatica al caricamento, segnalando alternative deterministiche e ricorsioni non gestibili (`0.9.6.2`).
- [x] Produrre un albero sintattico generico ma tipizzato tramite nomi di nodo e campi dichiarati dal pacchetto (`0.9.6.2`).
- [x] Conservare recovery con nodi mancanti, diagnostiche navigabili e intervalli sorgente esatti; il vecchio AST DDF resta temporaneamente una proiezione di compatibilità per i servizi IDE (`0.9.6.2`).
- [x] Migrare tutti i costrutti DDF correnti, incluso `else`, e verificarli contro corpus valido, non valido e incompleto (`0.9.6.2`).

### 0.9.6.3 — Servizi IDE derivati dal pacchetto

- [x] Alimentare Outline, folding, breadcrumb, selezione sintattica e navigazione dai metadati dei nodi.
- [x] Derivare completamento, Signature Help, hover, snippet e simboli dalle regole del pacchetto.
- [x] Rendere formatter, indentazione, coppie automatiche e Quick Fix configurabili senza duplicare la grammatica.
- [x] Aggiornare menu, impostazioni, icon bar e stato applicazione quando cambia il linguaggio attivo.
- [x] Mantenere cancellazione, versionamento degli snapshot e budget prestazionali con pacchetti utente realistici.

### 0.9.6.4 — Tipi, semantica e rappresentazione intermedia

- [x] Definire dichiarativamente tipi primitivi, conversioni, compatibilità, mutabilità, scope e firme.
- [x] Collegare le produzioni a un insieme versionato di operazioni semantiche sicure, non a codice host arbitrario.
- [x] Introdurre una rappresentazione intermedia comune consumata da diagnostica, interprete, debugger e futuri backend.
- [x] Distinguere errori del sorgente, errori nella definizione del linguaggio, operazioni non supportate dal motore ed errori runtime.
- [x] Prevedere estensioni native opzionali soltanto come plug-in espliciti, identificabili e autorizzabili separatamente.

### 0.9.6.5 — Runtime generico e libreria standard

- [x] Eseguire la rappresentazione intermedia tramite una VM indipendente dalla sintassi concreta.
- [x] Definire operazioni controllate per valori, scope, chiamate, flusso, array, strutture e funzioni host.
- [x] Spostare funzioni standard, firme e collegamenti runtime nel pacchetto DDF.
- [x] Conservare limiti di esecuzione, cancellazione, posizioni sorgente e predisposizione per breakpoint e ispezione variabili.

### 0.9.6.6 — Progetto e sistema di build

- [x] Separare il pacchetto di linguaggio dal manifest del progetto che sceglie sorgenti, entry point, dipendenze, backend e output (`0.9.6.6`).
- [x] Definire comandi distinti per convalida, Build, Run e Debug con diagnostiche navigabili (`0.9.6.6`).
- [x] Rendere il primo backend basato sulla VM generica e mantenere un contratto sostituibile per backend futuri (`0.9.6.6`).
- [x] Usare anche i buffer non salvati quando richiesto, senza scritture implicite o perdita delle modifiche (`0.9.6.6`).
- [x] Rendere riproducibili versione del linguaggio, opzioni e artefatti di build (`0.9.6.6`).

### 0.9.6.7 — Autonomia dell'utente e strumenti di verifica

- [x] Consentire installazione, selezione, aggiornamento e disattivazione dei pacchetti di linguaggio (`0.9.6.7`).
- [x] Fornire anteprima di tokenizzazione, albero sintattico, diagnostiche e formattazione su un playground isolato (`0.9.6.7`).
- [x] Aggiungere test dichiarativi al pacchetto e un validatore eseguibile prima dell'attivazione (`0.9.6.7`).
- [x] Documentare quali personalizzazioni sono puramente dichiarative e quali richiedono una nuova operazione del motore o un plug-in (`0.9.6.7`).
- [x] Preparare l'architettura del futuro editor assistito mantenendo JSON ed EBNF come formato autorevole e sempre utilizzabile (`0.9.6.7`).

### Criteri di completamento della 0.9.6

- il DDF corrente viene caricato dal proprio pacchetto e conserva i test di compatibilità;
- almeno un secondo linguaggio dimostrativo usa lessico, grammatica e sintassi differenti senza modificare il codice dell'editor;
- editor, parser, IntelliSense, formatter, diagnostica, VM e build consumano la stessa definizione versionata;
- un pacchetto non valido viene rifiutato con diagnostiche chiare e fallback sicuro;
- i pacchetti puramente dichiarativi non possono eseguire codice host arbitrario;
- build, Run e Debug dichiarano sempre linguaggio e configurazione effettivamente utilizzati.

## 0.10.0 — Variabili e call stack durante la pausa

- [x] Suddividere la icon bar monolitica in barre mobili per categoria, attivabili singolarmente da Visualizza e con visibilità, ordine, riga e posizione persistenti (`0.10.0`, incremento locale del 2026-09-06).
- [x] Aggiungere la palette Espressioni di controllo (Watch), con inserimento/rimozione di variabili, campi e indici costanti in sola lettura (incremento locale del 2026-09-06).
- [x] Aggiornare Watch alle pause, indicare che i valori sono dell'ultima pausa durante Continua e svuotarli alla fine; distinguere scope non disponibile, percorso non valido e limiti dello snapshot.
- [x] Estendere Watch alle espressioni pure senza effetti collaterali e alla persistenza separata per progetto (`0.10.0`).

- [x] Acquisire uno snapshot immutabile dello stato runtime a ogni breakpoint, con campi ed elementi copiati e limiti espliciti per profondità/dimensione.
- [x] Mostrare automaticamente parametri, variabili locali e globali con nome, tipo dichiarato e valore (`0.10.0`, incremento locale del 2026-09-06).
- [x] Distinguere lo scope corrente dagli scope dei chiamanti ancora attivi e aggiornare la palette Variabili quando si seleziona un frame (`0.10.0`, incremento locale del 2026-09-06).
- [x] Rendere espandibili array e istanze di strutture usando esclusivamente lo snapshot immutabile, senza esporre oggetti interni del runtime (`0.10.0`, incremento locale del 2026-09-06).
- [x] Mostrare il call stack attivo, ordinato dal frame corrente ai chiamanti, con navigazione al punto di chiamata tramite doppio clic o Invio (`0.10.0`, incremento locale del 2026-09-06).
- [x] Associare ogni frame e snapshot al documento sorgente corretto nel runtime multi-file (`0.11.0`).
- [x] Aggiornare e svuotare Watch e Variabili in modo coerente su Pausa, Continua, Stop e fine esecuzione (`0.10.0`, incremento locale del 2026-09-06).
- [x] Coprire valori primitivi, array, strutture, shadowing, chiamate annidate, espressioni pure e rifiuto degli effetti collaterali con test Core e smoke WinForms (`0.10.0`).

## 0.10.1 — Esecuzione passo-passo

- [x] Aggiungere Step Into per entrare nelle funzioni chiamate (`F11`, `0.10.1`).
- [x] Aggiungere Step Over per completare una chiamata restando nel frame corrente (`F10`, anticipato nella `0.10.0`).
- [x] Aggiungere Step Out per raggiungere il chiamante della funzione corrente (`Shift+F11`, anticipato nella `0.10.0`).
- [x] Evidenziare la prossima istruzione e sincronizzare i comandi Step Over/Step Out, le scorciatoie e la toolbar (incremento locale del 2026-09-06).
- [x] Integrare lo step con breakpoint, variabili, call stack, cicli e cancellazione (`0.10.1`).
- [x] Esporre Step Into, Step Over e Step Out nella icon bar con stati coerenti alla pausa (`0.10.1`).

## 0.11.0 — Runtime multi-file

Priorità anticipata: questa milestone inizia immediatamente dopo il
completamento della `0.10.1`, prima del blocco dedicato alla flowchart.

- [x] Implementare per DDF la visibilità di progetto: tutti i sorgenti selezionati dal manifest condividono funzioni, strutture e globali (`0.11.0`).
- [x] Mantenere `@@'...'` come direttiva di libreria/capacità e non come inclusione di file; l'appartenenza dei moduli è definita da `sources` nel manifest (`0.11.0`).
- [x] Costruire uno snapshot coerente di tutti i buffer, compresi quelli modificati ma non salvati, all'avvio del runtime (`0.11.0`).
- [x] Definire il caricamento runtime dei moduli del workspace e un unico grafo di dichiarazioni (`0.11.0`).
- [x] Risolvere funzioni, strutture e globali tra documenti indicizzati (`0.11.0`).
- [x] Rifiutare nomi runtime duplicati (`DDFB010`) e dipendenze globali in avanti/cicliche (`DDFB011`); l'inizializzazione segue file e dichiarazioni in ordine deterministico (`0.11.0`).
- [x] Rendere effettivi durante l'esecuzione i breakpoint per file introdotti nella `0.9.0.1`; Step Into/Over/Out cambiano visibilmente scheda e avanzano per istruzione anche nei file esterni (`0.11.0`).
- [x] Conservare file e intervalli sorgente nelle diagnostiche, nello stack, nelle pause e nella navigazione (`0.11.0`).
- [x] Aggiungere test end-to-end su workspace temporanei multi-file e buffer non salvati (`0.11.0`).

## 0.11.1 — Flowchart dinamica e didattica

La prima versione è una vista del controllo di flusso derivata dall'analisi del
codice. La digitazione resta la fonte autorevole; il disegno non esegue il
programma. La flowchart rimarrà una rappresentazione in sola lettura: modificarne
nodi o collegamenti non genererà né riscriverà automaticamente il codice.

- [x] Definire nel Core un modello di nodi e archi con intervalli sorgente e ruoli semantici, indipendente dal renderer (`0.11.1`, incremento Core del 2026-09-08).
- [x] Derivare ingresso, uscita, sequenze, condizioni, cicli, chiamate e ritorni dalla IR, senza riconoscere keyword tramite testo (`0.11.1`, incremento Core del 2026-09-08).
- [x] Mostrare etichette per i rami delle condizioni e distinguere il flusso di controllo dal grafo delle chiamate e dal flusso dei dati (`0.11.1`, primo renderer del 2026-09-08).
- [x] Limitare la vista alla funzione selezionata, navigare alle funzioni chiamate locali o multi-file tramite doppio clic e menu `ƒ`, e comprimere le sequenze lineari in gruppi espandibili senza perdere gli intervalli sorgente (`0.11.1`, incremento del 2026-09-12).
- [x] Aggiornare il modello in background con ritardo breve, cancellazione e scarto degli snapshot obsoleti tramite snapshot immutabili associati alla versione esatta del documento (`0.11.1`, incremento Core del 2026-09-08).
- [x] Preservare zoom, posizione e stabilità del layout durante l'applicazione degli snapshot nella palette grafica, ancorando il nodo corrente e usando la posizione relativa come fallback durante i cambi strutturali (`0.11.1`, incremento viewport del 2026-09-12).
- [x] Aggiungere alla palette una modalità Pan con icona a forma di mano, esclusiva rispetto al puntatore, per trascinare la viewport senza attivare o modificare i nodi (`0.11.1`, incremento del 2026-09-12).
- [x] Gestire codice incompleto mostrando nodi parziali o l'ultima vista valida chiaramente indicata come non aggiornata; segnalare costrutti non rappresentabili (`0.11.1`, incremento recovery del 2026-09-12).
- [x] Sincronizzare la selezione tra codice e nodi e permettere la navigazione al sorgente senza modificare testo o Undo (`0.11.1`, incremento selezione del 2026-09-12).
- [x] Collegare pausa e passi del debugger al nodo corrente usando lo snapshot sorgente effettivamente in esecuzione (`0.11.1`, incremento debugger/Flowchart del 2026-09-13).
- [x] Integrare la vista in una palette opzionale e in un controllo `FlowchartView` riutilizzabile, con comandi accessibili da menu e toolbar; il confezionamento nel futuro Editor SDK resta assegnato alla `0.11.2` (`0.11.1`, estrazione controllo del 2026-09-13).
- [x] Esporre un comando Modalità didattica On/Off sempre raggiungibile in entrambe le modalità, indipendente dal profilo sintattico; flowchart e strumenti ordinari di debug restano utilizzabili anche fuori dalla modalità didattica (`0.11.1`, comando in `Visualizza` e API/evento del controllo riutilizzabile, 2026-09-13).
- [x] Preservare documento, selezione, Undo e stato di debug al cambio di modalità, senza avviare esecuzioni o modificare permessi e impostazioni di compilazione (`0.11.1`, commutazione solo presentazionale, 2026-09-13).
- [x] Memorizzare la preferenza nel contesto dell'utente o dell'istanza host e sospendere gli aggiornamenti esclusivamente didattici quando disattivata (`0.11.1`, preferenza standalone e stato per istanza di `FlowchartView`, 2026-09-13).
- [x] Rendere la modalità didattica immediatamente utile con una guida contestuale per ogni tipo di nodo, enfasi cromatica sui rami della decisione selezionata e spiegazione del passo corrente con i valori runtime coinvolti (`0.11.1`, primo livello didattico del 2026-09-13).
- [x] Esportare l'intero diagramma in SVG vettoriale o JPEG per materiale didattico e documentazione, conservando layout, frecce, rami e stato didattico (`0.11.1`, incremento export del 2026-09-13).
- [x] Verificare rami annidati, cicli, ritorni anticipati, chiamate, errori transitori e budget di reattività con test Core e smoke UI (`0.11.1`, gate finale del 2026-09-13: 182 test Core, smoke WinForms completo e pipeline Flowchart complessa entro il target p95 di 50 ms).

Criteri di completamento: il diagramma rappresenta correttamente i costrutti
supportati, segue codice e debug senza spostamenti incontrollati, espone i propri
limiti e non peggiora la fluidità della digitazione. La modalità didattica è
commutabile in ogni contesto; successivi incrementi potranno aggiungere traccia
dei passi, valori sui nodi ed esercizi guidati.

## 0.11.2 — Editor incorporabile e SDK host iniziale

Estrarre un componente riutilizzabile dalla shell standalone. Il primo host
supportato sarà WinForms/.NET su Windows, coerente con l'implementazione corrente;
integrazioni WPF, native o web richiederanno adapter e verifiche dedicate. Il Core
rimarrà indipendente dalla tecnologia grafica. Questo SDK permette all'applicazione
ospitante di integrare il componente; la gestione di plug-in installabili resta
prevista nella `0.15`.

- [x] Estrarre la superficie centrale in un controllo pubblico `DdfEditorView` con contratto host `IDdfEditorSurface` versione `0.1`, senza esporre i controlli interni (`0.11.2`, primo incremento del 2026-09-13).
- [x] Pubblicare un servizio documentale indipendente dalla UI con identità stabile, versione del buffer, documenti virtuali/su disco, eventi di ciclo di vita e chiusura protetta dei documenti modificati; usare la stessa implementazione nella shell (`0.11.2`, secondo incremento del 2026-09-13).
- [x] Collegare `DdfEditorView` al servizio documentale con sincronizzazione bidirezionale e portare il contratto host alla versione `0.2`; estrarre dalla `MainForm` l'associazione fra identità del buffer, editor, scheda, selezioni e viewport (`0.11.2`, terzo incremento del 2026-09-13).
- [x] Separare servizi documentali e palette dalla MainForm, pubblicando contratti versionati dedicati (`0.11.2`, catalogo palette e contratto host `0.3` del 2026-09-13).
- [x] Fare usare alla shell standalone la stessa superficie `DdfEditorView`, associandola all'editor del documento attivo anche durante lo switch tra schede (`0.11.2`, primo incremento del 2026-09-13).
- [x] Fare convergere la shell sulle stesse API documentali, linguistiche e operative offerte agli integratori: Convalida/Compila attraversano `IDdfEditorBuildService`; Run/Debug attraversano `IDdfEditorExecutionService` e `IDdfEditorDebugService`; handler pubblici conservano manifest, file workspace non aperti, buffer non salvati, ricevuta, breakpoint, Watch, Call Stack, navigazione multifile e Flowchart (`0.11.2`, contratti host `0.30`-`0.31`, completato il 2026-09-26).
- [x] Rendere il cambio profilo disponibile direttamente all'utente anche nella shell embedded e fare convergere menu, `TryActivate`, `UseEmbedded` e sostituzione della sessione sulla stessa pipeline di conversione e aggiornamento dei servizi, senza persistenza implicita per le richieste dell'host (`0.12.1`, contratto host `0.32`, 2026-09-27).
- [x] Consentire all'host di registrare, mostrare, nascondere e attivare palette tramite ID stabili, inclusa la Flowchart; la shell usa lo stesso catalogo e ne persiste la visibilità (`0.11.2`, quarto incremento del 2026-09-13).
- [x] Consentire all'host di configurare tema del codice, font, zoom, word wrap, tabulazione e scorciatoie per singola istanza, senza accessi impliciti al profilo o al file system (`0.11.2`, quinto incremento e contratto host `0.4` del 2026-09-13).
- [x] Consentire all'host di scegliere atomicamente il pacchetto linguaggio per singola istanza, preservando il precedente in caso di errore e senza modificare il catalogo globale (`0.11.2`, sesto incremento e contratto host `0.5` del 2026-09-13).
- [x] Esporre un servizio di analisi indipendente dalla UI con richieste e snapshot versionati per diagnostiche, Outline e completamento; cancellare le richieste superate e fare usare alla pipeline principale della shell lo stesso servizio (`0.11.2`, settimo incremento e contratto host `0.6` del 2026-09-13).
- [x] Fare convergere il popup di completamento della shell su `DdfEditorAnalysisRequest` e `DdfEditorAnalysisSnapshot`, mantenendo un coordinatore separato per non interrompere diagnostiche e Outline e scartando risultati appartenenti a testo, cursore o linguaggio superati (`0.11.2`, ottavo incremento del 2026-09-13).
- [x] Introdurre `IDdfEditorCommandService` con ID stabili, metadati, stato dinamico, eventi ed esecuzione protetta; fare derivare la Command Palette della shell dallo stesso catalogo e portare il contratto host alla versione `0.7` (`0.11.2`, nono incremento del 2026-09-13).
- [x] Registrare nella superficie isolata i primi comandi puramente incorporabili (Undo, Redo, selezione, eliminazione, word wrap e focus) e notificare proattivamente nella superficie e nella shell le variazioni di stato dovute a testo, selezione, documento, folding, navigazione ed esecuzione/debug (`0.11.2`, decimo incremento del 2026-09-13).
- [x] Portare il contratto host alla versione `0.8` con analisi esplicita osservabile e navigazione alla definizione locale o tra buffer virtuali, senza dialoghi né accessi impliciti al file system; esporre i relativi comandi, snapshot, destinazioni ed eventi (`0.11.2`, undicesimo incremento del 2026-09-13).
- [x] Consentire all'host di fornire la propria strategia di persistenza per documenti e preferenze tramite snapshot immutabili, provider sostituibile, comandi espliciti e ripristino non distruttivo; portare il contratto host alla versione `0.9` (`0.11.2`, dodicesimo incremento del 2026-09-14).
- [x] Esporre stato, comando ed evento della modalità didattica nello SDK; il pulsante del `FlowchartView` resta raggiungibile anche quando l'host personalizza menu e toolbar (`0.11.2`, stato per istanza del 2026-09-13).
- [x] Realizzare una prima applicazione host WinForms separata con due istanze, profili tecnico/didattico, documenti virtuali, navigazione, Modalità didattica On/Off e provider di persistenza isolati; verificarne il lifecycle nello smoke dinamico (`0.11.2`, tredicesimo incremento del 2026-09-14).
- [x] Pubblicare `IDdfEditorExecutionService` con fotografia dei buffer host, Run/Stop cooperativo, stato, output, risultato e sessioni concorrenti isolate; portare il contratto host alla versione `0.10` e rendere l'esecuzione osservabile nella demo (`0.11.2`, quattordicesimo incremento del 2026-09-14).
- [x] Pubblicare `IDdfEditorDebugService` con breakpoint per identità documento, stati Running/Paused/Stopping, posizione sorgente, Continue/Stop e cleanup sicuro durante una pausa; portare il contratto host alla versione `0.11` e rendere il ciclo osservabile nella demo (`0.11.2`, quindicesimo incremento del 2026-09-14).
- [x] Estendere il debugger pubblico con Step Into, Step Over e Step Out, conservando lo stato per istanza e la navigazione sorgente tra buffer virtuali; portare il contratto host alla versione `0.12` e aggiungere i comandi alla superficie e alla demo (`0.11.2`, sedicesimo incremento del 2026-09-14).
- [x] Esporre snapshot immutabili di Call Stack, frame, variabili visibili, globali e valori strutturati a ogni pausa, con posizione documentale navigabile anche tra buffer virtuali; portare il contratto host alla versione `0.13` e visualizzare i dati nella demo (`0.11.2`, diciassettesimo incremento del 2026-09-14).
- [x] Aggiungere Watch pubbliche per espressioni pure sul frame selezionato e sulle globali dello snapshot, rifiutando chiamate, assegnazioni e mutazioni; portare il contratto host alla versione `0.14` e aggiungere un campo di valutazione alla demo (`0.11.2`, diciottesimo incremento del 2026-09-14).
- [x] Verificare persistenza e lifecycle dell'host durante una sessione di debug in pausa: salvare documenti, breakpoint rimappabili e testi Watch senza serializzare la VM; bloccare ripristino e sostituzione dei servizi durante Run/Debug e garantire il cleanup dei servizi posseduti (`0.11.2`, diciannovesimo incremento e contratto host `0.15` del 2026-09-14).
- [x] Consentire documenti virtuali senza percorsi fisici, senza letture o scritture implicite e con istanze isolate (`0.11.2`, servizio documentale del 2026-09-13).
- [x] Consentire la gestione di progetti salvati nei contenitori dell'host tramite snapshot immutabili, identità e metadati opachi, riferimento verificato al pacchetto linguistico, apertura non distruttiva, eventi e comandi; non imporre formato, percorso o protocollo (`0.11.2`, ventesimo incremento e contratto host `0.16` del 2026-09-14).
- [x] Esporre eventi e comandi per modifiche, selezione, diagnostiche, navigazione, build, esecuzione e debug; aggiungere una pipeline di build host in memoria condivisa con Run/Debug e snapshot immutabili dei buffer (`0.11.2`, ventunesimo incremento e contratto host `0.17` del 2026-09-21).
- [x] Isolare più istanze: nessuna intercettazione globale delle scorciatoie, stato condiviso involontario o scrittura implicita nel profilo dell'host; metadati delle scorciatoie per istanza, ownership dei servizi iniettati e assenza di persistenza automatica verificati da smoke dedicati (`0.11.2`, ventiduesimo incremento e contratto host `0.18` del 2026-09-21).
- [x] Definire un catalogo host per tipi, oggetti, funzioni, firme, documentazione e permessi, condiviso tramite snapshot immutabili e versionati da IntelliSense, type checker, Build, Run e Debug; le invocazioni usano delegate espliciti e i permessi mancanti producono `DDF409` (`0.11.2`, ventitreesimo incremento e contratto host `0.19` del 2026-09-22).
- [x] Esporre solo oggetti e operazioni registrati esplicitamente, con accesso sola lettura o lettura/scrittura, adattatori bidirezionali, durata `ExecutionSnapshot` o `Live` limitata alla singola esecuzione e nessuna reflection arbitraria dal sorgente; type checker e VM rifiutano le scritture non autorizzate con `DDF309`/`DDF411` (`0.11.2`, ventiquattresimo incremento e contratto host `0.20` del 2026-09-22).
- [x] Separare modalità sola scrittura/analisi, simulazione ed esecuzione con effetti sull'host: `AnalysisOnly` non avvia la VM né legge l'host, `Simulation` mantiene le scritture locali e usa delegate sostitutivi espliciti per le operazioni mutanti, `HostEffects` abilita le operazioni reali; il debug accetta soltanto le ultime due (`0.11.2`, venticinquesimo incremento e contratto host `0.21` del 2026-09-22).
- [x] Definire threading, chiamate asincrone, cancellazione, timeout, budget di chiamate/risorse e rilascio LIFO di `IDisposable`/`IAsyncDisposable`: `DdfHostInvocationContext` propaga identità, modalità, scadenza e token; `DDF413`/`DDF414`/`DDF415` distinguono limiti, timeout e cleanup. Documentare come requisito un processo worker separato per script non fidati, estensioni native o operazioni che non rispettano la cancellazione, poiché il runtime in-process non è una sandbox (`0.11.2`, ventiseiesimo incremento e contratto host `0.22` del 2026-09-22).
- [x] Esporre variabili e oggetti personalizzati con tipi, valori iniziali, accesso in lettura/scrittura e restituzione dei risultati all'host tramite snapshot profondamente distaccati dalle celle della VM; la demo mostra valori iniziali/finali e isolamento tra istanze (`0.11.2`, ventisettesimo incremento e contratto host `0.23` del 2026-09-24).
- [x] Supportare procedure richiamabili manualmente o da eventi espliciti dell'host tramite `InvokeAsync`, argomenti nominati e snapshot dell'origine; restituire tipo e valore profondamente distaccato, diagnosticare argomenti mancanti/superflui/non convertibili con `DDF416` e rifiutare invocazioni concorrenti sulla stessa istanza (`0.11.2`, ventottesimo incremento e contratto host `0.24` del 2026-09-24).
- [x] Offrire un contratto di accesso ai database tramite `IDdfHostDatabaseService`, operazioni registrate con statement e parametri nominati, risultati tipizzati, permessi, simulazione, cancellazione/timeout e identificatori di transazione opachi forniti dall'applicazione; connessioni, SQL concreto e credenziali non attraversano il confine dello script (`0.11.2`, ventinovesimo incremento e contratto host `0.25` del 2026-09-24).
- [x] Esporre funzioni matematiche e logiche tramite librerie host tipizzate registrate atomicamente e dimostrare algoritmi personalizzati che elaborano dati e aggiornano gli oggetti dell'applicazione, con permessi condivisi e comportamento distinto in Simulation/HostEffects (`0.11.2`, trentesimo incremento e contratto host `0.26` del 2026-09-24).
- [x] Definire quando applicare i risultati e come gestire gli errori parziali delle operazioni host tramite policy `Immediate`/`RollbackOnFailure`, riepilogo ordinato degli effetti e transazioni create dall'applicazione; Undo/Redo resta limitato al testo e non annulla azioni già eseguite (`0.11.2`, trentunesimo incremento e contratto host `0.27` del 2026-09-24).
- [x] Realizzare un'applicazione dimostrativa separata che legge dati da un database di prova, applica una regola personalizzata e restituisce risultati all'host, con completamento, debug e flowchart: `DDFLanguageEditor.DatabaseDemo` integra database in memoria dietro il contratto pubblico, Simulation, transazione con rollback, griglia dati, Output e sincronizzazione sorgente/Flowchart/debug (`0.11.2`, trentaduesimo incremento del 2026-09-24; contratto host invariato `0.27`).
- [x] Separare definitivamente il profilo embedded dall'IDE standalone: compilazione e avvio manuale sono nascosti, l'esecuzione nasce dagli eventi dell'host, `Ctrl+S` delega la persistenza all'applicazione e la nuova palette `API host` rende esplorabili controlli, proprietà, eventi, tipi, funzioni, database e permessi. IntelliSense e runtime supportano proprietà host esplicitamente scrivibili, inclusa la modifica sicura dei colori dei controlli (`0.11.2`, contratto host `0.28`, 2026-09-25).
- [x] Incapsulare la shell embedded nel controllo pubblico `DdfEmbeddedEditorControl`: l'host non configura più direttamente `MainForm`, mentre documenti virtuali, salvataggio, catalogo host, esecuzione e debug passano da un lifecycle WinForms unico e testato nella DatabaseDemo (`0.11.2`, contratto host `0.29`, 2026-09-25).
- [x] Separare `DDFLanguageEditor.Core`, `DDFLanguageEditor.WinForms` e il launcher `DDFLanguageEditor.App`; generare pacchetti NuGet beta locali, aggiungere `MinimalHost`, aggiornare la guida e verificare lifecycle, più istanze e pubblicazione standalone self-contained (`0.11.2`, 2026-09-25).

Criteri di completamento: un'applicazione separata incorpora l'editor senza
modificarne i sorgenti, espone proprie API documentate e fa eseguire uno script
sui propri oggetti simulati. Apertura, chiusura e più istanze preservano stato,
scorciatoie e risorse dell'host. La prova sarà proposta a un primo integratore
per validare tempi di integrazione e utilità prima di ampliare gli adapter.

## 0.12 — Definizione completa del linguaggio DDF

Questa linea non riguarda il formato generico dei pacchetti, già introdotto
nella `0.9.6`, ma la progettazione intenzionale del DDF come linguaggio autonomo,
originale, semplice e coerente. Il risultato dovrà essere una specifica leggibile,
una grammatica formale e un pacchetto eseguibile che descrivono lo stesso linguaggio.

### 0.12.0 — Identità e principi del linguaggio

- [x] Definire gli obiettivi del DDF, i casi d'uso prioritari e ciò che lo distingue dai linguaggi esistenti (`DDF_LANGUAGE_DESIGN.md`, baseline del 2026-09-26).
- [x] Stabilire i principi di leggibilità, semplicità, esplicitezza, sicurezza e prevedibilità (`DDF_LANGUAGE_DESIGN.md`).
- [x] Inventariare ogni comportamento storico, separando ciò che viene conservato, modificato o eliminato (`DDF_CURRENT_LANGUAGE_INVENTORY.md`).
- [x] Confrontare in modo mirato DDF con C e con altri linguaggi affini, evitando compatibilità accidentali (`DDF_CURRENT_LANGUAGE_INVENTORY.md`).
- [x] Registrare decisioni e alternative rifiutate in documenti di design versionati (`DDF_LANGUAGE_DECISIONS.md`, decisioni DDF-0001–DDF-0005).

### 0.12.1 — Lessico e sintassi definitiva

- [ ] Definire caratteri ammessi, identificatori, letterali, commenti, direttive, keyword, operatori e delimitatori.
  - [x] Fissare il nucleo lessicale: identificatori Unicode case-sensitive, cifre ASCII, booleani minuscoli, stringhe su una riga, escape chiusi e commenti (`DDF_LEXICAL_SPEC.md`, 2026-09-26).
  - [x] Chiudere le direttive insieme al modello moduli della `0.12.3`: `@@'Nome'` resta una richiesta legacy deprecata di libreria/capacità e non include sorgenti o moduli (`DDF_MODULE_SYSTEM_SPEC.md`, DDF-0016, 2026-09-29).
  - [x] Chiudere keyword e delimitatori del nucleo, senza alias impliciti (`DDF_SYNTAX_SPEC.md`, 2026-09-27).
  - [x] Chiudere l'insieme degli operatori del nucleo: identità DDF per `<<`/`>>`, logica leggibile con `!`, `&&`, `||`, `^^`, confronti ordinari e rimozione delle forme beta ambigue (`DDF_OPERATOR_SPEC.md`, 2026-09-26).
- [x] Definire precedenza, associatività e forme prefisse, infisse e postfisse senza ambiguità (`DDF_OPERATOR_SPEC.md`).
- [ ] Specificare dichiarazioni, espressioni, blocchi, controllo di flusso, funzioni, strutture e collezioni.
  - [x] Fissare unità di compilazione, globali dichiarative, funzioni, strutture, espressioni e corpi di controllo obbligatoriamente racchiusi tra graffe (`DDF_SYNTAX_SPEC.md`, 2026-09-27).
  - [ ] Chiudere la sintassi delle collezioni insieme alla semantica di array e `dict` nella `0.12.2`.
- [x] Eliminare contraddizioni e alias provvisori dal nucleo eseguibile, mantenendo la prima bozza soltanto come documento storico esplicitamente non normativo (`DDF_SYNTAX_CONFORMANCE.md`, 2026-09-27).
- [x] Allineare EBNF, metadati del pacchetto, formatter, snippet, Quick Fix ed esempi canonici (`DDF_SYNTAX_CONFORMANCE.md` e corpus `samples/language-profiles`, 2026-09-27).
  - [x] Rendere l'EBNF il validatore normativo per analisi, Build e test dichiarativi mantenendo separato il recupero dell'AST storico (2026-09-27).
- [x] Verificare profili DDF, didattico italiano e ispirato al C sullo stesso corpus e sulle stesse API host, confrontando IR semantica e risultato runtime (`DDF_LANGUAGE_PROFILES.md`, 2026-09-27).
- [x] Localizzare diagnostiche, documentazione, snippet ed etichette della flowchart nei profili didattici (`0.12.1`, 2026-09-27).
  - [x] Fornire il pacchetto didattico italiano con keyword, booleani, snippet, metadati IDE, messaggi diagnostici e spiegazioni della Flowchart localizzati; mantenere separato il messaggio tecnico stabile usato da motore e Quick Fix dal testo presentato all'utente.
  - [x] Fare applicare immediatamente alla Flowchart etichette dei rami, testo dell'uscita e guida didattica del pacchetto attivo, senza riavvio e senza modificare la semantica della IR.
- [x] Convertire dinamicamente le schede aperte durante la selezione di un profilo equivalente, usando ruoli semantici anziché sostituzioni testuali, preservando commenti/stringhe/identificatori, senza salvataggi impliciti e con Undo/Redo per documento (`0.12.1`, 2026-09-27). Una futura conversione dell'intero workspace non aperto dovrà invece avere anteprima e conferma prima di scrivere file su disco.
- [x] Rendere il DDF l'unico linguaggio incluso e il fallback incorporato; distribuire italiano e sintassi familiare C come pacchetti iniziali installati nel catalogo utente, rimuovendo selezione e caricamento diretto fuori dal Gestore pacchetti (`0.12.1`, 2026-09-27).

### 0.12.2 — Tipi e semantica definitiva

- [x] Formalizzare tipi primitivi e composti, assenza di inferenza dichiarativa, conversioni, mutabilità, scope e durata dei valori (`DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-28).
- [x] Definire assegnazione, confronto, uguaglianza, verità, overflow, valori assenti ed errori (`DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-28).
- [x] Specificare chiamate, parametri, valori restituiti ed esclusione esplicita degli overload (`DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-28).
- [x] Stabilire regole di inizializzazione, divieto di shadowing, visibilità e diagnostiche semantiche (`DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-28).
- [x] Associare ogni costrutto alle operazioni sicure della IR e dichiarare le capacità non supportate (`DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-28).
- [x] Vincolare ogni tipo eseguibile a un mapping CLR/C# deterministico accettato da Roslyn, mantenendo distinti compatibilità di rappresentazione e accesso autorizzato alle API .NET (`DDF_TYPE_SYSTEM_SPEC.md`, DDF-0013, 2026-09-28).
- [x] Eseguire l'audit trasversale di compatibilità con .NET 10/Roslyn e fissare i gate per IR bound, mapping, runtime ABI, nomi, riferimenti, emit, PDB, packaging e confronto differenziale (`ROSLYN_COMPATIBILITY_AUDIT.md`, DDF-0014, 2026-09-28).
- [x] Allineare pacchetto, type checker, VM e servizi IDE ai tredici gap di conformità censiti nella specifica, inclusi cicli di strutture e mapping backend versionato (`0.12.2`, 2026-09-29).
  - [x] Chiudere i gap semantici di pacchetto/checker: `dict` escluso dal DDF, `char` non numerico, array monodimensionali, niente shadowing, ritorno su ogni percorso, uguaglianza vietata per composti, indici stringa in sola lettura e contenimento strutture aciclico; aggiungere il primo corpus negativo eseguibile (`0.12.2`, 2026-09-28).
  - [x] Allineare valori realmente memorizzati e VM: promozione `int -> float` su inizializzazioni, assegnazioni, argomenti, campi e ritorni; aritmetica intera checked senza passaggio da `double`; divisione intera verso zero; potenze intere non negative; diagnostiche stabili per overflow e risultati floating non finiti (`0.12.2`, 2026-09-28).
- [x] Creare il corpus normativo dei tipi con manifesto e schema versionati, copertura completa dei sorgenti, diagnostiche semantiche e oracoli runtime CLR; dichiarare VM e futuro backend Roslyn nello stesso contratto differenziale (`0.12.2`, 2026-09-29).
  - [ ] Collegare il runner Roslyn allo stesso manifesto quando il backend verticale sarà introdotto nella `0.13`; ogni divergenza rispetto all'oracolo VM bloccherà il rilascio.

### 0.12.3 — Moduli e libreria standard

- [x] Consolidare il modello multi-file sperimentato nella `0.11.0` in una semantica formale dei moduli (`DDF_MODULE_SYSTEM_SPEC.md`, DDF-0016, 2026-09-29).
- [x] Decidere la forma definitiva di import/include: nessun include testuale; `@@'...'` è una direttiva legacy deprecata e le dipendenze appartengono al manifesto (`DDF_MODULE_SYSTEM_SPEC.md`, DDF-0016, 2026-09-29).
- [x] Definire esportazioni, globali, ordine di inizializzazione, dipendenze cicliche e risoluzione dei nomi (`DDF_MODULE_SYSTEM_SPEC.md`, 2026-09-29).
  - [x] Rendere espliciti identità, versione ed export del modulo; validare API pubblica, ordine globale deterministico e auto-dipendenza (`ddf-project.json` schema `1.1`, 2026-09-29).
  - [x] Implementare il resolver di dipendenze trans-modulo, la visibilità non transitiva dei soli export e il rifiuto dei cicli fra moduli; registrare l'intero grafo nella ricevuta riproducibile (`DDF_MODULE_SYSTEM_SPEC.md`, DDF-0017, 2026-09-29).
- [x] Progettare namespace e packaging senza confonderli con cartelle o dettagli dell'editor (`0.12.3`, 2026-09-29).
  - [x] Riservare `.ddf/modules/<id>/<version>` come store locale sostituibile, escluso da glob e indice workspace e privo di significato semantico (`0.12.3`, 2026-09-29).
  - [x] Qualificare funzioni, strutture e globali nella VM tramite identità/versione del modulo, mantenendo visibili soltanto gli export diretti e permettendo nomi privati uguali (`0.12.3`, 2026-09-29).
  - [x] Definire `.ddfmod` come archivio sorgente deterministico e implementare il gestore Core con staging, rollback, versioni affiancate, sostituzione esplicita, rimozione esatta e difese da path traversal (`DDF_MODULE_PACKAGE_FORMAT.md`, DDF-0018, 2026-09-29).
  - [x] Integrare il gestore moduli nella UI standalone con anteprima non distruttiva di identità, versione, linguaggio, export e dipendenze, creazione, installazione/sostituzione, convalida e rimozione (`0.12.3`, 2026-09-29).
  - [x] Alimentare analisi live, diagnostica semantica/tipi, completamento, Signature Help e navigazione con i soli export delle dipendenze dirette, senza inserire lo store tra i file del workspace; aggiungere regressioni sui falsi `DDF201`/`DDF312` (`0.12.3`, 2026-09-29).
  - [x] Definire una capability host esplicita, revocabile e a privilegi minimi per lo store embedded: radice assoluta e permessi distinti di lettura/convalida, installazione, rimozione e creazione pacchetti; per impostazione predefinita menu e toolbar restano assenti (`0.12.3`, contratto host `0.33`, DDF-0020, 2026-09-29).
- [x] Definire la libreria standard minima, le sue convenzioni e il comportamento degli errori: `ddf.standard@0.12.3` espone cinque funzioni con firme, effetti e diagnostiche dichiarati, schema `1.1`, adapter `v3` e confine esplicito da moduli/API host (`DDF_STANDARD_LIBRARY_SPEC.md`, DDF-0019, 2026-09-29).

### 0.12.4 — Specifica verificabile e migrazione

- [x] Pubblicare una specifica DDF autonoma con sezioni normative ed esempi non normativi: `DDF_LANGUAGE_SPEC.md` consolida lessico, grammatica, tipi, operatori, controllo di flusso, moduli, runtime, libreria standard e confini host/Roslyn senza dipendere dalle bozze storiche (`0.12.4`, DDF-0021, 2026-09-29).
- [ ] Creare corpus completi di programmi validi, non validi, incompleti e multi-file.
- [ ] Rendere eseguibili gli esempi normativi tramite i test dichiarativi del pacchetto.
- [ ] Definire versionamento del linguaggio, compatibilità, deprecazioni e migrazioni automatiche possibili.
- [ ] Documentare le differenze rispetto alle beta e offrire una guida di aggiornamento dei sorgenti.

### 0.12.5 — DDF Language Candidate

- [ ] Congelare temporaneamente grammatica e semantica per un ciclo completo di utilizzo reale.
- [ ] Verificare editor, IntelliSense, formatter, diagnostica, debugger, runtime e build sul corpus normativo.
- [ ] Raccogliere e classificare i problemi senza modificare implicitamente la specifica.
- [ ] Accettare cambiamenti incompatibili soltanto tramite decisione registrata e nuova versione del linguaggio.
- [ ] Dichiarare il candidato pronto per la stabilizzazione `1.0` solo con tutti i gate superati.

### Criteri di completamento della 0.12

- specifica, EBNF, pacchetto DDF e comportamento runtime non presentano divergenze note;
- ogni keyword, operatore, tipo, costrutto e funzione standard ha semantica ed esempi espliciti;
- il corpus normativo viene eseguito automaticamente e include casi negativi e multi-file;
- compatibilità, deprecazioni e migrazione dalle beta sono documentate;
- nessun dettaglio essenziale del DDF dipende esclusivamente dal codice dell'editor.

## 0.13 — Compilatore, linker, loader e CLI

Costruire una toolchain utilizzabile indipendentemente dall'editor, dopo aver
consolidato la specifica DDF. Il backend principale sarà .NET + Roslyn: IR comune
verso C# generato, poi assembly IL e runtime CLR. La VM DDF rimane un riferimento
durante la transizione; la sua conservazione successiva richiede un caso d'uso
esplicito. Il collegamento iniziale usa riferimenti tra assembly e risoluzione
delle dipendenze .NET, senza richiedere un linker nativo proprietario.

Questa toolchain appartiene esclusivamente all'applicazione standalone e alla
CLI. I pacchetti `DDFLanguageEditor.Core` e `DDFLanguageEditor.WinForms` restano
compiler-free; l'embedded conserva validazione e preparazione IR in memoria per
Run/Debug, ma esegue sempre scripting interpretato e non produce assembly.

Prima del backend completo, realizzare un prototipo verticale dello stesso
programma come script integrato e EXE autonomo: includere una chiamata host,
un errore, un breakpoint e il confronto con la VM. Roslyn non fornisce
automaticamente il debugger del nostro linguaggio: mappatura e controllo della
pausa sono attività esplicite della milestone.

Il backend può iniziare soltanto dopo i gate R1–R6 di
`ROSLYN_COMPATIBILITY_AUDIT.md`; non sono ammesse traduzioni testuali dirette.

### 0.13.0 — Contratti e formato compilato

- [ ] Creare un assembly compilatore Roslyn separato, referenziabile soltanto da standalone e CLI; aggiungere un test architetturale che vieti riferimenti a Roslyn/compilatore da Core e WinForms.
- [ ] Separare nel contratto SDK `Validate/PrepareScript` dall'attuale nome storico Build e non esporre mai il servizio di compilazione tramite `DdfEditorView` o `DdfEmbeddedEditorControl`.
- [ ] Definire unità di compilazione, interfacce esportate e separazione tra sorgente, IR e artefatti persistenti.
- [ ] Definire assembly .NET, manifest versionato, dipendenze, simboli PDB e mappe verso profilo e sorgente originali.
- [ ] Specificare compatibilità con runtime .NET e SDK host, diagnostiche per artefatti incompatibili e limiti di caricamento.

### 0.13.1 — Compilazione separata

- [ ] Compilare singoli moduli e librerie mantenendo i controlli della pipeline condivisa.
- [ ] Evolvere la IR strutturale `1.0` in una IR bound `2.0` con simboli stabili, tipo di ogni espressione, conversioni esplicite, l-value e chiamate risolte.
- [ ] Generare alberi C# dalla IR bound e compilare con Roslyn, usando esclusivamente i mapping CLR versionati dei tipi e conservando la semantica DDF di valori, conversioni, overflow, non-null, scope e ordine di valutazione.
- [ ] Introdurre una runtime ABI DDF indipendente dalla UI per controlli numerici, errori, I/O, budget, cancellazione, `end` e bridge delle capacità host.
- [ ] Applicare name mangling deterministico e impedire che identificatori, stringhe o direttive DDF vengano inseriti come sorgente C# non validato.
- [ ] Fissare versione Roslyn/C#, reference assemblies, nullable, overflow, ottimizzazione e determinismo nella ricevuta di build.
- [ ] Riportare diagnostiche e punti di pausa al codice originale e implementare l'ispezione delle variabili senza esporre nomi temporanei generati.
- [ ] Conservare diagnostiche e informazioni di debug per file, riga, simbolo e scope.
- [ ] Rendere riproducibili gli artefatti e definire invalidazione della cache per sorgenti, pacchetti e opzioni.
- [ ] Verificare equivalenza osservabile tra esecuzione da sorgente e da modulo compilato.
- [ ] Confrontare il backend .NET e la VM su risultati, errori e chiamate host prima di sostituire il percorso predefinito.
- [ ] Emettere Portable PDB con sequence point sui documenti DDF originali e verificare breakpoint, stepping, call stack e variabili contro il debugger VM.

### 0.13.2 — Linker e loader

- [ ] Risolvere import ed export tra moduli compilati separatamente, segnalando simboli mancanti, duplicati o incompatibili.
- [ ] Costruire il programma collegato con dipendenze e ordine di inizializzazione deterministici.
- [ ] Caricare ed eseguire il programma senza richiedere i sorgenti originali o l'editor.
- [ ] Validare struttura, istruzioni e riferimenti degli artefatti prima dell'esecuzione.
- [ ] Conservare breakpoint, call stack e ispezione delle variabili tramite mappe di debug opzionali.
- [ ] Definire caricamento/scaricamento degli assembly nel processo standalone/CLI; per codice non fidato usare isolamento di processo e permessi del sistema operativo. L'host embedded non carica artefatti DDF compilati.

### 0.13.3 — CLI e distribuzione

- [ ] Esporre comandi CLI di convalida, build, run, test e packaging basati sullo stesso Core dell'IDE.
- [ ] Definire codici di uscita, diagnostiche leggibili da strumenti e uso in CI.
- [ ] Pubblicare EXE .NET self-contained per un sistema e un'architettura espliciti, con runtime, librerie e manifest necessari; valutare separatamente il file singolo.
- [ ] Bloccare l'esportazione standalone quando mancano implementazioni delle API richieste all'host.
- [ ] Separare dipendenze di compilazione Roslyn/SDK da dipendenze di esecuzione del programma pubblicato.
- [ ] Includere licenze e avvisi dei componenti effettivamente redistribuiti, distinguendo sorgenti MIT, binari Windows e librerie di terzi.
- [ ] Mantenere Native AOT e altri backend come estensioni successive, verificando i limiti su caricamento e generazione dinamica di codice.

### 0.13.4 — Riferimenti a librerie esterne

Consentire a progetti standalone e script embedded di usare librerie
installate nel sistema o fornite da terzi senza permettere agli script di caricare
arbitrariamente file, assembly o simboli nativi. Il progetto dichiara una
dipendenza logica; il resolver del contesto decide se e come soddisfarla.

- [ ] Estendere il manifest di progetto con riferimenti versionati a librerie gestite .NET, componenti COM/ActiveX registrati, librerie native e servizi forniti dall'host, distinguendone identità, versione, architettura, origine e capacità richieste.
- [ ] Introdurre un resolver sostituibile: in standalone risolve riferimenti di compilazione da origini configurate e autorizzate dall'utente; in embedded l'applicazione host approva o rifiuta capacità e fornisce adapter runtime, senza consegnare assembly al compilatore.
- [ ] Evitare ricerca implicita e non deterministica nell'intero sistema: produrre un lockfile con versione, percorso risolto, hash e dipendenze transitive per build riproducibili.
- [ ] Importare nello script soltanto API esplicitamente descritte o adattate, alimentando con gli stessi metadati type checker, IntelliSense, documentazione, runtime e debugger; nessuna reflection o `P/Invoke` arbitraria dal sorgente DDF.
- [ ] Supportare per prime le librerie .NET compatibili con il runtime scelto; gestire DLL native successivamente tramite adapter tipizzati o worker isolato, verificando ABI, architettura, ownership della memoria, callback ed errori.
- [ ] Aggiungere un catalogo Windows opzionale per componenti COM registrati e Type Library, inclusi scenari legacy come Microsoft ActiveX Data Objects (`ADODB`), generando o caricando un interop adapter senza esporre direttamente ProgID, CLSID o registro allo script.
- [ ] Per COM verificare registrazione per macchina/utente, versione della Type Library, compatibilità x86/x64, apartment STA/MTA, rilascio deterministico degli oggetti e disponibilità sul sistema di destinazione.
- [ ] Preferire provider .NET gestiti per le nuove integrazioni dati; mantenere COM/ADO come compatibilità esplicita per sistemi esistenti, con credenziali e stringhe di connessione fornite dall'host o da un archivio protetto e mai incorporate nel sorgente.
- [ ] Applicare permessi, attendibilità, firma/hash, timeout, cancellazione e limiti di risorse anche alle chiamate esterne; una libreria non fidata o non interrompibile deve essere eseguita fuori processo.
- [ ] Rendere diagnostici e navigabili riferimenti mancanti, versioni incompatibili, conflitti, architetture errate e simboli non esposti, senza effettuare fallback silenziosi.
- [ ] Definire packaging e distribuzione: distinguere librerie da incorporare, dipendenze già presenti nel sistema e componenti forniti dall'host, rispettando licenze e divieti di redistribuzione.
- [ ] Verificare lo stesso contratto tipizzato nei due contesti: build/esecuzione compilata standalone e convalida/esecuzione interpretata embedded, includendo debug, aggiornamento della libreria, revoca di un permesso e assenza della dipendenza.

### Criteri di completamento della 0.13

- almeno un programma multi-file viene compilato separatamente, collegato ed eseguito fuori dall'IDE;
- esecuzione da sorgente e da artefatto produce gli stessi risultati sul corpus normativo;
- build ripetute con gli stessi input producono artefatti deterministici;
- moduli incompatibili o malformati vengono rifiutati con diagnostiche chiare;
- riferimenti esterni producono lo stesso contratto tipizzato, ma vengono risolti per la compilazione soltanto nello standalone e come adapter/capacità runtime nell'embedded;
- debug e CLI condividono le regole del pacchetto senza implementazioni parallele del linguaggio.

## 0.14 — DDF Language Package Studio

Il Package Studio sarà inizialmente integrato nell'editor per riusare anteprima,
diagnostiche e gestione dei pacchetti. La logica di authoring resterà però in un
Core indipendente dalla UI, così da poter offrire successivamente anche un
eseguibile autonomo. Non sarà un plug-in e JSON/EBNF resteranno i formati autorevoli.

### 0.14.0 — Core di authoring indipendente

- [ ] Modellare progetto di authoring, modifiche, validazione, migrazioni e generazione del pacchetto senza dipendenze WinForms.
- [ ] Conservare commenti e contenuto non riconosciuto quando possibile, evitando riscritture distruttive.
- [ ] Produrre anteprime e diff prima di applicare trasformazioni strutturali.
- [ ] Rendere ogni operazione annullabile e coperta da test su file temporanei.

### 0.14.1 — Editor assistito integrato

- [ ] Creare e clonare un linguaggio da modello vuoto, dal DDF o da un pacchetto installato.
- [ ] Offrire editor guidati per manifest, lessico, keyword, operatori, tipi, semantica, formatter, IntelliSense, runtime, build e libreria standard.
- [ ] Aggiungere un editor EBNF con diagnostiche, riferimenti e anteprima dell'albero sintattico.
- [ ] Integrare playground e test dichiarativi con aggiornamento immediato e navigazione agli errori.
- [ ] Consentire sempre la visualizzazione e modifica diretta dei JSON e dell'EBNF sottostanti.
- [ ] Aggiornare menu, Command Palette e icon bar con stato e comandi coerenti.

### 0.14.2 — Validazione e distribuzione

- [ ] Validare dipendenze tra sezioni, versioni del motore, operazioni richieste e copertura minima dei test.
- [ ] Confrontare una nuova versione con quella precedente e segnalare modifiche incompatibili.
- [ ] Generare archivi `.ddflang` riproducibili con manifest, checksum e report di convalida.
- [ ] Installare il risultato soltanto dopo test superati, senza sovrascrivere una versione funzionante.
- [ ] Preparare template, esempi e una guida completa alla pubblicazione manuale.

### 0.14.3 — Distribuzione autonoma opzionale

- [ ] Separare la shell del Package Studio dal Core condiviso senza duplicare la logica.
- [ ] Valutare un eseguibile autonomo self-contained per autori che non necessitano dell'intero editor.
- [ ] Garantire interoperabilità completa tra progetto di authoring, editor integrato e applicazione autonoma.
- [ ] Mantenere utilizzabile il flusso manuale JSON/EBNF e da riga di comando.

### Criteri di completamento della 0.14

- un utente può creare, provare, convalidare, versionare e impacchettare un linguaggio senza modificare il codice dell'editor;
- nessuna operazione visuale rende il pacchetto illeggibile o dipendente dalla UI;
- errori e cambiamenti incompatibili sono mostrati prima dell'installazione;
- il pacchetto generato supera lo stesso loader e gli stessi test usati dall'applicazione.

## 0.15 — Extension SDK e host per plug-in

I plug-in sono distinti dai pacchetti dichiarativi e dal Package Studio. Servono
soltanto per nuove operazioni eseguibili, backend o accessi controllati al sistema
host che non possono essere descritti in modo sicuro come dati.

### 0.15.0 — Contratto e modello di sicurezza

- [ ] Definire manifest, identità dell'editore, versione, compatibilità, hash e dipendenze del plug-in.
- [ ] Definire capacità granulari per file, rete, processi, dispositivi, UI e codice nativo.
- [ ] Adottare negazione predefinita, consenso esplicito, revoca e registro delle autorizzazioni.
- [ ] Separare errori del plug-in da errori del linguaggio, del progetto e del motore.

### 0.15.1 — SDK e punti di estensione

- [ ] Pubblicare API versionate per operazioni semantiche/runtime, funzioni host e backend di build.
- [ ] Pubblicare adapter e resolver versionati per trasformare librerie esterne autorizzate in API DDF tipizzate, senza esporre direttamente loader, reflection o puntatori allo script.
- [ ] Evitare l'accesso diretto agli oggetti interni dell'editor e della VM.
- [ ] Fornire template, esempi minimi, test contract e controlli di compatibilità.
- [ ] Definire lifecycle, cancellazione, limiti di risorse e diagnostica dei plug-in.

### 0.15.2 — Plugin host e gestione utente

- [ ] Valutare e preferire isolamento fuori processo per codice non incorporato.
- [ ] Installare, aggiornare, abilitare, disabilitare e rimuovere plug-in senza confonderli con i pacchetti linguaggio.
- [ ] Mostrare provenienza, capacità richieste, stato, errori e impatto sul progetto.
- [ ] Impedire che l'apertura di un sorgente o di un pacchetto attivi implicitamente codice eseguibile.
- [ ] Coprire crash, timeout, revoca, incompatibilità e aggiornamenti con test di integrazione.

### Criteri di completamento della 0.15

- un pacchetto dichiarativo continua a non poter eseguire codice host arbitrario;
- ogni plug-in attivo è identificato, compatibile e autorizzato per capacità esplicite;
- un plug-in guasto o revocato non compromette documenti, impostazioni o fallback del linguaggio;
- build e runtime registrano esattamente plug-in e versioni coinvolti.

## 0.16 — Assistente IA locale e cloud

Offrire assistenza alla scrittura, spiegazione degli errori, creazione di test e
supporto didattico sia nell'editor standalone sia nel componente incorporato.
Il servizio sarà opzionale e indipendente dal backend di esecuzione .NET/Roslyn.
Fornitori previsti da verificare durante l'implementazione: server locali come
LM Studio/Ollama e servizi cloud come OpenAI (GPT), Anthropic (Claude) e Moonshot
(Kimi). I nomi indicano integrazioni pianificate, non compatibilità già implementata.

### 0.16.0 — Contratto e contesto condiviso

- [ ] Definire un adapter per fornitore con modello, endpoint, autenticazione, streaming, cancellazione, timeout e capacità dichiarate.
- [ ] Gestire differenze tra API, limiti di contesto, output strutturati e tool calling senza presumere protocolli identici.
- [ ] Costruire il contesto da profilo sintattico, esempi, codice selezionato, diagnostiche e catalogo delle API host.
- [ ] Separare generazione di codice, spiegazioni e supporto didattico da esecuzione e autorizzazioni dell'host.

### 0.16.1 — Connessioni locali e cloud

- [ ] Consentire scelta esplicita di fornitore e modello e un test di connessione; nessun passaggio automatico da locale a cloud.
- [ ] Esporre la configurazione nella shell standalone e tramite SDK host, con stato per istanza e indicazione del destinatario attivo.
- [ ] Conservare credenziali fuori da sorgenti, pacchetti, log e manifest dei progetti; supportare un gestore di credenziali fornito dall'host.
- [ ] Mostrare e limitare il contesto inviato; escludere segreti e dati del database per impostazione predefinita, lasciando all'host la selezione dei dati autorizzati.
- [ ] Gestire indisponibilità, limiti del servizio e modelli locali troppo onerosi mantenendo reattivo e utilizzabile l'editor senza IA.
- [ ] Mostrare consumo token e stime di costo quando disponibili, distinguendo stime da addebiti effettivi e rendendo configurabili i budget.

### 0.16.2 — Assistenza verificabile

- [ ] Proporre modifiche con anteprima, controllo della revisione del documento e applicazione annullabile su conferma dell'utente.
- [ ] Validare le proposte con parser e type checker del profilo attivo, senza equiparare validità sintattica a correttezza dell'algoritmo.
- [ ] Eseguire test o azioni host solo tramite comandi espliciti e autorizzati, con risultati distinguibili dalle spiegazioni generate.
- [ ] In modalità didattica offrire spiegazioni e suggerimenti graduati; derivare sempre la flowchart dal codice analizzato.
- [ ] Verificare adapter con servizi simulati e prove opt-in reali, coprendo cancellazione, contesto obsoleto, credenziali e isolamento tra istanze.

Criteri di completamento: almeno un fornitore locale e uno cloud funzionano con
lo stesso contratto in standalone e integrato; nessuna trasmissione cloud è
implicita; l'utente può verificare le modifiche e continuare a lavorare senza IA.
Gli altri adapter saranno aggiunti in base alle richieste degli integratori,
verificando API, termini del servizio e licenze dei modelli al momento dell'uso.

## Dopo 0.16 — Backend aggiuntivi e preparazione della 1.0

- [ ] Estendere il manifest di progetto di base con dipendenze, configurazioni e distribuzione di pacchetti riutilizzabili.
- [ ] Valutare backend compilati separati senza accoppiare l'editor a uno specifico compilatore.
- [ ] Definire i gate finali di compatibilità, distribuzione, documentazione e supporto per `1.0.0`.

## Dopo 1.0 — Portabilità dell'SDK e compatibilità degli host

Questa fase resta intenzionalmente l'ultimo ampliamento della roadmap: prima si
stabilizzano contratto, runtime, sicurezza, distribuzione e integrazione
WinForms. La compatibilità riguarda il linguaggio e la tecnologia
dell'applicazione ospitante, non i profili sintattici caricati nell'editor e non
implica supporto nativo ai linguaggi imitati dai pacchetti DDF.

- [ ] Pubblicare pacchetti SDK separati e versionati per Core e WinForms, senza richiedere un riferimento all'eseguibile standalone.
- [ ] Mantenere C#/.NET come integrazione di riferimento e aggiungere demo e contract test per host VB.NET e F#.
- [ ] Validare l'incorporamento in WPF tramite un adattatore ufficiale, coprendo focus, tastiera, DPI, temi, ridimensionamento e ciclo di vita.
- [ ] Valutare adattatori grafici dedicati per WinUI, Avalonia e MAUI senza duplicare analisi, runtime, debug o catalogo host.
- [ ] Definire un protocollo fuori processo, versionato e capability-based per applicazioni non-.NET come C++ nativo, Delphi, Java, Python, Qt ed Electron.
- [ ] Separare chiaramente la matrice supportata da integrazioni sperimentali, indicando sistema operativo, runtime, architettura, framework UI e versione minima.
- [ ] Automatizzare build, smoke test e applicazioni campione per ogni combinazione dichiarata supportata.
- [ ] Valutare la portabilità multipiattaforma del Core separatamente dalla UI: nessun supporto Linux o macOS verrà dichiarato finché runtime, I/O, debug e distribuzione non saranno verificati end-to-end.

Criteri di completamento: ogni combinazione dichiarata supportata dispone di un
pacchetto installabile, una demo compilabile, test automatici e documentazione
del ciclo di vita; gli host non-.NET comunicano attraverso un confine esplicito
e non ottengono accesso diretto agli oggetti interni dell'editor o della VM.

## Principi di sviluppo

- Ogni release deve avere criteri di completamento verificabili.
- La logica del linguaggio non deve dipendere dalla UI.
- Lessico, grammatica, semantica e servizi IDE devono derivare da una definizione versionata e validabile, senza liste parallele.
- La sintassi dichiarativa non implica automaticamente semantica eseguibile: nuove capacità host richiedono operazioni controllate o plug-in espliciti.
- I bug corretti devono essere accompagnati da un test di regressione.
- Le modifiche alla sintassi richiedono aggiornamento della specifica e dei test.
- Le nuove dipendenze devono essere motivate e documentate.
- Ogni nuova funzione interattiva principale deve essere valutata per menu, scorciatoia e icon bar nello stesso incremento.
- Nessuna operazione ordinaria deve perdere modifiche, spostare il cursore in modo inatteso o contaminare Undo/Redo.
- Fluidità percepita, stabilità delle selezioni e assenza di flickering sono criteri di completamento, non rifiniture opzionali.
- Compatibilità e migrazioni tecnologiche vengono affrontate separatamente dalle
  funzionalità utente.

## Registro decisioni

| Data | Decisione | Motivazione |
| --- | --- | --- |
| 2026-09-28 | Limitare compilatore Roslyn, emit, linker, loader e pubblicazione a standalone e CLI | L'embedded deve rimanere sempre scripting interpretato di espansione dell'host: conserva analisi e debug, ma non genera, carica o persiste assembly DDF e non acquisisce dipendenze dal compilatore. |
| 2026-09-27 | Includere soltanto il DDF e trattare tutti gli altri profili come pacchetti utente installati | Mantenere una sola identità linguistica predefinita e un solo fallback affidabile; italiano, like-C e futuri profili attraversano lo stesso catalogo, la stessa convalida e lo stesso lifecycle dei pacchetti creati dagli utenti. |
| 2026-09-27 | Convertire dinamicamente le schede aperte quando cambia il profilo sintattico | Rendere immediatamente osservabile il cambio lingua senza riscritture manuali; la trasformazione usa ruoli semantici, resta in memoria, marca i buffer modificati ed è annullabile. I file non aperti richiederanno una futura conversione workspace esplicita prima di qualsiasi scrittura su disco. |
| 2026-09-25 | Prevedere librerie esterne sia in standalone sia in embedded tramite riferimenti dichiarati, resolver e adapter controllati | Riutilizzare assembly .NET, componenti COM registrati come `ADODB`, DLL di sistema e componenti di terzi mantenendo build riproducibili; l'utente autorizza le origini standalone e l'applicazione host conserva il controllo completo in modalità embedded. |
| 2026-09-25 | Collocare dopo la 1.0 l'estensione dell'SDK ad altri linguaggi e framework host | Consolidare prima il contratto .NET/WinForms e affrontare poi VB.NET, F#, WPF, altri framework grafici e host non-.NET con adapter, test e livelli di supporto espliciti. |
| 2026-09-08 | Mantenere la flowchart derivata e in sola lettura, senza editing diagramma → codice | Conservare il codice come unica fonte autorevole, evitare trasformazioni ambigue e concentrare il diagramma su comprensione, navigazione e debug. |
| 2026-09-05 | Prevedere assistenza IA opzionale locale e cloud con adapter sostituibili | Permettere di scegliere in base a hardware, qualità, riservatezza e costo, con configurazione esplicita sia standalone sia integrata e senza fallback cloud implicito. |
| 2026-09-05 | Priorità allo scripting incorporabile per automazioni, dati e logiche personalizzate | CAD e SCADA erano esempi; l'obiettivo è estendere applicazioni generiche tramite variabili, servizi e comandi host, senza un percorso di certificazioni settoriali. |
| 2026-09-05 | Rendere la modalità didattica sempre attivabile e disattivabile in standalone e integrato | Offrire assistenza didattica come opzione indipendente da sintassi e host, preservando codice, debug, permessi e risultato compilato. |
| 2026-09-05 | Adottare .NET + Roslyn come backend principale | Generare C# dalla IR comune e assembly eseguibili nel CLR per programmi standalone e CLI; l'integrazione host resta invece scripting interpretato tramite VM e capacità registrate. |
| 2026-09-05 | Presentare i pacchetti come profili sintattici della piattaforma | Offrire familiarità e localizzazione didattica senza promettere compatibilità completa con C++, Java, C# o VB; semantica e capacità restano condivise. |
| 2026-09-05 | Orientare il prodotto anche all'integrazione in CAD, SCADA, calcolo e formazione | Offrire scripting e strumenti di codice sulle API dell'applicazione ospitante tramite un componente riutilizzabile; primo SDK rinumerato nella `0.11.2`. |
| 2026-09-05 | Inserire la flowchart dinamica, ora `0.11.1`, inizialmente derivata dal codice | Rendere visibili struttura e passi runtime per autori e studenti mantenendo una fonte autorevole e riusando AST/IR e debugger. |
| 2026-09-05 | Anticipare compilatore, linker, loader e CLI alla `0.13`, spostando Package Studio alla `0.14` e SDK alla `0.15` | Rendere il linguaggio definito nella `0.12` distribuibile ed eseguibile fuori dall'IDE prima di ampliare gli strumenti di authoring e le estensioni. |
| 2026-09-05 | Dedicare la `0.12.x` alla definizione completa del DDF | Separare l'architettura generica dei pacchetti dalla progettazione normativa del linguaggio e arrivare alla 1.0 con specifica, corpus e migrazione verificabili. |
| 2026-09-05 | Realizzare il Package Studio come UI integrata sopra un Core di authoring riutilizzabile | Offrire subito un flusso coerente con l'editor senza impedire una futura applicazione autonoma e senza rendere obbligatoria la UI. |
| 2026-09-05 | Tenere Package Studio e plug-in come concetti distinti | L'authoring manipola dati dichiarativi sicuri; lo SDK della `0.15` riguarda esclusivamente codice eseguibile e capacità host esplicitamente autorizzate. |
| 2026-09-06 | Anticipare la `0.11.0` subito dopo la `0.10.1` | Rendere disponibili runtime e debug multi-file prima della flowchart; il blocco flowchart resta subito successivo e sarà rinumerato senza alterare il resto della roadmap. |
| 2026-09-08 | Assegnare `0.11.1` alla flowchart e spostare l'Editor SDK a `0.11.2` | Eliminare l'identificativo provvisorio `0.10.2` mantenendo l'ordine deciso dopo il runtime multi-file. |
| 2026-09-01 | Introdurre pacchetti di linguaggio dichiarativi prima del debugger avanzato | Rendere DDF il linguaggio predefinito di una piattaforma configurabile ed evitare di consolidare parser, runtime e build hardcoded prima della 1.0. |
| 2026-09-01 | Separare pacchetto di linguaggio, manifest di progetto e plug-in eseguibili | Permettere agli utenti di personalizzare grammatica e semantica sicura tramite dati, mantenendo riproducibilità e un confine esplicito per il codice host. |
| 2026-08-28 | Mantenere WinForms e .NET Framework 4.8 durante `0.1.1` | Stabilizzare il comportamento prima di valutare una migrazione. |
| 2026-08-30 | Migrare a .NET 10 prima della 1.0 | Evitare di accumulare altro codice UI/runtime legacy e arrivare alla prima stabile su una base LTS già collaudata. |
| 2026-08-30 | Distribuire la beta .NET 10 come pacchetto self-contained Windows x64 | Consentire l'avvio su Windows x64 senza installare separatamente il Desktop Runtime, accettando un archivio più grande. |
| 2026-08-30 | Anticipare variabili e call stack allo step-by-step | Rendere osservabile lo stato fermato ai breakpoint prima di aggiungere nuovi modi di avanzamento dell'esecuzione. |
| 2026-08-30 | Anteporre editing multidocumento al resto del debugger | Evitare perdita di modifiche nel passaggio tra file e dare a breakpoint, analisi e futuro runtime multi-file un'identità documentale stabile. |
| 2026-08-30 | Non considerare ancora `@@'...'` un import eseguibile | La direttiva è riconosciuta e indicizzata, ma la semantica di moduli, visibilità e inizializzazione deve essere definita esplicitamente prima del runtime multi-file. |
| 2026-08-30 | Completare una linea 0.9.x dedicata all'Editor Experience prima del debugger avanzato e del runtime multi-file | Scrivere DDF deve risultare comodo, interattivo e affidabile prima di investire in compilazione, build e architetture più complesse. |
| 2026-08-28 | Separare la logica testabile dalla finestra principale | Ridurre l'accoppiamento e preparare lexer e parser. |
| 2026-08-28 | Considerare la specifica storica come materiale di progetto, non come grammatica definitiva | Contiene contraddizioni che devono essere risolte esplicitamente. |
| 2026-08-28 | Adottare `//`, `/* ... */` e `*` nel sottoinsieme riconosciuto | Allineare il comportamento corrente a delimitatori non ambigui e testabili. |
| 2026-08-28 | Mantenere temporaneamente `True` e `False` oltre alle forme minuscole canoniche | Non interrompere i sorgenti sperimentali esistenti prima del parser. |
| 2026-08-28 | Usare un test runner eseguibile senza pacchetti esterni | Conservare una build riproducibile e senza nuove dipendenze NuGet. |
| 2026-08-28 | Salvare i sorgenti in UTF-8 senza BOM e rifiutare sequenze UTF-8 non valide | Rendere la codifica prevedibile ed evitare conversioni silenziose. |
| 2026-08-28 | Scrivere prima in un file temporaneo nella directory di destinazione | Non alterare il documento esistente se la generazione del nuovo contenuto fallisce. |
| 2026-08-28 | Conservare al massimo dieci percorsi recenti nelle impostazioni utente | Offrire continuità tra sessioni senza introdurre database o formati aggiuntivi. |
| 2026-08-28 | Ri-lessicalizzare dal token precedente alla modifica fino a fine documento | Conservare un confine di stato sicuro per commenti e stringhe multilinea riutilizzando il prefisso invariato. |
| 2026-08-28 | Usare codici diagnostici stabili `DDF001`–`DDF004` | Rendere testabili e riconoscibili gli errori lessicali prima del parser. |
| 2026-08-28 | Adottare un parser ricorsivo con espressioni a precedenza | Ottenere un AST semplice da testare e recuperare più errori durante la digitazione. |
| 2026-08-28 | Usare codici `DDF101`–`DDF105` per gli errori sintattici | Distinguere in modo stabile la fase sintattica dalla diagnostica lessicale. |
| 2026-08-28 | Centralizzare il vocabolario in `DdfLanguageDefinition` | Permettere il confronto con C e l'estensione dei termini senza duplicare liste in lexer e parser. |
| 2026-08-28 | Derivare l'outline dall'AST recuperato anche con errori | Mantenere la navigazione utile mentre l'utente sta ancora digitando. |
| 2026-08-28 | Implementare il folding come proiezione di sola lettura | Il RichTextBox legacy non visualizza affidabilmente il formato `Hidden`; una proiezione mantiene sorgente e cronologia Undo invariati. |
| 2026-08-28 | Trattare gli smoke dinamici come gate prima delle nuove funzioni IDE | Le sequenze rapide hanno rivelato confini lessicali, snapshot obsoleti e contaminazione dell'Undo non coperti dagli esempi statici. |
| 2026-08-28 | Esercitare i menu tramite le vere voci WinForms con dipendenze esterne sostituibili | Coprire i flussi utente senza aprire dialoghi modali, alterare documenti o salvare impostazioni dell'utente. |
| 2026-08-28 | Derivare il completamento da catalogo e indice simboli | Conservare l'estensibilità del vocabolario e preparare il futuro confronto con C senza duplicare dizionari nella UI. |
| 2026-08-28 | Formattare il flusso di token senza riscrivere l'AST | Conservare commenti e sorgenti incompleti, mantenendo le regole lessicali estendibili tramite catalogo. |
| 2026-08-29 | Mantenere lo stato beta nei metadati e spostarne i dettagli in About | Liberare spazio nell'editor senza nascondere maturità, versione e licenza del prodotto. |
| 2026-08-29 | Risolvere i simboli per identità e ambito, non tramite sostituzione testuale | Evitare che rinomina e navigazione tocchino omonimi, commenti, stringhe o blocchi non correlati. |
| 2026-08-29 | Limitare il primo workspace ai simboli globali dei file `.ddf` | Rendere navigazione e completamento multi-file affidabili prima di introdurre progetti, dipendenze e build. |
| 2026-08-29 | Costruire il type checker prima di scegliere il runtime | Le stesse regole semantiche saranno riutilizzabili da interprete, compilatore o traduttore verso C. |
| 2026-08-29 | Adottare un interprete AST interno come primo runtime DDF | Rendere il linguaggio eseguibile e testabile senza vincolarlo subito a C, .NET o compilatori esterni. |
| 2026-08-29 | Esporre I/O e conversioni tramite un catalogo standard e callback host | Mantenere il runtime testabile e indipendente dalla UI preparando future implementazioni console o compilate. |
| 2026-08-29 | Usare un tema chiaro applicativo con area codice scura | Eliminare superfici miste e preservare il contrasto dell'editor senza duplicare colori nei controlli dinamici. |
| 2026-08-29 | Incorporare e caricare direttamente un ICO con frame piccoli dedicati | Evitare icone obsolete o illeggibili dovute all'estrazione shell e al ridimensionamento del logo completo. |
