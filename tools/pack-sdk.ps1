param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'sdk'))
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $outputDirectory.StartsWith($artifactPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso artefatti SDK non sicuro: $outputDirectory"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$projects = @(
    (Join-Path $repositoryRoot 'DDFLanguageEditor.Core\DDFLanguageEditor.Core.csproj'),
    (Join-Path $repositoryRoot 'DDF - Program Language Editor.csproj')
)

$dotnet = Get-Command dotnet -ErrorAction Stop
foreach ($project in $projects) {
    & $dotnet.Source pack $project `
        --configuration $Configuration `
        --output $outputDirectory `
        --disable-build-servers `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$expectedPackages = @(
    'DDFLanguageEditor.Core.0.11.2-beta.nupkg',
    'DDFLanguageEditor.WinForms.0.11.2-beta.nupkg'
)
$missingPackages = @($expectedPackages | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $outputDirectory $_) -PathType Leaf)
})
if ($missingPackages.Count -gt 0) {
    throw "Pacchetti SDK mancanti: $($missingPackages -join ', ')"
}

Write-Host "Pacchetti SDK verificati: $outputDirectory"
