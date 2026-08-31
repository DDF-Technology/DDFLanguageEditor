# DDFLanguageEditor roadmap

Questo documento guida lo sviluppo del progetto e viene aggiornato insieme al
codice. Le attività completate vengono marcate, mentre cambiamenti di direzione
e decisioni sul linguaggio vengono registrati nella sezione finale.

## Obiettivo del progetto

Costruire progressivamente un editor affidabile per il linguaggio sperimentale
DDF, mantenendo distinti tre livelli:

1. esperienza di editing e gestione dei documenti;
2. analisi lessicale, sintattica e semantica;
3. eventuale interprete, compilatore o altro modello di esecuzione.

Lo sviluppo del runtime è iniziato dopo la stabilizzazione della grammatica,
del parser e del primo sistema dei tipi; resta separato dall'interfaccia.

## Stato delle release

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
| `0.9.3` | Ricerca e navigazione del workspace | In corso (`0.9.3.3` completata) |
| `0.9.4` | Personalizzazione, sessione e recovery | Pianificata |
| `0.9.5` | Fluidità, accessibilità e rifinitura | Pianificata |
| `0.10.0` | Variabili e call stack durante la pausa | Pianificata |
| `0.10.1` | Step Into, Step Over e Step Out | Pianificata |
| `0.11.0` | Runtime multi-file del workspace | Pianificata |
| `Dopo 0.11` | Progetti, build e backend compilati | Differita |

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
- [ ] Introdurre cronologia di navigazione Indietro/Avanti tra file e posizioni.
- [ ] Mostrare breadcrumb di file, funzione, struttura e blocco corrente.
- [ ] Aggiungere una Command Palette ricercabile per tutte le azioni principali.
- [ ] Supportare apertura mediante trascinamento di file e cartelle e workspace recenti.

## 0.9.4 — Personalizzazione, sessione e recovery

- [ ] Salvare impostazioni per font, zoom, tema, tab/spazi, fine riga e formattazione.
- [ ] Rendere configurabili le scorciatoie rilevando conflitti e mantenendo preset ripristinabili.
- [ ] Aggiungere autosave opzionale e Hot Exit per conservare buffer non salvati.
- [ ] Ripristinare workspace, schede, layout, cursori e breakpoint della sessione precedente.
- [ ] Creare recovery locale dopo arresti anomali senza sovrascrivere silenziosamente i file originali.
- [ ] Rilevare modifiche esterne e offrire confronto, ricarica o conservazione del buffer.

## 0.9.5 — Fluidità, accessibilità e rifinitura

- [ ] Definire budget misurabili per input, selezione, scroll, colorazione e apertura dei popup.
- [ ] Evitare flickering e aggiornamenti completi durante le operazioni incrementali.
- [ ] Mantenere reattivi documenti e workspace di dimensioni realistiche con lavoro cancellabile in background.
- [ ] Aggiungere evidenza della riga corrente, spazi invisibili, guide di indentazione e word wrap configurabile.
- [ ] Valutare minimappa e overview delle diagnostiche come opzioni disattivabili.
- [ ] Completare navigazione da tastiera, nomi accessibili, contrasto e comportamento DPI multi-monitor.
- [ ] Creare smoke dinamici di sessioni prolungate, digitazione rapida e alternanza tra più comandi.

## 0.10.0 — Variabili e call stack durante la pausa

- [ ] Acquisire uno snapshot immutabile dello stato runtime a ogni breakpoint.
- [ ] Mostrare parametri e variabili locali con nome, tipo e valore.
- [ ] Distinguere lo scope corrente dagli scope esterni ancora attivi.
- [ ] Rendere espandibili array e istanze di strutture senza esporre oggetti interni del runtime.
- [ ] Mostrare il call stack attivo con navigazione al punto di chiamata.
- [ ] Associare ogni frame e snapshot al documento sorgente corretto, preparando le pause multi-file.
- [ ] Aggiornare e svuotare le palette in modo coerente su Pausa, Continua, Stop e fine esecuzione.
- [ ] Coprire valori primitivi, array, strutture, shadowing e chiamate annidate con test Core e smoke WinForms.

## 0.10.1 — Esecuzione passo-passo

- [ ] Aggiungere Step Into per entrare nelle funzioni chiamate.
- [ ] Aggiungere Step Over per completare una chiamata restando nel frame corrente.
- [ ] Aggiungere Step Out per raggiungere il chiamante della funzione corrente.
- [ ] Evidenziare la prossima istruzione e sincronizzare comandi, scorciatoie e toolbar.
- [ ] Integrare lo step con breakpoint, variabili, call stack, cicli e cancellazione.
- [ ] Esporre Step Into, Step Over e Step Out nella icon bar con stati coerenti alla pausa.

## 0.11.0 — Runtime multi-file

- [ ] Definire formalmente se i simboli del workspace siano visibili implicitamente o richiedano una direttiva di import esplicita.
- [ ] Decidere se estendere `@@'...'` come import di moduli DDF o introdurre una sintassi distinta, senza attribuirgli retroattivamente una semantica non definita.
- [ ] Costruire uno snapshot coerente di tutti i buffer, compresi quelli modificati ma non salvati, all'avvio del runtime.
- [ ] Definire il caricamento runtime dei moduli del workspace e un unico grafo di dichiarazioni.
- [ ] Risolvere funzioni, strutture e globali tra documenti indicizzati.
- [ ] Definire conflitti di nome, ordine di inizializzazione delle variabili globali e dipendenze cicliche.
- [ ] Rendere effettivi durante l'esecuzione i breakpoint per file introdotti nella `0.9.0.1`.
- [ ] Conservare file e intervalli sorgente nelle diagnostiche, nello stack e nella navigazione.
- [ ] Aggiungere test end-to-end su workspace temporanei multi-file.

## Dopo 0.11 — Progetti, build e backend compilati

- [ ] Definire formato progetto, dipendenze, configurazioni e flusso di build soltanto dopo la stabilizzazione dell'esperienza editor.
- [ ] Valutare backend compilati separati senza accoppiare l'editor a uno specifico compilatore.

## Principi di sviluppo

- Ogni release deve avere criteri di completamento verificabili.
- La logica del linguaggio non deve dipendere dalla UI.
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
