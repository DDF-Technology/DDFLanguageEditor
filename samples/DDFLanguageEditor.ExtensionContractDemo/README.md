# Extension contract demo

Eseguire dalla radice del repository:

```powershell
dotnet run --project samples/DDFLanguageEditor.ExtensionContractDemo/DDFLanguageEditor.ExtensionContractDemo.csproj -c Release
```

Questo smoke costruisce **solo dati di prova** per i quattro punti di
estensione: semantica, runtime, funzioni host e backend di build. Verifica
manifesto del protocollo, frame, round-trip, correlazione monouso e
classificazione di un errore simulato. Non crea un plug-in installabile, non
firma assembly, non concede permessi e non esegue codice di terze parti.

Il risultato `BuildBackend` è intenzionalmente un fallimento simulato: non
viene prodotto un vero PE né scritto un file. Per una futura integrazione reale
serviranno catalogo attestato, lock, consenso, grant, audit e worker isolato.
