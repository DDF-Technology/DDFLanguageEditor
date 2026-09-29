# Formato dei pacchetti modulo DDF 1.0

Stato: normativo per DDF `0.12.3`.

## Scopo

Un pacchetto modulo distribuisce sorgenti DDF già organizzati come modulo. Usa
l'estensione `.ddfmod` ed è un archivio ZIP con `ddf-project.json` alla radice.
L'identità, la versione, gli export e le dipendenze provengono esclusivamente dal
manifesto schema `1.1`: nome del file, percorso dell'archivio e directory di
installazione non sono namespace e non cambiano la semantica.

Il formato contiene sorgenti, non assembly .NET e non codice nativo. Non concede
capability host, reflection o accesso a librerie di sistema.

## Contenuto

Il packer Core include:

- `ddf-project.json` alla radice;
- tutti e soli i sorgenti esistenti selezionati da `sources`, conservandone i
  percorsi relativi;
- nessun output di build e nessun modulo annidato sotto `.ddf/modules`.

L'ordine delle entry e il timestamp ZIP sono deterministici. Lo stesso modulo
immutato produce quindi lo stesso archivio byte per byte. Risorse aggiuntive non
fanno ancora parte del contratto `1.0`; quando serviranno saranno dichiarate dal
manifesto, non raccolte implicitamente dalla cartella.

## Store e installazione

`DdfModulePackageStore` gestisce lo store locale del progetto:

```text
<project>/.ddf/modules/<module.id>/<module.version>/
```

`Pack(moduleRoot, packagePath)` crea un `.ddfmod`. `Install(sourcePath)` accetta
un archivio oppure, per sviluppo locale, una cartella modulo. Versioni diverse
della stessa identità sono installate affiancate. La stessa versione è
immutabile per impostazione predefinita: deve essere sostituita passando
esplicitamente `replaceExisting: true`. `List()` restituisce soltanto directory
la cui identità coincide con il percorso dello store; `Remove(id, version)`
rimuove esclusivamente la coppia esatta.

`Inspect(sourcePath)` applica gli stessi controlli senza modificare lo store e
restituisce identità, versione, linguaggio, export e dipendenze per l'anteprima.
Nello standalone **Moduli > Gestisci moduli progetto...** usa questa operazione
prima di chiedere conferma. Il comando richiede una cartella workspace e non è
esposto automaticamente dalla shell embedded.

L'installazione copia o estrae prima in staging, ricarica e valida il manifesto e
verifica che esso selezioni almeno un sorgente. Solo dopo sposta atomicamente il
contenuto nella destinazione. In caso di errore ripristina la versione precedente.

## Limiti di sicurezza

Il gestore rifiuta:

- estensioni archivio diverse da `.ddfmod`;
- percorsi assoluti, segmenti `.`/`..`, drive e path duplicati senza distinzione
  fra maiuscole e minuscole;
- più di 4096 file, singoli file oltre 16 MiB o payload oltre 64 MiB;
- manifesti non validi, sorgenti dichiarati mancanti e destinazioni esterne allo
  store;
- sovrascritture della stessa versione non richieste esplicitamente.

Queste protezioni rendono sicura la gestione del filesystem, ma non attestano
l'autore del pacchetto. Firma, provenienza remota, lock file e risoluzione da
registry restano fuori dal formato `1.0`.

## Diagnostiche del gestore

| Codice | Significato |
| --- | --- |
| `DDFM001` | sorgente o percorso non valido/non trovato |
| `DDFM002` | estensione del pacchetto non valida |
| `DDFM003` | manifesto non valido |
| `DDFM004` | payload sorgente mancante o non valido |
| `DDFM005` | creazione dell'archivio fallita |
| `DDFM006` | identità/versione già installata senza sostituzione esplicita |
| `DDFM007` | estrazione o installazione sicura fallita |

La validazione semantica completa degli export e del grafo avviene nella normale
pipeline di build quando un progetto consuma il modulo. Il gestore non attiva né
esegue codice durante installazione o rimozione.
