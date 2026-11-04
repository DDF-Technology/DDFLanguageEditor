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
su una copia/autoring project, espone manifest, lessico, grammatica e altri
contratti, richiede anteprima/conferma e convalida, e genera una cartella o
un `.ddflang`. Il suo `Playground e test` verifica la bozza corrente; il
Playground del menu verifica il pacchetto già caricato. Non sono quindi
duplicati. Il percorso iniziale nella scheda `Inizia` guida identità → sintassi
→ prova → convalida → distribuzione; le altre schede restano avanzate.

## Moduli e libreria standard

Il pacchetto DDF include una libreria standard minima (`ddf.standard`), ma
questa non è un modulo di progetto. Sono presenti esempi di workspace con
moduli in `samples`, non una raccolta ufficiale di moduli base/estesi già
installati globalmente. I moduli vengono installati nello store del singolo
progetto e referenziati esplicitamente. La roadmap prevede una prima famiglia
ufficiale portabile di moduli/librerie prima della 1.0, separata da adapter di
piattaforma e plugin eseguibili.
