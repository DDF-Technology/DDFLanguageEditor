# DDFLanguageEditor

Prototipo di editor desktop per il linguaggio sperimentale DDF. L'applicazione raccoglie in una
singola interfaccia WinForms l'area di scrittura e gli strumenti iniziali del linguaggio; la
specifica sintattica storica è conservata in `DDF - Program Language Spec.txt`.

## Stack e requisiti

- C# e Windows Forms;
- .NET Framework 4.8;
- Windows e Visual Studio 2022 con workload desktop .NET.

## Apertura e build

Aprire `DDF - Program Language Editor.sln` con Visual Studio 2022, ripristinare eventuali
dipendenze e compilare la soluzione in configurazione Debug o Release.

## Struttura

- `Main.cs`, `Main.Designer.cs`, `Main.resx`: finestra principale e risorse UI;
- `Program.cs`: punto di ingresso dell'applicazione;
- `DDF - Program Language Spec.txt`: bozza della grammatica e delle istruzioni;
- `Resource/`: risorse grafiche dell'editor;
- `Properties/`: metadati e impostazioni del progetto .NET Framework.

## Stato e limiti

Il repository documenta un prototipo storico. Non include ancora una pipeline completa di
tokenizzazione, parsing, interpretazione o compilazione; la specifica va quindi considerata una
bozza progettuale e non un contratto stabile del linguaggio.

## Proprietà e licenza

Copyright © 2026 Fabio De Deo — [www.ddf.technology](https://www.ddf.technology/). Tutti i
diritti riservati. Consultare [LICENSE](LICENSE).
