[CmdletBinding()]
param(
    [string]$OutputRoot,
    [switch]$LaunchSandbox
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sampleRoot = Join-Path $repoRoot "samples\clean-vm-acceptance"
$cliProject = Join-Path $repoRoot "DDFLanguageEditor.Cli\DDFLanguageEditor.Cli.csproj"
$cliDll = Join-Path $repoRoot "DDFLanguageEditor.Cli\bin\Release\ddf.dll"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $stamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $OutputRoot = Join-Path $repoRoot (".artifacts\clean-vm-acceptance\" + $stamp)
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "La directory di output deve essere nuova: $OutputRoot"
}

$inputRoot = Join-Path $OutputRoot "input"
$packageRoot = Join-Path $inputRoot "package"
$resultsRoot = Join-Path $OutputRoot "results"
New-Item -ItemType Directory -Path $inputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

Write-Host "Compilazione della CLI Release..."
& dotnet build $cliProject --configuration Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Compilazione della CLI non riuscita."
}

Write-Host "Pubblicazione del programma DDF di accettazione..."
$cliOutput = & dotnet $cliDll package $sampleRoot --runtime win-x64 `
    --name DdfCleanVmProbe --output $packageRoot --diagnostics json 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Pubblicazione DDF non riuscita:`n$($cliOutput | Out-String)"
}
$cliJsonPath = Join-Path $OutputRoot "package-command.json"
$cliOutput | Set-Content -LiteralPath $cliJsonPath -Encoding UTF8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "clean-vm-guest.ps1") `
    -Destination (Join-Path $inputRoot "clean-vm-guest.ps1")

$archivePath = Join-Path $OutputRoot "DdfCleanVmProbe-win-x64.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath `
    -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
($archiveHash + "  " + [System.IO.Path]::GetFileName($archivePath)) |
    Set-Content -LiteralPath ($archivePath + ".sha256") -Encoding Ascii

$publishManifestPath = Join-Path $packageRoot "support\ddf-publish.json"
$publishManifest = Get-Content -LiteralPath $publishManifestPath -Raw |
    ConvertFrom-Json
$kitManifest = [ordered]@{
    schemaVersion = "1.0"
    state = "prepared-not-executed"
    createdUtc = [DateTime]::UtcNow.ToString("o")
    sourceProject = "samples/clean-vm-acceptance"
    runtimeIdentifier = $publishManifest.runtimeIdentifier
    executable = $publishManifest.executable
    publicationFingerprint = $publishManifest.publicationFingerprint
    archive = [ordered]@{
        file = [System.IO.Path]::GetFileName($archivePath)
        size = (Get-Item -LiteralPath $archivePath).Length
        sha256 = $archiveHash
    }
    expected = [ordered]@{
        exitCode = 0
        outputContains = @("DDF CLEAN VM OK", "Valore restituito: 42")
    }
}
$kitManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath `
    (Join-Path $OutputRoot "acceptance-kit.json") -Encoding UTF8

function Escape-Xml([string]$Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}
$inputEscaped = Escape-Xml $inputRoot
$resultsEscaped = Escape-Xml $resultsRoot
$wsb = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$inputEscaped</HostFolder>
      <SandboxFolder>C:\DdfAcceptance</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$resultsEscaped</HostFolder>
      <SandboxFolder>C:\DdfAcceptanceResults</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <PrinterRedirection>Disable</PrinterRedirection>
  <LogonCommand>
    <Command>powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File C:\DdfAcceptance\clean-vm-guest.ps1 -PackageRoot C:\DdfAcceptance\package -ResultsRoot C:\DdfAcceptanceResults</Command>
  </LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $OutputRoot "DDF-clean-vm-acceptance.wsb"
$wsb | Set-Content -LiteralPath $wsbPath -Encoding UTF8

Write-Host "Kit pronto: $OutputRoot"
Write-Host "Archivio SHA-256: $archiveHash"
if ($LaunchSandbox) {
    $sandbox = Get-Command WindowsSandbox.exe -ErrorAction SilentlyContinue
    if ($null -eq $sandbox) {
        throw "Windows Sandbox non e disponibile: il kit e pronto ma non e stato eseguito."
    }
    Start-Process -FilePath $wsbPath
    Write-Host "Windows Sandbox avviato. Attendere la ricevuta nella cartella results."
}
