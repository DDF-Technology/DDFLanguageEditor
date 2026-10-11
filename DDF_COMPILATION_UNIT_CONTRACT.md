# Contratto delle unità di compilazione DDF

Stato: contratto backend-neutral `1.0`, introdotto nella milestone `0.13.0`.

Questo documento stabilisce il confine che precede qualsiasi emissione Roslyn,
CIL o bytecode DDF. Non definisce ancora il formato degli assembly .NET: tale
formato, il manifest persistente, i PDB e le mappe di debug appartengono al
passo successivo della roadmap.

## Unità

Una **unità di compilazione** coincide sempre con un modulo DDF identificato da
`module.id@module.version`. Una cartella e un singolo file non costituiscono
automaticamente unità distinte. Tutti i sorgenti selezionati dal manifesto del
modulo appartengono alla stessa unità; ogni dipendenza risolta produce invece
una propria unità.

L'unità è indipendente dal backend e comprende cinque contratti immutabili:

1. identità del modulo, versione esatta del pacchetto linguaggio e versione IR;
2. snapshot sorgenti ordinati, con percorso relativo, testo esatto, SHA-256 e
   indicazione dell'eventuale buffer non salvato;
3. un documento IR strutturale per ogni snapshot sorgente;
4. una vista IR bound `2.1` corrispondente, con simboli, tipi, conversioni,
   categorie di valore, chiamate già risolte e natura pura/mutante delle
   funzioni host;
5. interfaccia pubblica ed elenco delle dipendenze dirette esatte.

`DdfCompilationUnit.ContractVersion` vale `1.0`. Il suo fingerprint include
identità, versioni, dipendenze, firme esportate, percorsi, hash dei sorgenti e
fingerprint della IR bound.
Non dipende da percorsi assoluti, orari, ordine di enumerazione del filesystem o
tipi Roslyn.

## Interfaccia pubblica

`DdfCompilationInterface` contiene esclusivamente gli elementi dichiarati in
`module.exports`. Ogni `DdfCompilationExport` conserva:

- nome DDF case-sensitive;
- categoria `Function`, `Structure` o `Global`;
- firma canonica espressa usando tipi DDF, mai tipi CLR o simboli Roslyn;
- documento relativo e intervallo della dichiarazione originale.

Gli elementi non esportati restano implementazione privata. Gli export delle
dipendenze transitive non vengono riesportati implicitamente. La firma è il
primo contratto verificabile per risolvere e collegare in futuro moduli compilati
separatamente. La IR bound `2.1` ne rafforza l'identità simbolica senza
cambiarne la proprietà da parte del linguaggio DDF.

## Separazione dei livelli

I tre livelli non sono intercambiabili:

| Livello | Modello corrente | Persistenza |
| --- | --- | --- |
| Sorgente | `DdfCompilationSourceSnapshot` | input in memoria; il testo non entra automaticamente nell'artefatto |
| IR strutturale | `DdfCompilationIrDocument` / `DdfIrProgram` | input in memoria condiviso con la VM; nessuna serializzazione implicita |
| IR bound | `DdfBoundCompilationUnit` / `DdfBoundIrDocument` | input semantico del compilatore; nessuna serializzazione implicita |
| Artefatto | formato da definire nel passo successivo | manifest e payload versionati, emessi soltanto dallo standalone/CLI |

`DdfBuildArtifact` rimane per ora la ricevuta e il piano di esecuzione VM. Espone
le `CompilationUnits` soltanto in memoria e le esclude esplicitamente dalla
serializzazione JSON. In questo modo la ricevuta non diventa accidentalmente un
formato compilato pubblico e non incorpora testo sorgente o oggetti IR.

Il futuro backend riceverà `DdfCompilationUnit` e produrrà il descrittore
persistente distinto definito in
[DDF_DOTNET_ARTIFACT_FORMAT.md](DDF_DOTNET_ARTIFACT_FORMAT.md). Nessun backend
può modificare sorgenti, IR o interfaccia
pubblica ricevuti; eventuali mappe verso C#, CIL o bytecode sono output del
backend e non ridefiniscono la semantica DDF.

## Confini applicativi

- Core crea e valida unità backend-neutral senza dipendere da Roslyn.
- `DDFLanguageEditor.Compiler` potrà consumarle nello standalone o nella CLI.
- `DdfEditorView` e `DdfEmbeddedEditorControl` non espongono compilatore,
  assembly o operazioni di emissione persistente.
- `PrepareScript` embedded può usare il piano VM in memoria, ma non converte
  l'unità in un artefatto compilato.

## Invarianti verificati

La suite automatica controlla corrispondenza uno-a-uno fra sorgenti, documenti
IR strutturali e documenti bound, ordinamento deterministico, immutabilità delle
collezioni, firme degli export, isolamento dei simboli privati, stabilità del
fingerprint, variazione in presenza di buffer modificati e assenza di
sorgenti/IR dalla ricevuta persistita.
