# DDF CLI

Stato: primo slice della `0.13.3`. La CLI usa la stessa preparazione Core e lo
stesso backend Roslyn verificato dell'IDE, ma rimane un prodotto esclusivamente
standalone: non viene referenziata né esposta dall'SDK embedded.

## Comandi disponibili

```text
ddf validate [progetto] [--diagnostics text|json]
ddf test [progetto] [--diagnostics text|json]
ddf build [progetto] [--output directory] [--diagnostics text|json]
ddf run [progetto] [--output directory] [--worker path]
        [--timeout secondi] [--diagnostics text|json]
ddf package [progetto] --runtime win-x64 [--name nome]
            [--output directory] [--diagnostics text|json]
ddf --version
```

`progetto` può indicare la cartella contenente `ddf-project.json` oppure il
manifest stesso; se omesso viene usata la cartella corrente.

`validate` carica il pacchetto DDF incorporato di fallback, risolve sorgenti e
moduli, esegue parser, controlli semantici e lowering condivisi senza scrivere
artefatti.

`test` convalida prima il progetto e riesegue il corpus dichiarativo
`tests.json` del pacchetto linguistico attivo. Ogni caso può verificare token,
diagnostiche, formattazione e valore runtime; il report espone nome, esito e
messaggio, oltre al riepilogo totale/superati/falliti. Questo primo contratto è
un test di conformità del linguaggio, non introduce ancora asserzioni
applicative nei sorgenti DDF né capability host simulate.

`build` ripete la stessa preparazione, crea il piano .NET, genera DLL e Portable
PDB deterministici e li conserva nella cache content-addressed sotto
`<output>/<chiave>`. Se `--output` non è indicato, la radice è
`<progetto>/<output.directory>/dotnet`. Ogni hit viene ricostruito e verificato
prima del riuso.

`run` parte dallo stesso artefatto content-addressed, collega e verifica il
bundle, quindi lo invia al worker monouso della `0.13.2`. Non esiste fallback
in-process: worker assente, timeout, attestazione del token o contenimento OS
falliti terminano il comando. Il percorso viene risolto accanto alla CLI, dalla
build di sviluppo, tramite `DDF_RUNNER_PATH` oppure `--worker`; quest'ultima
opzione è utile a host e test che distribuiscono il worker separatamente.
Capability host non disponibili nella CLI vengono rifiutate prima dell'avvio.
`--timeout` accetta da 1 a 3600 secondi e vale 30 secondi per default.

`package` produce il primo EXE DDF distribuibile per un RID esplicito. Il
profilo iniziale supporta `win-x64`, è self-contained, non richiede .NET sul PC
destinazione e non usa ancora single-file o trimming. Il package contiene
soltanto l'EXE principale e `support\` nella radice; runtime, moduli DDF, ABI
runtime, licenze, avvisi e il manifest `ddf-publish.json` `1.2` con dimensioni
e SHA-256 risiedono nella cartella di supporto. L'apphost risolve
`support/<nome>.dll` rispetto al proprio percorso, non alla directory di lavoro.
In assenza di un override applicativo il publisher incorpora l'icona DDF e ne
registra SHA-256 e `publicationFingerprint` nel manifest.
Il gate automatico avvia inoltre l'EXE con ricerca .NET globale neutralizzata e
ricontrolla runtime locale, inventario, dimensioni e hash del payload.
Quando il catalogo
standalone risolve DLL .NET esterne, il publisher le incorpora soltanto se
riceve per ogni identità un'approvazione esplicita di redistribuzione con
licenza; in caso contrario termina con `DDFC605`. Le DLL autorizzate vengono
ricontrollate rispetto al bundle e dichiarate in `externalReferences`, mentre
licenze e avvisi sono raccolti in `support/THIRD-PARTY-LICENSES`. Roslyn e
`DDFLanguageEditor.Compiler` sono dipendenze di build e vengono rifiutati se
appaiono nell'output; anche le capability che richiedono un host bloccano la
pubblicazione. Senza `--output`, la destinazione è
`<progetto>/.ddf-build/publish/win-x64/<nome>`; la directory finale deve essere
nuova per evitare sovrascritture implicite.

## Diagnostiche e codici d'uscita

Il formato `text` è pensato per terminali e usa righe
`percorso(riga,colonna): codice: messaggio`. Il formato `json` emette un unico
documento `1.0` con comando, esito, stato, percorso del risultato e diagnostiche,
adatto a CI senza dover analizzare testo localizzato.

| Codice | Significato |
|---:|---|
| `0` | operazione completata |
| `1` | errore interno non classificato |
| `2` | progetto, sorgente o artefatto non valido |
| `3` | programma avviato ma terminato con diagnostica runtime DDF |
| `64` | sintassi della CLI non valida |
| `70` | ambiente, filesystem o toolchain non disponibile |

Il contract test esegue realmente `validate`, `test`, due `build`, `run` e `package`,
verifica JSON e codici d'uscita, richiede un cache hit alla seconda build,
richiede che tutti i sette casi dichiarativi del pacchetto siano visibili e
superati,
ottiene risultato `7` e output `cli` sia dal worker sia dall'EXE self-contained,
controlla manifest/licenze e l'assenza delle dipendenze di compilazione, e
conserva `DDF404` come errore runtime; controlla inoltre un sorgente non valido
e un comando sconosciuto.

## Accettazione su macchina pulita

Il gate automatico non sostituisce la prova distributiva esterna. Il progetto
`samples/clean-vm-acceptance` e gli script in `tools` preparano un package
identificato da hash e fingerprint, lo eseguono in Windows Sandbox o in una VM
x64 priva di .NET moderno e conservano il verbale JSON. Procedura, criteri e
artefatti da archiviare sono descritti in
[DDF_CLEAN_VM_ACCEPTANCE.md](DDF_CLEAN_VM_ACCEPTANCE.md). La roadmap resta
aperta finché tale verbale non proviene realmente da un ambiente pulito.
