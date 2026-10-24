param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$checkpointRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'semantic-checkpoint'))
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $checkpointRoot.StartsWith($artifactPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso ricevute non sicuro: $checkpointRoot"
}

New-Item -ItemType Directory -Path $checkpointRoot -Force | Out-Null
$sessionId = 'semantic-checkpoint-' + (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$sessionRoot = Join-Path $checkpointRoot $sessionId
New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$solution = Join-Path $repositoryRoot 'DDF - Program Language Editor.sln'
$coreTests = Join-Path $repositoryRoot `
    "tests\DDFLanguageEditor.Tests\bin\$Configuration\DDFLanguageEditor.Tests.exe"
$compilerTests = Join-Path $repositoryRoot `
    "tests\DDFLanguageEditor.Compiler.Tests\bin\$Configuration\DDFLanguageEditor.Compiler.Tests.exe"
$editorSmoke = Join-Path $repositoryRoot `
    "tests\DDFLanguageEditor.EditorSmokeTests\bin\$Configuration\DDFLanguageEditor.EditorSmokeTests.exe"

$stages = [System.Collections.Generic.List[object]]::new()
$startedUtc = (Get-Date).ToUniversalTime()
$failure = $null

function Invoke-CheckpointStage {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @()
    )

    $logPath = Join-Path $sessionRoot ($Id + '.log')
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host "`n[$Id] $FilePath $($Arguments -join ' ')"
    $lines = @(& $FilePath @Arguments 2>&1 | ForEach-Object {
        $_ | Out-Host
        $_.ToString()
    })
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) { $exitCode = 0 }
    $timer.Stop()
    $lines | Set-Content -LiteralPath $logPath -Encoding UTF8

    $rootPrefixLength = $repositoryRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar).Length + 1
    $relativeLog = $logPath.Substring($rootPrefixLength).Replace('\', '/')
    $stage = [ordered]@{
        id = $Id
        status = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' })
        exitCode = [int]$exitCode
        durationMilliseconds = [long]$timer.ElapsedMilliseconds
        log = $relativeLog
    }
    $stages.Add([pscustomobject]$stage)
    if ($exitCode -ne 0) {
        throw "Gate '$Id' fallito con exit code $exitCode. Log: $logPath"
    }
}

try {
    Invoke-CheckpointStage -Id 'release-build' -FilePath $dotnet -Arguments @(
        'build', $solution, '--configuration', $Configuration,
        '--disable-build-servers', '--maxcpucount:1', '-p:NuGetAudit=false')
    Invoke-CheckpointStage -Id 'core' -FilePath $coreTests
    Invoke-CheckpointStage -Id 'backend-equivalence' -FilePath $compilerTests `
        -Arguments @('--backend-equivalence-only')
    Invoke-CheckpointStage -Id 'deterministic-build' -FilePath $compilerTests `
        -Arguments @('--deterministic-build-only')

    $smokeArguments = @()
    if ($Visible) { $smokeArguments += '--visible' }
    Invoke-CheckpointStage -Id 'winforms-smoke' -FilePath $editorSmoke `
        -Arguments $smokeArguments

    $performanceArguments = @('--realistic-performance-only')
    if ($Visible) { $performanceArguments += '--visible' }
    Invoke-CheckpointStage -Id 'release-performance' -FilePath $editorSmoke `
        -Arguments $performanceArguments
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $automaticStatus = if ($failure) { 'failed' } else { 'passed' }
    $receipt = [ordered]@{
        '$schema' = 'https://www.ddf.technology/schemas/ddf/semantic-checkpoint-receipt-1.0.json'
        schemaVersion = '1.0'
        sessionId = $sessionId
        configuration = $Configuration
        startedUtc = $startedUtc.ToString('o')
        completedUtc = (Get-Date).ToUniversalTime().ToString('o')
        automated = [ordered]@{
            status = $automaticStatus
            stages = @($stages)
            failure = $failure
        }
        manual = [ordered]@{
            status = 'pending'
            checklist = 'DDF_SEMANTIC_CHECKPOINT_MANUAL_SESSION.md'
            completedBy = $null
            completedAt = $null
            notes = ''
        }
        readiness = 'not-ready'
    }
    $receiptPath = Join-Path $sessionRoot 'receipt.json'
    $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding UTF8
    Copy-Item -LiteralPath $receiptPath -Destination `
        (Join-Path $checkpointRoot 'latest.json') -Force
    Write-Host "`nRicevuta: $receiptPath"
}

if ($failure) {
    Write-Error $failure
    exit 1
}

Write-Host 'Gate automatico superato. La sessione manuale visuale/accessibile resta obbligatoria.'
exit 0
