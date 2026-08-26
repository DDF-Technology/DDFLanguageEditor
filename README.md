# DDFLanguageEditor

> **Beta 0.1 — progetto sperimentale in sviluppo.** La versione disponibile è
> un prototipo di editor sintattico: non è ancora un ambiente di sviluppo né
> un'implementazione completa del linguaggio DDF.

DDFLanguageEditor conserva ed espone il primo esperimento WinForms dedicato al
linguaggio sperimentale DDF. Offre un'area di scrittura scura con numeri di riga,
evidenziazione delle regole note e indentazione assistita.

## Funzioni attualmente disponibili

- evidenziazione sintattica basata su parole chiave e delimitatori;
- numerazione delle righe visibili;
- conversione di Tab in quattro spazi;
- rientro automatico dopo `{` e `(`;
- editor locale senza rete, account o telemetria;
- bozza storica della sintassi in `DDF - Program Language Spec.txt`.

## Limiti della beta

- nessun comando Apri, Salva o Esporta;
- nessun tokenizer, parser, albero sintattico o validatore;
- nessun interprete, compilatore, debugger o runtime DDF;
- nessun completamento automatico o gestione progetto;
- l'evidenziazione ricalcola l'intero documento e non è ottimizzata per file grandi;
- alcune regole dell'highlighter e della specifica storica non sono ancora
  allineate: la specifica è una bozza progettuale, non un contratto stabile.

Il testo inserito nell'editor rimane soltanto nella sessione corrente. Chiudendo
l'applicazione viene perso: usare contenuti di prova e copiarli altrove prima
dell'uscita.

## Requisiti e build

- Windows 10 o 11;
- .NET Framework 4.8;
- Visual Studio 2022 con workload desktop .NET, oppure MSBuild compatibile.

```powershell
msbuild "DDF - Program Language Editor.sln" /t:Rebuild /p:Configuration=Release
```

L'eseguibile `DDFLanguageEditor.exe` viene generato in `bin\Release`. La release pubblica è unsigned:
Windows può mostrare un avviso di reputazione.

## Struttura

- `Main.cs` e `Main.Designer.cs`: finestra, indentazione ed evidenziazione;
- `Resource/DictRules.cs`: token, delimitatori e colori sperimentali;
- `DDF - Program Language Spec.txt`: specifica storica in bozza;
- `Properties/`: metadati e risorse del progetto .NET Framework.

## Licenza e dipendenze

Codice e documentazione originali sono distribuiti con licenza [MIT](LICENSE),
Copyright © 2026 Fabio De Deo.

Non sono presenti pacchetti NuGet o librerie incorporate. Windows Forms e .NET
Framework appartengono a Microsoft e sono richiesti come componenti di sistema;
vedere [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## English summary

**Beta 0.1 — experimental work in progress.** DDFLanguageEditor currently is a
small Windows Forms syntax-editor prototype with line numbers, highlighting and
assisted indentation. It does not yet open or save files and includes no parser,
compiler, interpreter, debugger or DDF runtime. The historical language
specification is an unstable design draft. Source and documentation are
available under the MIT License.
