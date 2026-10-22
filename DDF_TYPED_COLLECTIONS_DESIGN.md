# Collezioni tipizzate DDF 0.18 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.18`; il pacchetto canonico DDF `0.13.0`
non riconosce ancora le forme descritte. Il contratto machine-readable è
`ddf-typed-collections-design.json`.

## Obiettivi

- offrire una sequenza dinamica, una mappa ordinata e una vista host in sola
  lettura con tipi verificabili;
- mantenere ordine, identità, aliasing e mutazioni deterministici;
- integrare l'iterazione `each` senza dipendere da collezioni CLR;
- usare `maybe T` per l'assenza e gli errori tipizzati per i fallimenti reali;
- evitare un sistema di generici universali prima di averne una necessità.

## Famiglie di tipo chiuse

La prima superficie usa tre famiglie integrate:

```ddf
sequence<int> values;
map<string, int> scores;
view<Customer> customers << customerRepository.snapshot();
```

`sequence<T>`, `map<K,V>` e `view<T>` sono forme riconosciute dal sistema dei
tipi, non template definibili dall'utente. Non possono essere applicate ad
altri costruttori né usate per dichiarare nuovi tipi generici. Le collezioni
sono sempre tipizzate e non accettano valori `object`, `dynamic` o CLR.

Una `sequence<T>` e una `map<K,V>` non inizializzate nascono vuote e non nulle.
Una `view<T>` deve invece provenire da una funzione standard o host autorizzata:
non può essere costruita localmente né lasciata senza inizializzatore.

## Sequenze

Una sequenza conserva l'ordine di inserimento e supporta:

- proprietà in sola lettura `count`;
- indicizzazione `values[index]` in lettura e scrittura;
- `add(value)`, `insert(index, value)`, `removeAt(index)` e `clear()`;
- iterazione `each` sullo snapshot dell'ordine corrente.

Gli indici sono `int`, a base zero. Un indice fuori intervallo produce l'errore
recuperabile già previsto per gli array. `removeAt` restituisce il valore
rimosso; `clear` non modifica l'identità della sequenza.

## Mappe ordinate

Una mappa conserva l'ordine della prima inserzione delle chiavi. Aggiornare una
chiave non la sposta; rimuoverla e inserirla nuovamente la colloca in fondo.
Le chiavi iniziali ammesse sono `int`, `char`, `bool`, `string` ed enum nominali.
Sono esclusi `float`, opzionali, strutture, array e collezioni per evitare
uguaglianza o hashing instabili.

La superficie supporta:

- proprietà in sola lettura `count`;
- `set(key, value)`, `find(key) out maybe V`, `contains(key) out bool`,
  `remove(key) out bool` e `clear()`;
- iterazione `each` su valori `entry<K,V>` immutabili con campi `key` e `value`.

`entry<K,V>` è una forma interna chiusa prodotta dall'iterazione: non è una
tupla generale e non può essere costruita dall'utente. Una chiave assente è
`none`, non un errore e non un valore predefinito ambiguo.

## Viste host in sola lettura

`view<T>` è uno snapshot ordinato, immutabile e limitato fornito da una API host
tipizzata. Espone soltanto `count`, indicizzazione in lettura e `each`. Non
espone l'oggetto host, handle, enumeratori, callback o reflection.

Il descrittore host dichiara tipo elemento, ordine, limite massimo, costo di
snapshot, errori recuperabili e capability richiesta. L'acquisizione avviene
una volta sul confine host; revoca, timeout o errore non producono una vista
parziale. I valori attraversano gli adapter DDF e rispettano lifetime e budget
dell'esecuzione embedded.

## Identità, mutazioni e stato

Sequenze e mappe sono riferimenti DDF gestiti e non nulli. Assegnazione e
passaggio a funzione condividono l'identità; le mutazioni autorizzate sono
visibili attraverso gli alias. Non supportano `==` o `!=`.

L'iterazione acquisisce uno snapshot superficiale prima del primo elemento:
mutazioni successive della collezione originale non alterano percorso, ordine
o conteggio del ciclo corrente. I valori composti conservano l'aliasing DDF già
normativo. Ogni crescita e snapshot consuma budget espliciti di elementi e
memoria; il superamento fallisce atomicamente.

`fixed` non accetta collezioni nella prima verticale, perché non esiste ancora
immutabilità profonda. `state sequence<T>` e `state map<K,V>` restano rinviati
finché persistenza, schema, migrazione, quote e transazioni non saranno estesi.

## IR, backend e strumenti

La IR usa operazioni backend-neutral distinte per costruzione, conteggio,
indice e mutazione. Non contiene `List<T>`, `Dictionary<K,V>`, `IReadOnlyList<T>`
o tipi Roslyn. Il backend può scegliere rappresentazioni interne differenti,
ma ordine, errori, aliasing e atomicità devono coincidere con la VM.

Debugger e Watch mostrano tipo, conteggio e figli entro gli attuali limiti di
profondità/numero; non materializzano l'intera collezione senza richiesta.
Flowchart rappresenta le mutazioni come operazioni e delega l'attraversamento
al nodo `each`. IntelliSense espone soltanto i membri validi per la famiglia e
il tipo concreto. Questi membri sono capacità integrate, non metodi OOP
definibili, sovrascrivibili o raggiungibili tramite reflection.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF342` | argomenti di tipo mancanti o non validi |
| `DDF343` | tipo chiave della mappa non supportato |
| `DDF344` | costruzione o mutazione vietata su `view<T>` |
| `DDF345` | membro non disponibile per la famiglia di collezione |
| `DDF346` | limite di elementi o memoria superato |
| `DDF347` | uso di collezione in `fixed` o `state` non supportato |

## Ordine di implementazione

1. descrittori di tipo chiusi, grammatica e diagnostiche;
2. `sequence<T>` in type checker, IR, VM e Roslyn;
3. `map<K,V>`, ordine stabile ed integrazione con `maybe V`;
4. `entry<K,V>` ed iterazione differenziale;
5. protocollo SDK e adapter snapshot per `view<T>`;
6. formatter, IntelliSense, debugger, Watch e Flowchart;
7. corpus di aliasing, quote, errori, embedded e backend differenziali.

L'attivazione richiederà DDF `0.18` e presuppone i contratti `maybe T`, `each`
e degli errori strutturati. Fino alla verticale completa, `sequence`, `map`,
`view` ed `entry` restano identificatori ordinari nella `0.13`.
