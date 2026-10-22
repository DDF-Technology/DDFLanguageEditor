# Iterazione tipizzata DDF 0.17 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.17`; il pacchetto canonico DDF `0.13.0`
non riconosce ancora le forme descritte. Il contratto machine-readable è
`ddf-typed-iteration-design.json`.

## Obiettivi

- attraversare array, future sequenze e viste host senza usare indici manuali;
- rendere esplicito il tipo dell'elemento e verificabile il relativo scope;
- garantire ordine, valutazione e comportamento delle mutazioni deterministici;
- mantenere equivalenza fra VM, Roslyn, debugger, Flowchart e SDK embedded;
- evitare iteratori CLR, reflection e generici universali nel linguaggio.

## Sintassi scelta

```ddf
main() out int
{
    int[4] values;
    values[0] << 10;
    values[1] << 20;
    values[2] << 30;
    values[3] << 40;

    int total << 0;
    each(int value in values)
    {
        if(value == 30)
        {
            continue;
        }

        total << total + value;
    }

    ret total;
}
```

`each(T elemento in espressione)` richiede sempre il tipo esplicito. Il tipo
deve coincidere esattamente con il tipo elemento della raccolta; non vengono
applicate conversioni implicite a ogni iterazione. L'espressione della raccolta
viene valutata una sola volta prima del primo elemento.

Il binding dell'elemento nasce nuovamente a ogni iterazione, è visibile soltanto
nel corpo e non può essere riassegnato, incrementato o decrementato. Non è un
alias scrivibile dell'elemento originale: modificare il binding non aggiorna la
raccolta. La modifica intenzionale di un array continua a usare l'indice e un
normale ciclo `for`.

## Ordine, controllo di flusso e mutazioni

- gli array vengono attraversati dall'indice `0` a `length - 1`;
- una raccolta vuota esegue zero iterazioni;
- `continue` passa all'elemento successivo e `brk` termina il ciclo più interno;
- `ret`, `end`, errori e cancellazione seguono le regole comuni del controllo
  di flusso e del futuro cleanup strutturato;
- l'array sorgente produce uno snapshot superficiale dei valori al momento
  dell'ingresso: assegnazioni successive agli slot originali non cambiano la
  sequenza già acquisita;
- le future sequenze dinamiche vengono congelate in uno snapshot entro i budget
  prima dell'iterazione; nessuna modifica strutturale può alterare il percorso;
- una vista host è enumerabile soltanto se il suo contratto dichiara ordine,
  tipo elemento, limiti e acquisizione snapshot. Una vista live o non ordinata
  viene rifiutata.

Lo snapshot è superficiale: valori scalari ed enum sono copiati; strutture e
array annidati conservano le regole di identità/aliasing DDF già normative. Il
binding resta non scrivibile anche quando il valore referenziato è composto.
Una futura capacità di iterazione mutabile richiederà un costrutto distinto.

## Collezioni supportate

La prima verticale eseguibile coprirà esclusivamente array DDF. Il protocollo IR
è però chiuso e versionato per aggiungere in seguito:

- `sequence<T>` dinamiche con ordine di inserimento;
- viste host in sola lettura con snapshot autorizzato;
- mappe attraverso una vista tipizzata di coppie chiave/valore, dopo la
  definizione del relativo tipo strutturale.

Stringhe, numeri, strutture arbitrarie, oggetti host e mappe non ordinate non
sono implicitamente iterabili. Non esiste duck typing basato sulla presenza di
un metodo CLR `GetEnumerator`.

## IR, debugger e strumenti

La IR conserva un nodo di iterazione strutturato con espressione raccolta,
tipo elemento, simbolo del binding, corpo e destinazioni bound di `continue` e
`brk`. Non conserva `IEnumerable`, enumeratori CLR o dettagli Roslyn. VM e
backend compilato devono produrre gli stessi snapshot e lo stesso ordine.

Durante una pausa il debugger mostra il binding nel solo frame dell'iterazione
corrente e un indice sintetico in sola lettura, presentato dagli strumenti ma
non accessibile dal codice. Step Over attraversa un elemento; Step Into entra
nelle chiamate del corpo; il Flowchart rappresenta acquisizione, condizione di
elemento disponibile, corpo, `continue`, `brk` e uscita.

Completamento, hover e Signature Help espongono il tipo elemento derivato dalla
raccolta. Formatter e profili sintattici conservano la medesima semantica;
eventuali traduzioni di `each`/`in` sono definite dal pacchetto, non hardcoded.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF337` | espressione non iterabile |
| `DDF338` | tipo del binding incompatibile con il tipo elemento |
| `DDF339` | binding dell'iterazione modificato |
| `DDF340` | vista host priva di ordine o snapshot valido |
| `DDF341` | snapshot oltre i limiti autorizzati |

## Ordine di implementazione

1. ruoli lessicali `each`/`in`, grammatica, AST e diagnostiche;
2. type checker e IR strutturata inizialmente per gli array;
3. snapshot deterministico in VM e controllo del budget;
4. lowering Roslyn e test differenziali;
5. formatter, IntelliSense, debugger, Watch e Flowchart;
6. profili sintattici e corpus positivo/negativo;
7. protocollo SDK per viste host, dopo il contratto delle collezioni dinamiche.

L'attivazione richiederà DDF `0.17` e un nuovo pacchetto canonico. DDF `0.14`,
`0.15` e `0.16` restano rispettivamente dedicati a errori strutturati, valori
opzionali e qualificazione dei moduli. Fino alla verticale completa, `each` e
`in` restano identificatori ordinari nella `0.13`.
