# Authoring dei linguaggi

DDFLanguageEditor usa pacchetti versionati e convalidati. I file JSON ed EBNF
sono la fonte autorevole: l'interfaccia grafica non è necessaria per creare,
versionare o distribuire un linguaggio.

## Cosa è puramente dichiarativo

Senza ricompilare l'editor è possibile definire o rinominare:

- caratteri, identificatori, numeri, stringhe, commenti, direttive, keyword e operatori;
- produzioni EBNF e metadati dei nodi sintattici supportati;
- tipi primitivi, conversioni, compatibilità, firme e regole di scope;
- collegamenti alle operazioni semantiche e runtime già presenti nel catalogo sicuro;
- stile del formatter, completamento, signature help e funzioni standard basate su binding incorporati;
- backend e opzioni già conosciuti dal motore;
- test di tokenizzazione, diagnostica, formattazione ed esecuzione.

Queste definizioni sono dati: non possono eseguire script, reflection, processi,
chiamate native o codice host arbitrario.

## Quando serve estendere il motore

Serve una nuova versione del motore quando il linguaggio richiede un comportamento
che non esiste nel catalogo chiuso, per esempio un nuovo modello di controllo del
flusso, una nuova categoria di valore, una diversa regola di dispatch o un backend
di compilazione inedito. L'operazione va implementata, identificata, convalidata e
coperta da test nel Core; solo dopo può essere richiamata dal pacchetto.

## Quando serve un plug-in

Un plug-in esplicito è riservato all'integrazione con il sistema esterno: file,
rete, database, dispositivi, processi o librerie native. Deve avere identità,
versione, hash e capacità autorizzate separatamente. Il solo caricamento di un
pacchetto dichiarativo non concede mai tali privilegi.

## Flusso consigliato

1. Copiare un pacchetto valido e assegnargli ID, nome, versione ed estensioni propri.
2. Modificare lessico, EBNF e adapter mantenendo coerenti i riferimenti.
3. Aggiungere casi minimi e casi di errore in `tests.json`.
4. Usare **Help > Linguaggio > Playground linguaggio** durante la progettazione.
5. Usare **Convalida pacchetto attivo** e poi **Gestisci pacchetti** per installarlo.
6. Distribuire il contenuto della cartella come archivio `.ddflang`.

La suite automatica costruisce anche un linguaggio dimostrativo `Nova Demo`: usa
`yield` al posto di `ret` e `:=` al posto di `<<`, quindi lo analizza, formatta ed
esegue senza modificare né ricompilare il codice dell'editor.

## Futuro editor assistito

Il Package Studio è pianificato nella `0.14.x`; lo SDK dei plug-in eseguibili
nella `0.15.x`. Il catalogo delle API registrate dall'applicazione ospitante
appartiene invece al primo SDK di integrazione della `0.11.2`.

L'editor visuale previsto sarà un livello sopra gli stessi file: moduli guidati
dallo schema, anteprima delle differenze, playground e test obbligatori prima
dell'attivazione. Dovrà sempre permettere di vedere e modificare JSON/EBNF grezzi,
senza introdurre un formato parallelo o rendere la UI necessaria alla build.

## Direzione dei profili sintattici

I pacchetti destinati agli utenti offriranno sintassi alternative per la semantica
comune della piattaforma, senza supporto completo a C++, Java, C# o VB. I profili
didattici potranno localizzare keyword, spiegazioni e strumenti; la modalità
didattica resta attivabile e disattivabile con qualsiasi profilo, sia in standalone
sia nell'editor integrato. Il cambio di profilo non converte automaticamente il codice.

Il backend futuro .NET + Roslyn compilerà C# generato dalla IR comune. La VM è
ancora il runtime attuale. Schema, vincoli e decisioni aggiornate sono in
[ECOSYSTEM_ARCHITECTURE.md](ECOSYSTEM_ARCHITECTURE.md) e [ROADMAP.md](ROADMAP.md).
