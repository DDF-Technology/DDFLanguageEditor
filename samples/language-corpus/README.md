# Corpus normativo DDF

`corpus.json` e' il manifesto eseguibile dei programmi che delimitano il
comportamento DDF 0.13 attualmente attivo. Le quattro directory hanno
significato normativo:

- `valid`: analisi ed esecuzione devono riuscire con il risultato dichiarato;
- `invalid`: la fase dichiarata deve produrre esattamente i codici elencati;
- `incomplete`: l'editor deve recuperare senza eccezioni e mantenere gli span validi;
- `multi-file`: build, risoluzione moduli ed esecuzione seguono il manifest progetto.

Ogni `.ddf` deve essere elencato nel manifesto. Il test Core rifiuta file orfani,
ID duplicati, categorie sovrapposte e risultati diversi dal contratto.
I casi validi sono inoltre incorporati con nome `corpus:<id>` nel `tests.json`
del pacchetto canonico: il loader li esegue prima dell'attivazione e la suite
verifica che sorgente e valore restituito rimangano sincronizzati al manifesto.

La consistenza corrente comprende 12 programmi validi, 16 non validi,
8 sorgenti incompleti e 5 workspace multi-file. I costrutti attivi `fixed`,
`state`, enum nominali, `select` e `continue` hanno casi dedicati; i workspace
`active-features-workspace` e `control-flow-matrix` li combinano fra piu' file.
Il secondo attraversa inoltre `if`, `for`, `while`, `do while`, chiamate e rami
alternativi raggiungibili e non raggiungibili. I programmi validi vengono
provati anche attraverso analisi, IntelliSense, formatter, editor embedded,
debugger, VM e Build; i casi eseguibili sono inoltre confrontati con Roslyn.

Costrutti ancora pianificati, come error handling strutturato, opzionali,
qualificazione dei moduli, collezioni, nuovi tipi, risorse e asincronia, non
entrano nel corpus come funzionalita' supportate finche' la relativa verticale
non viene attivata integralmente.
