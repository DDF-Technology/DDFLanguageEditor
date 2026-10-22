# Dialoghi host DDF — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce la
capability futura `ui.dialog.message` e il provider
`ddf.ui.dialog.message.v1`. Non aggiunge riferimenti WinForms al linguaggio,
alla IR o all'SDK Core. Il contratto machine-readable è
`ddf-host-dialog-design.json`.

## Superficie DDF

La funzione canonica è:

```ddf
dialogMessage(
    string text,
    string title,
    dialogButtons buttons,
    dialogIcon icon) out dialogResult
```

Il titolo è sempre presente nella firma; `""` chiede all'host il proprio titolo
predefinito. La prima verticale non introduce overload o parametri opzionali.

Gli enum chiusi sono:

- `dialogButtons`: `ok`, `okCancel`, `yesNo`, `yesNoCancel`;
- `dialogIcon`: `none`, `info`, `warning`, `error`, `question`;
- `dialogResult`: `ok`, `cancel`, `yes`, `no`.

Il provider deve restituire un risultato compatibile con i pulsanti richiesti.
La chiusura esplicita della finestra tramite cornice produce `cancel`, anche se
non era visualizzato un pulsante Cancel. Stop, dispose o chiusura dell'host non
restituiscono `cancel`: cancellano cooperativamente la chiamata e l'esecuzione.

Testo e titolo sono testo semplice, non markup. Link, HTML, handle, coordinate,
dimensioni, top-most, suoni, owner nativi e finestre arbitrarie non sono
controllabili dallo script.

## Capability e policy

La presenza della funzione nel catalogo non concede il permesso. Ogni chiamata
richiede la capability esatta `ui.dialog.message` e un provider compatibile
`ddf.ui.dialog.message.v1`.

La policy host dichiara:

- set di pulsanti e icone ammessi;
- lunghezza massima di testo e titolo;
- numero massimo di dialoghi per esecuzione;
- timeout massimo e comportamento headless;
- identità logica dell'owner e del dispatcher, non visibili allo script.

Valori predefiniti: 32.768 code unit per il testo, 256 per il titolo, massimo
8 dialoghi per esecuzione, una sola richiesta pendente per istanza e timeout di
5 minuti. L'host può soltanto restringere questi limiti senza cambiare firma o
semantica.

Lo standalone registra un provider predefinito governato e usa la finestra
principale come owner. Nell'embedded la capability è deny-by-default: l'host la
concede, configura e implementa esplicitamente. CLI, worker headless e test non
aprono finestre; senza un provider simulato producono `ui.dialog.unavailable`.

## Threading e sospensione

Le istruzioni DDF restano sullo scheduler di esecuzione, mai sul thread UI. La
VM o il worker emette una richiesta immutabile e sospende cooperativamente lo
script. L'host inoltra soltanto la presentazione al dispatcher UI configurato,
poi completa la risposta sul canale della capability.

Nessun lock dell'editor o di `state` viene mantenuto durante l'attesa. Output,
Stop e heartbeat dell'host restano reattivi. Una seconda richiesta concorrente
della stessa istanza viene rifiutata come `ui.dialog.busy`; non viene creata una
coda nascosta e non è ammessa reentrancy dal callback del provider.

Durante una chiamata il debugger espone lo stato `awaiting-capability` con la
sola identità `ui.dialog.message`, senza owner o dettagli UI. Un breakpoint
prima della chiamata non apre il dialogo; dopo la risposta lo stepping riprende
sulla successiva posizione DDF. Stop cancella la richiesta pendente. Una pausa
del debugger non deve bloccare il dispatcher UI.

## Richiesta e risposta

La richiesta immutabile contiene soltanto:

- execution ID e call ID monouso;
- testo, titolo, pulsanti e icona tipizzati;
- scadenza e token di cancellazione;
- capability ID, provider contract e policy fingerprint.

La risposta contiene call ID, `dialogResult` e ricevuta priva di dati nativi.
Risposte duplicate, tardive, con ID errato o risultato incompatibile producono
un fault di contratto. Il worker fuori processo usa il canale brokerizzato
esistente: non carica toolkit grafici e non crea UI.

## Errori ed effetti

L'effetto semantico è `ui`; la funzione non è pura, non è utilizzabile in
`fixed`, non viene ritentata automaticamente e produce una ricevuta di effetto.
Le identità recuperabili future sono:

- `ui.dialog.denied`;
- `ui.dialog.unavailable`;
- `ui.dialog.busy`;
- `ui.dialog.timeout`;
- `ui.dialog.cancelled`.

La compatibilità corrente usa `DDF409` per capability negata/assente, `DDF413`
per quota o richiesta concorrente, `DDF414` per timeout e `DDF415` per cleanup.
`DDF424` è riservato a una risposta provider incoerente ed è un fault del
motore, non un errore gestibile dallo script.

## SDK e strumenti

Il contratto SDK pianificato `0.38` aggiunge interfacce opzionali: provider,
policy, dispatcher, request, response e receipt. Nessun membro obbligatorio
viene aggiunto alle interfacce host esistenti. Host non aggiornati continuano a
funzionare senza la capability.

Catalogo host, Explorer API, IntelliSense, Signature Help e hover mostrano
disponibilità, firma, effetto, limiti e valori enum dal medesimo snapshot. Non
deducono il supporto dalla presenza di WinForms o dal nome della funzione.

## Verifica

I test automatici usano un provider in memoria e coprono richiesta, risultato,
chiusura, diniego, headless, limiti, timeout, reentrancy, risposta tardiva,
cancellazione, dispose e equivalenza VM/Roslyn senza aprire finestre. Gli smoke
manuali verificano ownership, focus, accessibilità e dispatcher nello standalone
e nella HostDemo embedded.

## Ordine di implementazione

1. enum DDF e descrittore capability/API;
2. contratti SDK opzionali e provider simulato;
3. sospensione cooperativa VM e bridge Roslyn/worker;
4. adapter WinForms standalone con owner e dispatcher;
5. pubblicazione embedded deny-by-default ed Explorer API;
6. integrazione debugger, ricevute, quote e cleanup;
7. test automatici e smoke manuali standalone/embedded.

Fino alla verticale completa `dialogMessage`, i tre enum e il provider non sono
registrati nel pacchetto o negli host correnti.
