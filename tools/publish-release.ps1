param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SkipArchive
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$project = Join-Path $repositoryRoot 'DDF - Program Language Editor.csproj'
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'publish'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'release'))
$packageName = "DDFLanguageEditor-0.8.1-$RuntimeIdentifier-self-contained"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishRoot $packageName))
$archivePath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot "$packageName.zip"))
$checksumPath = "$archivePath.sha256"

function Assert-PathUnderArtifactRoot([string]$Path) {
    $prefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Percorso artefatto non sicuro: $Path"
    }
}

Assert-PathUnderArtifactRoot $publishDirectory
Assert-PathUnderArtifactRoot $archivePath
Assert-PathUnderArtifactRoot $checksumPath

foreach ($path in @($publishDirectory, $archivePath, $checksumPath)) {
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

$requiredFiles = @(
    'DDFLanguageEditor.exe',
    'DDFLanguageEditor.dll',
    'DDFLanguageEditor.Core.dll',
    'DDFLanguageEditor.deps.json',
    'DDFLanguageEditor.runtimeconfig.json',
    'coreclr.dll',
    'hostfxr.dll',
    'hostpolicy.dll',
    'System.Windows.Forms.dll',
    'System.Drawing.Common.dll'
)

$missingFiles = @($requiredFiles | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $publishDirectory $_) -PathType Leaf)
})
if ($missingFiles.Count -gt 0) {
    throw "Pubblicazione self-contained incompleta. File mancanti: $($missingFiles -join ', ')"
}

$applicationDll = Join-Path $publishDirectory 'DDFLanguageEditor.dll'
$publishedVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).FileVersion
if ($publishedVersion -ne '0.8.1.0') {
    throw "Versione pubblicata inattesa: $publishedVersion (attesa 0.8.1.0)"
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

Write-Host "Pubblicazione self-contained verificata: $publishDirectory"
if (-not $SkipArchive) {
    Write-Host "Pacchetto: $archivePath"
    Write-Host "SHA-256: $checksum"
}
