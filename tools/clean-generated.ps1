[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$IncludeEvidence
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactEvidenceRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$canonicalArtifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$protectedRoots = @(
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.git')),
    $artifactEvidenceRoot,
    $canonicalArtifactRoot
)

function Assert-SafeGeneratedPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Percorso esterno al repository: $full"
    }
    $leaf = Split-Path -Leaf $full
    $isEvidenceChild = $IncludeEvidence -and
        [string]::Equals((Split-Path -Parent $full), $artifactEvidenceRoot,
            [StringComparison]::OrdinalIgnoreCase)
    if ($leaf -notin @('bin', 'obj', 'build', 'build-temp', 'test-temp', 'tmp') -and
        -not $isEvidenceChild) {
        throw "Directory non classificata come generata: $full"
    }
    return $full
}

$generated = @(Get-ChildItem -LiteralPath $repositoryRoot -Directory -Recurse -Force |
    Where-Object {
        $_.Name -in @('bin', 'obj')
    })

# The recursive expression above deliberately protects artifact roots again below;
# normalize and filter before any removal so no computed broad path is accepted.
$generated = @($generated | Where-Object {
    $candidate = $_.FullName
    -not ($protectedRoots | Where-Object {
        $candidate.StartsWith($_ + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
    })
})

foreach ($name in @('build', 'build-temp', 'test-temp', 'tmp', 'obj')) {
    $candidate = Join-Path $artifactEvidenceRoot $name
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $generated += Get-Item -LiteralPath $candidate
    }
}
if ($IncludeEvidence -and (Test-Path -LiteralPath $artifactEvidenceRoot)) {
    $generated += Get-ChildItem -LiteralPath $artifactEvidenceRoot -Directory -Force
}

$generated = @($generated | Sort-Object FullName -Unique |
    Sort-Object { $_.FullName.Length } -Descending)
$bytes = 0L
foreach ($directory in $generated) {
    $safePath = Assert-SafeGeneratedPath $directory.FullName
    $size = (Get-ChildItem -LiteralPath $safePath -Recurse -File -Force `
        -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    if ($size) { $bytes += [long]$size }
    if ($PSCmdlet.ShouldProcess($safePath, 'Rimuovi output rigenerabile')) {
        Remove-Item -LiteralPath $safePath -Recurse -Force
    }
}

Write-Host ("Directory elaborate: {0}; spazio interessato: {1:N2} MB" -f `
    $generated.Count, ($bytes / 1MB))
if (-not $IncludeEvidence) {
    Write-Host 'Artefatti di release, firma, accettazione e checkpoint preservati.'
}
