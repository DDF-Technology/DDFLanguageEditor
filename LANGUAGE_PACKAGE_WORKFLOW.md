# Pacchetti linguaggio: scelta, prova e creazione

Questo documento descrive il comportamento attuale; un pacchetto cambia la
sintassi e i servizi editoriali DDF, **non** rende eseguibile un altro
linguaggio (C, Java ecc.).

## Pacchetto attivo e pacchetti installati

`Linguaggio > Informazioni sul pacchetto attivo` mostra ciò che l'editor usa
adesso. `Linguaggio > Gestisci pacchetti` elenca soltanto le versioni copiate
nello store utente. Il DDF incorporato è un fallback interno, quindi non viene
conteggiato come pacchetto installato. Un pacchetto caricato direttamente da
una cartella o da un archivio può essere attivo senza comparire nello store;
per conservarne una copia gestita, usare `Installa attivo` (oppure `Installa
cartella`/`Installa archivio`), poi selezionare la versione installata e
premere `Attiva`. Il gestore distingue questi stati in alto.

`Ripristina DDF incorporato` è un'azione, non una casella da attivare e
disattivare. Riporta l'editor al fallback e salva questa preferenza. Quando
il DDF incorporato è già attivo, la voce è selezionata e disabilitata perché
non c'è nulla da ripristinare. Per cambiare di nuovo lingua si seleziona un
pacchetto installato nel gestore. Un cambio profilo può convertire la sintassi
dei documenti aperti; il programma mostra prima gli eventuali blocchi di
migrazione e non attiva un pacchetto incompatibile.

## Playground e Package Studio

`Playground linguaggio` è un banco di **prova in sola analisi** del pacchetto
scelto: si scrive un esempio isolato e si osservano token, albero, tipi,
diagnostiche, IR e anteprima della formattazione. Non cambia il pacchetto,
non salva codice e non modifica i documenti dell'editor.

`Package Studio` serve invece a **creare e modificare** un pacchetto. Lavora
su una copia/progetto di authoring, espone manifest, lessico, grammatica e altri
contratti, richiede anteprima/conferma e convalida, e genera una cartella o
un `.ddflang`. Il suo `Playground e test` verifica la bozza corrente; il
Playground del menu verifica il pacchetto già caricato. Non sono quindi
duplicati. La finestra presenta quattro schede principali: `Inizia`,
`Playground e test`, `Anteprima` e `Avanzate`. `Inizia` guida identità → sintassi
→ prova → convalida → distribuzione; `Avanzate` raccoglie i dieci editor tecnici
senza renderli tappe obbligatorie. I pulsanti della guida aprono direttamente
la scheda pertinente, anche quando si trova in `Avanzate`.

Per creare un profilo sintattico semplice, nella scheda `Inizia` scegliere una
parola chiave o un operatore, digitare la nuova scrittura e premere il relativo
`Aggiungi al profilo`. Ripetere per gli altri elementi: l'elenco mostra tutte
le sostituzioni preparate e permette di rimuoverle. È possibile anche scambiare
due scritture dello stesso tipo, purché entrambe siano incluse e ogni scrittura
finale sia univoca. Il cambio di un operatore conserva ruolo e precedenze.
Premere
`Anteprima profilo` per elaborare tutto insieme; senza passare da `Aggiungi`
il pulsante include anche gli elementi attualmente selezionati e modificati.
Il riquadro sottostante mostra il ruolo della parola, i possibili riferimenti
in grammatica EBNF, i test e gli snippet IntelliSense, con una riga di codice
di esempio quando disponibile; per l'operatore mostra ruolo, precedenza e
binding runtime. Il cambio guidato prepara insieme `lexicon.json`, i terminali
di `grammar.ebnf`, le etichette pertinenti di `grammar.json`, i sorgenti dei
casi in `tests.json`, gli snippet di `intellisense.json` e i binding in
`runtime.json`. Nei programmi di prova stringhe e commenti restano intatti.
L'anteprima mostra il diff e convalida il pacchetto risultante: **non applica**
la modifica. Una scrittura non valida, duplicata o un risultato non convalidabile
non può essere confermato. Dopo aver esaminato il diff, premere `Conferma e
applica anteprima`, quindi usare `Genera .ddflang...`. Le modifiche precedenti
non confermate vanno prima esaminate e confermate separatamente. Qualsiasi
modifica all'elenco o al campo annulla l'anteprima precedente, per impedire una
conferma obsoleta. Per cambiare precedenze, ruoli, nuove operazioni o regole
strutturali sono ancora necessarie le schede avanzate.
Il pacchetto attivo nell'editor non cambia.

Il pulsante `5 · Genera .ddflang...` della scheda `Inizia` è operativo:
se trova modifiche guidate non confermate mostra l'anteprima e richiede
la conferma; se lo snapshot è già pronto apre la scelta del percorso
dell'archivio. I draft JSON/EBNF richiedono prima `Anteprima sorgenti` o
`Anteprima EBNF` e conferma. La generazione convalida il pacchetto, ma non lo
installa né lo attiva: per questo si usa il gestore pacchetti dell'editor.

## Moduli e libreria standard

Il pacchetto DDF include una libreria standard minima (`ddf.standard`), ma
questa non è un modulo di progetto. Sono presenti esempi di workspace con
moduli in `samples`, non una raccolta ufficiale di moduli base/estesi già
installati globalmente. I moduli vengono installati nello store del singolo
progetto e referenziati esplicitamente. La roadmap prevede una prima famiglia
ufficiale portabile di moduli/librerie prima della 1.0, separata da adapter di
piattaforma e plugin eseguibili.
