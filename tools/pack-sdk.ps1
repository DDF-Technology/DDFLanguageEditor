param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'sdk'))
$matrixPath = Join-Path $repositoryRoot 'ddf-version-matrix.json'
$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
$sdkComponents = @($matrix.components | Where-Object {
    $_.id -in @('core-sdk', 'winforms-sdk')
})
$forbiddenAssemblyPrefixes = @($matrix.embeddedBoundary.forbiddenAssemblyPrefixes |
    ForEach-Object { [string]$_ })
$forbiddenPackagePrefixes = @($matrix.embeddedBoundary.forbiddenPackagePrefixes |
    ForEach-Object { [string]$_ })
if ($sdkComponents.Count -ne 2) {
    throw 'La matrice deve dichiarare esattamente i componenti core-sdk e winforms-sdk.'
}
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $outputDirectory.StartsWith($artifactPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso artefatti SDK non sicuro: $outputDirectory"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$projects = @($sdkComponents | ForEach-Object {
    Join-Path $repositoryRoot ([string]$_.project).Replace('/', '\')
})

$dotnet = Get-Command dotnet -ErrorAction Stop
foreach ($project in $projects) {
    & $dotnet.Source pack $project `
        --configuration $Configuration `
        --output $outputDirectory `
        --disable-build-servers `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$expectedPackages = @($sdkComponents | ForEach-Object {
    "$($_.packageId).$($_.packageVersion).nupkg"
})
$missingPackages = @($expectedPackages | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $outputDirectory $_) -PathType Leaf)
})
if ($missingPackages.Count -gt 0) {
    throw "Pacchetti SDK mancanti: $($missingPackages -join ', ')"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata
$verificationDirectory = [System.IO.Path]::GetFullPath((Join-Path $outputDirectory '.verify'))
$verificationPrefix = $outputDirectory.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $verificationDirectory.StartsWith($verificationPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso temporaneo di verifica non sicuro: $verificationDirectory"
}
if (Test-Path -LiteralPath $verificationDirectory) {
    Remove-Item -LiteralPath $verificationDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $verificationDirectory -Force | Out-Null

try {
    foreach ($component in $sdkComponents) {
        $packagePath = Join-Path $outputDirectory `
            "$($component.packageId).$($component.packageVersion).nupkg"
        $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
        try {
            $nuspecEntry = @($archive.Entries | Where-Object {
                $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase)
            })
            if ($nuspecEntry.Count -ne 1) {
                throw "Nuspec non univoco in $packagePath"
            }
            $reader = New-Object System.IO.StreamReader($nuspecEntry[0].Open())
            try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $namespace = New-Object System.Xml.XmlNamespaceManager($nuspec.NameTable)
            $namespace.AddNamespace('n', $nuspec.DocumentElement.NamespaceURI)
            $id = $nuspec.SelectSingleNode('//n:metadata/n:id', $namespace).InnerText
            $version = $nuspec.SelectSingleNode('//n:metadata/n:version', $namespace).InnerText
            if ($id -ne $component.packageId -or $version -ne $component.packageVersion) {
                throw "Metadati NuGet divergenti per $($component.id): $id@$version"
            }
            $dependencyIds = @($nuspec.SelectNodes('//n:metadata/n:dependencies//n:dependency',
                    $namespace) | ForEach-Object { [string]$_.id })
            $forbiddenDependencies = @($dependencyIds | Where-Object {
                $dependency = $_
                $forbiddenPackagePrefixes | Where-Object {
                    $dependency.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
                }
            })
            if ($forbiddenDependencies.Count -gt 0) {
                throw "Dipendenze Roslyn vietate in $($component.packageId): " +
                    ($forbiddenDependencies -join ', ')
            }

            $forbiddenPayload = @($archive.Entries | Where-Object {
                $fileName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
                $forbiddenAssemblyPrefixes | Where-Object {
                    $fileName.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase)
                }
            })
            if ($forbiddenPayload.Count -gt 0) {
                throw "Payload compilatore vietato in $($component.packageId): " +
                    (($forbiddenPayload | ForEach-Object FullName) -join ', ')
            }

            foreach ($required in @('ddf-version-matrix.json',
                    'ddf-version-matrix.schema.json',
                    'ddf-product-update-result.schema.json',
                    'ddf-release-manifest.schema.json',
                    'ddf-language-integration.json',
                    'ddf-language-integration.schema.json',
                    'DDF_LANGUAGE_INTEGRATION.md',
                    'ddf-backend-equivalence.json',
                    'ddf-backend-equivalence.schema.json',
                    'DDF_BACKEND_EQUIVALENCE.md')) {
                $entry = $archive.Entries | Where-Object {
                    $_.FullName -eq "contentFiles/any/any/$required"
                }
                if ($null -eq $entry) {
                    throw "Artefatto autorevole mancante in $($component.packageId): $required"
                }
                if ($required -in @('ddf-version-matrix.json',
                        'ddf-version-matrix.schema.json')) {
                    $sourcePath = Join-Path $repositoryRoot $required
                    $sourceHash = (Get-FileHash -LiteralPath $sourcePath `
                        -Algorithm SHA256).Hash
                    $entryStream = $entry.Open()
                    try {
                        $sha256 = [System.Security.Cryptography.SHA256]::Create()
                        try {
                            $entryHashBytes = $sha256.ComputeHash($entryStream)
                        }
                        finally {
                            $sha256.Dispose()
                        }
                    }
                    finally {
                        $entryStream.Dispose()
                    }
                    $entryHash = ([System.BitConverter]::ToString(
                        $entryHashBytes)).Replace('-', '')
                    if ($entryHash -ne $sourceHash) {
                        throw "Artefatto autorevole divergente in $($component.packageId): $required"
                    }
                }
            }

            $assemblyEntry = @($archive.Entries | Where-Object {
                $_.FullName -match ('^lib/[^/]+/' +
                    [regex]::Escape([string]$component.packageId) + '\.dll$')
            })
            if ($assemblyEntry.Count -ne 1) {
                throw "Assembly SDK non univoco in $packagePath"
            }
            $assemblyPath = Join-Path $verificationDirectory `
                "$($component.id)-$($component.packageId).dll"
            $inputStream = $assemblyEntry[0].Open()
            $outputStream = [System.IO.File]::Create($assemblyPath)
            try { $inputStream.CopyTo($outputStream) } finally {
                $outputStream.Dispose()
                $inputStream.Dispose()
            }
            $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName(
                $assemblyPath).Version.ToString()
            $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath)
            if ($assemblyVersion -ne $component.assemblyVersion -or
                $fileInfo.FileVersion -ne $component.fileVersion -or
                $fileInfo.ProductVersion -ne $component.informationalVersion) {
                throw "Versioni assembly divergenti per $($component.id): " +
                    "$assemblyVersion / $($fileInfo.FileVersion) / $($fileInfo.ProductVersion)"
            }
            $assemblyStream = [System.IO.File]::OpenRead($assemblyPath)
            $peReader = [System.Reflection.PortableExecutable.PEReader]::new($assemblyStream)
            try {
                $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader(
                    $peReader)
                $assemblyReferences = @($metadata.AssemblyReferences | ForEach-Object {
                    $reference = $metadata.GetAssemblyReference($_)
                    $metadata.GetString($reference.Name)
                })
                $forbiddenReferences = @($assemblyReferences | Where-Object {
                    $referenceName = $_
                    $forbiddenAssemblyPrefixes | Where-Object {
                        $referenceName.StartsWith($_,
                            [System.StringComparison]::OrdinalIgnoreCase)
                    }
                })
                if ($forbiddenReferences.Count -gt 0) {
                    throw "Riferimenti compilatore vietati in $($component.packageId): " +
                        ($forbiddenReferences -join ', ')
                }
            }
            finally {
                $peReader.Dispose()
                $assemblyStream.Dispose()
            }
        }
        finally {
            $archive.Dispose()
        }
    }
}
finally {
    if (Test-Path -LiteralPath $verificationDirectory) {
        Remove-Item -LiteralPath $verificationDirectory -Recurse -Force
    }
}

Write-Host "Pacchetti SDK e metadati verificati contro ddf-version-matrix@1.0: $outputDirectory"
