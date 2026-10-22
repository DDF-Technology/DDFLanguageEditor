# Gestione degli errori DDF 0.14 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
target della verticale successiva; il pacchetto canonico DDF `0.13.0` non
riconosce ancora le forme descritte. Il contratto machine-readable è
`ddf-error-handling-design.json`.

## Obiettivi

- distinguere errori applicativi recuperabili e fault del motore;
- rendere visibili nel sorgente sia il recupero sia la propagazione;
- eseguire il cleanup una sola volta su ogni uscita cooperativa;
- preservare identità, codice, posizione e stack tra VM, Roslyn e API host;
- evitare eccezioni CLR, filtri dinamici e gerarchie OOP nello script.

## Sintassi scelta

```ddf
load(string text) out int raises operation.invalidInput
{
    attempt
    {
        ret toInt(text);
    }
    recover operation.invalidInput problem
    {
        print(problem.message);
        raise problem;
    }
    always
    {
        print("load completato");
    }
}
```

Le parole previste sono `attempt`, `recover`, `always`, `raises`, `raise` ed
`error`. L'intestazione `raises` dichiara l'insieme verificabile degli errori
recuperabili che possono oltrepassare una funzione DDF. Le funzioni standard e
host forniscono lo stesso dato dal catalogo. Il type checker deve rifiutare una
chiamata potenzialmente fallibile se l'errore non è gestito da un `attempt`
contenitore e non compare nella dichiarazione `raises` della funzione corrente.

Un `attempt` deve avere almeno un `recover` oppure `always`. Ogni `recover`
tipizzato effettua corrispondenza esatta sull'identità stabile, non sul testo del
messaggio né su un tipo CLR. `recover error problem` è il recupero generico di
soli errori recuperabili: può apparire una volta e deve essere l'ultimo handler.
Handler duplicati o irraggiungibili sono errori statici.

Il valore associato all'handler è immutabile e confinato al relativo blocco. I
membri iniziali sono `type`, `code`, `message`, `document`, `start` e `length`.
Lo stack completo resta disponibile a diagnostica e debugger finché il
linguaggio non possiede una sequenza tipizzata in sola lettura.

`raise problem;` può propagare soltanto il valore catturato da un handler attivo:
non costruisce errori arbitrari. Identità, posizione originaria e stack vengono
preservati; il confine corrente viene aggiunto senza riscrivere l'origine.

## Cleanup e precedenza

`always` viene eseguito esattamente una volta dopo il blocco protetto e dopo un
eventuale handler, anche con `ret`, `brk`, futuro `continue`, `end`, errore,
timeout o cancellazione cooperativa. I fault del motore saltano ogni `recover`,
ma attraversano `always` prima di terminare l'esecuzione.

Se il cleanup fallisce dopo un esito normale o recuperato, il suo errore diventa
l'esito corrente. Se esiste già un errore o fault in propagazione, questo resta
primario e l'errore del cleanup viene conservato come soppresso. Un arresto del
processo, un guasto del sistema operativo o una perdita di alimentazione non
possono offrire garanzie di cleanup.

Una transazione `state` viene confermata soltanto se l'intera invocazione,
compresi handler e cleanup, termina con successo. Errori non gestiti, fault o
cleanup falliti causano rollback.

## Ordine di implementazione

1. lessico, grammatica dichiarativa, AST e diagnostiche statiche;
2. IR comune e analisi dei percorsi/errori dichiarati;
3. VM con stack, handler, cleanup e rollback `state`;
4. lowering Roslyn semanticamente equivalente e test differenziali;
5. formatter, IntelliSense, debugger, Flowchart e profili sintattici;
6. corpus completo, inclusi fault non intercettabili e cleanup annidati.

L'attivazione richiederà DDF `0.14` e un nuovo pacchetto canonico: fino ad allora
le nuove parole restano identificatori ordinari nella `0.13`.
