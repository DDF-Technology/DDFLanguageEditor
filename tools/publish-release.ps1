param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SkipArchive,
    [switch]$SkipSignedManifest,
    [string]$ArtifactSubdirectory = '',
    [string]$SigningKeyId = 'official-2026'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$project = Join-Path $repositoryRoot 'DDFLanguageEditor.App\DDFLanguageEditor.App.csproj'
$runnerProject = Join-Path $repositoryRoot 'DDFLanguageEditor.Runner\DDFLanguageEditor.Runner.csproj'
$updaterProject = Join-Path $repositoryRoot 'DDFLanguageEditor.Updater\DDFLanguageEditor.Updater.csproj'
$versionMatrixPath = Join-Path $repositoryRoot 'ddf-version-matrix.json'
$versionMatrix = Get-Content -LiteralPath $versionMatrixPath -Raw |
    ConvertFrom-Json
$applicationVersion = [string]$versionMatrix.product.version
if ([string]::IsNullOrWhiteSpace($applicationVersion)) {
    throw 'Versione prodotto non trovata nella matrice autorevole.'
}
$baseArtifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
if ($ArtifactSubdirectory -and $ArtifactSubdirectory -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') {
    throw 'ArtifactSubdirectory deve essere un singolo nome sicuro, non un percorso.'
}
$artifactRoot = if ($ArtifactSubdirectory) {
    [System.IO.Path]::GetFullPath((Join-Path $baseArtifactRoot "acceptance\$ArtifactSubdirectory"))
} else {
    $baseArtifactRoot
}
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'publish'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'release'))
$packageName = "DDFLanguageEditor-$applicationVersion-$RuntimeIdentifier-self-contained"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot $packageName))
$runnerPublishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot "$packageName-runner"))
$updaterPublishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot "$packageName-updater"))
$archivePath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot "$packageName.zip"))
$checksumPath = "$archivePath.sha256"
$releaseManifestPath = Join-Path $releaseRoot 'ddf-release-manifest.json'

function Assert-PathUnderArtifactRoot([string]$Path) {
    $prefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Percorso artefatto non sicuro: $Path"
    }
}

Assert-PathUnderArtifactRoot $publishDirectory
Assert-PathUnderArtifactRoot $runnerPublishDirectory
Assert-PathUnderArtifactRoot $updaterPublishDirectory
Assert-PathUnderArtifactRoot $archivePath
Assert-PathUnderArtifactRoot $checksumPath
Assert-PathUnderArtifactRoot $releaseManifestPath

foreach ($path in @($publishDirectory, $runnerPublishDirectory, $updaterPublishDirectory, $archivePath, $checksumPath,
        $releaseManifestPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$dotnet = Get-Command dotnet -ErrorAction Stop
& $dotnet.Source publish $project `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --disable-build-servers `
    --maxcpucount:1 `
    -p:NuGetAudit=false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The script runner is separate from the updater worker. The editor resolves
# this executable at startup and must not depend on a machine-wide dotnet host.
& $dotnet.Source publish $runnerProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --disable-build-servers `
    --maxcpucount:1 `
    -p:NuGetAudit=false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:PublishDir="$runnerPublishDirectory\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$publishedRunner = Join-Path $runnerPublishDirectory 'DDFLanguageEditor.Runner.exe'
if (-not (Test-Path -LiteralPath $publishedRunner -PathType Leaf)) {
    throw 'Runner DDF self-contained assente dalla pubblicazione.'
}
$runnerDestination = Join-Path $publishDirectory 'support\runner'
New-Item -ItemType Directory -Path $runnerDestination -Force | Out-Null
Copy-Item -LiteralPath $publishedRunner `
    -Destination (Join-Path $runnerDestination 'DDFLanguageEditor.Runner.exe')

# The updater must be launched from outside the installation it replaces.
# Ship a self-contained single-file worker so the editor can copy the verified
# executable to its separate state directory after explicit install consent.
& $dotnet.Source publish $updaterProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --disable-build-servers `
    --maxcpucount:1 `
    -p:NuGetAudit=false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:PublishDir="$updaterPublishDirectory\"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$publishedUpdater = Join-Path $updaterPublishDirectory 'DDFLanguageEditor.Updater.exe'
if (-not (Test-Path -LiteralPath $publishedUpdater -PathType Leaf)) {
    throw 'Worker updater self-contained assente dalla pubblicazione.'
}
$updaterDestination = Join-Path $publishDirectory 'support\updater'
New-Item -ItemType Directory -Path $updaterDestination -Force | Out-Null
Copy-Item -LiteralPath $publishedUpdater `
    -Destination (Join-Path $updaterDestination 'DDFLanguageEditor.Updater.exe')

$requiredFiles = @(
    'DDFLanguageEditor.exe',
    'DDFLanguageEditor.dll',
    'DDFLanguageEditor.WinForms.dll',
    'DDFLanguageEditor.Core.dll',
    'DDFLanguageEditor.deps.json',
    'DDFLanguageEditor.runtimeconfig.json',
    'coreclr.dll',
    'hostfxr.dll',
    'hostpolicy.dll',
    'System.Windows.Forms.dll',
    'System.Drawing.Common.dll',
    'support\runner\DDFLanguageEditor.Runner.exe',
    'support\updater\DDFLanguageEditor.Updater.exe'
)

$missingFiles = @($requiredFiles | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $publishDirectory $_) -PathType Leaf)
})
if ($missingFiles.Count -gt 0) {
    throw "Pubblicazione self-contained incompleta. File mancanti: $($missingFiles -join ', ')"
}

# A normal launch constructs the build service before showing the main form.
# The health check exercises that same dependency from the published layout.
$healthStart = [System.Diagnostics.ProcessStartInfo]::new()
$healthStart.FileName = Join-Path $publishDirectory 'DDFLanguageEditor.exe'
$healthStart.Arguments = '--verify-installation'
$healthStart.WorkingDirectory = $publishDirectory
$healthStart.UseShellExecute = $false
$healthStart.CreateNoWindow = $true
$healthStart.RedirectStandardOutput = $true
$healthStart.RedirectStandardError = $true
$healthProcess = [System.Diagnostics.Process]::Start($healthStart)
try {
    if (-not $healthProcess.WaitForExit(30000)) {
        $healthProcess.Kill($true)
        throw 'Self-test del prodotto pubblicato scaduto dopo 30 secondi.'
    }
    $healthOutput = $healthProcess.StandardOutput.ReadToEnd().Trim()
    $healthError = $healthProcess.StandardError.ReadToEnd().Trim()
    if ($healthProcess.ExitCode -ne 0 -or
        $healthOutput -ne 'DDF INSTALLATION OK') {
        throw "Self-test del prodotto pubblicato fallito (exit $($healthProcess.ExitCode)): $healthError"
    }
}
finally { $healthProcess.Dispose() }

$requiredLanguageFiles = @(
    'manifest.json',
    'lexicon.json',
    'grammar.json',
    'grammar.ebnf',
    'types.json',
    'semantics.json',
    'formatting.json',
    'intellisense.json',
    'runtime.json',
    'build.json',
    'standard-library.json',
    'tests.json'
)
$publishedLanguageDirectory = Join-Path $publishDirectory 'language-packages\ddf'
$missingLanguageFiles = @($requiredLanguageFiles | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $publishedLanguageDirectory $_) -PathType Leaf)
})
$requiredSchemas = @(
    'language-package.schema.json',
    'language-lexicon.schema.json',
    'language-grammar.schema.json',
    'language-types.schema.json',
    'language-semantics.schema.json',
    'language-runtime.schema.json',
    'language-standard-library.schema.json',
    'language-build.schema.json',
    'language-tests.schema.json',
    'ddf-project.schema.json',
    'ddf-references-lock.schema.json',
    'ddf-product-update-result.schema.json',
    'ddf-release-manifest.schema.json'
)
$missingSchemas = @($requiredSchemas | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $publishDirectory $_) -PathType Leaf)
})
if ($missingLanguageFiles.Count -gt 0 -or $missingSchemas.Count -gt 0) {
    throw "Pacchetto linguaggio DDF pubblicato incompleto. File mancanti: $(@($missingLanguageFiles + $missingSchemas) -join ', ')"
}

$applicationDll = Join-Path $publishDirectory 'DDFLanguageEditor.dll'
$publishedVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).FileVersion
$parsedApplicationVersion = [version]$applicationVersion
$expectedFileVersion = '{0}.{1}.{2}.{3}' -f `
    $parsedApplicationVersion.Major, `
    $parsedApplicationVersion.Minor, `
    ([Math]::Max(0, $parsedApplicationVersion.Build)), `
    ([Math]::Max(0, $parsedApplicationVersion.Revision))
if ($publishedVersion -ne $expectedFileVersion) {
    throw "Versione pubblicata inattesa: $publishedVersion (attesa $expectedFileVersion)"
}

$runtimeConfig = Get-Content -LiteralPath (Join-Path $publishDirectory 'DDFLanguageEditor.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtimeConfig.runtimeOptions.framework -or $runtimeConfig.runtimeOptions.frameworks) {
    throw 'Il runtimeconfig dichiara un framework esterno: il pacchetto non risulta self-contained.'
}

if (-not $SkipArchive) {
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
    $checksum = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$checksum  $([System.IO.Path]::GetFileName($archivePath))" -Encoding ascii
}

if (-not $SkipArchive -and -not $SkipSignedManifest) {
    $privateKeyBase64 = [Environment]::GetEnvironmentVariable(
        'DDF_RELEASE_SIGNING_KEY_PKCS8_BASE64')
    if ([string]::IsNullOrWhiteSpace($privateKeyBase64)) {
        throw 'Secret DDF_RELEASE_SIGNING_KEY_PKCS8_BASE64 mancante: il manifesto firmato è obbligatorio per una release.'
    }
    if ([string]::IsNullOrWhiteSpace($SigningKeyId)) {
        throw 'SigningKeyId richiesto.'
    }
    $coreAssembly = Join-Path $publishDirectory 'DDFLanguageEditor.Core.dll'
    Add-Type -Path $coreAssembly
    $privateKeyBytes = [Convert]::FromBase64String($privateKeyBase64)
    $signingKey = [System.Security.Cryptography.ECDsa]::Create()
    try {
        $bytesRead = 0
        $signingKey.ImportPkcs8PrivateKey($privateKeyBytes, [ref]$bytesRead)
        if ($bytesRead -ne $privateKeyBytes.Length -or $signingKey.KeySize -ne 256) {
            throw 'La chiave release deve essere ECDSA P-256 PKCS#8.'
        }
        $persistence = [System.Collections.Generic.Dictionary[string,string]]::new(
            [System.StringComparer]::Ordinal)
        foreach ($property in $versionMatrix.persistence.PSObject.Properties) {
            $persistence.Add([string]$property.Name, [string]$property.Value)
        }
        $compatibility = [DDFLanguageEditor.Core.DdfReleaseCompatibility]::new(
            [string]$versionMatrix.editorSdk.legacyContractVersion,
            [string]$versionMatrix.editorSdk.contractVersion,
            $persistence)
        $archiveName = [System.IO.Path]::GetFileName($archivePath)
        $assetUri = [Uri]::new(
            "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v$applicationVersion/$archiveName")
        $asset = [DDFLanguageEditor.Core.DdfReleaseManifestAsset]::new(
            [DDFLanguageEditor.Core.DdfProductUpdateComponentKind]::Standalone,
            [string]$versionMatrix.productUpdate.productId,
            $applicationVersion,
            $RuntimeIdentifier,
            ($RuntimeIdentifier -split '-')[-1],
            $assetUri,
            (Get-Item -LiteralPath $archivePath).Length,
            $checksum)
        $releaseNotesPath = Join-Path $repositoryRoot 'RELEASE_NOTES.md'
        $releaseNotes = Get-Content -LiteralPath $releaseNotesPath -Raw
        $channel = if ([string]$versionMatrix.productUpdate.defaultChannel -eq
            'stable') {
            [DDFLanguageEditor.Core.DdfProductUpdateChannel]::Stable
        } else {
            [DDFLanguageEditor.Core.DdfProductUpdateChannel]::Beta
        }
        $unsigned = [DDFLanguageEditor.Core.DdfReleaseManifest]::CreateUnsigned(
            [string]$versionMatrix.productUpdate.productId,
            $applicationVersion,
            $channel,
            [DateTimeOffset]::UtcNow,
            $releaseNotes,
            $compatibility,
            [DDFLanguageEditor.Core.DdfReleaseManifestAsset[]]@($asset))
        $signed = [DDFLanguageEditor.Core.DdfReleaseManifestSigner]::Sign(
            $unsigned, $SigningKeyId, $signingKey)
        $manifestJson = [DDFLanguageEditor.Core.DdfReleaseManifestCodec]::Serialize(
            $signed)
        [System.IO.File]::WriteAllText($releaseManifestPath, $manifestJson,
            [System.Text.UTF8Encoding]::new($false))

        $publicKey = $signingKey.ExportSubjectPublicKeyInfo()
        $keys = [System.Collections.Generic.Dictionary[string,byte[]]]::new(
            [System.StringComparer]::Ordinal)
        $keys.Add($SigningKeyId, $publicKey)
        $trust = [DDFLanguageEditor.Core.DdfReleaseTrustStore]::new($keys)
        $verification = [DDFLanguageEditor.Core.DdfReleaseManifestVerifier]::Verify(
            $manifestJson, $trust)
        if (-not $verification.Success) {
            throw "Verifica del manifesto appena firmato fallita: $($verification.Issues -join '; ')"
        }
        if ($SigningKeyId -ne
            [DDFLanguageEditor.Core.DdfOfficialReleaseTrustStore]::ActiveKeyId) {
            throw "SigningKeyId non corrispondente alla chiave ufficiale attiva."
        }
        $officialTrust = [DDFLanguageEditor.Core.DdfOfficialReleaseTrustStore]::Create()
        $officialVerification =
            [DDFLanguageEditor.Core.DdfReleaseManifestVerifier]::Verify(
                $manifestJson,
                $officialTrust)
        if (-not $officialVerification.Success) {
            throw "Il manifesto non è verificabile con il trust store distribuito: $($officialVerification.Issues -join '; ')"
        }
        $publicKeyFingerprint = [Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($publicKey)
        ).ToLowerInvariant()
        if ($publicKeyFingerprint -ne
            [DDFLanguageEditor.Core.DdfOfficialReleaseTrustStore]::ActiveKeySha256) {
            throw 'Fingerprint della chiave privata diverso dalla chiave ufficiale incorporata.'
        }
    }
    finally {
        $signingKey.Dispose()
        [Array]::Clear($privateKeyBytes, 0, $privateKeyBytes.Length)
    }
}

Write-Host "Pubblicazione self-contained verificata: $publishDirectory"
if (-not $SkipArchive) {
    Write-Host "Pacchetto: $archivePath"
    Write-Host "SHA-256: $checksum"
    if ($SkipSignedManifest) {
        Write-Host 'Manifesto: omesso esplicitamente; artefatto candidato non distribuibile come release.'
    }
    else {
        Write-Host "Manifesto firmato: $releaseManifestPath"
        Write-Host "Signing key: $SigningKeyId ($publicKeyFingerprint)"
    }
}
