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

Lo sviluppo di un runtime inizierà soltanto dopo la stabilizzazione della
grammatica e del parser.

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
| `0.7.4+` | Diagnostica runtime e debugging | Pianificata |

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

## 0.7.4 e successive — Runtime e debug

- [ ] Aggiungere stack trace DDF e navigazione dagli errori runtime.
- [ ] Progettare breakpoint, esecuzione passo-passo e ispezione delle variabili.
- [ ] Definire il caricamento runtime dei moduli del workspace.
- [ ] Valutare backend compilati separati senza accoppiare l'editor a uno specifico compilatore.

## Principi di sviluppo

- Ogni release deve avere criteri di completamento verificabili.
- La logica del linguaggio non deve dipendere dalla UI.
- I bug corretti devono essere accompagnati da un test di regressione.
- Le modifiche alla sintassi richiedono aggiornamento della specifica e dei test.
- Le nuove dipendenze devono essere motivate e documentate.
- Compatibilità e migrazioni tecnologiche vengono affrontate separatamente dalle
  funzionalità utente.

## Registro decisioni

| Data | Decisione | Motivazione |
| --- | --- | --- |
| 2026-08-28 | Mantenere WinForms e .NET Framework 4.8 durante `0.1.1` | Stabilizzare il comportamento prima di valutare una migrazione. |
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
