param(
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'DDF - Program Language Editor.sln'
$dotnet = Get-Command dotnet -ErrorAction Stop

& $dotnet.Source build $solution --configuration Debug --disable-build-servers --maxcpucount:1
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $repositoryRoot 'tests\DDFLanguageEditor.Tests\bin\Debug\DDFLanguageEditor.Tests.exe')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$smokeArguments = @()
if ($Visible) { $smokeArguments += '--visible' }
& (Join-Path $repositoryRoot 'tests\DDFLanguageEditor.EditorSmokeTests\bin\Debug\DDFLanguageEditor.EditorSmokeTests.exe') @smokeArguments
exit $LASTEXITCODE
