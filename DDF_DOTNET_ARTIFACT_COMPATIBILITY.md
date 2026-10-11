# Compatibilità e verifica degli artefatti DDF .NET

Stato: gate conclusivo della milestone `0.13.0`.

Questa specifica disciplina l'ispezione che precede il futuro caricamento. Il
codice corrente **non carica né esegue assembly**: legge metadati e payload come
byte, convalida contratti, hash e limiti e restituisce diagnostiche strutturate.
Il loader reale appartiene alla `0.13.2`.

## Contesti autorizzati

| Contesto | Ispezione | Futuro caricamento |
| --- | --- | --- |
| Standalone | consentita | consentito dopo tutti i gate |
| CLI | consentita | consentito dopo tutti i gate |
| Editor embedded | metadati leggibili solo dagli strumenti standalone | sempre vietato |

`DDFC201` blocca in modo incondizionato un artefatto compilato destinato a un
host `Embedded`. L'embedded continua a usare `PrepareScript` e la VM in memoria;
non riceve API per emit, load o esecuzione di assembly DDF.

## Matrice di compatibilità

`DdfDotNetArtifactCompatibility.Inspect` richiede corrispondenza esatta per:

- schema manifest/source map e backend;
- contratto dell'unità di compilazione e versione IR;
- Target Framework dichiarato;
- runtime ABI DDF;
- Runtime Identifier, quando il manifest non è portabile;
- capability host richieste;
- identità, versione, fingerprint e nome assembly di ogni dipendenza DDF.

La compatibilità non viene dedotta da una versione “più recente”: finché non
esisterà una policy esplicita, TFM, ABI e dipendenze sono contratti esatti. Le
capability host sono ID deny-by-default; una capability assente blocca il
caricamento. Esse non rendono automaticamente accessibile alcuna API .NET,
COM o nativa.

La verifica di una singola dipendenza disponibile non sostituisce il link del
programma. Dal manifest `1.1`, `DdfDotNetArtifactLinker` chiude l'intero grafo e
confronta anche ogni import con l'export tipizzato corrispondente prima che un
loader possa essere autorizzato.

## Verifica dei payload

`VerifyPayloads` risolve ogni percorso sotto una radice già scelta dallo
standalone/CLI, rifiuta uscite dalla cartella e file mancanti, confronta
dimensione e SHA-256 e non invoca reflection, `Assembly.Load` o inizializzatori.

Dopo il link, `DdfDotNetPeVerifier` aggiunge un gate strutturale sui byte delle
DLL: PE CLR leggibile, IL-only, identità assembly e riferimenti DDF coerenti,
assenza di entry point, ModuleRef nativi e `P/Invoke`. Il reference set fornito
deve avere lo stesso fingerprint della ricevuta e ogni riferimento esterno deve
coincidere per nome, versione, cultura e public key token. Tutti i corpi IL sono
decodificati e controllati per opcode, operandi, token, branch, switch, firme
locali e regioni di eccezione; budget configurabili impediscono metadati o IL
eccessivi. La verifica semantica di stack e dataflow e la persistenza della
allowlist nel package restano obbligatorie prima del loader di prodotto.
Il manifest deve dichiarare esattamente una DLL e al massimo un Portable PDB e
una source map.

I limiti predefiniti sono:

| Risorsa | Limite |
| --- | ---: |
| singolo payload | 256 MiB |
| totale payload | 512 MiB |
| payload | 3 |
| dipendenze DDF | 256 |
| export | 10.000 |
| documenti source map | 10.000 |
| sequence point | 2.000.000 |

Lo standalone o la CLI possono imporre limiti più restrittivi, mai aggirarli
automaticamente per far passare un progetto. Prima del futuro caricamento deve
essere completata l'intera verifica, inclusa ogni dipendenza transitiva.

## Diagnostiche di compatibilità

| Codice | Significato |
| --- | --- |
| `DDFC200` | ambiente runtime assente |
| `DDFC201` | caricamento embedded vietato |
| `DDFC202` | Target Framework incompatibile |
| `DDFC203` | runtime ABI DDF incompatibile |
| `DDFC204` | Runtime Identifier incompatibile |
| `DDFC205` | capability host mancante |
| `DDFC206` | quantità o dimensione payload oltre i limiti |
| `DDFC207` | troppe dipendenze o esportazioni |
| `DDFC208` | source map oltre i limiti |
| `DDFC209` | dipendenza compilata assente o incompatibile |
| `DDFC210` | radice artefatto assente o non valida |
| `DDFC211` | payload mancante o esterno alla radice |
| `DDFC212` | dimensione reale non valida o oltre budget |
| `DDFC213` | hash reale non corrispondente |

Le diagnostiche del formato `DDFC001`–`DDFC019` restano preliminari a queste:
un manifest strutturalmente invalido non arriva alla valutazione runtime.

## Limiti e sicurezza futura

Una verifica positiva significa soltanto “compatibile con la policy dichiarata”,
non “codice fidato”. Gli assembly non attendibili dovranno essere eseguiti in un
processo isolato con permessi del sistema operativo; un `AssemblyLoadContext`
separato facilita lo scaricamento ma non costituisce un sandbox di sicurezza.

Il futuro loader dovrà inoltre:

1. verificare ricorsivamente manifest e payload prima del primo load;
2. chiudere l'insieme delle reference consentite;
3. impedire risoluzioni implicite dalla macchina o dalla GAC;
4. caricare esclusivamente nello standalone/CLI;
5. mantenere PDB e source map opzionali ma coerenti con lo stesso fingerprint;
6. non esporre mai nomi C# generati nelle UI DDF.
