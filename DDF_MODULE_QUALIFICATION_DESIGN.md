# Qualificazione e alias dei moduli DDF 0.16 — disegno accettato

Stato: **accettato, non ancora implementato**. Questo documento definisce il
contratto della futura verticale DDF `0.16`; il pacchetto canonico DDF `0.13.0`
e il manifesto progetto `1.2` continuano a usare la risoluzione non qualificata
corrente. Il contratto machine-readable è
`ddf-module-qualification-design.json`.

## Obiettivi

- rendere utilizzabili dipendenze dirette che esportano simboli omonimi;
- separare stabilmente identità del modulo, alias del consumatore e nomi export;
- impedire che file, cartelle o ordine delle dipendenze diventino namespace;
- mantenere binding, navigazione, debugger, VM e Roslyn deterministici;
- evitare import impliciti, wildcard e re-export accidentali.

## Manifesto e sintassi

Ogni dipendenza diretta del futuro manifesto progetto `1.3` dichiara un alias
obbligatorio, valido come identificatore DDF e unico nel modulo consumatore:

```json
"dependencies": [
  { "id": "ddf.math", "version": "1.0.0", "alias": "math" },
  { "id": "ddf.business", "version": "2.1.0", "alias": "business" }
]
```

Il sorgente usa `alias::simbolo`:

```ddf
main() out int
{
    math::Point origin;
    math::Color selected << math::Color.Green;
    int total << math::add(20, 22);
    ret total + business::score;
}
```

`::` qualifica esclusivamente un export di una dipendenza diretta. `.` conserva
il significato di accesso a membro o valore enum; perciò
`math::Color.Green` è letto senza ambiguità come alias, tipo esportato e membro
enum. Le dichiarazioni del modulo corrente restano non qualificate.

## Regole di binding

- l'alias appartiene all'intero modulo consumatore, non a un singolo file;
- alias, id e versione sono case-sensitive nella semantica DDF;
- l'alias deve essere unico, non può essere una keyword e non può essere
  ricavato automaticamente dall'id o dal percorso;
- soltanto gli export della dipendenza diretta indicata sono visibili;
- dipendenze transitive, dichiarazioni private e simboli assenti restano
  inaccessibili anche conoscendone nome o posizione;
- gli export delle dipendenze non sono più risolti in forma non qualificata;
- non esistono wildcard, alias di singoli simboli, `open`, `using` o re-export
  impliciti nella prima verticale;
- due dipendenze possono esportare lo stesso nome, perché il binding comprende
  sempre identità/versione del modulo, alias e identità del simbolo;
- un alias rinominato modifica il sorgente e il manifest in un'unica operazione
  workspace; non rinomina né modifica il modulo installato.

Il nuovo manifesto è un cambio di schema esplicito. La migrazione da `1.2` non
inventa alias: l'editor propone valori e segnala collisioni, ma l'utente deve
confermare prima di salvare `1.3`. Build e analisi falliscono chiuse se alias o
riferimenti non sono validi.

## Pipeline e strumenti

AST e IR bound conservano un riferimento risolto, non la sola stringa
`alias::nome`. L'identità stabile contiene modulo, versione, categoria e firma;
l'alias resta metadato del consumatore. VM e Roslyn eseguono lo stesso simbolo
bound, mentre name mangling CLR resta un dettaglio del backend.

Fingerprint, unità di compilazione, import dell'artefatto e source map includono
la tabella degli alias. Completamento offre prima gli alias e, dopo `::`, solo
gli export compatibili; hover mostra alias e identità reale. Vai a definizione,
Trova riferimenti e Rename attraversano il manifest senza inserire i sorgenti
installati nel workspace modificabile. Breakpoint, Call Stack e Watch mostrano
nomi DDF qualificati, mai nomi CLR generati.

## Diagnostiche riservate

| Codice | Significato futuro |
| --- | --- |
| `DDF330` | alias di dipendenza mancante o non valido |
| `DDF331` | alias duplicato nel modulo consumatore |
| `DDF332` | alias sconosciuto nel riferimento qualificato |
| `DDF333` | simbolo assente o non esportato dalla dipendenza indicata |
| `DDF334` | uso non qualificato di un export esterno |
| `DDF335` | tentativo di raggiungere una dipendenza transitiva |
| `DDF336` | migrazione del manifest ambigua o non confermata |

## Ordine di implementazione

1. manifesto `1.3`, schema, migrazione assistita e fingerprint;
2. token `::`, grammatica, AST e diagnostiche;
3. resolver e IR bound con identità stabile dei simboli;
4. VM, moduli, costanti, tipi ed enum qualificati;
5. backend Roslyn, artefatti, linker e test differenziali;
6. completamento, hover, navigazione, Rename, debugger e Outline;
7. corpus con collisioni, privati, transitivi e alias non validi.

L'attivazione richiederà DDF `0.16` e il manifesto progetto `1.3`. DDF `0.14`
resta dedicato agli errori strutturati e DDF `0.15` ai valori opzionali. Fino
alla verticale completa, nessun alias viene dedotto e `::` non appartiene alla
grammatica DDF `0.13`.
