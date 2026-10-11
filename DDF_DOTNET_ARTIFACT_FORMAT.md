# Formato degli artefatti DDF per .NET

Stato: contratto evoluto a `1.1` nella milestone `0.13.2`, prodotto dall'emitter
Roslyn e pubblicabile atomicamente, inclusi Portable PDB, mappa DDF e interfacce
di import deterministiche.

## File prodotti

Ogni unità di compilazione DDF potrà produrre un insieme atomico di file:

- `<assembly>.dll`: assembly PE gestito;
- `<assembly>.pdb`: simboli **Portable PDB** opzionali;
- `<assembly>.ddfmap.json`: mappa opzionale tra codice generato e sorgente DDF;
- `<assembly>.ddfartifact.json`: manifest obbligatorio dell'insieme.

Il manifest usa `format: ddf-dotnet-artifact` e `schemaVersion: 1.1`. La mappa
usa `format: ddf-dotnet-source-map` e `schemaVersion: 1.1`. Gli schemi macchina
sono rispettivamente `ddf-dotnet-artifact.schema.json` e
`ddf-dotnet-source-map.schema.json`.

Il programma già collegato espone inoltre un manifest di package separato:
`ddf-dotnet-linked-package` `1.0`, descritto da
`ddf-dotnet-linked-package.schema.json`. Non sostituisce i manifest dei moduli:
li blocca insieme ai payload e al reference set verificato.

## Manifest

`DdfDotNetArtifactManifest` registra:

- identità/versione del modulo e versione dell'unità di compilazione;
- fingerprint dell'unità, versione IR, pacchetto e profilo linguistico esatti;
- backend `dotnet-roslyn`, Target Framework e versione della runtime ABI DDF;
- Runtime Identifier opzionale e capability host esatte richieste;
- nome logico dell'assembly;
- ricevuta di build con versione e fingerprint del piano, versione del profilo
  compilatore, Roslyn, C#, e fingerprint del reference set chiuso;
- export DDF con categoria e firma canonica, senza nomi CLR generati;
- import DDF richiesti con modulo/versione, categoria, nome e firma canonica;
- dipendenze DDF esatte con fingerprint e assembly atteso;
- ruolo, percorso relativo, SHA-256 e dimensione di ogni payload.

È obbligatoria esattamente una DLL. PDB portabile e mappa sorgente sono
indipendenti e al massimo singoli. Percorsi assoluti, `..`, duplicati e hash non
validi vengono rifiutati. Il manifest non contiene testo sorgente, IR, timestamp
o percorsi assoluti, così resta riproducibile e non dipende dalla macchina.

Le dipendenze elencate sono moduli DDF già compilati, non riferimenti arbitrari
del sistema. I riferimenti a librerie .NET/COM/native saranno disciplinati nella
`0.13.4` e non possono essere fatti passare come dipendenze DDF.

`DdfDotNetArtifactLinker` risolve il grafo chiuso dei manifest prima di qualsiasi
load. Ogni import deve trovare un export con identità, categoria e firma esatte
nel modulo dichiarato; fingerprint e nome assembly devono coincidere col
descrittore della dipendenza. Moduli, assembly o export duplicati, dipendenze
mancanti, ambienti incompatibili e cicli bloccano il link con `DDFC500`–`DDFC505`.
Il risultato ordina sempre le dipendenze prima dei consumatori e possiede un
fingerprint indipendente dall'ordine di enumerazione dei file.

Il successivo `DdfDotNetPeVerifier` apre le DLL con `PEReader`, mai tramite il
loader CLR. Controlla che il payload sia un assembly IL-only senza entry point,
che l'identità coincida col manifest, che tutti e soli i riferimenti agli altri
moduli DDF diretti siano dichiarati e che non esistano ModuleRef nativi o metodi
`P/Invoke`. `DdfDotNetLinkedBundle` `1.1` conserva poi copie dei quattro payload
per modulo, ordine topologico e fingerprint complessivo. Non è ancora un formato
di distribuzione persistente. Prima di costruirlo, il verifier confronta il
reference set con la ricevuta e con ogni identità assembly esterna, quindi
decodifica tutti i corpi IL controllando opcode, operandi, token, branch, switch,
firme locali e regioni di eccezione entro budget configurabili. Il passaggio
semantico propaga inoltre l'altezza dello stack su tutti i rami e sugli ingressi
dei gestori, usando le firme metadati per `call`, `callvirt`, `newobj` e `ret`.
Il gate firme attraversa anche campi, proprietà, MemberRef, TypeSpec,
MethodSpec e firme standalone/locali, rifiuta costrutti unsafe fuori profilo e
verifica gli indici di argomenti e locali codificati nelle istruzioni.

Il manifest package canonico registra fingerprint del bundle e del programma,
entry module, capability, hash dei quattro payload di ogni modulo e lock delle
reference gestite. Ogni reference contiene identità assembly completa, SHA-256
e dimensione; i percorsi assoluti restano deliberatamente esclusi. In lettura il
fingerprint del reference set viene ricalcolato, quindi una allowlist alterata
non può essere accettata come quella usata in compilazione. La pubblicazione su
disco e il loader restano fasi successive.

Le funzioni host sincrone realmente richiamate sono dichiarate come capability
`host.function.<slug>-<sha256>`. Letture e scritture usano privilegi distinti:
`host.object.read|write.*` per oggetti primitivi e `host.member.read|write.*`
per proprietà di primo livello. Lo slug è soltanto descrittivo; l'hash del nome
o percorso DDF originale preserva maiuscole, punti e underscore senza collisioni
con il formato canonico minuscolo del manifest. Dichiarare una capability non la
implementa: il runner standalone deve sia pubblicizzarla nell'ambiente verificato
sia fornirla esplicitamente tramite la runtime ABI.

## Portable PDB e mappa DDF

Il Portable PDB è il formato di simboli del backend .NET. Non è però il contratto
semantico dell'editor: `DdfDotNetSourceMap` conserva esplicitamente il profilo
linguistico originale, il fingerprint dell'unità e, per ciascun documento:

- percorso relativo e SHA-256 del sorgente originale;
- percorso relativo del documento C# generato;
- sequence point ordinati e non sovrapposti;
- intervallo a caratteri DDF e coordinate riga/colonna generate.

La mappa `1.1` include inoltre una tabella deterministica dei simboli compilati:
identità bound, nome e tipo DDF, categoria, mutabilità, dichiarazione sorgente e
nome CLR privato. Il futuro adapter debugger usa il nome CLR soltanto come chiave
tecnica e presenta sempre il nome DDF in Call Stack, Variabili e Watch. Simboli
host o standard privi di una dichiarazione nel modulo non vengono inseriti.

L'emitter genera oggi un Portable PDB con sequence point nativi verso i
documenti DDF originali e una mappa separata costruita da annotazioni
sintattiche sicure su ogni punto eseguibile. Le regioni C# generate restano
`hidden`; il test del PDB rifiuta qualsiasi sequence point visibile che non
appartenga a un documento `.ddf`. La successiva integrazione debugger userà i
sequence point insieme alla tabella simboli per breakpoint, Call Stack e Watch.
Le UI devono continuare a mostrare
soltanto file, nomi e coordinate DDF; i nomi temporanei C# restano interni.

`DdfDotNetDebugAdapter` è la prima proiezione read-only di questo contratto:
convalida i payload, legge metadati PE e Portable PDB senza caricare l'assembly,
associa token metodo a funzioni DDF, lega file/riga alle sequence point e filtra
parametri e locali secondo gli scope IL. Lo storage CLR non compare nelle sue
API pubbliche. Pausa del processo e lettura dei valori runtime restano compiti
del runner debugger successivo.

## Separazione e sicurezza

Il modello vive in `DDFLanguageEditor.Compiler`, che dipende da Core. Core,
WinForms e SDK embedded non dipendono dal compilatore. La serializzazione rifiuta
proprietà sconosciute e normalizza export, dipendenze, payload e documenti prima
della scrittura deterministica.

`DdfDotNetArtifactEmitter` produce PE gestiti e manifest byte-per-byte
riproducibili in memoria, in ordine topologico. Il manifest è creato solo dopo
un emit riuscito e il relativo payload contiene hash e dimensione del PE.
`DdfDotNetArtifactPublisher` verifica nuovamente byte, manifest e percorsi,
scrive l'intero bundle in una directory di staging sorella e lo rende visibile
con una sola rinomina. Una destinazione già esistente viene rifiutata: nessuna
build precedente viene sovrascritta implicitamente. Errori e manomissioni non
pubblicano risultati parziali e lo staging creato dall'operazione viene rimosso.
DLL, Portable PDB, mappa DDF e manifest vengono verificati e pubblicati insieme;
gli assembly non vengono caricati.

Il primo loader standalone/CLI può ora caricare un bundle che supera nuovamente
tutti questi contratti. Ricostruisce il grafo senza sorgenti, usa un contesto
collezionabile deny-by-default e una sessione monouso; l'embedded viene sempre
rifiutato. Questo percorso in-process è riservato agli artefatti prodotti dal
backend DDF verificato e non sostituisce il futuro isolamento di processo per
package non fidati. Compatibilità
runtime/SDK, verifica dei payload e limiti sono definiti in
[DDF_DOTNET_ARTIFACT_COMPATIBILITY.md](DDF_DOTNET_ARTIFACT_COMPATIBILITY.md);
emit, C# generato e compilazione appartengono alla `0.13.1`, mentre il loader
reale appartiene alla `0.13.2`.
