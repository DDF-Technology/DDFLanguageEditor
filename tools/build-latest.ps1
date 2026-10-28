[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$latestRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'latest'))
$latestDirectory = [IO.Path]::GetFullPath((Join-Path $latestRoot 'standalone'))
$stagingDirectory = [IO.Path]::GetFullPath((Join-Path $artifactRoot (
    '.build-latest-' + [Guid]::NewGuid().ToString('N'))))
$project = Join-Path $repositoryRoot 'DDFLanguageEditor.App\DDFLanguageEditor.App.csproj'
$versionMatrixPath = Join-Path $repositoryRoot 'ddf-version-matrix.json'

function Assert-PathUnderArtifactRoot([string]$Path) {
    $prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Percorso artefatto non sicuro: $Path"
    }
}

Assert-PathUnderArtifactRoot $latestDirectory
Assert-PathUnderArtifactRoot $stagingDirectory

$runningFromLatest = @(Get-Process DDFLanguageEditor -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            $_.Path -and $_.Path.StartsWith(
                $latestDirectory + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)
        } catch { $false }
    })
if ($runningFromLatest.Count -gt 0) {
    throw "Chiudere la build corrente prima di sostituirla. PID: $($runningFromLatest.Id -join ', ')"
}

$matrix = Get-Content -LiteralPath $versionMatrixPath -Raw | ConvertFrom-Json
$productVersion = [string]$matrix.product.version
if ([string]::IsNullOrWhiteSpace($productVersion)) {
    throw 'Versione prodotto assente dalla matrice autorevole.'
}

$git = Get-Command git -ErrorAction Stop
$commit = (& $git.Source -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    throw 'Impossibile determinare il commit Git corrente.'
}
$shortCommit = (& $git.Source -C $repositoryRoot rev-parse --short=8 HEAD).Trim()
$workingTreeChanges = @(& $git.Source -C $repositoryRoot status --porcelain)
$gitDirty = $workingTreeChanges.Count -gt 0
$now = Get-Date
$buildId = $now.ToString('yyyyMMdd.HHmmss') + '.' + $shortCommit +
    $(if ($gitDirty) { '-dirty' } else { '' })

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

try {
    $dotnet = Get-Command dotnet -ErrorAction Stop
    & $dotnet.Source restore $project `
        --runtime $RuntimeIdentifier `
        --disable-build-servers `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "Restore non riuscito: exit code $LASTEXITCODE" }

    & $dotnet.Source build $project `
        --configuration $Configuration `
        --no-restore `
        --disable-build-servers `
        --output $stagingDirectory `
        -p:RuntimeIdentifier=$RuntimeIdentifier `
        -p:SkipDdfRunnerCopy=true `
        -t:Rebuild
    if ($LASTEXITCODE -ne 0) { throw "Build non riuscita: exit code $LASTEXITCODE" }

    $requiredFiles = @(
        'DDFLanguageEditor.exe',
        'DDFLanguageEditor.dll',
        'DDFLanguageEditor.WinForms.dll',
        'DDFLanguageEditor.Core.dll',
        'DDFLanguageEditor.Compiler.dll',
        'DDFLanguageEditor.Runtime.dll',
        'DDFLanguageEditor.Runner.dll'
    )
    $missing = @($requiredFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $stagingDirectory $_) -PathType Leaf)
    })
    if ($missing.Count -gt 0) {
        throw "Build incompleta. File mancanti: $($missing -join ', ')"
    }

    $inventory = @($requiredFiles | ForEach-Object {
        $path = Join-Path $stagingDirectory $_
        $item = Get-Item -LiteralPath $path
        [ordered]@{
            path = $_
            length = $item.Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    $manifest = [ordered]@{
        schemaVersion = '1.0'
        product = 'DDFLanguageEditor'
        productVersion = $productVersion
        buildId = $buildId
        createdLocal = $now.ToString('o')
        createdUtc = $now.ToUniversalTime().ToString('o')
        gitCommit = $commit
        gitDirty = $gitDirty
        workingTreeChanges = @($workingTreeChanges)
        configuration = $Configuration
        targetFramework = 'net10.0-windows'
        runtimeIdentifier = $RuntimeIdentifier
        selfContained = $false
        entryPoint = 'DDFLanguageEditor.exe'
        files = $inventory
    }
    $manifestPath = Join-Path $stagingDirectory 'BUILD-INFO.json'
    $manifest | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $manifestPath -Encoding utf8

    $verificationStandardOutput = Join-Path $stagingDirectory '.verify.stdout.txt'
    $verificationStandardError = Join-Path $stagingDirectory '.verify.stderr.txt'
    $verificationProcess = Start-Process `
        -FilePath (Join-Path $stagingDirectory 'DDFLanguageEditor.exe') `
        -ArgumentList '--verify-installation' `
        -WorkingDirectory $stagingDirectory `
        -RedirectStandardOutput $verificationStandardOutput `
        -RedirectStandardError $verificationStandardError `
        -WindowStyle Hidden `
        -PassThru `
        -Wait
    $verificationExitCode = $verificationProcess.ExitCode
    $verificationOutput = @(Get-Content -LiteralPath $verificationStandardOutput `
        -ErrorAction SilentlyContinue)
    $verificationErrors = @(Get-Content -LiteralPath $verificationStandardError `
        -ErrorAction SilentlyContinue)
    if ($verificationExitCode -ne 0 -or
        -not ($verificationOutput -contains 'DDF INSTALLATION OK')) {
        throw "Self-test della build non riuscito: $(@($verificationOutput + $verificationErrors) -join ' ')"
    }
    Remove-Item -LiteralPath $verificationStandardOutput,$verificationStandardError `
        -Force -ErrorAction SilentlyContinue

    New-Item -ItemType Directory -Path $latestRoot -Force | Out-Null
    if (Test-Path -LiteralPath $latestDirectory) {
        Remove-Item -LiteralPath $latestDirectory -Recurse -Force
    }
    Move-Item -LiteralPath $stagingDirectory -Destination $latestDirectory

    Write-Host ''
    Write-Host 'BUILD CORRENTE PRONTA' -ForegroundColor Green
    Write-Host "Build ID: $buildId"
    Write-Host "Versione: $productVersion"
    Write-Host "Git: $commit$(if ($gitDirty) { ' (dirty)' } else { ' (clean)' })"
    Write-Host "EXE: $(Join-Path $latestDirectory 'DDFLanguageEditor.exe')"
    Write-Host "Manifesto: $(Join-Path $latestDirectory 'BUILD-INFO.json')"
} finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}
