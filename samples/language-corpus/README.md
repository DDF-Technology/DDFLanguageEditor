# Corpus normativo DDF

`corpus.json` e' il manifesto eseguibile dei programmi che delimitano il
comportamento DDF 0.12. Le quattro directory hanno significato normativo:

- `valid`: analisi ed esecuzione devono riuscire con il risultato dichiarato;
- `invalid`: la fase dichiarata deve produrre esattamente i codici elencati;
- `incomplete`: l'editor deve recuperare senza eccezioni e mantenere gli span validi;
- `multi-file`: build, risoluzione moduli ed esecuzione seguono il manifest progetto.

Ogni `.ddf` deve essere elencato nel manifesto. Il test Core rifiuta file orfani,
ID duplicati, categorie sovrapposte e risultati diversi dal contratto.
