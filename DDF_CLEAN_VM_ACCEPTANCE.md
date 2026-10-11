# Accettazione EXE DDF su Windows x64 pulito

Questo gate distributivo verifica che un EXE generato da sorgente DDF funzioni
su Windows x64 senza un runtime .NET moderno installato. È deliberatamente
separato dai test ermetici automatici: il risultato è valido soltanto quando il
programma è stato eseguito in Windows Sandbox o in una VM realmente pulita.

## Preparazione del kit

Da PowerShell, nella radice del repository:

```powershell
.\tools\prepare-clean-vm-acceptance.ps1
```

Lo script compila la CLI Release, pubblica
`samples/clean-vm-acceptance/main.ddf` come `DdfCleanVmProbe.exe` e crea sotto
`.artifacts/clean-vm-acceptance/<timestamp>/`:

- il package esatto verificabile in `input/package`;
- l'archivio ZIP del package e il relativo file `.sha256`;
- `acceptance-kit.json`, che registra fingerprint, hash e risultato atteso;
- `DDF-clean-vm-acceptance.wsb` e lo script eseguito nel guest;
- la directory `results`, destinata al verbale restituito dal guest.

Ogni esecuzione usa una directory nuova: un vecchio package non viene
sovrascritto né confuso con quello sottoposto al test.

## Esecuzione

Se Windows Sandbox è installato si può preparare e avviare il gate con:

```powershell
.\tools\prepare-clean-vm-acceptance.ps1 -LaunchSandbox
```

La configurazione disabilita esplicitamente la vGPU: il programma da provare è
una CLI e il rendering software elimina una dipendenza grafica irrilevante dal
gate. Se la Sandbox si arresta prima di produrre il verbale, l'accettazione non
è superata e va ripetuta su un host Windows stabile o su una VM esterna. Il PC
di sviluppo con Windows 11 Pro Experimental `26H2 26300.9457` presenta proprio
questo problema host: Hyper-V crea il guest e monta le directory, quindi termina
prima del logon con `StartComputeSystem 0xC0370103`, anche usando un `.wsb`
vuoto. Tale esito non costituisce né successo né fallimento del package DDF.

In alternativa si può aprire manualmente il file `.wsb`. Per una VM Windows x64
esterna, copiare la directory `input`, predisporre una directory scrivibile per
i risultati e avviare:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File .\clean-vm-guest.ps1 `
  -PackageRoot .\package `
  -ResultsRoot .\results
```

La VM deve essere priva del comando `dotnet.exe` e delle chiavi di installazione
del runtime .NET moderno. La presenza del .NET Framework incluso in Windows non
è rilevante: l'EXE pubblicato usa esclusivamente il runtime in `support`.

## Procedura manuale passo per passo

Il kit attualmente candidato al collaudo si trova in:

```text
D:\Projects\DeveloperTools\DDFLanguageEditor\.artifacts\clean-vm-acceptance\20261011-170727
```

Non ricostruire il progetto e non copiare soltanto l'EXE: il test deve usare
l'intera directory del kit, senza modificarne il contenuto. La procedura può
essere eseguita su un PC fisico pulito oppure in una VM Windows 10/11 x64.

1. Preparare il sistema di prova.

   - Usare un'installazione Windows x64 nuova o ripristinata da uno snapshot.
   - Non installare .NET SDK, runtime .NET moderno, Visual Studio o il progetto
     DDF. Il .NET Framework già incluso in Windows è ammesso.
   - Non è necessaria una connessione Internet; tenerla disattivata rende il
     collaudo più ripetibile.
   - Se si usa una VM, creare uno snapshot prima della prova.

2. Copiare sul sistema pulito l'intera directory `20261011-170727`, per esempio
   in `C:\DDF-Acceptance\20261011-170727`. Sono ammessi chiavetta USB, immagine
   ISO o cartella condivisa. Al termine della copia, lavorare sulla copia locale
   del guest e non direttamente sulla cartella condivisa.

3. Aprire **Windows PowerShell** come utente normale. Non servono privilegi di
   amministratore. Impostare il percorso in cui è stato copiato il kit:

   ```powershell
   $kit = 'C:\DDF-Acceptance\20261011-170727'
   Test-Path "$kit\input\clean-vm-guest.ps1"
   Test-Path "$kit\input\package\DdfCleanVmProbe.exe"
   ```

   Entrambi i comandi devono restituire `True`.

4. Verificare che non sia presente un runtime .NET moderno:

   ```powershell
   Get-Command dotnet.exe -ErrorAction SilentlyContinue
   ```

   Il comando non deve produrre alcun risultato. Se trova `dotnet.exe`, quel
   sistema non è valido per questo gate: ripristinare lo snapshot o usare
   un'altra installazione pulita.

5. Verificare che lo ZIP non sia stato alterato durante il trasferimento:

   ```powershell
   (Get-FileHash "$kit\DdfCleanVmProbe-win-x64.zip" -Algorithm SHA256).Hash.ToLowerInvariant()
   Get-Content "$kit\DdfCleanVmProbe-win-x64.zip.sha256"
   ```

   L'hash calcolato deve essere:

   ```text
   fd820d6c8438b66d633be5a895d97e747f535db9ead1993f07ea97f0d5e458a1
   ```

6. Avviare il collaudo. `ExecutionPolicy Bypass` vale soltanto per questo
   processo e non modifica permanentemente le impostazioni di Windows:

   ```powershell
   powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass `
     -File "$kit\input\clean-vm-guest.ps1" `
     -PackageRoot "$kit\input\package" `
     -ResultsRoot "$kit\results"
   ```

7. Attendere il completamento senza lanciare manualmente l'EXE. In caso di
   successo la console deve mostrare:

   ```text
   Accettazione DDF su VM pulita superata.
   ```

8. Leggere il verbale prodotto:

   ```powershell
   Get-Content "$kit\results\clean-vm-acceptance-result.json" -Raw
   ```

   Il verbale valido deve contenere `"state": "passed"`, exit code `0` e
   nell'output entrambe le stringhe `DDF CLEAN VM OK` e
   `Valore restituito: 42`. Tutti i controlli elencati nel documento devono
   risultare `true`.

9. Copiare fuori dal sistema pulito, senza rinominarli:

   - `results\clean-vm-acceptance-result.json`;
   - `acceptance-kit.json`;
   - `package-command.json`;
   - `DdfCleanVmProbe-win-x64.zip.sha256`.

   Riportare il JSON nella directory `results` del kit originale nel repository.
   Se il test fallisce, conservare comunque il verbale con stato `failed` e non
   installare dipendenze per tentare di far passare il package: il dettaglio del
   fallimento servirà per correggere la distribuzione e ripetere il test su uno
   snapshot pulito.

## Esito registrato

Il gate è stato superato il 2026-10-12 su Windows 10 Professional x64 build
`10.0.19045`, macchina `DESKTOP-OTF5926`, priva del comando e delle chiavi di
installazione .NET moderne. Il kit `20261011-170727` ha mantenuto l'hash
SHA-256 atteso
`fd820d6c8438b66d633be5a895d97e747f535db9ead1993f07ea97f0d5e458a1`.

Il verbale originale resta nel kit locale; una copia normalizzata e il manifesto
del kit sono versionati in
`acceptance/clean-vm/20261011-170727/`. Il report riporta `state: passed`, nove
controlli superati, exit code `0`, nessun errore e
le sentinelle `DDF CLEAN VM OK` e `Valore restituito: 42`. Durante la prova il
guest harness è stato corretto per usare `System.Diagnostics.Process` con
timeout esplicito: Windows PowerShell 5.1 non restituiva in modo affidabile
l'exit code e il controllo al chiamante usando `Start-Process -Wait` insieme
agli stream reindirizzati. Il package verificato non è stato modificato.

## Criteri e verbale

Il guest controlla allowlist della radice, inventario, dimensioni e SHA-256,
runtime self-contained, assenza delle dipendenze di compilazione e assenza di
una installazione .NET moderna. Avvia quindi l'EXE da una directory estranea,
con `PATH` vuoto, radici .NET inesistenti e lookup multilivello disabilitato.

Il gate passa soltanto con exit code `0`, output `DDF CLEAN VM OK` e valore
restituito `42`. Il verbale persistente è
`results/clean-vm-acceptance-result.json`. Conservare insieme verbale, ZIP,
`.sha256`, `acceptance-kit.json` e `package-command.json`; solo a quel punto la
relativa voce della roadmap può essere marcata come completata.
