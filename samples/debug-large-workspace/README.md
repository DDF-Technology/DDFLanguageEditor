# Large debugger stress workspace

Banco di prova manuale per il debugger DDF `0.12.5`.

- 41 sorgenti DDF: `main.ddf` e una catena `stage01`–`stage40`.
- Profondità massima dello stack: 41 frame incluso `main()`.
- Risultato deterministico atteso: `3344`.
- Nessuna dipendenza esterna; pacchetto richiesto: `ddf 0.12.3`.

## Percorso consigliato

1. Aprire questa cartella con **File > Apri cartella progetto**.
2. Mettere un breakpoint in `stage-20.ddf`, riga 3, e uno in
   `stage-40.ddf`, riga 14.
3. Avviare il debug da qualsiasi scheda aperta: l'esecuzione deve comunque
   partire da `main()` e fermarsi soltanto sui punti raggiunti.
4. In `stage20` verificare `value`, `local` e `checkpoint`; lo stack deve
   contenere `main()` e le funzioni da `stage01()` a `stage20()`.
5. Usare F11 per entrare nello stage successivo, F10 per avanzare e
   Shift+F11 per tornare al chiamante, controllando lo switch automatico delle
   schede.
6. In `stage40` aggiungere a Watch `baseValue`, `total`, `index`, `values[0]`
   e `values[3]`, quindi avanzare nel ciclo e verificare gli aggiornamenti.
7. Continuare fino al termine: il valore restituito deve essere `3344`.

Annotare tempi di risposta percepiti, eventuali pause anomale, schede non
sincronizzate, frame mancanti o valori Watch non aggiornati.
