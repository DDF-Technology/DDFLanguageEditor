# Core di authoring dei pacchetti linguaggio

Stato: **fondazione implementata per DDFLanguageEditor 0.14.0**.

Il Core di authoring è indipendente da WinForms e modella un progetto di
pacchetto come snapshot immutabile di documenti UTF-8. JSON ed EBNF restano le
fonti autorevoli: il modello non converte i file in oggetti UI e non installa il
risultato nel catalogo dei linguaggi.

## Contratti correnti

- `DdfLanguageAuthoringProject` conserva documenti, radice sorgente e revisione;
- ogni documento conserva testo originale, percorso relativo e SHA-256;
- `DdfLanguageAuthoringChangeSet` applica un gruppo atomico di upsert/rimozioni
  soltanto alla revisione attesa;
- ogni modifica riuscita restituisce il change set inverso per l'annullamento;
- `DdfLanguageAuthoringPreview` rappresenta senza effetti i file aggiunti,
  modificati o rimossi e il relativo diff a righe con numeri vecchi/nuovi;
- `IDdfLanguageAuthoringTransformation` deve produrre un change set prima che
  una trasformazione strutturale possa essere presentata e confermata;
- `ApplyPreview` accetta la conferma soltanto se revisione e fingerprint
  dell'intero progetto coincidono ancora con lo snapshot mostrato;
- `DdfLanguageAuthoringSession` conserva uno storico undo/redo limitato e
  thread-safe; ogni voce lega change set diretto e inverso ai fingerprint prima
  e dopo l'operazione;
- `Validate` materializza uno snapshot temporaneo e usa lo stesso
  `DdfLanguagePackageLoader` dell'applicazione;
- `GenerateDirectory` scrive prima in staging, convalida il pacchetto e promuove
  la directory soltanto se la destinazione non esiste;
- `IDdfLanguageAuthoringMigration` descrive passaggi espliciti fra versioni di
  schema; percorsi mancanti, ambigui, ciclici o non correlati alla revisione
  vengono rifiutati.

Caricamento e generazione rifiutano percorsi assoluti, risalite e reparse point.
File non riconosciuti e commenti restano byte-per-byte invariati finché una
modifica non li sostituisce esplicitamente. Questo permette al futuro editor
guidato di lavorare senza distruggere metadati che ancora non comprende.

Il diff usa un confronto deterministico line-based e quote esplicite. Per
documenti eccezionalmente grandi passa a una rappresentazione conservativa
"tutto rimosso/tutto aggiunto", evitando memoria non limitata senza nascondere
alcuna modifica.

## Confini intenzionali

La fondazione non offre ancora editor strutturali visuali, salvataggio di una
sessione di authoring, archivio `.ddflang`, installazione o UI. La
generazione corrente produce una directory validata, non un pacchetto
distribuibile: packaging riproducibile e installazione appartengono alla
`0.14.2`.

Le future UI useranno direttamente anteprima, diff e conferma del Core, senza
introdurre un secondo formato proprietario.

## Creazione e clonazione

`DdfLanguageAuthoringProjectFactory` produce progetti distaccati con una nuova
identità dichiarata e supporta cinque origini:

- starter strutturale vuoto;
- DDF incorporato;
- directory di pacchetto;
- archivio `.ddflang`;
- versione presente nel `DdfLanguagePackageStore`.

Lo starter vuoto conserva il substrato dichiarativo DDF necessario al motore ma
riduce la suite a un singolo test di salute. Non può essere letteralmente privo
di regole o test, perché non sarebbe caricabile né verificabile. Gli editor
guidati successivi permetteranno di sostituire progressivamente lessico,
grammatica e sezioni mantenendo sempre visibili le diagnostiche.

Ogni clone riceve ID, nome, versione pacchetto, versione linguaggio ed
estensioni nuovi; il sorgente non viene modificato o installato. Directory e
archivi vengono validati dal loader reale prima della clonazione. Gli archivi
sono estratti in un'area temporanea con quote su file e dimensioni, nomi unici e
soltanto percorsi top-level sicuri. Un clone da store verifica inoltre che il
descrittore installato coincida con identità e versione presenti su disco.

## Verifica

Il gate Core carica il pacchetto DDF incorporato come progetto di authoring,
conserva un file sconosciuto contenente commenti, applica e annulla una modifica,
rifiuta revisione obsoleta e path traversal, convalida tramite il loader reale,
genera una directory senza sovrascriverla e attraversa una migrazione dichiarata
da schema `0.9` a `1.0`.
Un secondo gate costruisce una trasformazione con aggiunta, modifica e rimozione,
controlla righe e hash del diff, conferma l'anteprima e rifiuta la stessa conferma
dopo una modifica concorrente del progetto.
Un terzo gate concatena trasformazioni in una sessione con storico limitato,
verifica undo e redo esatti, invalidazione del ramo redo dopo una nuova modifica,
assenza di effetti per una conferma fallita e generazione finale da file
temporanei convalidati dal loader reale.
Il gate dei modelli crea uno starter valido, clona DDF, directory, archivio e
pacchetto installato, assegna identità distinte, conserva un file autore
sconosciuto e verifica che il pacchetto sorgente rimanga invariato.
