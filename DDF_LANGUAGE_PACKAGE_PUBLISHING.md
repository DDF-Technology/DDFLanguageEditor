# Pubblicazione manuale di un pacchetto linguaggio DDF

Questa guida descrive il flusso locale `template → modifica → test → archivio →
installazione → attivazione`. I pacchetti configurano la sintassi e i servizi
dell'engine DDF; un profilo C-familiare o didattico italiano **non** esegue C o
Java. `manifest.json`, JSON di sezione, `grammar.ebnf` e `tests.json` restano i
file autorevoli. Non modificare il fallback incorporato.

## 1. Scegliere una base

Nel repository sono presenti tre esempi funzionanti:

- `language-packages/ddf`: DDF incorporato e fallback;
- `language-packages/ddf-c-familiar`: grafie familiari al C;
- `language-packages/ddf-it-didactic`: grafie didattiche italiane.

Per una nuova identità usare il progetto dimostrativo
`samples/DDFLanguageEditor.LanguagePackagePublishingDemo`. Richiede il .NET SDK
10 sulla macchina di sviluppo. Dalla radice del repository, in PowerShell:

```powershell
dotnet run --project samples/DDFLanguageEditor.LanguagePackagePublishingDemo -c Release -- starter "D:\DdfPackages\mio-pacchetto" mio-pacchetto "Mio pacchetto" 0.1.0 0.13 .mioddf
```

`starter` crea un pacchetto strutturalmente valido con un test minimo, non una
grammatica vuota. Sostituire `starter` con `clone-ddf` per conservare l'intero
corpus di test del DDF. La cartella di destinazione deve essere nuova; nessun
comando sovrascrive una cartella o un archivio esistente. I modelli usano le API
Core `DdfLanguageAuthoringProjectFactory.CreateBlank` e `CloneEmbeddedDdf`.

## 2. Modificare e provare

Aprire i JSON della cartella generata oppure installare/attivare il pacchetto e
usare `Linguaggio > Package Studio...`. Nello Studio ogni modifica guidata
richiede `Anteprima editor guidati` e poi conferma; i sorgenti diretti hanno
una loro anteprima. Usare `Convalida` prima di generare. Cambiare una grafia
richiede coerenza fra lessico, grammatica, formatter, IntelliSense, test e
profili; modificare solo una keyword può lasciare il pacchetto non caricabile.

`samples/language-package-publication/hello.ddf` è un programma minimo che
restituisce `42`. Il file `example-test-case.json` mostra il corrispondente
caso dichiarativo da aggiungere all'array `cases` di `tests.json` (non è un
pacchetto autonomo). Ogni pacchetto deve avere almeno un caso eseguibile senza
diagnostiche e con `expectedReturn`. I casi dichiarativi vengono eseguiti dal
loader prima dell'installazione e dell'attivazione.

Se si sta pubblicando una nuova versione dello **stesso ID**, aumentare
`packageVersion`, selezionare `Confronta versione...` nel Package Studio e
indicare la cartella precedente. Le rotture certe sono segnalate separatamente
dalle modifiche che richiedono revisione manuale (in particolare grammatica e
semantica). Il confronto non sostituisce i test sui sorgenti reali degli utenti.

## 3. Generare e controllare l'archivio

Dal Package Studio usare `Genera .ddflang...`, oppure dalla radice del repository:

```powershell
dotnet run --project samples/DDFLanguageEditor.LanguagePackagePublishingDemo -c Release -- publish "D:\DdfPackages\mio-pacchetto" "D:\DdfPackages\mio-pacchetto-0.1.0.ddflang"
Get-FileHash "D:\DdfPackages\mio-pacchetto-0.1.0.ddflang" -Algorithm SHA256
```

La CLI stampa lo SHA-256 dell'intero archivio, che deve coincidere con
`Get-FileHash`. L'archivio contiene il `manifest.json` del pacchetto,
`ddflang-checksums.json` con SHA-256/dimensione di ogni file sorgente e
`ddflang-validation.json` con identità, versione e test superati. L'ordine
delle entry e i timestamp sono fissi: due generazioni dallo stesso snapshot
producono gli stessi byte. Non inserire segreti nei file del pacchetto.

## 4. Installare e attivare

Nell'editor standalone aprire `Linguaggio > Gestisci pacchetti...`, scegliere
`Installa archivio`, selezionare il `.ddflang`, poi convalidare e attivare il
pacchetto. L'installazione verifica i checksum e riesegue i test dichiarativi
anche dopo la copia in staging. Non attiva il pacchetto automaticamente.

La stessa coppia ID/versione già installata viene **rifiutata**: incrementare
`packageVersion` per installare la versione nuova accanto alla precedente.
Per tornare al linguaggio base usare `Linguaggio > Ripristina DDF incorporato`.
Gli archivi legacy senza metadati checksum possono ancora essere installati,
ma sono convalidati dal loader e dai test dichiarativi.

## Limiti e responsabilità

SHA-256 rileva alterazioni rispetto all'inventario nell'archivio, ma non prova
chi abbia pubblicato il pacchetto: manca una firma dell'autore. Il report non
certifica compatibilità semantica completa. Installazione, attivazione e
pubblicazione su GitHub sono operazioni distinte; questa procedura non esegue
upload né release pubbliche.
