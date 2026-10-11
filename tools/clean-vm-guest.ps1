[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$ResultsRoot
)

$ErrorActionPreference = "Stop"
$reportPath = $null
$startedUtc = [DateTime]::UtcNow.ToString("o")
$checks = [ordered]@{}
$failure = $null
$exitCode = $null
$standardOutput = ""
$standardError = ""

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Acceptance([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

try {
    Write-Host "[1/6] Lettura e validazione del manifest..."
    New-Item -ItemType Directory -Path $ResultsRoot -Force | Out-Null
    $reportPath = Join-Path $ResultsRoot "clean-vm-acceptance-result.json"
    $package = (Resolve-Path -LiteralPath $PackageRoot).Path
    $manifestPath = Join-Path $package "support\ddf-publish.json"
    Assert-Acceptance (Test-Path -LiteralPath $manifestPath -PathType Leaf) `
        "Manifest support/ddf-publish.json assente."

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-Acceptance ([Environment]::Is64BitOperatingSystem) `
        "Il guest non e un sistema operativo x64."
    Assert-Acceptance ($manifest.runtimeIdentifier -eq "win-x64") `
        "Il package non dichiara il RID win-x64."
    Assert-Acceptance ([bool]$manifest.selfContained) `
        "Il manifest non dichiara un package self-contained."
    Assert-Acceptance ($manifest.supportDirectory -eq "support") `
        "La directory di supporto dichiarata non e support."

    $rootFiles = @(Get-ChildItem -LiteralPath $package -File)
    $rootDirectories = @(Get-ChildItem -LiteralPath $package -Directory)
    Assert-Acceptance ($rootFiles.Count -eq 1) `
        "La radice deve contenere un solo file."
    Assert-Acceptance ($rootDirectories.Count -eq 1 -and `
        $rootDirectories[0].Name -eq "support") `
        "La radice deve contenere soltanto la directory support."
    Assert-Acceptance ($rootFiles[0].Name -eq $manifest.executable) `
        "L'unico file radice non coincide con l'eseguibile dichiarato."

    Write-Host "[2/6] Verifica di inventario e SHA-256..."
    $declared = @{}
    $verifiedFileCount = 0
    $manifestFileCount = @($manifest.files).Count
    foreach ($item in @($manifest.files)) {
        $verifiedFileCount++
        $relative = ([string]$item.path).Replace("/", "\")
        Assert-Acceptance (-not [System.IO.Path]::IsPathRooted($relative)) `
            ("Percorso assoluto nel manifest: " + $relative)
        $path = [System.IO.Path]::GetFullPath((Join-Path $package $relative))
        Assert-Acceptance ($path.StartsWith($package + `
            [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) `
            ("Percorso fuori dal package: " + $relative)
        Assert-Acceptance (-not $declared.ContainsKey($relative)) `
            ("Percorso duplicato nel manifest: " + $relative)
        $declared[$relative] = $true
        Assert-Acceptance (Test-Path -LiteralPath $path -PathType Leaf) `
            ("File dichiarato assente: " + $relative)
        $info = Get-Item -LiteralPath $path
        Assert-Acceptance ($info.Length -eq [long]$item.size) `
            ("Dimensione non valida: " + $relative)
        Assert-Acceptance ((Get-Sha256 $path) -eq [string]$item.sha256) `
            ("SHA-256 non valido: " + $relative)
        if (($verifiedFileCount % 25) -eq 0 -or `
                $verifiedFileCount -eq $manifestFileCount) {
            Write-Host ("      " + $verifiedFileCount + "/" + `
                $manifestFileCount + " file verificati")
        }
    }

    $actualRelative = @(Get-ChildItem -LiteralPath $package -File -Recurse |
        ForEach-Object {
            $_.FullName.Substring($package.Length + 1)
        })
    $allowedExtra = "support\ddf-publish.json"
    $unexpected = @($actualRelative | Where-Object {
        $_ -ne $allowedExtra -and -not $declared.ContainsKey($_)
    })
    Assert-Acceptance ($unexpected.Count -eq 0) `
        ("File non inventariati: " + ($unexpected -join ", "))
    Assert-Acceptance ($actualRelative.Count -eq $declared.Count + 1) `
        "L'inventario del package non coincide con il manifest."
    $checks["manifestAndHashes"] = $true
    $checks["rootAllowlist"] = $true

    Write-Host "[3/6] Verifica del runtime self-contained..."
    foreach ($runtimeFile in @("hostfxr.dll", "hostpolicy.dll", `
            "coreclr.dll", "System.Private.CoreLib.dll")) {
        Assert-Acceptance (Test-Path -LiteralPath `
            (Join-Path $package ("support\" + $runtimeFile)) -PathType Leaf) `
            ("Runtime self-contained incompleto: " + $runtimeFile)
    }
    $forbidden = @(Get-ChildItem -LiteralPath $package -File -Recurse |
        Where-Object {
            $_.Name -match "CodeAnalysis" -or
            $_.Name -eq "DDFLanguageEditor.Compiler.dll"
        })
    Assert-Acceptance ($forbidden.Count -eq 0) `
        "Il package contiene dipendenze riservate alla compilazione."
    $checks["runtimeSelfContained"] = $true
    $checks["compilerDependenciesAbsent"] = $true

    Write-Host "[4/6] Verifica dell'assenza di .NET moderno nel guest..."
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    $modernDotNetRegistry = @(
        "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost",
        "HKLM:\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost"
    ) | Where-Object { Test-Path -LiteralPath $_ }
    $checks["dotnetCommandAbsent"] = ($null -eq $dotnetCommand)
    $checks["modernDotNetRegistryAbsent"] = ($modernDotNetRegistry.Count -eq 0)
    Assert-Acceptance $checks["dotnetCommandAbsent"] `
        "dotnet.exe e presente nel guest: la macchina non e pulita."
    Assert-Acceptance $checks["modernDotNetRegistryAbsent"] `
        "Il registro segnala un runtime .NET moderno installato."

    $executable = Join-Path $package ([string]$manifest.executable)
    $runRoot = Join-Path $env:TEMP ("ddf-clean-vm-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    $oldPath = $env:PATH
    $oldDotNetRoot = $env:DOTNET_ROOT
    $oldDotNetRootX64 = $env:DOTNET_ROOT_X64
    $oldMultilevel = $env:DOTNET_MULTILEVEL_LOOKUP
    $oldHostPath = $env:DOTNET_HOST_PATH
    try {
        Write-Host "[5/6] Avvio controllato dell'EXE DDF..."
        $env:PATH = ""
        $env:DOTNET_ROOT = Join-Path $runRoot "missing-dotnet"
        $env:DOTNET_ROOT_X64 = Join-Path $runRoot "missing-dotnet-x64"
        $env:DOTNET_MULTILEVEL_LOOKUP = "0"
        Remove-Item Env:\DOTNET_HOST_PATH -ErrorAction SilentlyContinue
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $executable
        $startInfo.WorkingDirectory = $runRoot
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        Assert-Acceptance $process.Start() "Impossibile avviare l'EXE."
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            throw "L'EXE non e terminato entro 30 secondi."
        }
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        $standardOutput = $stdoutTask.GetAwaiter().GetResult()
        $standardError = $stderrTask.GetAwaiter().GetResult()
        Write-Host ("[6/6] EXE terminato con exit code " + $exitCode + ".")
    }
    finally {
        $env:PATH = $oldPath
        $env:DOTNET_ROOT = $oldDotNetRoot
        $env:DOTNET_ROOT_X64 = $oldDotNetRootX64
        $env:DOTNET_MULTILEVEL_LOOKUP = $oldMultilevel
        if ($null -eq $oldHostPath) {
            Remove-Item Env:\DOTNET_HOST_PATH -ErrorAction SilentlyContinue
        }
        else {
            $env:DOTNET_HOST_PATH = $oldHostPath
        }
    }
    Assert-Acceptance ($exitCode -eq 0) `
        ("L'EXE ha restituito il codice " + $exitCode + ".")
    Assert-Acceptance ($standardOutput -match "DDF CLEAN VM OK") `
        "Output sentinella assente."
    Assert-Acceptance ($standardOutput -match "Valore restituito:\s*42") `
        "Valore DDF atteso (42) assente."

    $checks["foreignWorkingDirectory"] = $true
    $checks["globalDotNetNeutralized"] = $true
    $checks["programResult"] = $true
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($null -eq $reportPath) {
        New-Item -ItemType Directory -Path $ResultsRoot -Force | Out-Null
        $reportPath = Join-Path $ResultsRoot "clean-vm-acceptance-result.json"
    }
    $report = [ordered]@{
        schemaVersion = "1.0"
        state = if ($null -eq $failure) { "passed" } else { "failed" }
        startedUtc = $startedUtc
        completedUtc = [DateTime]::UtcNow.ToString("o")
        machine = $env:COMPUTERNAME
        os = [Environment]::OSVersion.VersionString
        is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
        packageRoot = $PackageRoot
        checks = $checks
        processExitCode = $exitCode
        standardOutput = $standardOutput
        standardError = $standardError
        failure = $failure
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath `
        -Encoding UTF8
}

if ($null -ne $failure) {
    Write-Error $failure
    exit 1
}
Write-Host "Accettazione DDF su VM pulita superata."
exit 0
