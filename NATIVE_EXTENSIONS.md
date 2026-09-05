# Estensioni native DDF

Le estensioni native costituiscono il confine esplicito fra i pacchetti di
linguaggio dichiarativi e l'eventuale codice host. Un pacchetto `.ddflang` non
può caricare assembly, indicare percorsi eseguibili o richiamare metodi .NET.
Può soltanto dichiarare in `manifest.json` una lista opzionale
`nativeExtensions`.

## Identità e capacità

Ogni requisito contiene:

- `id`: identificatore stabile del plug-in;
- `publisher`: identità dichiarata dell'autore;
- `version`: versione esatta richiesta;
- `sha256`: impronta esatta del contenuto autorizzabile;
- `capabilities`: insieme minimo di capacità richieste, senza wildcard.

Esempio puramente dichiarativo:

```json
"nativeExtensions": [
  {
    "id": "ddf.console-plus",
    "publisher": "ddf.technology",
    "version": "1.2.0",
    "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "capabilities": [ "runtime:function", "runtime:output" ]
  }
]
```

Il loader convalida forma, duplicati e identità prima di attivare il pacchetto,
ma non cerca né carica il binario. La dichiarazione rimane quindi opzionale e
inerte finché non esiste un'autorizzazione separata.

## Autorizzazione deny-by-default

`DdfNativeExtensionAuthorizationPolicy` confronta ogni requisito con grant
forniti dall'host. Un grant è valido soltanto se `id`, `publisher`, `version` e
`sha256` coincidono e se autorizza tutte le capacità richieste. Gli esiti sono:

- `AuthorizationRequired`: non esiste alcun grant per l'identificatore;
- `IdentityMismatch`: il grant riguarda un'altra versione, autore o impronta;
- `CapabilityNotAuthorized`: l'identità coincide ma manca almeno una capacità;
- `Authorized`: identità esatta e tutte le capacità risultano autorizzate.

L'autorizzazione non risiede nel pacchetto di linguaggio: un pacchetto non può
approvare se stesso. La futura gestione installazione/consenso dovrà conservare i
grant in uno store dell'host, verificare nuovamente SHA-256 prima del caricamento
e revocare il consenso quando cambia identità o contenuto.

## Stato corrente

La `0.9.6.4` introduce contratto, validazione, modello di autorizzazione e
visibilità nella finestra delle informazioni del pacchetto. Non è ancora
presente un loader di binari nativi. Registrazione, isolamento, UI di consenso e
invocazione arriveranno nelle milestone dedicate a runtime generico e gestione
dei pacchetti; fino ad allora nessuna estensione dichiarata viene eseguita.
