param(
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'DDF - Program Language Editor.sln'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw 'MSBuild di Visual Studio 2022 non trovato.'
}

& $msbuild $solution /t:Rebuild /p:Configuration=Debug /p:UseSharedCompilation=false /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $repositoryRoot 'tests\DDFLanguageEditor.Tests\bin\Debug\DDFLanguageEditor.Tests.exe')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$smokeArguments = @()
if ($Visible) { $smokeArguments += '--visible' }
& (Join-Path $repositoryRoot 'tests\DDFLanguageEditor.EditorSmokeTests\bin\Debug\DDFLanguageEditor.EditorSmokeTests.exe') @smokeArguments
exit $LASTEXITCODE
