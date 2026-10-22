# Valori opzionali DDF 0.15 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.15`; il pacchetto canonico DDF `0.13.0`
non riconosce ancora le forme descritte. Il contratto machine-readable è
`ddf-optional-values-design.json`.

## Obiettivi

- rappresentare presenza e assenza senza introdurre `null`;
- obbligare il codice a gestire l'assenza prima di usare il valore;
- mantenere la stessa semantica in IR, VM, Roslyn, debugger e SDK;
- distinguere l'assenza, che è un dato normale, da un errore recuperabile;
- offrire un confine esplicito per database, ricerca e API host.

## Sintassi scelta

```ddf
findScore(int identifier) out maybe int
{
    if(identifier == 42)
    {
        ret some(100);
    }

    ret none;
}

main() out int
{
    maybe int candidate << findScore(42);

    select(candidate)
    {
        when some(value)
        {
            ret value;
        }

        when none
        {
            ret 0;
        }
    }
}
```

`maybe T` è un tipo distinto da `T`. `some(expression)` costruisce un valore
presente e `none` rappresenta l'assenza; `none` riceve il tipo dal contesto.
Non esistono conversioni implicite fra `T` e `maybe T`, né accessi non
controllati come `.value` o `unwrap`.

L'accesso al contenuto avviene estendendo `select` con i pattern
`when some(binding)` e `when none`. Una selezione opzionale deve contenere
esattamente entrambi i rami; `otherwise` non è ammesso, così l'assenza non può
essere nascosta. Il binding di `some` è immutabile e visibile soltanto nel ramo.

## Regole di tipo e runtime

- `maybe void`, `maybe error` e `maybe maybe T` sono vietati nella prima
  verticale; sono ammessi primitivi, enum, strutture e array supportati;
- una variabile `maybe T` non inizializzata assume deterministicamente `none`;
- parametri e risultati possono essere opzionali; `fixed maybe T` è ammesso
  soltanto con un costruttore costante valido;
- `state maybe T` resta rinviato finché schema, migrazione e adapter di
  persistenza non includono esplicitamente la rappresentazione opzionale;
- due valori dello stesso `maybe T` sono confrontabili se `T` supporta
  l'uguaglianza: `none == none`, `some(x) == some(y)` secondo `T`, mentre
  presenza e assenza sono differenti;
- l'assenza non produce diagnostiche o stack di errore. Un risultato database
  non trovato usa `maybe`; query non valida, permesso negato o guasto usano il
  contratto degli errori tipizzati.

## ABI, SDK e strumenti

La IR conserva un valore tagged backend-neutral equivalente a
`{ hasValue, value }`; non espone `Nullable<T>`, riferimenti CLR o tipi Roslyn.
Il backend Roslyn può scegliere una rappresentazione interna compatibile, ma
non può cambiarne semantica. Firma di modulo, fingerprint e manifest includono
la forma completa `maybe T`.

Le API host dichiarano esplicitamente parametri e risultati opzionali e usano
un adapter che distingue presenza e assenza. Un valore CLR `null` non viene mai
convertito automaticamente in `none`. Watch e Variabili mostrano `some(...)` o
`none`; il contenuto è visibile come binding soltanto nel ramo attivo. Il
Flowchart rappresenta due archi denominati `Presente` e `Assente`.

## Ordine di implementazione

1. lessico, grammatica, AST, diagnostiche e type checker;
2. valore tagged nella IR comune, firme e fingerprint;
3. VM, costanti, chiamate di funzione e moduli;
4. lowering Roslyn e test differenziali;
5. adapter SDK/host, database e persistenza `state` versionata;
6. formatter, IntelliSense, debugger, Watch, Flowchart e profili sintattici;
7. corpus positivo/negativo e compatibilità degli artefatti.

L'attivazione richiederà DDF `0.15` e un nuovo pacchetto canonico. DDF `0.14`
resta dedicato alla verticale degli errori strutturati: le due semantiche devono
integrarsi, ma non saranno introdotte insieme. Fino alla consegna completa,
`maybe`, `some` e `none` restano identificatori ordinari nella `0.13`.
