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
`0.11.0`, la flowchart dinamica `0.11.1` e l'Editor SDK `0.11.2`; completate
le definizioni e la migrazione `0.12.0`–`0.12.4`, è iniziato il ciclo candidato
`0.12.5`.
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
| `0.12.x` | Definizione completa e candidata alla stabilizzazione del linguaggio DDF | Completata (`0.12.0`–`0.12.5`) |
| `0.13.x` | Compilatore, linker, loader, CLI ed evoluzione applicativa controllata del DDF | In corso (`0.13.0`–`0.13.1` completate; `0.13.2` in hardening; `0.13.3`–`0.13.4` completate; `0.13.5` pianificata) |
| `0.14.x` | DDF Language Package Studio integrato e riutilizzabile | Pianificata |
| `0.15.x` | Extension SDK e host sicuro per plug-in eseguibili | Pianificata |
| `0.16.x` | Assistente IA opzionale con fornitori locali e cloud | Pianificata |
| `Dopo 0.16` | Stabilizzazione dell'ecosistema e preparazione della 1.0 | Differita |
| `Dopo 1.0` | Ecosistema maturo, CIL diretto, bytecode DDF e portabilità SDK | Differita |

## Guardrail architetturali e di prodotto

Questi vincoli sono trasversali e prevalgono sulla convenienza del singolo
incremento. Una deroga richiede una decisione registrata, motivazione, impatto,
migrazione e test; non può essere introdotta incidentalmente durante una feature.

1. **Stabilità della sintassi.** Ogni nuova keyword o forma sintattica richiede
   un problema concreto non risolvibile con libreria, funzione, modulo o
   metadato; proposta semantica completa, alternative rifiutate, migrazione dei
   sorgenti, aggiornamento dei profili e corpus precedono l'implementazione. Le
   finestre di candidate/release vietano ampliamenti non correttivi.
2. **Separazione delle librerie.** `ddf.standard` contiene soltanto contratti
   portabili e indispensabili. Le librerie ufficiali applicative sono moduli
   versionati separati; wrapper .NET, COM/native e API host dichiarano
   esplicitamente backend, piattaforma e capability e non possono essere
   presentati come libreria standard DDF.
3. **IR indipendente dai backend.** Core, AST, simboli e IR non referenziano
   Roslyn, sintassi C#, `ITypeSymbol`, eccezioni CLR o nomi temporanei del
   generatore. Un test architetturale blocca dipendenze dai backend verso il
   Core; soltanto gli adapter esterni traducono la IR bound nel target.
4. **SDK additivo e versionato.** Le nuove funzioni usano capability, nuove
   interfacce opzionali e snapshot immutabili; non aggiungono membri obbligatori
   alle interfacce implementate dagli host senza un bump e un percorso di
   migrazione. Compatibilità binaria, sorgente e dei dati persistiti vengono
   testate separatamente.
5. **Portabilità dimostrata, non dichiarata.** Manifest e lockfile classificano
   transitivamente ogni dipendenza come `Portable`, `DotNet`, specifica di un
   sistema, `Native`, `COM` o `HostProvided`. Una build `portable` fallisce se
   usa componenti non portabili; nessun fallback silenzioso è ammesso.
6. **Bytecode guidato dai casi reali.** Il formato bytecode viene progettato
   soltanto dopo stabilità di linguaggio, ABI, moduli, errori, collezioni,
   asincronia e librerie e dopo applicazioni reali rappresentative. Fino ad
   allora IR e artefatti interni non promettono compatibilità binaria pubblica.
7. **OOP soltanto per necessità.** Classi, ereditarietà, virtual dispatch e
   interfacce del linguaggio non vengono introdotte per familiarità. Una
   proposta deve dimostrare un caso d'uso non risolvibile in modo leggibile con
   strutture, moduli, funzioni, enum, collezioni e cataloghi host.
8. **Un'unica semantica verificabile.** VM, Roslyn, futuro CIL diretto e futura
   VM bytecode consumano la stessa IR bound e superano lo stesso corpus
   differenziale. Una divergenza di valore, output, errore, effetto, cleanup o
   debug blocca la release del backend interessato.
9. **Budget prestazionali non opportunistici.** Target, carichi e modalità di
   misura vengono fissati prima della feature. Un budget può cambiare soltanto
   con dati Release ripetibili e decisione registrata, mai mentre serve a
   nascondere una regressione aperta.
10. **Qualità prima della quantità delle librerie.** Una libreria ufficiale
    richiede API e portabilità documentate, esempi, versionamento, errori ed
    effetti dichiarati, licenza, test VM/backend, criteri di deprecazione e un
    responsabile manutentivo. Il numero dei pacchetti non è un gate di maturità.

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

- [x] Definire caratteri ammessi, identificatori, letterali, commenti, direttive, keyword, operatori e delimitatori.
  - [x] Fissare il nucleo lessicale: identificatori Unicode case-sensitive, cifre ASCII, booleani minuscoli, stringhe su una riga, escape chiusi e commenti (`DDF_LEXICAL_SPEC.md`, 2026-09-26).
  - [x] Chiudere le direttive insieme al modello moduli della `0.12.3`: `@@'Nome'` resta una richiesta legacy deprecata di libreria/capacità e non include sorgenti o moduli (`DDF_MODULE_SYSTEM_SPEC.md`, DDF-0016, 2026-09-29).
  - [x] Chiudere keyword e delimitatori del nucleo, senza alias impliciti (`DDF_SYNTAX_SPEC.md`, 2026-09-27).
  - [x] Chiudere l'insieme degli operatori del nucleo: identità DDF per `<<`/`>>`, logica leggibile con `!`, `&&`, `||`, `^^`, confronti ordinari e rimozione delle forme beta ambigue (`DDF_OPERATOR_SPEC.md`, 2026-09-26).
- [x] Definire precedenza, associatività e forme prefisse, infisse e postfisse senza ambiguità (`DDF_OPERATOR_SPEC.md`).
- [x] Specificare dichiarazioni, espressioni, blocchi, controllo di flusso, funzioni, strutture e collezioni.
  - [x] Fissare unità di compilazione, globali dichiarative, funzioni, strutture, espressioni e corpi di controllo obbligatoriamente racchiusi tra graffe (`DDF_SYNTAX_SPEC.md`, 2026-09-27).
  - [x] Chiudere la sintassi delle collezioni insieme alla semantica di array e `dict`: il nucleo ammette esclusivamente array monodimensionali `T[n]` a lunghezza fissa ed esclude `dict`, array jagged e multidimensionali (`DDF_LANGUAGE_SPEC.md`, `DDF_TYPE_SYSTEM_SPEC.md`, 2026-09-29).
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
  - [x] Collegare il runner Roslyn allo stesso manifesto e corpus normativo: il gate differenziale esegue gli stessi casi VM/Roslyn, inclusi workspace multifile e modulo installato, e blocca ogni divergenza di risultato, output, errore o posizione (`0.13.1`, 2026-10-03).

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
- [x] Creare un corpus normativo completo e versionato di programmi validi, non validi, incompleti e multi-file: `samples/language-corpus/corpus.json`, vincolato da `language-corpus.schema.json`, copre esecuzione, diagnostiche per fase, recupero editoriale, workspace piatti, moduli installati e collisioni; la suite Core rifiuta file orfani e divergenze (`0.12.4`, 2026-09-29).
- [x] Rendere eseguibili gli esempi normativi tramite i test dichiarativi del pacchetto: i sei casi validi del corpus sono incorporati come `corpus:*` in `language-packages/ddf/tests.json`, eseguiti atomicamente prima dell'attivazione e mantenuti sincronizzati con manifesto, sorgenti e risultati attesi dalla suite Core (`0.12.4`, 2026-09-29).
- [x] Definire versionamento del linguaggio, compatibilità, deprecazioni e migrazioni automatiche possibili: `DDF_LANGUAGE_VERSIONING.md`, DDF-0022 e la policy JSON con schema separano sette domini di versione, fissano bump e risoluzione esatta, registrano `@@` e classificano le conversioni beta come candidate sicure o manuali senza fingere un migratore già disponibile; una regressione ne verifica l'allineamento con pacchetto e corpus (`0.12.4`, 2026-09-29).
- [x] Documentare le differenze rispetto alle beta e offrire una guida di aggiornamento dei sorgenti: `DDF_MIGRATION_0_12.md` copre procedura con checkpoint, lessico, sintassi top-level, tipi, ritorni, runtime numerico, manifesti, moduli, pacchetti personalizzati, diagnostiche e criteri finali; ogni ID della policy ha un'istruzione operativa e le candidate automatiche restano esplicitamente non implementate (`0.12.4`, 2026-09-29).

### 0.12.5 — DDF Language Candidate

- [x] Congelare temporaneamente grammatica e semantica per un ciclo completo di utilizzo reale: `DDF_LANGUAGE_CANDIDATE.md` definisce perimetro, regola di modifica, registro problemi e gate; `ddf-language-candidate-baseline.json` rende verificabili con SHA-256 normalizzati i 17 artefatti normativi (`0.12.5`, 2026-09-29).
- [x] Verificare editor, IntelliSense, formatter, diagnostica, debugger, runtime e Build sul corpus normativo: il gate trasversale copre 6 programmi validi, 8 non validi, 4 incompleti e 3 workspace; sui validi prova snapshot IDE e completamento, formattazione idempotente con equivalenza runtime, pausa/ripresa del debugger e fingerprint Build riproducibile (`0.12.5`, 2026-09-29).
- [x] Raccogliere e classificare i problemi senza modificare implicitamente la specifica: registro JSON con schema, ID univoci, area, gravità, classificazione, stato, evidenza e impatto normativo; le due osservazioni iniziali erano difetti del gate, risolti senza modifiche DDF. Una modifica del contratto è rifiutata se priva di decisione e versione obiettivo (`0.12.5`, 2026-09-29).
- [x] Accettare cambiamenti incompatibili soltanto tramite decisione registrata e nuova versione del linguaggio: policy e schema di change control richiedono classificazione breaking, impatto normativo, decisione esistente collegata al problema e versione maggiore della baseline; cinque scenari negativi sono protetti dalla regressione (`0.12.5`, DDF-0024, 2026-09-29).
- [x] Dichiarare il candidato pronto per la stabilizzazione `1.0` solo con tutti i gate superati (`0.12.5`, 2026-09-30).
  - [x] Superare suite Core, build Release e smoke WinForms sul form reale, inclusi tutti i 76 comandi; rendere deterministica la preparazione degli appunti dopo l'osservazione CAND-0003 (`0.12.5`, 2026-09-30).
  - [x] Ripetere in cinque processi Release il budget CAND-0004 sul workspace realistico: tutti i p95 (`8,3`–`12,2 ms`) rispettano il target `16 ms`; mantenere un ingresso smoke mirato per la regressione (`0.12.5`, 2026-09-30).
  - [x] Risolvere CAND-0005/0006/0007 senza modificare il contratto DDF: separare le diagnostiche delle attivazioni rifiutate dal pacchetto attivo, ancorare e correggere il terminatore mancante sullo statement reale e uniformare prodotto/assembly/UI a `0.12.5 Beta`; 226 test Core, build Release e smoke dei 76 comandi verificano la regressione (`0.12.5`, 2026-09-30).
  - [x] Completare la sessione umana descritta in `DDF_CANDIDATE_MANUAL_SESSION.md`: verifica assistita e conferma finale del proprietario includono workspace di stress a 41 sorgenti e Playground; nessuna nuova anomalia, ricevuta `ready` (`0.12.5`, 2026-09-30).

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

- [x] Creare un assembly compilatore Roslyn separato, referenziabile soltanto da standalone e CLI; aggiungere un test architetturale che vieti riferimenti a Roslyn/compilatore da Core e WinForms. (`DDFLanguageEditor.Compiler`, Roslyn 5.9.0 e guardrail automatico introdotti il 2026-09-30; la generazione C# resta intenzionalmente nella 0.13.1.)
- [x] Separare nel contratto SDK `Validate/PrepareScript` dall'attuale nome storico Build e non esporre mai il servizio di compilazione tramite `DdfEditorView` o `DdfEmbeddedEditorControl`. (Contratto SDK `0.34`: `IDdfEditorPreparationService`, `ValidateScript` e `PrepareScript`; nessun membro pubblico Build/Compiler sulle superfici embedded, verificato per reflection e con preparazione VM senza I/O.)
- [x] Definire unità di compilazione, interfacce esportate e separazione tra sorgente, IR e artefatti persistenti. (`DDF_COMPILATION_UNIT_CONTRACT.md`, contratto backend-neutral `1.0`: un'unità immutabile per modulo con snapshot sorgenti, documenti IR, firme DDF esportate, dipendenze e fingerprint deterministico; sorgente e IR restano esclusi dalla ricevuta persistita, 2026-09-30.)
- [x] Definire assembly .NET, manifest versionato, dipendenze, simboli PDB e mappe verso profilo e sorgente originali. (`DDF_DOTNET_ARTIFACT_FORMAT.md`, manifest `1.0` e source map evoluta a `1.1` con schemi JSON, DLL obbligatoria, Portable PDB e mappa opzionali, export e dipendenze DDF esatte, hash/size e path sicuri; tre contract test separati nel solo assembly Compiler, 2026-10-01/02.)
- [x] Specificare compatibilità con runtime .NET e SDK host, diagnostiche per artefatti incompatibili e limiti di caricamento. (`DDF_DOTNET_ARTIFACT_COMPATIBILITY.md`: policy esatta TFM/ABI/RID/capability/dipendenze, divieto embedded, diagnostiche `DDFC200`–`DDFC213`, hash e path verificati senza load, budget configurabili e isolamento di processo richiesto per codice non fidato, 2026-10-01.)

### 0.13.1 — Compilazione separata

- [x] Compilare singoli moduli e librerie mantenendo i controlli della pipeline condivisa.
  - [x] Introdurre un piano deterministico per unità che accetta soltanto una preparazione Core valida, ordina topologicamente librerie e applicazione, conserva dipendenze dirette e assegna output separati senza invocare ancora Roslyn (`DDF_DOTNET_COMPILATION_PIPELINE.md`, piano `1.0`, diagnostiche `DDFC300`–`DDFC306`, 2026-10-01).
  - [x] Collegare ogni work item alla IR bound `2.0`, includendone il fingerprint deterministico nel confine dell'unità (2026-10-01).
  - [x] Produrre il primo assembly gestito reale soltanto dopo i gate semantici, senza stub: `DdfDotNetArtifactEmitter` emette in memoria una DLL per unità in ordine topologico e il relativo manifest `1.0`, senza load o esecuzione (`0.13.1`, 2026-10-01).
  - [x] Pubblicare il bundle completo tramite staging e rinomina atomica, verificando nuovamente manifest, hash, dimensioni e confinamento dei percorsi; rifiutare destinazioni esistenti, byte manomessi e risultati parziali senza lasciare staging (`DdfDotNetArtifactPublisher`, diagnostiche `DDFC409`–`DDFC411`, 2026-10-02).
- [x] Evolvere la IR strutturale `1.0` in una IR bound `2.0` con simboli stabili, tipo di ogni espressione, conversioni esplicite, l-value, chiamate risolte ed esito del flusso/ritorni (`DdfBoundCompilationUnit`, isolamento degli export e capability host, 2026-10-01).
- [x] Generare alberi C# dalla IR bound e compilare con Roslyn, usando esclusivamente i mapping CLR versionati dei tipi e conservando la semantica DDF di valori, conversioni, overflow, non-null, scope e ordine di valutazione.
  - [x] Implementare il primo slice verticale chiuso con funzioni, primitivi, variabili locali, blocchi, ritorni, `if`, `while`, `do`, operatori e chiamate nello stesso modulo; la IR non coperta viene rifiutata con `DDFC400` e non produce output parziale (`DdfDotNetSyntaxTreeGenerator`, 2026-10-01).
  - [x] Estendere lo slice con `for`, postfix checked, array monodimensionali, indicizzazione sicura di array/stringhe e default non-null delle stringhe; creare e validare semanticamente una `CSharpCompilation` immutabile in memoria senza invocare `Emit` (`DdfDotNetCompilationBuilder`, 2026-10-01).
  - [x] Estendere la generazione a strutture locali, membri, globali, funzioni standard e chiamate tra moduli; validare l'intero workspace campione come compilazioni Roslyn separate e collegate topologicamente, sempre senza emit (`DdfDotNetPlanCompilationBuilder`, 2026-10-01).
  - [x] Separare allocazione e inizializzazione: i campi di struttura vengono assegnati da costruttori che ricevono il contesto runtime, mentre ogni modulo espone un coordinatore esplicito e idempotente che inizializza prima le dipendenze dirette e poi i globali dei documenti in ordine deterministico. Le chiamate negli inizializzatori sono quindi supportate senza affidarsi all'ordine accidentale degli initializer CLR (`DdfGenerated_Helper_ModuleInitialize`, 2026-10-01).
- [x] Introdurre una runtime ABI DDF indipendente dalla UI per controlli numerici, errori, I/O, budget, cancellazione, `end` e bridge esplicito deny-by-default delle capacità (`DDFLanguageEditor.Runtime`, ABI `1.0.0` del 2026-10-01, evoluta a `1.1.0` per il debugger cooperativo, `1.2.0` per snapshot di valori e scope, `1.3.0` per risultati capability tipizzati, `1.4.0` per scritture ed elementi host checked, `1.5.0` per asincronia governata e risorse e `1.6.0` per effetti transazionali il 2026-10-04).
- [x] Applicare name mangling deterministico e impedire che identificatori, stringhe o direttive DDF vengano inseriti come sorgente C# non validato.
  - [x] Generare namespace, contenitore e identificatori univoci esclusivamente dagli ID bound, senza incorporare nomi DDF (`DdfDotNetNameMangler` `1.0`, 2026-10-01).
  - [x] Costruire alberi e letterali soltanto tramite API Roslyn, validare ogni token generato e aggiungere un test ostile che impedisce a direttive e codice DDF di diventare sintassi C# (`DDF_DOTNET_COMPILER_PROFILE.md`, 2026-10-01).
- [x] Fissare versione Roslyn/C#, reference assemblies, nullable, overflow, ottimizzazione e determinismo nella ricevuta di build.
  - [x] Definire il profilo Roslyn/C# chiuso e includerne opzioni e versioni nel fingerprint del piano; introdurre un reference set esplicito con identità, SHA-256 e rifiuto di path relativi/duplicati (`DDF_DOTNET_COMPILER_PROFILE.md`, 2026-10-01).
  - [x] Risolvere una directory reference pack fornita esplicitamente e collegare il relativo reference set alle work item e al fingerprint del piano, senza probing di processo/GAC/cartelle casuali (2026-10-01).
  - [x] Persistire nella ricevuta del manifest versione e fingerprint del piano, profilo compilatore, Roslyn, C# e fingerprint del reference set; verificare due emit completi byte-per-byte (`DdfDotNetBuildReceipt`, 2026-10-01).
- [x] Riportare diagnostiche e punti di pausa al codice originale e implementare l'ispezione delle variabili senza esporre nomi temporanei generati.
  - [x] Rimappare deny-by-default le diagnostiche Roslyn: esporre percorso/riga/colonna soltanto quando il mapped span appartiene a uno snapshot DDF noto, altrimenti classificare il problema come errore interno del backend (`DdfDotNetDiagnosticMapper`, 2026-10-02).
- [x] Conservare diagnostiche e informazioni di debug per file, riga, simbolo e scope.
  - [x] Generare Portable PDB deterministici e una source map DDF per ogni unità, collegando gli anchor delle istruzioni eseguibili a file/hash DDF e coordinate del C# generato; includere e verificare entrambi nel bundle atomico (`DdfDotNetSourceMapBuilder`, 2026-10-02). Restano simboli/scope e l'integrazione del debugger compilato.
  - [x] Emettere sequence point nativi sui documenti DDF originali tramite direttive `#line` create con `SyntaxFactory`, mantenere nascosti tutti gli helper/C# generati e verificare nel Portable PDB che ogni punto visibile appartenga a un file `.ddf` (`0.13.1`, 2026-10-02). Restano simboli, scope e integrazione debugger.
  - [x] Evolvere la source map a `1.1` con tabella deterministica dei simboli del modulo: identità bound, nome/tipo/categoria/mutabilità DDF, dichiarazione sorgente e storage CLR privato; verificare sugli scope del Portable PDB che ogni locale visibile sia traducibile senza esporre nomi generati (`0.13.1`, 2026-10-02). Il runner debugger resta da integrare.
- [x] Rendere riproducibili gli artefatti e definire invalidazione della cache per sorgenti, pacchetti e opzioni.
  - [x] Verificare che due emit dello stesso piano producano DLL, Portable PDB, source map e manifest identici byte-per-byte e pubblicabili senza sovrascritture implicite (2026-10-02).
  - [x] Introdurre una cache content-addressed versionata, la cui chiave deriva dagli schemi artefatto/source map e dal fingerprint completo del piano (sorgenti e IR bound, pacchetti, profilo Roslyn, reference set, TFM/RID e runtime ABI). Ogni hit viene ricostruito da manifest e source map e riverificato per ricevuta, path, dimensione e SHA-256 senza caricare assembly; una voce incompatibile o manomessa viene rigenerata e sostituita atomicamente, senza residui di staging (`DdfDotNetArtifactCache` `1.0`, diagnostiche `DDFC412`–`DDFC414`, 2026-10-04).
- [x] Verificare equivalenza osservabile tra esecuzione da sorgente e da modulo compilato sul corpus normativo eseguibile corrente.
  - [x] Eseguire nel solo harness di verifica il workspace multi-modulo emesso in un `AssemblyLoadContext` scaricabile, risolvendo esclusivamente gli artefatti del piano e la runtime ABI nota; verificare risultato `42`, output `42`, budget avanzato e inizializzazione delle dipendenze (`0.13.1`, 2026-10-02). Il runner di prodotto e il corpus differenziale completo restano separati.
  - [x] Aggiungere una prima matrice differenziale generale di 11 programmi eseguiti sia dalla VM sia dagli assembly Roslyn: controllo di flusso, array, strutture, `Input`, conversioni, divisione intera, `end`, risultato e `Output`, oltre agli errori `DDF404`, `DDF405`, `DDF408`, `DDF417`, `DDF418` e `DDF419` con percorso e offset sorgente esatti. Il gate ha individuato e corretto lo span dell'indice fuori limite, ora ancorato all'espressione indice anziché all'intero accesso (2026-10-03). Gli effetti host restano separati.
  - [x] Collegare direttamente `samples/language-corpus/corpus.json` al gate Roslyn, senza duplicare gli oracoli: 6 programmi validi, 2 errori runtime e 2 workspace multifile, incluso un modulo installato, devono coincidere con la VM per risultato, `Output`, terminazione, codice e posizione diagnostica. Il gate ha corretto i locali primitivi senza inizializzatore, che ora materializzano esplicitamente il default DDF senza aggiungere sequence point agli initializer impliciti dei campi struttura (2026-10-03). Il corpus dovrà continuare a crescere insieme al linguaggio.
- [x] Confrontare il backend .NET e la VM su risultati, errori e chiamate host prima di sostituire il percorso predefinito.
  - [x] Confrontare la prima slice eseguibile su risultati, output, terminazione, conversioni ed errori runtime deterministici; capability e chiamate host embedded restano escluse finché non sarà definito un bridge standalone esplicito e autorizzato (2026-10-03).
  - [x] Collegare le funzioni host usate dal codice compilato a `IDdfRuntimeCapabilityProvider` tramite ID canonici e collision-resistant; registrare nel manifest soltanto le capability realmente richiamate, bloccare con `DDFC205` gli ambienti che non le dichiarano e produrre `DDF409` a runtime in assenza del provider. Un gate differenziale verifica risultato `42` e una mutazione eseguita esattamente una volta sia dalla VM sia da Roslyn; l'embedded resta escluso dal caricamento degli artefatti (`DdfDotNetHostCapabilities`, 2026-10-03).
  - [x] Estendere il bridge a lettura e scrittura di oggetti primitivi e proprietà di primo livello esposte dall'host, con capability distinte a privilegio minimo e senza materializzare classi host, reflection o discovery nel C# generato. Il collector attraversa la IR strutturale e inserisce nel manifest soltanto gli accessi realmente presenti; il gate confronta risultato `42`, valori finali e una sola scrittura per oggetto/proprietà, escludendo funzioni e oggetti registrati ma inutilizzati. La runtime ABI `1.3.0` converte i risultati capability tipizzati e segnala con `DDF409` tipi incompatibili (2026-10-03). Restano membri annidati/array host, asincronia, timeout, transazioni e cleanup.
  - [x] Estendere il bridge a percorsi di membri annidati e array host senza materializzare l'oggetto completo nel codice compilato. Le capability `host.member.*` identificano l'intero percorso, mentre `host.element.read.*`/`write.*` ricevono soltanto indice e valore; il type checker verifica la scrivibilità di ogni livello e la runtime ABI `1.4.0` traduce gli indici non validi in `DDF405`. Il gate differenziale confronta VM e Roslyn su risultato `42`, stato finale, manifest minimo e una sola scrittura per oggetto, proprietà annidata ed elemento (2026-10-03). Restano asincronia, timeout, transazioni e cleanup.
  - [x] Governare capability asincrone mantenendo sincrona la semantica DDF: `IDdfRuntimeAsyncCapabilityProvider` riceve identità di esecuzione, token, istante iniziale, scadenza e registro risorse; timeout, massimo chiamate e massimo risorse producono `DDF414`/`DDF413`, mentre `IDisposable`/`IAsyncDisposable` sono deduplicati e rilasciati LIFO anche in errore, con `DDF415` per cleanup fallito. Il gate ABI verifica timeout cooperativo, budget, ordine LIFO e prosecuzione del cleanup; il gate Compiler esegue il bridge reale tramite provider asincrono. Le operazioni bloccanti/non cooperative richiedono ancora isolamento di processo (ABI `1.5.0`, 2026-10-04). Restano ricevuta effetti e transazioni commit/rollback standalone.
  - [x] Aggiungere ricevute immutabili degli effetti e transazioni standalone: la IR bound `2.1` distingue funzioni host pure e mutanti, mentre le scritture oggetto/membro/elemento sono effetti espliciti. `Immediate` segnala applicazione completa o parziale; `RollbackOnFailure` apre una transazione prima dello script, ne espone l'ID al provider, esegue commit o rollback e la rilascia sempre. Begin/commit/rollback falliti producono `DDF420`; risultato o contesto conservano sequenza, capability, tipo, stato, disposizione, ID ed errore. I gate coprono commit, rollback dopo errore, begin/commit falliti e un assembly Roslyn transazionale reale (`DDFLanguageEditor.Runtime` ABI `1.6.0`, 2026-10-04).
- [x] Verificare breakpoint, stepping, call stack e variabili del backend compilato contro il debugger VM; Portable PDB, source map e sequence point nativi sui documenti DDF sono già emessi e il C# generato è nascosto.
  - [x] Introdurre un adapter read-only che valida hash e dimensioni del bundle, legge direttamente PE/Portable PDB, ricostruisce frame DDF per token metodo, sequence point per lo stepping, binding di breakpoint per file/riga e simboli visibili per scope IL; nomi storage CLR restano interni e PDB manomessi vengono rifiutati (`DdfDotNetDebugAdapter`, `DDFC420`–`DDFC422`, 2026-10-03). Restano controllo del processo in pausa, lettura dei valori e confronto dinamico Step Into/Over/Out con la VM.
  - [x] Evolvere la runtime ABI a `1.1.0` con observer opzionale, sequence point e stack di ID bound; generare ingresso/uscita da ogni funzione in `try/finally` e introdurre un controller cooperativo verificato su breakpoint, pausa, Continua, Step Into, Step Over, Step Out e Stop attraverso moduli compilati reali (`DdfDotNetCooperativeDebugger`, 2026-10-03). Restano snapshot dei valori e confronto formale delle tracce con la VM.
  - [x] Evolvere la runtime ABI a `1.2.0` con scope espliciti e lettori tipizzati generati per parametri, locali e globali; produrre snapshot profondamente distaccati e proiettare soltanto identità, nomi, tipi e valori DDF. Array, strutture e combinazioni annidate sono espansi senza reflection tramite un contratto implementato dal codice generato, con limiti di 256 figli, profondità 8 e 1024 valori, rilevamento dei cicli e indicazione del troncamento. Verificati valori reali `left = 10`, `right = 11`, `offset = 1`, `total = 42` e aggiornamento `box.value: 0 → 1` nel programma multi-modulo compilato, oltre a immutabilità e uscita dallo scope (2026-10-03).
  - [x] Definire una traccia differenziale backend-neutral e confrontare VM e Roslyn su documento/span della pausa, Call Stack multifile, parametri, locali, globali, array e strutture. Il gate esegue sullo stesso workspace breakpoint, Step Into, due Step Over, Step Out e Continua, verifica risultato/output `42` e dimostra che una traccia volutamente diversa viene rifiutata. Le chiamate generate aggiornano e ripristinano lo span preciso del chiamante tramite `AtCall`, correggendo la prima divergenza scoperta dal confronto (`DdfDotNetDifferentialDebugTrace`, 2026-10-03).

### 0.13.2 — Linker e loader

- [x] Risolvere import ed export tra moduli compilati separatamente, segnalando simboli mancanti, duplicati o incompatibili.
  - [x] Evolvere il manifest artefatto a `1.1` con import tipizzati per modulo/versione, nome, categoria e firma; `DdfDotNetArtifactLinker` valida il grafo chiuso senza caricare PE, rifiuta moduli/assembly/export duplicati, dipendenze mancanti o con fingerprint diverso, firme incompatibili, ambienti disomogenei e cicli (`DDFC500`–`DDFC505`, 2026-10-04).
- [x] Costruire il programma collegato con dipendenze e ordine di inizializzazione deterministici.
  - [x] Produrre un primo `DdfDotNetLinkedProgram` immutabile con dipendenze prima dei consumatori, entry point logico, capability aggregate e fingerprint indipendente dall'ordine in cui i manifest sono forniti (2026-10-04).
  - [x] Costruire `DdfDotNetLinkedBundle` in memoria, mantenendo lo stesso ordine topologico, copiando DLL/PDB/source map/manifest senza esporre buffer modificabili e calcolando un fingerprint sull'intero programma collegato (`DdfDotNetLinkedBundleBuilder`, 2026-10-04). Il formato `1.1` include anche il lock delle reference gestite; la pubblicazione fisica/distribuzione resta separata.
- [x] Caricare ed eseguire il programma senza richiedere i sorgenti originali o l'editor.
  - [x] Introdurre `DdfDotNetStandaloneLoader`: ricostruisce manifest, source map e grafo esclusivamente dai byte del bundle, ricalcola package e reference lock, riverifica compatibilità e PE/IL, carica soltanto moduli DDF e runtime ABI in un `AssemblyLoadContext` collezionabile, inizializza i moduli nell'ordine topologico e risolve `main` dalla mappa verificata. La sessione è monouso per impedire contaminazione dello stato statico; il gate source-free esegue tre moduli e ottiene risultato/output `42` (2026-10-04).
- [x] Validare struttura, istruzioni e riferimenti degli artefatti prima dell'esecuzione.
  - [x] Ispezionare PE e metadati direttamente dai byte senza `Assembly.Load`: richiedere assembly CLR IL-only con identità esatta e senza entry point, verificare i riferimenti DDF dichiarati per nome/versione/cultura/chiave pubblica contro la definizione PE del modulo collegato, rifiutare riferimenti DDF assenti o non dichiarati, ModuleRef nativi e metodi `P/Invoke`; payload corrotti o PE illeggibili producono `DDFC506`–`DDFC509` (`DdfDotNetPeVerifier`, 2026-10-04).
  - [x] Decodificare integralmente ogni corpo IL senza eseguirlo: validare opcode e dimensioni operando, token metadati, destinazioni dei branch/switch, firme locali e regioni di eccezione; rifiutare `calli`, `jmp`, `localloc`, `cpblk` e `initblk`. Applicare budget configurabili e complessivi a tipi, campi, metodi, byte IL, istruzioni, switch ed exception region (`DdfDotNetPeVerificationLimits`, `DDFC511`–`DDFC512`, 2026-10-04).
  - [x] Richiedere al verifier lo stesso `DdfDotNetReferenceSet` della ricevuta di build e confrontare ogni riferimento gestito esterno per nome, versione, cultura e public key token; un reference set differente/incompleto o un riferimento fuori allowlist produce `DDFC510` (2026-10-04).
  - [x] Verificare semanticamente l'altezza dello stack tramite dataflow su branch, switch e ingressi dei gestori di eccezione: underflow, merge incompatibili, `leave` non vuoto, `ret` incoerente e superamento di `MaxStack` producono `DDFC513`. Le chiamate derivano numero parametri, istanza e valore restituito dalle firme metadati, senza caricare assembly (2026-10-04). La verifica tipizzata completa dei valori resta un ulteriore hardening prima di accettare producer diversi dal backend DDF controllato.
  - [x] Decodificare e validare tutte le firme presenti nelle tabelle di metadati: metodi, campi, proprietà, riferimenti, specifiche generiche, tipi e firme standalone/locali. Puntatori, function pointer e `TypedReference` restano fuori dal profilo DDF sicuro; ogni `ldarg`/`starg` e `ldloc`/`stloc` viene confrontato col numero reale di argomenti e locali. Firme non leggibili o indici fuori contratto producono `DDFC517`; un gate negativo trasforma un opcode locale valido in un accesso inesistente mantenendo invariati PE e stack (2026-10-04).
  - [x] Evolvere il bundle a `1.1`, poi `1.2`, e il manifest package canonico `ddf-dotnet-linked-package` da `1.0` a `1.1`: congela fingerprint del programma/bundle, hash di tutti i payload e allowlist gestita per identità completa, SHA-256, dimensione e classificazione `platform`/`packaged`, senza percorsi locali. Il contratto ricalcola il fingerprint del reference set e rifiuta lock manomessi con `DDFC514`–`DDFC516`; lo schema macchina è `ddf-dotnet-linked-package.schema.json` (2026-10-04/11).
- [x] Conservare breakpoint, call stack e ispezione delle variabili tramite mappe di debug opzionali.
  - [x] Il loader source-free conserva le mappe dei moduli e crea su richiesta `DdfDotNetCooperativeDebugger`; PDB e mappe sono verificati prima del load e vengono caricati insieme alle DLL, mantenendo i contratti già coperti dal confronto differenziale VM/Roslyn (2026-10-04).
- [ ] Definire caricamento/scaricamento degli assembly nel processo standalone/CLI; per codice non fidato usare isolamento di processo e permessi del sistema operativo. L'host embedded non carica artefatti DDF compilati.
  - [x] Fornire una sessione in-process collezionabile, monouso e `IDisposable` per artefatti prodotti dal backend DDF verificato; risoluzioni fuori allowlist sono negate e `Dispose` rimuove i riferimenti forti prima di richiedere `Unload` (2026-10-04).
  - [x] Introdurre il primo worker fuori processo monouso e il protocollo JSON `1.0`, evoluto fino a `1.8` con policy, broker, input e debug interattivo e a `1.9` con reference esterne incluse senza percorsi originali: richiesta/risposta correlate su pipe, bundle e reference ricostruiti e riverificati nel figlio, risultato scalare/output restituiti senza contaminare stdout, timeout o cancellazione con terminazione dell'intero albero (`DDFLanguageEditor.Runner`, `DDFC530`–`DDFC552`, 2026-10-04/11). Il gate end-to-end esegue il workspace a tre moduli e prova anche il kill per scadenza.
  - [x] Applicare il primo profilo di contenimento risorse Windows: Job Object limitato al solo worker quando il token è già ristretto oppure a bootstrap fidato più worker quando deve crearlo, memoria e tempo CPU configurabili, kill-on-close e rifiuto esplicito `DDFC535` se il profilo non può essere applicato; usare su ogni sistema una directory di lavoro temporanea dedicata e rimossa a fine sessione. Il gate end-to-end verifica l'applicazione del profilo oltre a esecuzione e timeout (2026-10-04).
  - [x] Rendere esplicita la policy di accesso deny-by-default, introdotta come `1.1` nel protocollo worker `1.2`, evoluta a `1.2` nel protocollo `1.5`/`1.6` e a `1.3` nel protocollo `1.7`: filesystem assente/read-only/read-write brokerizzato, rete assente o broker client, input console assente o interattivo brokerizzato e capability non autorizzate vengono validati esattamente o rifiutati con `DDFC536`/`DDFC549`; ricostruire da zero l'ambiente del processo senza inoltrare variabili applicative o credenziali e disabilitare telemetria/diagnostica .NET (2026-10-04/10). La policy è un contratto verificabile, non sostituisce i controlli OS ancora mancanti.
  - [x] Aggiungere la protezione opzionale e fail-closed della directory di sessione Windows: il client tenta una DACL non ereditata riservata all'identità corrente, espone l'esito e, quando `RequireProtectedWorkingDirectory` è attivo, rifiuta con `DDFC538` ogni host che non consenta di applicarla. Il gate accetta soltanto protezione realmente attestata oppure rifiuto esplicito, mai un downgrade silenzioso (2026-10-04).
  - [x] Applicare e attestare il token Windows ridotto: se il processo è già ristretto viene verificato senza una seconda trasformazione; altrimenti il bootstrap crea un token filtrato con privilegi massimi disabilitati, modalità LUA e gruppo `INTERACTIVE` deny-only. L'attestazione legge i gruppi reali del token anziché fidarsi del marcatore del figlio; la policy Windows lo richiede per default e `DDFC537` blocca creazione fallita, figlio non ristretto o attestazione mancante. Il gate prova sia il rifiuto senza attestazione sia l'esecuzione reale attestata (`2026-10-04`, corretto e riverificato su ACL/CoreCLR reali il `2026-10-10`).
  - [x] Introdurre il primo broker filesystem read-only senza accesso diretto dal worker: il client trasforma file UTF-8 preautorizzati in snapshot nome logico/contenuto/SHA-256, senza trasportare il percorso originale; il provider espone soltanto `brokerReadText`. Massimo 32 file, 1 MiB ciascuno e 4 MiB totali; traversal, duplicati, hash alterati e capability incoerenti producono `DDFC539`. Il gate compila ed esegue realmente un programma DDF che legge `config`, stampa `broker` e restituisce `6` (2026-10-04).
  - [x] Aggiungere il gate ostile/lifecycle del worker: provare che il percorso originale non entra nell'IPC, una risorsa logica assente produce `DDF409`, cinque sessioni reali consecutive restano monouso e non lasciano directory `ddf-worker-*`; conservare l'attestazione del token anche nelle risposte d'errore, evitando che il client mascheri la diagnostica DDF con un falso `DDFC537` (2026-10-04).
  - [x] Aggiungere il broker di scrittura transazionale al protocollo worker `1.2`: `brokerWriteText` modifica soltanto snapshot scrivibili in memoria; il client conserva fuori dall'IPC i target fisici, verifica hash/quota/concorrenza, prepara l'intero gruppo e applica sostituzioni con backup compensativo. Target assenti, risposte alterate o commit falliti producono `DDFC540`; il gate copre deny-by-default, conflitto e commit riuscito (2026-10-10).
  - [x] Rendere affidabile il bootstrap del token Windows ridotto sulle diverse ACL/installazioni .NET: eliminare i restricting SID che causavano `0xC0000022` nel caricamento di CoreCLR e usare un token filtrato attestato tramite `INTERACTIVE` deny-only, senza fallback silenzioso. L'intera suite compiler passa con token realmente attestato, inclusi cinque worker read-only e il nuovo commit transazionale (`14/14`, 2026-10-10).
  - [x] Proteggere i target filesystem concessi al broker: apertura read/write come verifica effettiva dei permessi, rifiuto dei reparse point in ogni componente del percorso, identità Windows `volume serial + file index` ricontrollata prima e dopo lo staging e ricevuta per nome logico senza path fisici. `DDFC541` distingue protezione/identità non attestabile dal conflitto o commit `DDFC540`; il gate ostile copre anche sostituzione del file con contenuto e hash identici (`14/14`, 2026-10-10).
  - [x] Inoltrare input deterministico senza concedere stdin o console al worker: il protocollo `1.3` accetta una coda UTF-8 immutabile entro 1.024 elementi, 64 KiB per valore e 1 MiB totale, consumata in ordine da `readLine`. La risposta attesta soltanto il conteggio; canale assente o coda esaurita producono `DDF408` nella posizione DDF e quote/null vengono rifiutati con `DDFC542` (`15/15`, 2026-10-10).
  - [x] Aggiungere debug deterministico fuori processo senza aprire un canale interattivo: il protocollo `1.4` accetta fino a 128 breakpoint DDF e una sequenza limitata di Continua/Step Into/Step Over/Step Out, esegue il piano tramite source map e observer cooperativo e restituisce fino a 256 snapshot con posizione, call stack e variabili esclusivamente DDF. Piani non validi, timeout o breakpoint mai raggiunti producono `DDFC543`; il gate reale copre breakpoint, Step Over, round-trip JSON della posizione e valore locale (`15/15`, 2026-10-10).
  - [x] Introdurre il primo confine di rete deterministico senza socket nel worker: il protocollo `1.5` e la policy `1.2` accettano fino a 32 snapshot di risposta preautorizzati, identificati soltanto da nome logico, media type, contenuto e SHA-256. `brokerReadResponse` non riceve URL o credenziali; manomissioni, duplicati e quote oltre 1 MiB/risposta o 4 MiB totali producono `DDFC544`. Il gate reale copre esecuzione, hash alterato e risorsa assente deny-by-default (`16/16`, 2026-10-10).
  - [x] Governare l'acquisizione HTTP(S) preflight lato host prima di creare lo snapshot: grant esatto per endpoint/media type/timeout/quota, HTTPS predefinito, proxy/cookie/redirect disabilitati, filtro degli indirizzi locali/privati/riservati e connessione fissata agli IP già risolti per impedire DNS rebinding. Credenziali e URL non entrano nell'IPC o nella ricevuta; `DDFC545` copre violazioni e fallimenti. Il gate usa resolver e trasporto simulati senza Internet e verifica IP pinnati, DNS misto pubblico/locale, redirect, media type e segreti host-only (`17/17`, 2026-10-10).
  - [x] Congelare il framing del futuro canale bidirezionale senza sostituire prematuramente il worker monouso: protocollo JSONL `1.0` con frame Start/richiesta/risposta capability/Complete/Abort, correlation ID, sequenza monotona, ID monouso, una sola richiesta pendente, massimo 256 chiamate e quote per frame. `DdfDotNetWorkerDuplexTranscript` rifiuta replay, risposta fuori ordine, completamento prematuro, payload incoerente o multilinea con `DDFC546`; il gate di contratto porta la suite a `18/18` (2026-10-10).
  - [x] Collegare realmente il framing JSONL a stdin/stdout mantenendo il percorso legacy: `ExecuteDuplexAsync` gestisce le richieste capability, il runner sospende e riprende l'esecuzione e `brokerFetchResponse(nome)` risolve soltanto `LiveNetworkResources` con grant, endpoint, credenziali e receipt conservati nell'host. Il preflight riusa filtro SSRF/DNS pinning/quote del broker HTTP; `DDFC547`/`DDFC548` coprono grant e scambi live non validi. Il gate end-to-end prova contenuto, receipt e deny-by-default (`19/19`, protocollo worker `1.6`, 2026-10-10).
  - [x] Aggiungere input console interattivo senza esporre stdin al codice DDF: la policy `1.3` abilita esplicitamente `BrokeredInteractive`, il runner consuma prima l'eventuale coda deterministica e poi richiede all'host una riga tramite il canale JSONL. `InteractiveInput` limita richieste e byte per valore, `DdfDotNetWorkerInteractiveInputHandler` mantiene la UI/console nell'host e `DDFC549` rifiuta quote, risposte o avvii legacy incoerenti. Il gate reale consuma due valori tardivi e verifica il fail-closed senza duplex (`20/20`, protocollo worker `1.7`, 2026-10-10).
  - [x] Aggiungere il debug interattivo fuori processo senza esporre CLR o codice generato: `DebugPlan.Interactive` invia a ogni pausa soltanto posizione, call stack e variabili DDF e attende dall'host Continua, Step Into, Step Over, Step Out o Stop. Il piano batch resta separato; sequenza, quote, timeout e MaxPauses rimangono fail-closed. `DdfDotNetWorkerInteractiveDebugHandler` valida snapshot e comando, mentre `DDFC550` copre protocollo legacy, payload invalido e arresto controllato. Il gate reale prova Step Over, Continua, Stop e deny senza duplex (`21/21`, protocollo worker `1.8`, 2026-10-10).
  - [x] Preparare la composizione standalone delle capability interattive sul singolo canale: `DdfDotNetWorkerDuplexCapabilityRouter` associa identificatori esatti agli handler di debug, input e rete, ordina deterministicamente il catalogo, rifiuta duplicati e nega ogni richiesta non registrata con `DDFC551`. In questo modo la futura shell non deve scegliere un solo handler e nessun provider osserva chiamate estranee (2026-10-10).
  - [x] Collegare il debugger compilato alla shell standalone senza contaminare l'embedded: `DdfDotNetEditorDebugService` implementa il contratto Core già consumato da menu e palette, prepara/emette/collega il bundle Roslyn, avvia esclusivamente il worker duplex e proietta pause DDF in riga corrente, Call Stack, Variabili e Watch. Continua, Step Into/Over/Out e Stop vengono inoltrati al worker; breakpoint di documento sono rimappati sugli snapshot sorgente. Soltanto `DDFLanguageEditor.App` referenzia Compiler/Runner e copia il runtime del worker accanto all'EXE, mentre `MainForm(true)` rifiuta l'innesto compilato e conserva il debugger VM dell'SDK embedded (2026-10-10).
  - [ ] Completare la sandbox di sicurezza con capability ulteriori ed equivalenti del profilo risorse e dell'identità file sui sistemi non-Windows; il protocollo `1.9` non espone socket, URL, credenziali, handle console o dettagli CLR al worker.

### 0.13.3 — CLI e distribuzione

- [x] Esporre comandi CLI di convalida, build, run, test e packaging basati sullo stesso Core dell'IDE (`0.13.3`, 2026-10-05).
  - [x] Introdurre l'eseguibile `ddf` `0.13.3` con `validate` e `build`: accetta cartella o `ddf-project.json`, usa preparazione Core, piano/emit Roslyn e cache content-addressed verificata; il gate esegue convalida, prima build, cache hit, sorgente invalido e comando sconosciuto. `run`, `test` e packaging restano separati (`DDF_CLI.md`, 2026-10-04).
  - [x] Aggiungere `ddf run` senza fallback in-process: collega e verifica l'artefatto compilato, blocca capability host non disponibili e lo esegue nel worker monouso con timeout configurabile e token ridotto. Il gate end-to-end verifica risultato `7`, output `cli`, cache riusata e propagazione di `DDF404` con exit code `3` (`DDF_CLI.md`, 2026-10-04).
  - [x] Aggiungere `ddf package` e produrre il primo EXE DDF: il profilo `win-x64` pubblica un'applicazione self-contained non single-file, con runtime, moduli, ABI, licenze e manifest SHA-256. Il gate avvia davvero l'EXE, ottiene output `cli`/risultato `7` e rifiuta la presenza di Compiler o Roslyn (`DDF_CLI.md`, 2026-10-04).
  - [x] Aggiungere `ddf test` come primo runner di conformità: dopo la convalida del progetto riesegue i casi `tests.json` del pacchetto attivo e rende disponibili identità del pacchetto, singoli esiti e riepilogo totale/superati/falliti in testo o JSON. Le future asserzioni applicative e capability host simulate restano nel piano semantico dedicato (`DDF_CLI.md`, 2026-10-05).
- [x] Definire codici di uscita, diagnostiche leggibili da strumenti e uso in CI (`0.13.3`, 2026-10-05).
  - [x] Definire il primo contratto CLI stabile: output testuale navigabile oppure documento JSON `1.0`, con exit code `0` successo, `2` validazione, `3` errore runtime DDF, `64` uso e `70` ambiente/toolchain; `1` è riservato agli errori interni. Il contratto crescerà senza cambiare il significato dei codici esistenti (2026-10-04).
- [x] Pubblicare EXE .NET self-contained per un sistema e un'architettura espliciti, con runtime, librerie e manifest necessari; `win-x64` è il primo profilo e il file singolo resta una valutazione separata (`ddf package`, 2026-10-04).
- [x] Bloccare l'esportazione standalone quando mancano implementazioni delle API richieste all'host (`DDFC602`, 2026-10-04).
- [x] Separare dipendenze di compilazione Roslyn/SDK da dipendenze di esecuzione del programma pubblicato, con controllo negativo del payload finale (2026-10-04).
- [x] Includere licenze e avvisi dei componenti effettivamente redistribuiti, distinguendo licenza prodotto e avvisi/runtime .NET (2026-10-04).
- [ ] Mantenere Native AOT e altri backend come estensioni successive, verificando i limiti su caricamento e generazione dinamica di codice.

### 0.13.4 — Riferimenti a librerie esterne

Consentire a progetti standalone e script embedded di usare librerie
installate nel sistema o fornite da terzi senza permettere agli script di caricare
arbitrariamente file, assembly o simboli nativi. Il progetto dichiara una
dipendenza logica; il resolver del contesto decide se e come soddisfarla.

Le integrazioni non vengono presentate come un unico insieme indistinto. Nomi,
manifesti, catalogo e UI devono conservarne sempre la categoria:

- `ddf.standard`: libreria standard portabile, indipendente da CLR e sistema operativo;
- `ddf.dotnet.*`: adapter gestiti per librerie e provider dell'ecosistema .NET;
- `ddf.platform.*`: integrazioni dipendenti dalla piattaforma, incluse COM/ActiveX e DLL native;
- `host.*`: oggetti, controlli, eventi e servizi concessi esclusivamente dall'applicazione embedded.

Un pacchetto sintattico non abilita automaticamente alcuna integrazione. Ogni
adapter conserva versione, capacità, effetti, errori, licenza, requisiti di
distribuzione e classificazione di portabilità propri.

- [x] Estendere il manifest di progetto `1.2` con riferimenti versionati a librerie gestite .NET, componenti COM/ActiveX registrati, librerie native e servizi forniti dall'host, distinguendone identità logica, versione, architettura, origine e capacità richieste. Il manifest non ammette percorsi, CLSID, ProgID o simboli nativi; COM/native richiedono architettura esplicita, i servizi host origine `host`, raccolta assente/incoerenze/duplicati producono `DDFP020`/`DDFP018`/`DDFP019`. Finché manca il resolver, la build fallisce chiusa con `DDFB018` (`DDF_EXTERNAL_REFERENCES.md`, 2026-10-05).
- [x] Introdurre un resolver sostituibile e deny-by-default. `IDdfExternalReferenceResolver` riceve dichiarazione e contesto; il catalogo concreto non scandisce macchina/registro/GAC, pretende una corrispondenza esatta e una callback di autorizzazione. In standalone una voce gestita valida produce percorso, dimensione e SHA-256 dell'assembly; in embedded e per le altre integrazioni produce soltanto un ID di adapter tipizzato, senza consegnare assembly. I riferimenti di ogni modulo e le dipendenze transitive dichiarate vengono risolti e le dichiarazioni entrano nel fingerprint; `DDFR001`–`DDFR007` rendono espliciti catalogo ambiguo, diniego, contesto errato, assenza, adapter, immagine o dipendenza non valida (`DDF_EXTERNAL_REFERENCES.md`, 2026-10-05).
- [x] Evitare ricerca implicita e non deterministica nell'intero sistema: la build produce atomicamente `ddf-references.lock.json`, evoluto da `1.0` a `1.1` per includere il fingerprint API, con contesto, modulo proprietario, versione, binding, percorso autorizzato, dimensione, SHA-256 e dipendenze transitive del catalogo. Convalide/build successive richiedono fingerprint identico; lock alterato, precedente o illeggibile (`DDFR010`) non viene sovrascritto e una scrittura fallita produce `DDFR011`. Il manifest resta privo di percorsi macchina (`ddf-references-lock.schema.json`, 2026-10-05/09).
- [x] Importare nello script soltanto API esplicitamente descritte o adattate. `DdfExternalApiDefinition` `1.0` descrive senza codice CLR permessi, tipi, oggetti, funzioni, binding, firme, effetti e documentazione; valida collisioni, permessi e tipi e produce un fingerprint SHA-256 incluso nel lockfile. `DdfExternalApiCatalogProjector` alimenta con lo stesso contratto type checker, completamento/documentazione, runtime VM e debugger, mentre `IDdfExternalApiRuntimeAdapter` richiede un ID esatto e riceve soltanto binding e valori DDF. La proiezione di sola analisi non può eseguire operazioni; nessuna reflection o `P/Invoke` arbitraria è disponibile (`DDF_EXTERNAL_REFERENCES.md`, 2026-10-09).
- [x] Supportare per prime le librerie .NET compatibili con il runtime scelto tramite risoluzione esatta, lock, reference set Roslyn, bundle/worker verificati e ricevute di redistribuzione. DLL native, COM e callback ABI restano integrazioni successive tramite adapter tipizzati o worker isolato e non costituiscono un requisito di chiusura della prima verticale gestita (`2026-10-12`).
  - [x] Consegnare al compilatore soltanto assembly gestiti già risolti e bloccati dalla preparazione Core. `DdfDotNetResolvedReferenceSetBuilder` raccoglie DLL e dipendenze transitive dei soli binding `ManagedAssembly`, ricontrolla percorso, dimensione e SHA-256 subito prima di Roslyn e chiude sostituzioni TOCTOU con `DDFC415`; collisioni o reference set invalidi producono `DDFC416`. CLI e debugger compilato standalone condividono il percorso, mentre la superficie chiamabile resta limitata al descrittore API/adattatore tipizzato, senza reflection arbitraria. Il gate altera realmente una copia della DLL dopo la risoluzione e ne verifica il rifiuto (2026-10-10).
  - [x] Includere nel bundle e nel worker soltanto le DLL esterne gestite autorizzate, mantenendo separate le reference di piattaforma. Bundle `1.2` e package `1.1` legano la classificazione al fingerprint; il protocollo worker `1.9` trasporta i byte senza percorsi originali, ne ricostruisce l'identità PE entro 64 assembly/32 MiB e il loader li risolve in memoria nel proprio `AssemblyLoadContext`. `DDFC518` chiude incoerenze di packaging e `DDFC552` payload IPC alterati, duplicati o oltre quota. Il gate cancella realmente DLL principale e dipendenza dopo la creazione della richiesta, importa il package ed esegue comunque `main` ottenendo `7` (2026-10-11).
  - [x] Realizzare un primo percorso verticale dati con SQLite e un adapter ADO.NET governato. `DDFLanguageEditor.Data` registra soltanto statement host nominati con parametri coincidenti, risultati scalari tipizzati, permessi e timeout; connessioni/credenziali restano nella factory host. Transazioni logiche esplicite supportano commit/rollback e serializzazione, cancellazione e `DbCommand.Cancel()` coprono anche provider pseudo-asincroni e `DisposeAsync` attende le operazioni prima del rollback/cleanup. Lo stesso catalogo produce `DdfHostDatabaseDefinition` e `DdfExternalApiDefinition`; un gate SQLite reale verifica injection come dato, commit, rollback, timeout, binding non autorizzati e rilascio risorse. `ADODB` rimane una compatibilità legacy separata (`DDF_DATA_ADAPTERS.md`, 2026-10-09).
  - [x] Realizzare adapter HTTP/JSON con domini e metodi autorizzati, limiti di payload, timeout, cancellazione, credenziali protette e risposte tipizzate. `DDFLanguageEditor.Network` opera su una sola origine HTTPS, costruisce esclusivamente route/query/body dichiarati dall'host e converte risposte scalari JSON, testo o status code entro quote esplicite. La callback credenziali può modificare soltanto gli header; metodo, URI e contenuto restano sigillati. GET è puro, gli altri metodi sono effetti mutanti; lo stesso catalogo produce la libreria host e `DdfExternalApiDefinition`. Quattro gate senza rete reale verificano richiesta, segreti esterni, origine/metodo/binding vietati, payload e timeout (`DDF_HTTP_ADAPTERS.md`, 2026-10-09).
  - [x] Consolidare il filesystem governato con percorsi logici autorizzati, lettura e scrittura transazionale brokerizzata, quote, formati UTF-8/JSON/CSV e nessuna esposizione automatica dei percorsi reali. `DDFLanguageEditor.Storage` associa ogni funzione a una risorsa e operazione host dichiarata, impedendo allo script di fornire o enumerare percorsi. UTF-8 rigoroso, JSON con profondità limitata e CSV strutturalmente coerente vengono verificati entro quote per risorsa e transazione; le scritture immediate usano sostituzione atomica, mentre `RollbackOnFailure` usa staging, read-your-writes, commit serializzato con backup compensativo e cleanup deterministico. Lo stesso catalogo produce libreria host e descrittore API senza percorsi fisici; quattro gate coprono formati, permessi, traversal, quota, commit e rollback (`DDF_FILESYSTEM_ADAPTERS.md`, 2026-10-09).
  - [x] Verificare i tre adapter iniziali in standalone compilato e embedded interpretato usando lo stesso descrittore API, le stesse diagnostiche DDF e gli stessi test di revoca, assenza e aggiornamento. `DdfExternalApiCatalogProjector` può ora registrare più descrittori nello stesso catalogo embedded; `DdfExternalApiRuntimeInvoker` conserva contesto, deadline, transazione e cleanup senza rendere pubblico il costruttore host interno. Nel compilato, `DdfDotNetExternalApiCapabilityProvider` collega funzioni e binding alle capability canoniche già emesse, controllando a ogni chiamata permessi correnti e corrispondenza esatta di ID, versione, fingerprint bloccato e adapter. Il gate usa realmente SQLite, HTTP e filesystem nella stessa preparazione e ottiene `42` sia dalla VM sia dall'assembly Roslyn; revoca e assenza producono `DDF409` in entrambi, mentre un aggiornamento API divergente viene rifiutato prima dell'esecuzione (2026-10-09).
- [x] Definire il catalogo delle famiglie successive senza trasformarle automaticamente in libreria standard. `adapter-families/catalog.json`, schema `1.0` e `DdfAdapterFamilyCatalogLoader` distinguono in modo verificabile `available`, `candidate` e `deferred`, classificano ORM/micro-ORM, provider PostgreSQL/MySQL/SQL Server/ODBC, WebSocket, MQTT/messaggistica, ZIP/XML, PDF/documenti, immagini/report, matematica/statistica/matrici/segnali, crittografia e IA locale/cloud, e mantengono sempre `standardLibrary: false`. Soltanto `available` ammette l'implementazione distribuita: richiede caso d'uso, maintainer, capability, nessun blocker ed evidenze soddisfatte per sicurezza, portabilità, licenza e test; il loader rigoroso rifiuta proprietà ignote, ID/capability duplicate e promozioni incomplete, produce un fingerprint canonico ma non installa né autorizza adapter (`DDF_ADAPTER_FAMILY_CATALOG.md`, 2026-10-09).
#### Backlog di integrazione esterna non bloccante

I contratti provider-neutral, i simulatori e i bridge già marcati come completati
restano risultati della `0.13.4`. Le voci non completate di questa sezione sono
candidate post-milestone: richiedono hardware, SDK/vendor, licenze, protocolli o
matrici reali non disponibili nel gate ordinario. Non vengono dichiarate
supportate e dovranno ricevere una milestone esplicita prima dell'implementazione;
non bloccano la chiusura del percorso gestito .NET e della pubblicazione EXE.

- [x] Introdurre una famiglia `ddf.platform.hardware.*` per l'accesso governato alle interfacce hardware del computer. `DDFLanguageEditor.Hardware` separa identità logica e identità fisica, accetta soltanto dispositivi e operazioni iniettati dall'host, distingue `probe/read/write/configure`, combina permessi, quota, timeout, deadline e cancellazione e registra lease di esecuzione con cleanup deterministico. Lo script non enumera né fornisce porte, driver o chiavi native; `Exclusive` e `Shared` rendono esplicita la concorrenza, mentre disponibilità e generazione osservano l'hot-plug senza fallback impliciti (`DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [x] Implementare `serial` sopra `System.IO.Ports` per RS232 e adattatori USB/seriale. `DdfSerialCommunicationProfile` tipizza baud rate, bit dati, parità, stop, handshake, DTR e RTS; `DdfSerialEndpointOptions` conserva porta, profili, buffer, framing, delimitatore e quote esclusivamente nell'host. `DdfSerialHardwareDevice` usa I/O asincrono cancellabile, UTF-8 rigoroso, frame line/raw, profili nominati, segnali host `DataAvailable`/`Faulted`, hot-plug esplicito e cleanup tramite lease; `DdfSystemSerialPortConnection` apre solo la porta configurata e non chiama mai `GetPortNames`. Quattro gate con connessione iniettata verificano assenza di dettagli fisici nell'API, framing, profili, timeout, eventi e rilascio; i test su porte reali restano opt-in (`System.IO.Ports` 10.0.12, `DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [x] Supportare RS422/RS485 quando il dispositivo o il driver lo espone come porta seriale. `DdfDifferentialSerialProfile` distingue RS422 e RS485 full-duplex da RS485 half-duplex, seleziona direzione driver-managed, polarità RTS oppure adapter vendor e limita i ritardi di turnaround. `DdfDifferentialSerialHardwareDevice` riporta sempre la linea in ricezione dopo scrittura, cancellazione o errore e applica apertura/cambio profilo con cleanup o rollback. `System.IO.Ports` rifiuta esplicitamente i profili vendor, che richiedono un provider dedicato; nessuna porta COM viene considerata automaticamente compatibile sul piano elettrico. Quattro gate iniettati coprono validazione, direzione, turnaround, cancellazione e separazione vendor; i test reali restano opt-in e dichiarano transceiver, driver e cablaggio (`DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [x] Definire e verificare il contratto MIDI provider-neutral. `DdfMidiHardwareDevice`, `DdfMidiEndpointProfile` e `DdfMidiPacket` separano Windows MIDI Services/UMP dal byte stream MIDI 1.0 legacy, mantengono l'endpoint fisico fuori dall'API DDF e governano direzione, gruppi, scheduling, SysEx, quota della coda, hot-plug, fault, cleanup e rollback. Il codec canonico valida la lunghezza UMP dal Message Type; i timestamp ricevuti sono sempre osservabili, quelli in uscita richiedono autorizzazione. Quattro gate iniettati portano la suite hardware a 16 test (`DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [ ] Collegare il contratto a un bridge fisico C#/WinRT `Windows.Devices.Midi2` dopo aver fissato una versione non instabile, matrice Windows 11/API mode/architettura, condizioni di redistribuzione e test opt-in su endpoint reali. Il percorso legacy resta un provider esplicito e non un fallback silenzioso; callback DDF e osservabilità nel debugger richiedono prima la semantica eventi asincrona del linguaggio. Al 2026-10-10 l'SDK Windows MIDI Services è ancora pre-release, quindi la famiglia rimane `candidate` e l'assembly Core non acquisisce una dipendenza ABI instabile.
  - [x] Definire e verificare il contratto LPT/IEEE 1284 governato. `DdfParallelHardwareDevice` e `DdfParallelPortProfile` distinguono provider Windows, vendor e simulato, modi Compatibility/Nibble/Byte/EPP/ECP, flusso, quota, indirizzo daisy-chain e reset autorizzato. I dati hanno codec esadecimale canonico; stato, fault e hot-plug sono tipizzati. `IDdfParallelPortConnection` non offre indirizzi I/O, registri o IOCTL arbitrari e apertura/cambio profilo garantiscono cleanup o rollback. Quattro gate simulati portano la suite hardware a 20 test (`DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [ ] Selezionare un bridge LPT fisico soltanto fra driver firmati, interfacce Windows documentate o SDK vendor verificati; pubblicare matrice sistema operativo/architettura/modi IEEE 1284, licenza, esclusività e test opt-in su hardware reale. La famiglia `ddf.platform.hardware.parallel` resta `candidate` e nessun accesso diretto agli indirizzi I/O viene eseguito dal processo DDF.
  - [x] Definire e verificare il contratto provider-neutral GPIO/I²C/SPI/PWM. `DdfGpioProfile`, `DdfI2cProfile`, `DdfSpiProfile` e `DdfPwmProfile` mantengono pin, bus, indirizzo, chip-select, clock, modi, frequenze e duty-cycle nella configurazione host; frame digitali, byte e PWM sono canonici e limitati. La nuova operazione hardware `Exchange`, autorizzata soltanto con `read|write`, mantiene atomiche le transazioni I²C write/read e SPI full-duplex. `DdfIotHardwareDevice` garantisce lifecycle e rollback; quattro gate simulati portano la suite hardware a 24 test (`DDF_HARDWARE_ADAPTERS.md`, 2026-10-10).
  - [x] Collegare il contratto a `System.Device.Gpio` 4.2.0/`.NET IoT` mantenendo package e tipi provider dietro `IDdfIotEndpointConnection`. `DdfSystemDeviceIotConnection` gestisce GPIO, I²C, SPI e PWM, serializza profili e I/O, preserva gli scambi bus atomici e rilascia deterministicamente handle e canali; la lunghezza di lettura è autorizzata dal profilo host. Tre gate del bridge portano la suite hardware a 27 test e `DDF_IOT_PHYSICAL_TESTS.md` aggiunge matrice e smoke fisici opt-in con consenso separato per le uscite (2026-10-10).
  - [ ] Eseguire e registrare i gate fisici su board/OS/architettura/binding reali per ogni trasporto dichiarato, inclusi disconnessione e cleanup; soltanto allora promuovere `ddf.platform.hardware.iot` da `candidate`. Bridge USB e SDK vendor restano provider dedicati con matrice e licenza proprie.
  - [ ] Valutare USB HID e adapter per CAN; Modbus e gli altri protocolli industriali vengono modellati come librerie di protocollo sopra un trasporto autorizzato, non come accesso universale all'hardware.
  - [x] Definire per ogni dispositivo identità stabile, permessi `probe/read/write/configure`, accesso esclusivo o condiviso, provider, architettura/driver, timeout, cancellazione, hot-plug, quote, rilascio delle risorse e comportamento in caso di scollegamento. Il contratto è provider-neutral e non include tipi Roslyn o handle nativi (2026-10-10).
  - [x] Fornire un adapter loopback simulato per test automatici e modalità didattica. `DdfLoopbackHardwareDevice` supporta code di frame, echo, profili autorizzati, latenza, hot-plug e contatori lifecycle; quattro gate dedicati coprono superficie priva di identità fisiche, cleanup, quote/timeout e accesso esclusivo, mentre il gate trasversale applica lo stesso profilo e verifica il rilascio sia nella VM sia nell'assembly Roslyn. I test su dispositivi reali resteranno opt-in e dichiareranno modello, driver e ambiente (2026-10-10).
- [ ] Introdurre una famiglia separata `ddf.network.industrial.*` per Ethernet industriale. La scheda Ethernet non viene esposta come periferica generica: lo script vede endpoint, tag e operazioni logiche preconfigurati dall'host; interfaccia di rete, IP, MAC, porte, certificati, credenziali, file di descrizione e topologia rimangono fuori dall'API DDF.
  - [ ] Definire il trasporto di rete governato condiviso: identità logica dell'endpoint, allowlist esatta di origine/destinazione e protocollo, ruoli client/server, TCP/UDP/TLS, timeout, deadline, cancellazione, limiti di frame e frequenza, backpressure, reconnessione esplicita, keepalive, stato della sessione, audit e cleanup deterministico. Discovery, broadcast, multicast, raw socket e scansione della LAN sono deny-by-default e richiedono capacità distinte.
  - [ ] Modellare i protocolli industriali come adapter tipizzati sopra il trasporto autorizzato, non come invio di pacchetti arbitrari. Lettura, scrittura, subscribe, diagnostica e configurazione ricevono permessi distinti; indirizzi e tag esposti sono dichiarati dall'host e condivisi da IntelliSense, type checker, runtime, debugger e backend Roslyn.
  - [ ] Implementare per primo Modbus TCP client con unit ID e aree/registri host-side, function code ammessi, tipi e byte order dichiarati, coalescing limitato, timeout e massimo richieste. Vietare allo script indirizzi liberi e scritture multiple non autorizzate; verificare eccezioni Modbus, riconnessione, dispositivi simulati, quote e parità VM/Roslyn. Modbus RTU riusa lo stesso modello sopra l'adapter seriale autorizzato, senza duplicare la semantica applicativa.
  - [ ] Implementare OPC UA client come integrazione industriale primaria di livello informativo: endpoint e security policy esatti, trust store e certificati gestiti dall'host, autenticazione esterna allo script, NodeId/tag autorizzati, read/write/call/subscribe separati, limiti di subscription e monitored item, gestione qualità/timestamp e riconnessione senza accettazione implicita di certificati. Esporre modelli tipizzati e documentazione all'assistenza alla scrittura.
  - [ ] Consolidare MQTT per telemetria industriale sopra la famiglia messaggistica già candidata: broker e topic allowlist, TLS e credenziali host-side, publish/subscribe separati, QoS esplicito, retained message autorizzato, sessione, duplicati, ordering, backpressure e payload tipizzati entro quota. Non presentarlo come bus real-time deterministico.
  - [ ] Introdurre EtherNet/IP/CIP per fasi: iniziare dalla messaggistica esplicita verso oggetti, classi, istanze e attributi host-side; aggiungere solo successivamente I/O implicito/ciclico UDP con connection manager, RPI, multicast, watchdog e ownership verificati. Stack, marchi, specifiche, conformità e condizioni ODVA devono superare gate di licenza e interoperabilità prima della distribuzione.
  - [ ] Supportare inizialmente PROFINET attraverso un PLC, gateway OPC UA o adapter/SDK vendor certificabile. Un eventuale provider diretto distingue Controller/Device, RT/IRT, GSDML, allarmi, diagnostica, naming e discovery DCP, sincronizzazione, ridondanza e requisiti della NIC; raw Ethernet e scheduling real-time restano confinati in un worker o servizio dedicato e non nel runtime DDF in-process.
  - [ ] Supportare inizialmente EtherCAT attraverso un MainDevice/gateway o stack esplicito. Un provider diretto deve governare NIC esclusiva, frame Layer 2, file ENI/ESI, process image, stato dei SubDevice, Distributed Clocks, mailbox CoE/FoE/EoE, ciclo e watchdog; nessun fallback TCP/IP deve fingere compatibilità EtherCAT.
  - [ ] Valutare dopo i primi casi reali POWERLINK, Sercos III, CC-Link IE TSN e altri stack Industrial Ethernet. Ogni protocollo mantiene package, provider, licenza, matrice OS/architettura/NIC, modello temporale e test di conformità propri; il semplice uso del connettore RJ45 non costituisce compatibilità.
  - [ ] Definire un worker industriale isolato per stack nativi, driver o operazioni non cooperative: protocollo IPC versionato, processo con privilegi minimi, allowlist di librerie e NIC, heartbeat, arresto fail-closed, quote, audit, crash recovery e nessun segreto nel processo dello script. Il worker non trasforma Windows o .NET general-purpose in un runtime hard real-time.
  - [ ] Pubblicare una matrice per protocollo con ruoli supportati, modalità ciclica/aciclica, sistema operativo, architettura, NIC/driver o gateway, versione dello stack, licenza/redistribuzione, certificazione e limiti temporali misurati. Separare chiaramente `simulated`, `experimental`, `gateway-backed` e `certified`.
  - [ ] Aggiungere simulatori deterministici e test opt-in reali: errori di protocollo, perdita link, timeout, pacchetti duplicati/fuori ordine, reconnessione, backpressure, revoca dei permessi, cambio configurazione, cleanup, carico prolungato e confronto VM/Roslyn. I budget temporali sono gate di regressione e non vengono aumentati per nascondere peggioramenti.
  - [ ] Escludere dal controllo generico DDF PROFIsafe, CIP Safety, FSoE e altre funzioni safety. DDF potrà leggere diagnostiche o invocare funzioni non-safety di dispositivi certificati tramite adapter autorizzati, ma non verrà dichiarato componente di una catena di sicurezza senza un progetto, processo e certificazione separati.
- [ ] Aggiungere un catalogo Windows opzionale per componenti COM registrati e Type Library, inclusi scenari legacy come Microsoft ActiveX Data Objects (`ADODB`), generando o caricando un interop adapter senza esporre direttamente ProgID, CLSID o registro allo script.
- [ ] Per COM verificare registrazione per macchina/utente, versione della Type Library, compatibilità x86/x64, apartment STA/MTA, rilascio deterministico degli oggetti e disponibilità sul sistema di destinazione.
- [ ] Preferire provider .NET gestiti per le nuove integrazioni dati; mantenere COM/ADODB come compatibilità esplicita per sistemi esistenti, con credenziali e stringhe di connessione fornite dall'host o da un archivio protetto e mai incorporate nel sorgente.
- [ ] Applicare permessi, attendibilità, firma/hash, timeout, cancellazione e limiti di risorse anche alle chiamate esterne; una libreria non fidata o non interrompibile deve essere eseguita fuori processo.
- [x] Rendere diagnostici e navigabili riferimenti mancanti, versioni incompatibili, conflitti, architetture errate e simboli non esposti, senza effettuare fallback silenziosi. `DDFR001`, `DDFR012`, `DDFR013` e `DDFR014` distinguono assenza/ambiguità, versione, architettura e contratto divergente; dinieghi, contesto e resolver conservano i propri codici. Ogni problema punta alla proprietà esatta della reference nel `ddf-project.json` tramite offset UTF-8 corretti, anche nei moduli; la posizione segue la ricevuta fino a `DDFC415/416`, evitando di aprire una DLL come sorgente. Funzioni e valori non inclusi nel descrittore API restano normali `DDF312`/`DDF313` navigabili sul sorgente DDF e non attivano discovery o fallback. Core standalone/embedded condividono il percorso e i gate verificano versione, architettura, contratto, diniego, resolver assente, simbolo non esposto e sostituzione TOCTOU (2026-10-11).
- [x] Definire packaging e distribuzione della prima verticale gestita: le librerie .NET incorporabili richiedono ricevute esplicite, licenze e hash; dipendenze di sistema, componenti host, COM e native restano categorie distinte e non vengono copiate implicitamente. Le modalità concrete di redistribuzione delle candidate legacy/native appartengono al backlog non bloccante sopra descritto (`2026-10-12`).
  - [x] Pubblicare le DLL .NET esterne già incluse nel bundle soltanto con una ricevuta esplicita per identità assembly, licenza obbligatoria e avviso opzionale. `DdfDotNetExecutablePublisher` rifiuta ricevute assenti, duplicate o estranee con `DDFC605`, materializza i byte senza riaprire i percorsi originali, ricontrolla dimensione/SHA-256 nell'output e registra DLL e avvisi nel manifest `ddf-publish` `1.1`. Il gate prova il diniego, elimina gli originali e avvia realmente l'EXE autorizzato (`22/22`, 2026-10-11).
#### Chiusura standalone e distribuzione

- [x] Collegare il backend di build alla UI dell'editor standalone come requisito di chiusura della `0.13.4`; l'SDK embedded continua a non mostrare né esporre operazioni di compilazione o pubblicazione (`2026-10-12`).
  - [x] Aggiungere al menu `Compilazione` le operazioni distinte `Esegui build`, `Esegui ultima build`, `Compila ed esegui` e `Pubblica EXE...`, con comandi e icone abilitati soltanto quando progetto, toolchain e stato dell'editor lo consentono (`2026-10-11`).
    - [x] Collegare `Esegui build` al backend Roslyn reale, `Compila ed esegui` alla sequenza build più worker, `Esegui ultima build` agli artefatti verificati della sessione e `Pubblica EXE...` a una build fresca seguita dal publisher self-contained. Contratto Core neutro e backend sono innestati soltanto dal launcher App; la shell embedded nasconde menu e icone e rifiuta l'innesto (`2026-10-11`).
  - [x] Aggiungere la selezione persistente della configurazione `Debug` o `Release` e del target iniziale `win-x64`. `Esegui build` espone gli artefatti verificati in `<progetto>\bin\Debug\win-x64\artifacts\` oppure `<progetto>\bin\Release\win-x64\artifacts\`, mentre la cache tecnica separata risiede sotto `obj\ddf\cache\<Configurazione>\win-x64\`; non avvengono sovrascritture tra configurazioni (`2026-10-11`). Il profilo Roslyn rimane per ora deterministico e comune; l'eventuale differenza di ottimizzazione deve essere formalizzata prima di attribuirle semantica diversa.
  - [x] Compilare sempre il progetto/workspace e il relativo manifest, non la scheda casualmente attiva. Un singolo file usa un progetto implicito ma deve essere salvato; nella UI reale tutti i buffer modificati vengono salvati o l'operazione viene annullata prima di produrre l'artefatto persistente (`2026-10-11`).
  - [x] Mantenere separati gli esiti: la build produce assembly, simboli e manifest verificabili per sviluppo; `Compila ed esegui` usa l'artefatto appena prodotto nel worker; `Pubblica EXE...` produce il pacchetto self-contained distribuibile con EXE, runtime, moduli, sole dipendenze redistribuibili, hash, licenze e avvisi (`2026-10-11`).
    - [x] Eseguire esplicitamente l'ultima build valida della sessione senza ricompilarla, mostrando output, risultato e passi e segnalando eventuali modifiche sorgente successive; Stop cancella il worker e capability host mancanti sono negate (`2026-10-11`).
    - [x] Compilare e avviare in una sola operazione l'intero progetto/workspace, eseguendo l'esatto artefatto appena verificato e consentendo Stop soltanto durante la fase worker annullabile (`2026-10-11`).
  - [x] Rendere leggibile e stabile il layout degli output, separando nettamente ciò che usa l'utente dagli elementi interni del compilatore (`2026-10-11`).
    - [x] Verificare il layout attuale in entrambe le configurazioni: Debug e Release usano la stessa cache content-addressed `bin\<Configurazione>\win-x64\v1.0\<prefisso-hash>\<hash-completo>\modules\<modulo>\<versione>\`. Versione cache, prefisso e hash servono a riuso deterministico, isolamento e verifica degli artefatti, ma non costituiscono il percorso finale destinato all'utente (`2026-10-11`).
    - [x] Spostare la cache tecnica fuori dall'output pubblico sotto `obj\ddf\cache\<Configurazione>\<target>\...`, mantenendo separazione fra Debug e Release, invalidazione content-addressed e pulizia sicura. Gli output pubblici sono ora `bin\Debug\win-x64\artifacts\` e `bin\Release\win-x64\artifacts\`, privi delle directory `v1.0\prefisso\hash` (`2026-10-11`).
    - [x] Definire un output di build stabile per assembly, simboli e manifest del progetto corrente, aggiornato come bundle completo tramite staging, sostituzione e ripristino compensativo. `publish\<nome-app>\` rimane distinto; la palette Output mostra percorso pubblico e sola radice della cache tecnica (`2026-10-11`).
    - [x] Aggiungere test automatici equivalenti per Debug e Release: verificano output stabili distinti, cache interne distinte, assenza di `v1.0` dal percorso pubblico, cache hit deterministico, aggiornamento atomico, esecuzione dell'esatta ultima build e pubblicazione EXE. Suite Compiler verde `23/23` (`2026-10-11`).
  - [x] Rifinire il pacchetto prodotto da `Pubblica EXE...` con un layout ordinato e completare l'accettazione su una macchina Windows pulita (`2026-10-12`).
    - [x] Lasciare nella radice del pacchetto soltanto l'EXE principale e la directory `support\`: un apphost .NET dedicato punta esplicitamente a `support/<nome>.dll`, mentre runtime, librerie gestite/native, moduli, risorse, licenze, avvisi e manifest `ddf-publish` `1.2` restano sotto `support\`. Il gate avvia realmente l'EXE da una directory di lavoro differente e con spazi nel percorso (`2026-10-11`).
    - [x] Definire e testare l'allowlist della radice: è ammesso un solo file, l'EXE principale, e una sola directory, `support`; qualsiasi altro elemento interrompe la pubblicazione. Nessun file del runtime .NET deve risiedere accanto all'apphost (`2026-10-11`).
    - [x] Permettere di scegliere un'icona applicativa nella finestra di pubblicazione, con anteprima, filtro `.ico`, preferenza persistente e ripristino esplicito dell'icona DDF incorporata. Il publisher valida la struttura ICO, copia le risorse nell'apphost, registra origine/nome/SHA-256 nel manifest e include l'hash nel `publicationFingerprint`; il gate prova fallback, icona personalizzata differente e rifiuto `DDFC606` di un falso ICO (`2026-10-11`).
    - [x] Aggiungere un gate automatico ermetico per avvio, dipendenze, icona, manifest, licenze e pubblicazione atomica sia da un percorso normale sia da un percorso con spazi. Il processo pubblicato parte con `PATH` vuoto, `DOTNET_ROOT`/`DOTNET_ROOT_X64` inesistenti e lookup multilivello disabilitato; `hostfxr`, `hostpolicy`, `coreclr` e `System.Private.CoreLib` devono esistere in `support`. Ogni file, salvo il manifest stesso, deve essere dichiarato con dimensione e SHA-256 corretti; destinazioni esistenti restano inalterate e non lasciano staging (`2026-10-11`).
    - [x] Preparare un kit ripetibile per l'accettazione esterna: `samples/clean-vm-acceptance` produce un vero EXE da sorgente DDF; `tools/prepare-clean-vm-acceptance.ps1` conserva package, ZIP, SHA-256, fingerprint e configurazione Windows Sandbox; lo script guest verifica macchina priva di .NET moderno, allowlist, inventario, runtime locale e risultato `42`, scrivendo un verbale JSON. Il kit locale `20261011-170727` è stato generato correttamente e l'EXE è passato con ricerca .NET globale neutralizzata; l'harness ha correttamente rifiutato il PC di sviluppo perché non è pulito (`DDF_CLEAN_VM_ACCEPTANCE.md`, 2026-10-11).
    - [x] Eseguire l'accettazione finale anche su una VM Windows x64 pulita, senza alcuna installazione .NET, conservando il verbale e il package esatto verificato. Il kit `20261011-170727` è stato eseguito su Windows 10 Professional x64 `10.0.19045` senza comando né chiavi di installazione .NET moderne: tutti i nove controlli risultano veri, l'EXE termina con codice `0`, non produce errori e restituisce le sentinelle `DDF CLEAN VM OK` e `Valore restituito: 42`. Report normalizzato e manifesto del kit sono versionati in `acceptance/clean-vm/20261011-170727/`; l'archivio verificato mantiene SHA-256 `fd820d6c8438b66d633be5a895d97e747f535db9ead1993f07ea97f0d5e458a1`. L'harness usa ora l'API `System.Diagnostics.Process` con timeout esplicito perché `Start-Process -Wait` di Windows PowerShell 5.1 non restituiva in modo affidabile exit code e controllo al chiamante (`2026-10-12`).
  - [x] Mostrare nella palette Output il comando equivalente `ddf`, configurazione, target, directory finale e diagnostiche navigabili; aggiungere un'azione per aprire la cartella di output e test automatici/UI che provino Debug, Release, progetto multifile, build non valida, buffer non salvato e divieto assoluto nell'embedded (`2026-10-11`).
    - [x] Mostrare comando equivalente, configurazione, target, directory configurazione/cache e diagnostiche sorgente navigabili; il gate Compiler reale verifica Debug, Release, cache e target (`23/23`). Lo smoke WinForms attraversa entrambe le configurazioni, l'intero workspace multifile, il salvataggio controllato del buffer modificato, una build fallita con diagnostica navigabile, la conservazione dell'ultimo output valido e il divieto del backend/menu nell'embedded. `Apri cartella output` è disponibile da menu e toolbar dopo build o pubblicazione valide; smoke a 57 icone e 80 comandi (`2026-10-11`).
- [x] Verificare lo stesso contratto tipizzato nei due contesti: `Validate` embedded e `Build` standalone producono lo stesso fingerprint pur mantenendo la corretta policy degli artefatti; VM e assembly Roslyn restituiscono lo stesso risultato e il debugger differenziale coincide su posizione e stack prima di attraversare gli adapter. Un aggiornamento compatibile con fingerprint invariato viene accettato da entrambi, mentre revoca del permesso e adapter assente producono `DDF409` e un aggiornamento incompatibile viene respinto dal lock prima dell'esecuzione. Gate Compiler mirato e suite completa verdi (`23/23`, 2026-10-11).
- [x] Chiudere formalmente la `0.13.4` con build Release e Debug senza warning, Core `234/234`, Compiler `23/23`, Runtime `10/10`, CLI e suite Data/Network/Storage/Hardware complete, smoke WinForms reale su tutti gli 80 comandi e accettazione dell'EXE su Windows 10 x64 senza .NET moderno. Le candidate fisiche, industriali, COM e native restano backlog esplicito e non supporto implicito (`2026-10-12`).

### 0.13.5 — Evoluzione applicativa controllata del linguaggio DDF

Completare il DDF come linguaggio imperativo, tipizzato, modulare e adatto allo
scripting reale senza trasformarlo in un clone OOP di C# o Java. Questa fase
inizia soltanto dopo che il backend Roslyn dispone di un percorso verticale
verificabile: ogni nuova semantica deve essere implementata nella IR comune,
nella VM e nel backend compilato nello stesso incremento e confrontata sullo
stesso corpus. Le decisioni qui pianificate non riaprono né modificano il
candidato congelato `0.12.5`; richiederanno una nuova versione del linguaggio,
dei pacchetti e degli schemi interessati.

#### Valori immutabili e stato esplicito

- [ ] Introdurre `fixed` come dichiarazione di costante tipizzata, locale o di modulo, con inizializzatore obbligatorio, valutazione deterministica in compilazione e divieto di assegnazione, incremento o decremento.
- [ ] Definire le espressioni ammesse negli inizializzatori `fixed`, inizialmente limitate a letterali, enum, altre costanti e operazioni pure verificabili; conservare tipo esplicito nella prima versione.
- [ ] Introdurre `state` per lo stato mutabile appartenente all'istanza dello script, senza usare la semantica implicita e ambigua delle variabili statiche locali.
- [ ] Specificare lifecycle, inizializzazione, reset, isolamento tra istanze, concorrenza, snapshot, Watch e persistenza opzionale di `state`; standalone e host embedded devono decidere esplicitamente se e come serializzarlo.
- [ ] Documentare le funzioni top-level come funzioni di modulo, già equivalenti a funzioni statiche: non aggiungere `static` finché non esiste un modello di tipi con istanze che gli attribuisca un significato distinto.

#### Errori recuperabili e fault non intercettabili

- [ ] Definire valori di errore tipizzati e una dichiarazione verificabile degli errori recuperabili che ogni funzione o API host può produrre.
- [ ] Progettare una sintassi DDF coerente per esecuzione protetta, recupero selettivo, propagazione esplicita e cleanup garantito; `attempt`/`recover`/`always` sono nomi di lavoro, non ancora keyword normative.
- [ ] Distinguere errori recuperabili da fault del motore che lo script non può nascondere, inclusi limite di istruzioni, cancellazione forzata, revoca di capacità, violazione di sicurezza e artefatto runtime corrotto.
- [ ] Conservare codice stabile, tipo, messaggio, documento, span e stack DDF attraversando funzioni, moduli, adapter host, VM e assembly Roslyn, senza esporre eccezioni CLR allo script.
- [ ] Integrare gli errori tipizzati in analisi dei percorsi, IntelliSense, hover, Quick Fix, debugger, Call Stack, Flowchart e documentazione delle API host.

#### Controllo di flusso e valori simbolici

- [ ] Aggiungere `continue` con destinazione deterministica al ciclo più interno e supporto completo in parser, formatter, IR, Flowchart, stepping e diagnostiche fuori contesto.
- [ ] Introdurre enum nominali come insiemi tipizzati di costanti simboliche, senza conversioni implicite arbitrarie verso interi e senza trasformarli in classi.
- [ ] Introdurre una selezione multipla senza fall-through implicito, con ramo predefinito, rilevamento dei casi duplicati e verifica di esaustività quando il dominio è chiuso; `select`/`when`/`otherwise` sono nomi di lavoro.
- [ ] Progettare un tipo opzionale esplicito per rappresentare presenza o assenza senza introdurre `null`, con controllo obbligatorio prima dell'accesso e integrazione con risultati database, ricerca e API host; `maybe T` è una forma di lavoro.

#### Moduli, collezioni e iterazione

- [ ] Aggiungere qualificazione e alias dei simboli esportati dai moduli per evitare collisioni nello spazio dei nomi piatto, senza usare percorsi di file o directory come namespace impliciti.
- [ ] Introdurre un costrutto di iterazione tipizzata su array, sequenze e viste host, con ordine deterministico, regole esplicite per la modifica durante l'iterazione e supporto del debugger; `each` è un nome di lavoro.
- [ ] Definire collezioni dinamiche tipizzate minime: sequenza/lista, mappa e vista host in sola lettura, con conteggio, indicizzazione, iterazione e mutazioni autorizzate.
- [ ] Evitare generici universali nella prima versione: le collezioni parametrizzate sono capacità integrate e chiuse, condivise da type checker, runtime, host e Roslyn.

#### Tipi e libreria standard applicativa

- [ ] Valutare e introdurre `long` per identificativi e contatori estesi e `decimal` per importi e calcoli decimali esatti, senza importare l'intera famiglia numerica CLR; definire overflow, conversioni e mapping Roslyn.
- [ ] Rendere le stringhe realmente utilizzabili con concatenazione o formattazione sicura, ricerca, confronto, `contains`, prefisso/suffisso, sottostringa, sostituzione, trim, maiuscolo/minuscolo, split e join.
- [ ] Estendere la libreria matematica con valore assoluto, min/max, arrotondamento, radice, potenza, logaritmi e funzioni trigonometriche, mantenendo errori e risultati non finiti coerenti con il contratto numerico DDF.
- [ ] Definire tipi e operazioni controllate per date, orari e durate; rendere tempo corrente e casualità capacità sostituibili per test deterministici, non sorgenti globali implicite.
- [ ] Versionare effetti, errori, mapping CLR e binding di ogni nuova funzione standard senza inserire comportamenti hardcoded nel parser o nella UI.

#### Dialoghi e servizi dell'interfaccia host

- [ ] Consentire a uno script di richiedere un popup informativo in stile `MessageBox` tramite un'API DDF tipizzata (nome definitivo da stabilire, per esempio `dialog.message(...)`), senza esporre direttamente `System.Windows.Forms.MessageBox` o altri tipi C#.
- [ ] Supportare testo e titolo obbligatori/opzionali, insieme chiuso di pulsanti e icone (`info`, `warning`, `error`, `question`) e un risultato tipizzato (`ok`, `cancel`, `yes`, `no`) utilizzabile dal controllo di flusso DDF; vietare stringhe arbitrarie usate come nomi di pulsanti nella prima versione.
- [ ] Trattare l'apertura del dialogo come capability esplicita `ui.dialog.message`: lo standalone fornisce un adapter predefinito, mentre nell'embedded è l'applicazione host a concederla, personalizzarla o negarla. CLI, worker headless e host privi di UI devono restituire una diagnostica/errore DDF stabile, mai bloccare in attesa di una finestra invisibile.
- [ ] Eseguire il dialogo sul thread UI corretto, definendo modalità, ownership della finestra, focus, comportamento con debug in pausa, massimo numero di popup, prevenzione di reentrancy e cancellazione alla chiusura dell'host. Lo script non deve poter scegliere handle, owner nativi o creare finestre arbitrarie.
- [ ] Pubblicare la capability nel catalogo host, IntelliSense, hover e documentazione; in modalità embedded il nodo dedicato deve mostrare se il servizio è disponibile e quali pulsanti/icone sono consentiti.
- [ ] Verificare con un provider sostituibile la stessa semantica in VM e Roslyn: richiesta, risultato selezionato, diniego, host headless, chiusura, quote e nessuna apertura reale di finestre nei test automatici. Aggiungere uno smoke manuale standalone e uno nella HostDemo embedded.

#### Test, risorse e asincronia cooperativa

- [ ] Definire asserzioni, casi di test, errori attesi, setup controllato e risultati consumabili da editor e futura CLI, includendo simulazione delle capacità host.
- [ ] Introdurre una gestione deterministica delle risorse per database, file e librerie esterne, con cleanup LIFO garantito su uscita normale, `ret`, `brk`, `continue`, `end`, errore e cancellazione; `use` o `defer` sono forme da valutare.
- [ ] Definire operazioni asincrone cooperative per database, rete, IA e host senza introdurre thread condivisi nel linguaggio: sospensione, ripresa, timeout e cancellazione devono mantenere reattivi editor e applicazione host.
- [ ] Vietare accessi concorrenti impliciti a `state`; una singola istanza non esegue procedure concorrenti salvo futura capacità esplicita con un modello verificabile.
- [ ] Conservare breakpoint, Step Into/Over/Out, Watch, Call Stack e Flowchart attraverso sospensioni asincrone e cleanup senza mostrare frame o temporanei generati dal backend.

#### Threading e reattività dell'host embedded

- [x] Verificare il comportamento corrente: `DdfEditorExecutionService.RunAsync` cattura gli snapshot necessari e avvia l'interprete con `TaskCreationOptions.LongRunning` su `TaskScheduler.Default`; esecuzione e debug non percorrono quindi il thread UI che ha originato l'evento (`2026-10-11`).
- [ ] Rendere questo comportamento un contratto pubblico e permanente dell'Editor SDK: nessuno script embedded, compreso quello avviato da un evento di controllo, deve eseguire istruzioni DDF sul thread UI o sul `SynchronizationContext` dell'host.
- [ ] Separare tre contesti espliciti: acquisizione atomica degli snapshot sul chiamante, esecuzione dello script su scheduler dedicato e consegna di stato/output/completamento sul dispatcher configurato dall'host. Nessuna callback proveniente dal worker deve aggiornare direttamente un controllo WinForms, WPF, Avalonia o MAUI.
- [ ] Consentire all'host di fornire un dispatcher UI e, facoltativamente, uno scheduler di esecuzione governato, preservando sempre cancellazione, timeout, budget, una sola esecuzione attiva per istanza e assenza di concorrenza implicita su `state`.
- [ ] Instradare soltanto le capability che richiedono realmente la UI, come `ui.dialog.message` o la modifica di un controllo esposto, verso il dispatcher dell'host; sospendere cooperativamente lo script mentre la richiesta viene completata senza occupare o bloccare il thread UI.
- [ ] Definire `Stop` e dispose come operazioni asincrone osservabili: cancellare il token, impedire nuove callback, attendere il cleanup entro una scadenza e segnalare un fault stabile se una capability non collabora. Un thread .NET non può essere terminato in sicurezza: codice nativo, non fidato o non cooperativo deve usare il worker fuori processo terminabile.
- [ ] Aggiungere test di reattività nelle demo embedded: heartbeat UI e input restano responsivi durante script CPU-bound, attesa asincrona, breakpoint, popup, cancellazione e chiamata host lenta; verificare inoltre thread/dispatcher di ogni evento, assenza di callback dopo dispose e nessun deadlock con più istanze dell'editor.

#### Evoluzione e compatibilità dell'Editor SDK

- [ ] Versionare esplicitamente il contratto SDK richiesto dalla nuova semantica, distinguendo versione dei pacchetti NuGet, API pubblica, schemi persistiti, protocollo host e capacità del motore; ogni incremento deve dichiarare compatibilità binaria e sorgente.
- [ ] Mantenere `DDFLanguageEditor.Core` e `DDFLanguageEditor.WinForms` privi di dipendenze Roslyn: l'embedded continua a validare, interpretare e debuggare script, ma non genera, carica o persiste assembly DDF.
- [ ] Esporre `fixed` nei simboli, nel completamento e negli snapshot di debug come valore in sola lettura, senza richiedere nuove implementazioni agli host che non ne consumano i metadati.
- [ ] Definire un provider opzionale e capability-based per caricamento, salvataggio, reset e migrazione di `state`; in assenza del provider lo stato resta limitato alla singola istanza e alla sessione corrente.
- [ ] Pubblicare contratti immutabili per descrittori e valori di errore tipizzati, recuperabilità, propagazione, stack DDF ed esito delle invocazioni host, senza far attraversare eccezioni CLR il confine SDK.
- [ ] Aggiungere adapter tipizzati per opzionali, enum, `long`, `decimal`, date/durate, liste, mappe e viste host; vietare cast, reflection o esposizione automatica di collezioni CLR non registrate.
- [ ] Definire handle o lease di risorsa revocabili con ownership, lifetime e cleanup espliciti; l'host deve poter negare, revocare e osservare la capacità senza consegnare allo script l'oggetto nativo sottostante.
- [ ] Evolvere le invocazioni asincrone con sospensione cooperativa, cancellazione, timeout, budget e continuazioni versionate, mantenendo una sola esecuzione attiva per istanza salvo futura capacità esplicita.
- [ ] Estendere snapshot, Watch, Variabili, Call Stack, eventi di pausa e modello Flowchart con i nuovi valori senza esporre temporanei della VM o del codice generato e senza rendere mutabili gli snapshot pubblici.
- [ ] Versionare persistenza di documenti, breakpoint, Watch, layout, `state` opt-in e progetti; un host deve poter ispezionare e migrare uno snapshot precedente oppure rifiutarlo atomicamente senza perdere la versione leggibile.
- [ ] Preferire nuove interfacce opzionali, capability e adapter a nuovi membri obbligatori sulle interfacce esistenti; una funzione non supportata produce una diagnostica DDF stabile anziché rompere l'avvio dell'host.
- [ ] Definire deprecazione e migrazione per ogni API sostituita, mantenendo il vecchio contratto per un intervallo documentato e fornendo esempi prima della rimozione; nessuna rottura silenziosa è ammessa durante la beta.
- [ ] Aggiornare `DDFLanguageEditor.HostDemo`, `DDFLanguageEditor.DatabaseDemo` e `DDFLanguageEditor.MinimalHost` come esempi e contract test permanenti rispettivamente per isolamento multi-istanza, capacità applicative complete e integrazione minima compatibile.
- [ ] Coprire almeno un host compilato contro il contratto precedente e avviato con la nuova libreria, oltre a un host aggiornato che abilita tutte le nuove capability; documentare separatamente compatibilità binaria, sorgente e dei dati persistiti.
- [ ] Mantenere C#/.NET come host di riferimento senza anticipare il supporto post-1.0: VB.NET, F#, WPF e host non-.NET useranno in futuro gli stessi contratti versionati, non implementazioni parallele della semantica DDF.

#### Integrazione dichiarativa e gate di conformità

- [ ] Aggiornare specifica autonoma, EBNF, lessico, grammatica, tipi, semantica, runtime, formatter, IntelliSense, libreria standard, schemi e profili equivalenti per ogni incremento, senza liste parallele nel codice UI.
- [ ] Estendere il corpus con casi validi, non validi, incompleti, multi-file, embedded e compilati per `fixed`, `state`, errori, enum, selezione, opzionali, moduli qualificati, collezioni, nuovi tipi, risorse e asincronia.
- [ ] Confrontare VM e Roslyn su valore, output, errori, effetti host, ordine di cleanup, stato persistibile e punti di debug; ogni divergenza blocca il rilascio della relativa funzione.
- [ ] Aggiungere migrazioni e diagnostiche per keyword nuove o collisioni con identificatori esistenti; i profili sintattici possono rinominare la grafia ma non modificare la semantica.
- [ ] Aggiornare Playground e futuro Package Studio affinché mostrino token, albero, tipi, errori dichiarati, lowering e anteprima formatter dei nuovi costrutti prima dell'attivazione di un pacchetto.

#### Matrice obbligatoria di stabilità e controllo di flusso

La verifica della `0.13.5` è incrementale: non sostituisce la suite esistente e
non riparte da zero. I test già acquisiti restano regressioni permanenti; ogni
nuovo costrutto aggiunge casi Core, differenziali, smoke UI e checkpoint manuali
proporzionati al rischio. Una funzionalità non è completata finché tutti i
percorsi applicabili risultano verdi senza allentare i budget esistenti in
`EDITOR_PERFORMANCE_BUDGETS.md`.

- [ ] Conservare verdi tutti i test Core, gli smoke dinamici e i comandi reali di menu già acquisiti prima di aggiungere nuovi oracoli; nessuna nuova keyword può degradare editing, Undo/Redo, selezione, formattazione, folding, navigazione o cambio profilo.
- [ ] Creare una matrice combinatoria di `if`, selezione multipla, `for`, `while`, `do while`, funzioni, moduli ed error handling, includendo annidamenti e rami raggiungibili/non raggiungibili.
- [ ] Verificare `continue`, `brk`, `ret` ed `end` da ogni profondità consentita, sia nel flusso normale sia dentro esecuzione protetta, recupero e cleanup; le destinazioni della IR devono essere univoche e diagnosticabili.
- [ ] Dimostrare che il cleanup viene eseguito esattamente una volta e in ordine LIFO con uscita normale, `continue`, `brk`, `ret`, `end`, errore recuperato, errore propagato, timeout e cancellazione, anche quando un cleanup fallisce.
- [ ] Verificare `fixed` con espressioni costanti valide, dipendenze fra costanti, overflow e cicli, oltre al rifiuto di inizializzatori runtime e di ogni assegnazione, incremento o decremento.
- [ ] Verificare `state` su inizializzazione, più chiamate, reset, istanze parallele isolate, chiusura/riapertura, persistenza opt-in, snapshot, revoca dell'host e rifiuto dell'esecuzione concorrente sulla stessa istanza.
- [ ] Coprire errori dichiarati, recuperati, propagati e non gestiti, handler duplicati o irraggiungibili e fault non intercettabili; codice, tipo, messaggio, posizione e stack DDF devono restare invariati attraverso file, moduli e adapter host.
- [ ] Coprire enum, selezione multipla e opzionali con casi completi, incompleti, duplicati e non validi; nessun opzionale può essere letto senza controllo e nessun ramo predefinito può nascondere diagnostiche di tipo.
- [ ] Coprire qualificazione e alias di modulo con collisioni intenzionali, dipendenze dirette e transitive, export privati, alias duplicati e simboli assenti, mantenendo build e navigazione deterministiche.
- [ ] Coprire array, liste, mappe, viste host e iterazione su raccolte vuote, minime e grandi; definire e testare modifica durante iterazione, limiti, identità, aliasing, ordine e revoca della vista.
- [ ] Coprire `long`, `decimal`, stringhe, matematica, date, tempo e casualità con valori limite, conversioni, culture differenti, overflow, precisione, risultati non finiti e provider deterministici sostituibili.
- [ ] Aggiungere test generativi o matrici equivalenti per precedenze, recovery, formatter idempotente e trasformazione dei profili, includendo identificatori storici che collidono con le nuove keyword.
- [ ] Eseguire contract test SDK su host legacy senza nuove capability, host aggiornato completo, più istanze isolate, persistenza precedente/corrente, capability revocata e snapshot non migrabile; ogni fallimento deve lasciare l'host in uno stato coerente e riutilizzabile.

#### Debugger, Flowchart e osservabilità

- [ ] Per ogni nuovo statement verificare breakpoint prima, dentro e dopo il costrutto, Step Into/Over/Out, Continua, Stop, rimappatura dopo modifiche e cambio automatico della scheda in workspace e moduli multifile.
- [ ] Rendere osservabili in Watch e Variabili `fixed`, `state`, enum, opzionali, collezioni ed errori senza consentire mutazioni; Call Stack e selezione del frame devono mostrare soltanto simboli e sorgenti DDF.
- [ ] Verificare pausa e ripresa dentro recupero, cleanup, iterazione e sospensione asincrona, compresi timeout e cancellazione; nessun frame o temporaneo generato da Roslyn deve trapelare nella UI DDF.
- [ ] Estendere il modello Flowchart con `continue`, selezione multipla, ramo di errore, propagazione, cleanup e sospensione; ogni uscita deve raggiungere la destinazione semantica corretta.
- [ ] Ripetere i test geometrici della Flowchart su diagrammi semplici e complessi: nessuna intersezione o sovrapposizione tra linee continue e tratteggiate, etichette leggibili, gomiti condivisi quando leciti e frecce coerenti prima dei ricongiungimenti.
- [ ] Verificare che l'evidenziazione del percorso runtime nella Flowchart e la riga corrente dell'editor restino sincronizzate durante stepping, cambio file, recupero e cleanup.

#### Stress, prestazioni e confronto VM/Roslyn

- [ ] Estendere il workspace di stress a nuove semantiche e misurare molti file/moduli, stack profondi, numerosi `state`, collezioni grandi, errori ripetuti, sospensioni frequenti e breakpoint distribuiti senza bloccare la UI.
- [ ] Eseguire sessioni prolungate con apertura/chiusura documenti, modifica durante pausa, cambio profilo, Run/Debug ripetuti, cancellazione e risorse host; controllare memoria, handle, GDI/USER, code asincrone e assenza di callback dopo il dispose.
- [ ] Misurare input, selezione, scroll, colorazione, analisi, completamento, apertura popup, caricamento workspace, cambio pacchetto, Flowchart e debugger sui carichi realistici; una regressione oltre budget deve essere risolta o registrata e differita esplicitamente, mai nascosta alzando il target.
- [ ] Eseguire ogni programma normativo sia con IR/VM sia con IR/Roslyn e confrontare valore, output, errori, effetti host, ordine di valutazione, mutazioni di `state`, aliasing, cleanup, sospensioni e punti di pausa osservabili.
- [ ] Ripetere build e test in processi isolati e con culture/configurazioni controllate, verificando fingerprint, assembly, PDB e risultati deterministici; gli artefatti compilati non possono dipendere da stato globale o ordine casuale dei file.
- [ ] Eseguire test automatici Core a ogni incremento, smoke WinForms sui flussi reali, misure Release isolate per i budget e una sessione manuale visuale/accessibile prima di chiudere ogni checkpoint semantico rilevante.
- [ ] Bloccare attivazione del pacchetto, dichiarazione di conformità e chiusura della milestone se VM e Roslyn divergono oppure se restano anomalie non classificate in controllo di flusso, cleanup, debug o isolamento host.

#### Funzioni differite e confini intenzionali

- [ ] Valutare dopo il nucleo precedente, senza renderle prerequisiti automatici della prima stabile: parametri predefiniti, argomenti nominati, espressione condizionale, range, tuple, destructuring, funzioni come valori, lambda/closure, funzioni locali, pattern matching avanzato, attributi, slicing e comprehension.
- [ ] Continuare a escludere dal nucleo iniziale classi con ereditarietà, metodi virtuali, overload, operator overloading, reflection, `dynamic`, puntatori, memoria manuale, `goto`, macro/preprocessore, concorrenza condivisa, eccezioni CLR e caricamento arbitrario di librerie dal sorgente.
- [ ] Mantenere strutture, moduli, funzioni e cataloghi host come modello principale: eventuali future astrazioni a oggetti richiederanno un caso d'uso non risolvibile con questi strumenti e una decisione normativa separata.

### Criteri di completamento della 0.13

- almeno un programma multi-file viene compilato separatamente, collegato ed eseguito fuori dall'IDE;
- esecuzione da sorgente e da artefatto produce gli stessi risultati sul corpus normativo;
- build ripetute con gli stessi input producono artefatti deterministici;
- moduli incompatibili o malformati vengono rifiutati con diagnostiche chiare;
- riferimenti esterni producono lo stesso contratto tipizzato, ma vengono risolti per la compilazione soltanto nello standalone e come adapter/capacità runtime nell'embedded;
- debug e CLI condividono le regole del pacchetto senza implementazioni parallele del linguaggio;
- il nucleo applicativo pianificato nella `0.13.5` dispone di specifica, corpus e implementazione equivalente su VM e Roslyn, oppure ogni elemento non completato viene esplicitamente rinviato prima di dichiarare chiusa la milestone;
- `fixed`, `state`, errori recuperabili, valori opzionali, collezioni, risorse e asincronia non introducono stato globale implicito, `null`, eccezioni CLR o concorrenza non governata;
- gli host SDK esistenti continuano ad avviarsi senza adottare capability opzionali, mentre gli host aggiornati possono usare l'intera semantica nuova tramite contratti versionati e testati;
- Core e WinForms restano compiler-free e nessun percorso embedded genera o carica assembly DDF.

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

## Dopo 0.16 — Stabilizzazione dell'ecosistema e preparazione della 1.0

La prima stabile usa VM DDF per l'embedded e backend Roslyn per standalone/CLI.
Non viene trattenuta in attesa di CIL diretto o bytecode proprietario: prima si
validano linguaggio, SDK e librerie su applicazioni reali, mantenendo Roslyn come
backend maturo e la VM come oracolo semantico.

- [ ] Congelare l'insieme di funzioni linguistiche ammesse nella 1.0 e aprire una finestra di stabilizzazione senza nuove keyword, salvo correzioni governate e migrazioni obbligatorie.
- [ ] Estendere il manifest di progetto con dipendenze, configurazioni, classificazione transitiva della portabilità e distribuzione di pacchetti riutilizzabili.
- [ ] Separare `ddf.standard`, moduli ufficiali portabili, adapter .NET, componenti native/COM e capability host nei nomi, nei manifesti, nella documentazione e nella UI del gestore pacchetti.
- [ ] Portare a qualità stabile un insieme ridotto ma sufficiente di librerie ufficiali per testo, collezioni, matematica, date/tempo, serializzazione, I/O controllato, dati/database, logging, configurazione e test.
- [ ] Applicare a ogni libreria ufficiale il gate di documentazione, esempi, errori/effetti, licenza, test differenziali, compatibilità, deprecazione e ownership manutentiva; pacchetti incompleti restano sperimentali.
- [ ] Realizzare applicazioni standalone ed embedded rappresentative che usino moduli, librerie, errori, stato, asincronia, debug, persistenza e capability senza dipendere da scorciatoie interne dell'editor.
- [ ] Definire i gate finali di compatibilità, sicurezza, prestazioni, accessibilità, distribuzione, documentazione e supporto per `1.0.0`.
- [ ] Dichiarare portabile soltanto un progetto verificato end-to-end su ogni piattaforma supportata con lo stesso lockfile compatibile; COM, DLL native e API host specifiche impediscono il profilo `portable` salvo adapter alternativi espliciti.

## Dopo 1.0 — Ecosistema maturo, backend autonomi e portabilità SDK

L'ordine di questa fase è intenzionale: consolidare prima l'ecosistema su casi
reali, poi sostituire Roslyn con emissione CIL diretta, infine progettare il
bytecode. CIL e bytecode restano backend complementari; il secondo non cancella
il primo. La compatibilità degli host prosegue sullo stesso Core e non implica
supporto nativo ai linguaggi imitati dai profili sintattici.

### Gate di maturità dell'ecosistema

- [ ] Raccogliere casi d'uso e profili prestazionali da applicazioni reali, classificando ciò che richiede linguaggio, libreria portabile, adapter di piattaforma o capability host.
- [ ] Stabilizzare ABI runtime, modello dei moduli, errori, collezioni, cleanup, asincronia, debug map e formato dei pacchetti prima di promettere compatibilità per nuovi artefatti compilati.
- [ ] Mantenere un catalogo ufficiale curato e un processo di promozione da sperimentale a stabile; nessuna libreria diventa standard soltanto perché molto usata o perché avvolge una API .NET.

### Backend CIL diretto

- [ ] Definire un'interfaccia backend sostituibile che riceve soltanto IR bound e contratti runtime versionati, senza dipendere dalla UI o dal sorgente C# generato.
- [ ] Emettere direttamente metadati CLR, CIL, assembly, riferimenti e Portable PDB con name mangling e mappe DDF deterministici.
- [ ] Conservare Roslyn come oracolo fino a quando VM, Roslyn e CIL diretto coincidono sull'intero corpus e sulle applicazioni rappresentative per valori, errori, effetti, cleanup, prestazioni e debug.
- [ ] Rendere il backend CIL diretto predefinito soltanto dopo compatibilità, diagnostica, riproducibilità e supporto operativo equivalenti o superiori a Roslyn; mantenere rollback e selezione esplicita durante la transizione.

### Bytecode DDF e VM portabile

- [ ] Aprire la progettazione del bytecode soltanto dopo il gate di maturità dell'ecosistema e la stabilizzazione del backend CIL diretto; documentare i casi reali che richiedono un formato DDF indipendente dal CLR.
- [ ] Specificare formato binario versionato, instruction set, tipi, costanti, moduli, chiamate, errori, cleanup, asincronia, capability e debug map senza incorporare dettagli CIL, Roslyn o del layout di una VM particolare.
- [ ] Implementare compilatore IR bound → bytecode, verifier strutturale e tipizzato, linker/loader e ricevuta riproducibile prima di eseguire artefatti non fidati.
- [ ] Evolvere la VM di riferimento in una VM bytecode mantenendo limiti, cancellazione, capability, osservabilità e isolamento; l'esecuzione in-process non viene dichiarata sandbox senza un confine di processo.
- [ ] Verificare lo stesso artefatto bytecode su almeno due implementazioni o piattaforme della VM prima di dichiarare portabilità del formato; confrontare sempre VM, CIL diretto e backend di riferimento sul corpus normativo.
- [ ] Conservare il backend CIL per EXE/DLL .NET e usare il bytecode principalmente per embedded, distribuzione portabile e runtime controllati; la scelta appartiene al manifest e non cambia la semantica DDF.

### Portabilità dell'SDK e compatibilità degli host

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
e non ottengono accesso diretto agli oggetti interni dell'editor o della VM. Il
backend CIL è promosso soltanto dopo equivalenza con Roslyn e il bytecode è
dichiarato portabile soltanto dopo esecuzione dello stesso artefatto su runtime
indipendenti verificati.

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
| 2026-10-11 | Garantire nell'embedded che gli script siano eseguiti fuori dal thread UI, con ritorno sul dispatcher host soltanto per eventi e capability grafiche | L'esecuzione corrente usa già un task `LongRunning` di background, ma deve diventare un contratto SDK verificato. Un thread separato mantiene reattivo l'host; cancellazione cooperativa e budget governano il codice gestito, mentre operazioni non cooperative o non fidate richiedono un processo worker terminabile. |
| 2026-10-11 | Aggiungere dialoghi `MessageBox` tramite una capability DDF tipizzata e fornita dall'ambiente | Uno script deve poter mostrare messaggi e ricevere una scelta dell'utente sia nello standalone sia, se autorizzato, nell'embedded. L'API rimane indipendente da WinForms/C#, testabile e negabile negli ambienti headless; l'host conserva controllo su UI, thread, ownership, stile e quote. |
| 2026-10-11 | Separare cache di compilazione, output di build e pacchetto pubblicato; ordinare il pacchetto in radice più `support\` e consentire un'icona EXE personalizzata | La gerarchia content-addressed `v1.0\prefisso\hash\modules\...` è utile internamente e viene oggi generata sia in Debug sia in Release, ma non deve essere presentata come output finale. L'utente deve trovare percorsi brevi e stabili, mentre la pubblicazione conserva in radice l'EXE e i soli file tecnicamente necessari, raccoglie il resto in `support\` e incorpora un'icona scelta nel progetto. |
| 2026-10-11 | Rendere la build e la pubblicazione EXE accessibili dalla UI standalone prima di chiudere la `0.13.4` | Il backend e `ddf package` non bastano come esperienza IDE: il progetto/workspace deve poter essere costruito in profili Debug/Release prevedibili, eseguito dall'ultima build o pubblicato come EXE self-contained. Build, esecuzione e pubblicazione restano operazioni distinte; l'embedded non acquisisce alcuna funzione di compilazione. |
| 2026-10-10 | Separare Ethernet industriale in `ddf.network.industrial.*`, distinguendo protocolli IP, comunicazione ciclica e stack Layer 2 real-time | Modbus TCP, OPC UA e MQTT possono riusare trasporti governati; EtherNet/IP ciclico, PROFINET ed EtherCAT richiedono invece responsabilità temporali, stack, driver, NIC, licenze e conformità proprie. Gli script vedono soltanto endpoint e tag host autorizzati; raw Ethernet e funzioni safety restano esclusi dal runtime generico. |
| 2026-10-05 | Aggiungere `ddf.platform.hardware.*` per seriale RS232/RS422/RS485, MIDI, LPT e future interfacce GPIO/I²C/SPI/PWM/USB | Permettere automazione e didattica hardware senza concedere allo script accesso indiscriminato a porte, indirizzi I/O o driver. Le API .NET e di sistema vengono sempre racchiuse da adapter tipizzati, autorizzazioni, lifecycle deterministico e simulatori; LPT e funzioni proprietarie rimangono integrazioni legacy/vendor isolate. |
| 2026-10-05 | Classificare le integrazioni come `ddf.standard`, `ddf.dotnet.*`, `ddf.platform.*` e `host.*`, iniziando da SQLite/ADO.NET, HTTP/JSON e filesystem governato | Evitare di confondere portabilità, wrapper .NET, componenti di piattaforma e capacità dell'host; ADODB resta compatibilità legacy, mentre database moderni, rete e I/O controllato forniscono i primi casi reali per validare adapter, permessi, errori, risorse e packaging. |
| 2026-09-30 | Pubblicare la prima 1.0 con VM embedded e backend Roslyn stabile, rinviando CIL diretto e bytecode alla maturità dell'ecosistema | Evitare che il prodotto resti bloccato dalla costruzione prematura di una toolchain proprietaria; applicazioni e librerie reali devono prima stabilizzare semantica e ABI. Dopo la 1.0 l'ordine vincolante è ecosistema maturo, CIL diretto verificato contro Roslyn, quindi bytecode DDF e VM portabile. |
| 2026-09-30 | Rendere vincolanti dieci guardrail su sintassi, librerie, IR, SDK, portabilità, bytecode, OOP, conformità, prestazioni e qualità | Trasformare i principali rischi del progetto in gate verificabili: nessuna nuova sintassi senza problema reale, nessun wrapper di piattaforma presentato come standard, nessuna dipendenza Roslyn nel Core, nessuna portabilità o regressione occultata per dichiarazione. |
| 2026-09-30 | Usare `fixed` per le costanti tipizzate e `state` per lo stato mutabile dell'istanza | Distinguere in modo visibile valori immutabili e stato persistibile senza importare `const`/`static` e le relative assunzioni OOP; le funzioni top-level restano funzioni di modulo e la persistenza di `state` è sempre esplicita. |
| 2026-09-30 | Pianificare nella `0.13.5` l'evoluzione applicativa del DDF dopo il primo backend Roslyn verificabile | Completare errori tipizzati, controllo di flusso, valori opzionali, enum, moduli qualificati, collezioni, tipi applicativi, libreria standard, test, risorse e asincronia implementandoli insieme su VM e Roslyn, senza riaprire il candidato `0.12.5` o trasformare DDF in un linguaggio OOP generalista. |
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
