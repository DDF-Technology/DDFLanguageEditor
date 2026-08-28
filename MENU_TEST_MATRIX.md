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

Lo smoke conserva e ripristina il contenuto precedente degli appunti Windows.
Verifica inoltre gli stati abilitati/disabilitati all'apertura del menu.

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

## Scorciatoie e comando di esecuzione

Lo smoke verifica le 16 scorciatoie dichiarate, da `Ctrl+N` a
`Ctrl+Shift+M`. Il gate completo si avvia con:

```powershell
.\tests\run-dynamic-smoke.ps1
```

Usare `-Visible` per mostrare i form durante l'esecuzione.
