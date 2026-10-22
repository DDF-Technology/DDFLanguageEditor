param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$workRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'sdk-compatibility'))
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $workRoot.StartsWith($artifactPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso contract test non sicuro: $workRoot"
}

$matrixPath = Join-Path $repositoryRoot 'ddf-version-matrix.json'
$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
$currentContract = [string]$matrix.editorSdk.contractVersion
$legacyContract = [string]$matrix.editorSdk.legacyContractVersion
$legacyCommit = [string]$matrix.editorSdk.legacyBaselineCommit
$sdkComponents = @($matrix.components | Where-Object {
    $_.id -in @('core-sdk', 'winforms-sdk')
})
if ($sdkComponents.Count -ne 2) {
    throw 'Componenti SDK mancanti dalla matrice.'
}

if (Test-Path -LiteralPath $workRoot) {
    Remove-Item -LiteralPath $workRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $workRoot -Force | Out-Null

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$git = (Get-Command git -ErrorAction Stop).Source
$packScript = Join-Path $PSScriptRoot 'pack-sdk.ps1'
& $packScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$legacyArchive = Join-Path $workRoot 'legacy-source.zip'
$legacySource = Join-Path $workRoot 'legacy-source'
& $git -C $repositoryRoot archive --format=zip --output=$legacyArchive $legacyCommit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Expand-Archive -LiteralPath $legacyArchive -DestinationPath $legacySource

$legacyContractPath = Join-Path $legacySource 'DdfEditorContracts.cs'
$legacyContractSource = Get-Content -LiteralPath $legacyContractPath -Raw
if ($legacyContractSource -notmatch ('CurrentVersion\s*=\s*"' +
        [regex]::Escape($legacyContract) + '"')) {
    throw "Il commit baseline non espone il contratto legacy $legacyContract."
}

$legacyReferences = Join-Path $workRoot 'legacy-references'
& $dotnet build (Join-Path $legacySource 'DDF - Program Language Editor.csproj') `
    --configuration $Configuration --output $legacyReferences `
    --disable-build-servers -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$currentReferences = Join-Path $workRoot 'current-references'
New-Item -ItemType Directory -Path $currentReferences -Force | Out-Null
$packageRoot = Join-Path $artifactRoot 'sdk'
foreach ($component in $sdkComponents) {
    $packagePath = Join-Path $packageRoot `
        "$($component.packageId).$($component.packageVersion).nupkg"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = @($archive.Entries | Where-Object {
            $_.FullName -match ('^lib/[^/]+/' +
                [regex]::Escape([string]$component.packageId) + '\.dll$')
        })
        if ($entry.Count -ne 1) {
            throw "Assembly non univoco in $packagePath"
        }
        $destination = Join-Path $currentReferences "$($component.packageId).dll"
        $inputStream = $entry[0].Open()
        $outputStream = [System.IO.File]::Create($destination)
        try { $inputStream.CopyTo($outputStream) } finally {
            $outputStream.Dispose()
            $inputStream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

$hostProject = Join-Path $repositoryRoot `
    'tests\DDFLanguageEditor.SdkCompatibilityHost\DDFLanguageEditor.SdkCompatibilityHost.csproj'
$legacyHost = Join-Path $workRoot 'legacy-host'
& $dotnet build $hostProject --configuration $Configuration --output $legacyHost `
    --disable-build-servers -p:NuGetAudit=false `
    "-p:DdfSdkReferenceRoot=$legacyReferences" -p:DdfUpdatedHost=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $currentReferences 'DDFLanguageEditor.Core.dll') `
    -Destination (Join-Path $legacyHost 'DDFLanguageEditor.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $currentReferences 'DDFLanguageEditor.WinForms.dll') `
    -Destination (Join-Path $legacyHost 'DDFLanguageEditor.WinForms.dll') -Force
& $dotnet (Join-Path $legacyHost 'DDFLanguageEditor.SdkCompatibilityHost.dll') `
    legacy $currentContract
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Compatibilità binaria: host compilato con SDK $legacyContract avviato con DLL $currentContract."

$sourceCompatibleHost = Join-Path $workRoot 'source-compatible-host'
& $dotnet build $hostProject --configuration $Configuration `
    --output $sourceCompatibleHost --disable-build-servers -p:NuGetAudit=false `
    "-p:DdfSdkReferenceRoot=$currentReferences" -p:DdfUpdatedHost=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet (Join-Path $sourceCompatibleHost `
    'DDFLanguageEditor.SdkCompatibilityHost.dll') source $currentContract
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Compatibilità sorgente: host con sole API $legacyContract ricompilato e avviato contro $currentContract."

$updatedHost = Join-Path $workRoot 'updated-host'
& $dotnet build $hostProject --configuration $Configuration --output $updatedHost `
    --disable-build-servers -p:NuGetAudit=false `
    "-p:DdfSdkReferenceRoot=$currentReferences" -p:DdfUpdatedHost=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet (Join-Path $updatedHost 'DDFLanguageEditor.SdkCompatibilityHost.dll') `
    updated $currentContract
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Compatibilità dati persistiti: schema corrente, rifiuto legacy e migrazione esplicita verificati."
Write-Host "Capability aggiornate: catalogo completo, Run/Debug e state persistence verificati."

$smokeProject = Join-Path $repositoryRoot `
    'tests\DDFLanguageEditor.EditorSmokeTests\DDFLanguageEditor.EditorSmokeTests.csproj'
& $dotnet run --project $smokeProject --configuration $Configuration `
    -- --sdk-contract-matrix-only
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Matrice runtime SDK: persistenza, capability, isolamento e recupero dopo rifiuto verificati."

Remove-Item -LiteralPath $workRoot -Recurse -Force
Write-Host "Compatibilità SDK verificata: legacy $legacyContract -> $currentContract; updated $currentContract."
