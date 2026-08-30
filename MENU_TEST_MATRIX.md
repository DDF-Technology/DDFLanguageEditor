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
| Esci | chiude una finestra con documento pulito senza eccezioni residue |

I selettori Apri/Salva sono sostituiti nello smoke da risposte deterministiche;
nell'applicazione continuano a usare i dialoghi Windows. I file sono creati in
una directory temporanea dedicata e rimossi al termine. Le impostazioni dei file
recenti dell'utente non vengono salvate durante il test.

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
| Completamento | mostra i suggerimenti contestuali tramite la voce di menu |
| Formatta documento | normalizza il sorgente, conserva il cursore ed è annullabile in un passo |
| Vai alla definizione | seleziona la dichiarazione risolta dal riferimento corrente |
| Rinomina simbolo | modifica solo dichiarazione e riferimenti nello stesso ambito ed è annullabile |

Lo smoke conserva e ripristina il contenuto precedente degli appunti Windows.
Verifica inoltre gli stati abilitati/disabilitati all'apertura del menu e che
`Ctrl+X`, `Ctrl+C` e `Ctrl+V` usino lo stato corrente dell'editor anche quando
le corrispondenti voci di menu erano rimaste disabilitate.

## Esegui

| Comando | Verifica dinamica |
| --- | --- |
| Avvia | esegue `main` con `F5`, seleziona Output e verifica valore e completamento |
| Breakpoint | attiva con `F9` o dal gutter, marca la riga, sospende sullo statement e continua con `F5` |
| Arresta | espone `Shift+F5` e viene abilitato soltanto durante un'esecuzione |

Il core verifica inoltre cancellazione cooperativa, limite anti-loop ed errori
runtime posizionati nel sorgente.

## Visualizza

| Comando | Verifica dinamica |
| --- | --- |
| Comprimi blocco | comprime più blocchi dalla firma nella stessa proiezione di sola lettura |
| Espandi tutto | ripristina il sorgente originale senza alterarne il contenuto |

Sono controllati anche testo e disponibilità contestuale dei due comandi, i
colori, la colonna dei numeri di riga, l'espansione selettiva di un singolo
blocco e il blocco delle modifiche nella vista compressa.

## Help

| Comando | Verifica dinamica |
| --- | --- |
| About | apre il popup e verifica icona ad alta risoluzione, autore, sito, licenza MIT, versione e stato beta |

Lo smoke controlla inoltre che la finestra principale sia configurata per
l'avvio massimizzato, che la diagnostica resti visibile anche senza problemi e
che la precedente barra beta non sia più presente nel layout. Verifica anche il
gutter non selezionabile, l'allineamento a sinistra dei numeri di riga, la
larghezza iniziale dell'Outline e le transizioni pinned/auto-hide delle due
palette.

## Barra strumenti

La toolbar espone 14 pulsanti a icona con tooltip per Nuovo, Apri, Salva,
Annulla, Ripristina, Taglia, Copia, Incolla, Trova, Formatta, folding, Run e
Stop, oltre al breakpoint. Lo smoke verifica presenza, ordine funzionale,
accessibilità e condivisione degli handler Breakpoint/Run/Stop con il menu Esegui.

Lo stesso gate verifica che tutte le superfici applicative usino la palette
chiara e che soltanto editor, folding e gutter conservino lo sfondo scuro.
Verifica inoltre la nuova icona incorporata, la centratura delle form secondarie
e il docking senza sovrapposizioni tra palette destra e pannello inferiore.
Il popup Trova/Sostituisci viene inoltre controllato per allineamento della
checkbox, ordine dei pulsanti, dimensioni uniformi e visibilità contestuale.

## Scorciatoie e comando di esecuzione

Lo smoke verifica le 22 scorciatoie dichiarate, incluse `F12` per la definizione,
`F2` per la rinomina, `F9` per i breakpoint, `F5` per Run/Continua e `Shift+F5` per Stop. Il gate completo si avvia con:

```powershell
.\tests\run-dynamic-smoke.ps1
```

Usare `-Visible` per mostrare i form durante l'esecuzione.
