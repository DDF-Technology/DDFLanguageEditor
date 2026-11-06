param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [Parameter(Mandatory = $true)]
    [string]$AcceptanceRoot,
    [string]$PreviousBuildRoot,
    [string]$CurrentVersion = '0.12.5',
    [switch]$RollbackProbe,
    [switch]$CrashProbe
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$root = [IO.Path]::GetFullPath($AcceptanceRoot)
$prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $root.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La directory di accettazione deve essere dentro .artifacts.'
}
if (Test-Path -LiteralPath $root) {
    throw 'La directory di accettazione esiste già; scegliere una directory nuova.'
}
$previous = $null
if (($RollbackProbe -or $CrashProbe) -and -not $PreviousBuildRoot) {
    throw 'Le prove di recovery richiedono -PreviousBuildRoot.'
}
if ($RollbackProbe -and $CrashProbe) {
    throw 'RollbackProbe e CrashProbe sono mutuamente esclusivi.'
}
if ($PreviousBuildRoot) {
    $previous = [IO.Path]::GetFullPath($PreviousBuildRoot)
    $publishPrefix = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'publish')).
        TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    if (-not $previous.StartsWith($publishPrefix,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $previous 'DDFLanguageEditor.exe'))) {
        throw 'La build precedente deve essere una pubblicazione in .artifacts/publish.'
    }
    $previousVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
        (Join-Path $previous 'DDFLanguageEditor.dll')).FileVersion
    if ($previousVersion -ne ($CurrentVersion + '.0')) {
        throw "Versione precedente inattesa: $previousVersion"
    }
}
$archive = [IO.Path]::GetFullPath($ArchivePath)
$manifestFile = [IO.Path]::GetFullPath($ManifestPath)
$core = Join-Path $repositoryRoot 'DDFLanguageEditor.Core\bin\Debug\DDFLanguageEditor.Core.dll'
if (-not (Test-Path -LiteralPath $core)) {
    throw 'Compilare il progetto Core Debug prima del test.'
}
Add-Type -Path $core
$json = [IO.File]::ReadAllText($manifestFile, [Text.Encoding]::UTF8)
$trust = [DDFLanguageEditor.Core.DdfOfficialReleaseTrustStore]::Create()
$verification = [DDFLanguageEditor.Core.DdfReleaseManifestVerifier]::Verify($json, $trust)
if (-not $verification.Success) {
    throw "Manifesto non verificato: $($verification.Issues -join '; ')"
}
$release = $verification.Manifest
$asset = @($release.Assets | Where-Object {
    $_.ComponentKind -eq [DDFLanguageEditor.Core.DdfProductUpdateComponentKind]::Standalone -and
    $_.RuntimeIdentifier -eq 'win-x64'
})
if ($asset.Count -ne 1) { throw 'Asset Windows x64 ambiguo o assente.' }
$asset = $asset[0]
$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $asset.Sha256 -or (Get-Item -LiteralPath $archive).Length -ne $asset.Size) {
    throw 'Archivio locale non corrispondente al manifesto firmato.'
}

$request = [DDFLanguageEditor.Core.DdfProductUpdateRequest]::new(
    [DDFLanguageEditor.Core.DdfProductUpdateComponentKind]::Standalone,
    $asset.ComponentId, $CurrentVersion,
    [DDFLanguageEditor.Core.DdfProductUpdateChannel]::Beta)
$inventory = [DDFLanguageEditor.Core.DdfProductUpdateArchiveInspector]::Inspect(
    $archive, $json, $trust, $request, $asset.RuntimeIdentifier,
    $asset.Architecture, $null, [Threading.CancellationToken]::None)

$parent = $null
if ($previous -and -not $RollbackProbe -and -not $CrashProbe) {
    $parent = Start-Process -FilePath (Get-Command pwsh).Source -PassThru `
        -WindowStyle Hidden -ArgumentList @('-NoProfile', '-Command',
            'Start-Sleep -Seconds 600')
}
try {
$now = [DateTimeOffset]::UtcNow.AddMinutes(-1)
$manifestHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
$parentId = if ($parent) { $parent.Id } else { 1234 }
$parentStarted = if ($parent) {
    [DateTimeOffset]::new($parent.StartTime.ToUniversalTime(), [TimeSpan]::Zero)
} else { $now.AddMinutes(-1) }
$plan = [DDFLanguageEditor.Core.DdfProductUpdateInstallationPlan]::new(
    'real-release-isolated', $asset.ComponentId, $CurrentVersion, $release.Version,
    $asset.RuntimeIdentifier, $asset.Architecture, $manifestHash,
    $release.Signature.KeyId, $asset.Uri, $asset.Size, $asset.Sha256,
    (Join-Path $root 'target'), (Join-Path $root 'staging'),
    (Join-Path $root 'backup'), 'DDFLanguageEditor.exe', $parentId,
    $parentStarted, ('a' * 64), $false, $inventory, $now)
$planPath = Join-Path $root 'state\plan.json'
$journalPath = Join-Path $root 'state\journal.json'
[DDFLanguageEditor.Core.DdfProductUpdateInstallationJournalStore]::WritePlan(
    $planPath, $plan)
if ($previous) {
    Copy-Item -LiteralPath $previous -Destination $plan.TargetRoot -Recurse
}
$store = [DDFLanguageEditor.Core.DdfProductUpdateInstallationJournalStore]::new(
    $journalPath)
$journal = $store.Create(
    [DDFLanguageEditor.Core.DdfProductUpdateInstallationJson]::ComputePlanSha256($plan),
    $now)
$null = $store.Transition($journal.Revision,
    [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::DownloadAuthorized,
    $now.AddSeconds(1))
$stager = [DDFLanguageEditor.Core.DdfProductUpdateStager]::new()
try {
    $seal = $stager.StageAsync($plan, $json, $trust, $store,
        $now.AddSeconds(2)).GetAwaiter().GetResult()
    if ($store.Read().State -ne
        [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::Staged -or
        $seal.FileCount -ne $inventory.Count -or
        ((-not $previous) -and (Test-Path -LiteralPath $plan.TargetRoot))) {
        throw 'Staging non sigillato o target modificato prima del consenso.'
    }
    "STAGING OK: $($seal.FileCount) file; SHA-256 $actualHash; target intatto."
}
finally { $stager.Dispose() }
if ($previous) {
    $current = $store.Read()
    $null = $store.Transition($current.Revision,
        [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::InstallAuthorized,
        [DateTimeOffset]::UtcNow)
    if ($CrashProbe) {
        $targetExe = Join-Path $plan.TargetRoot 'DDFLanguageEditor.exe'
        $oldHash = (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash
        $current = $store.Read()
        $current = $store.Transition($current.Revision,
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::WaitingForEditorExit,
            [DateTimeOffset]::UtcNow)
        $null = $store.Transition($current.Revision,
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::Applying,
            [DateTimeOffset]::UtcNow)
        [IO.Directory]::Move($plan.TargetRoot, $plan.BackupRoot)
        $engine = [DDFLanguageEditor.Core.DdfProductUpdateWorker]::new()
        $recovered = $engine.Recover($planPath, $journalPath, $plan.Nonce)
        if ($recovered.Kind.ToString() -ne 'RolledBack' -or
            $store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::RolledBack -or
            -not (Test-Path -LiteralPath $plan.StagingRoot) -or
            (Test-Path -LiteralPath $plan.BackupRoot) -or
            (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash -ne $oldHash) {
            throw 'Recovery reale non ha ripristinato il target dopo lo swap interrotto.'
        }
        $health = [DDFLanguageEditor.Core.DdfProductUpdateProcessHealthCheck]::new(
            [TimeSpan]::FromSeconds(30)).CheckAsync($plan,
                [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if (-not $health.Success) { throw 'La build ripristinata non supera il self-test.' }
        "RECOVERY ISOLATO OK: $CurrentVersion ripristinata dopo swap interrotto."
    }
    elseif ($RollbackProbe) {
        Add-Type -ReferencedAssemblies $core -TypeDefinition @'
using System;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;
public sealed class DdfUpdateAcceptanceImmediateWaiter : IDdfProductUpdateProcessWaiter
{
    public Task WaitForExitAsync(int processId, DateTimeOffset processStartedUtc,
        TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
}
'@
        $targetExe = Join-Path $plan.TargetRoot 'DDFLanguageEditor.exe'
        $targetCore = Join-Path $plan.TargetRoot 'DDFLanguageEditor.Core.dll'
        $oldExe = (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash
        $oldCore = (Get-FileHash -LiteralPath $targetCore -Algorithm SHA256).Hash
        $engine = [DDFLanguageEditor.Core.DdfProductUpdateWorker]::new(
            [DdfUpdateAcceptanceImmediateWaiter]::new())
        $applied = $engine.ApplyAsync($planPath, $journalPath,
            $plan.Nonce).GetAwaiter().GetResult()
        if ($applied.Kind.ToString() -ne 'AwaitingHealthCheck' -or
            $store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::Verifying) {
            throw 'Swap isolato non arrivato a Verifying.'
        }
        [IO.File]::WriteAllBytes($targetExe, [byte[]](1, 2, 3, 4))
        $recovered = $engine.Recover($planPath, $journalPath, $plan.Nonce)
        if ($recovered.Kind.ToString() -ne 'RolledBack' -or
            $store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::RolledBack -or
            (Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash -ne $oldExe -or
            (Get-FileHash -LiteralPath $targetCore -Algorithm SHA256).Hash -ne $oldCore -or
            -not (Test-Path -LiteralPath $recovered.QuarantineRoot)) {
            throw 'Rollback reale non ha ripristinato la build precedente.'
        }
        $health = [DDFLanguageEditor.Core.DdfProductUpdateProcessHealthCheck]::new(
            [TimeSpan]::FromSeconds(30)).CheckAsync($plan,
                [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if (-not $health.Success) { throw 'La build ripristinata non supera il self-test.' }
        "ROLLBACK ISOLATO OK: $CurrentVersion ripristinata; nuova copia in quarantena."
    }
    else {
    $workerExe = Join-Path $repositoryRoot `
        'DDFLanguageEditor.Updater\bin\Debug\DDFLanguageEditor.Updater.exe'
    if (-not (Test-Path -LiteralPath $workerExe)) {
        throw 'Compilare il progetto Updater Debug prima del test.'
    }
    $stdout = Join-Path $root 'worker.stdout.txt'
    $stderr = Join-Path $root 'worker.stderr.txt'
    $worker = Start-Process -FilePath $workerExe -PassThru -WindowStyle Hidden `
        -ArgumentList @('--plan', ('"' + $planPath + '"'),
            '--journal', ('"' + $journalPath + '"'),
            '--nonce', $plan.Nonce) -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
        while ([DateTimeOffset]::UtcNow -lt $deadline -and -not $worker.HasExited -and
            $store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::WaitingForEditorExit) {
            Start-Sleep -Milliseconds 200
        }
        if ($store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::WaitingForEditorExit) {
            throw 'Il worker non ha atteso la chiusura del processo precedente.'
        }
        if (-not (Test-Path -LiteralPath $plan.TargetRoot) -or
            (Test-Path -LiteralPath $plan.BackupRoot)) {
            throw 'Il worker ha modificato il target prima della chiusura.'
        }
        Stop-Process -Id $parent.Id
        $parent.WaitForExit()
        $worker.WaitForExit(60000) | Out-Null
        if (-not $worker.HasExited -or $worker.ExitCode -ne 0 -or
            $store.Read().State -ne
            [DDFLanguageEditor.Core.DdfProductUpdateInstallationState]::Completed) {
            throw "Worker non completato: $((Get-Content $stderr -Raw))"
        }
        $newVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
            (Join-Path $plan.TargetRoot 'DDFLanguageEditor.dll')).FileVersion
        $expectedVersion = $release.Version + '.0'
        if ($newVersion -ne $expectedVersion -or
            -not (Test-Path -LiteralPath $plan.BackupRoot)) {
            throw 'Versione promossa o backup precedente non valido.'
        }
        "INSTALLAZIONE ISOLATA OK: $newVersion; backup preservato; health check completato."
    }
    finally { $worker.Dispose() }
    }
}
}
finally {
    if ($parent) {
        if (-not $parent.HasExited) { Stop-Process -Id $parent.Id }
        $parent.Dispose()
    }
}
