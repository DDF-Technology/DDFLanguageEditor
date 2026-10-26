param(
    [string]$Repository = 'DDF-Technology/DDFLanguageEditor',
    [string]$KeyId = 'official-2026',
    [string]$BackupPath = (Join-Path $env:USERPROFILE ".ddf\release-keys\$KeyId.pkcs8.dpapi")
)

$ErrorActionPreference = 'Stop'
$expectedFingerprints = @{
    'official-2026' = 'cef7cb71ca0c95d06a4cf8b56d1996fe4e2dea0fd94ee490fa7184753465e204'
}
if (-not $expectedFingerprints.ContainsKey($KeyId)) {
    throw "KeyId non presente nell'allowlist locale: $KeyId"
}
if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
    throw "Backup DPAPI non trovato: $BackupPath"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI non disponibile.'
}
gh auth status --hostname github.com | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI non autenticata.'
}

$protectedBytes = [System.IO.File]::ReadAllBytes($BackupPath)
$entropy = [System.Text.Encoding]::UTF8.GetBytes(
    "DDFLanguageEditor/release-signing/$KeyId/v1")
$privateBytes = $null
$key = $null
try {
    $privateBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
        $protectedBytes,
        $entropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $key = [System.Security.Cryptography.ECDsa]::Create()
    $read = 0
    $key.ImportPkcs8PrivateKey($privateBytes, [ref]$read)
    if ($read -ne $privateBytes.Length -or $key.KeySize -ne 256) {
        throw 'Il backup non contiene una chiave ECDSA P-256 PKCS#8 valida.'
    }
    $publicKey = $key.ExportSubjectPublicKeyInfo()
    $fingerprint = [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($publicKey)
    ).ToLowerInvariant()
    if ($fingerprint -ne $expectedFingerprints[$KeyId]) {
        throw 'La chiave privata non corrisponde al fingerprint pubblico ufficiale.'
    }
    $privateBase64 = [Convert]::ToBase64String($privateBytes)
    $privateBase64 | gh secret set DDF_RELEASE_SIGNING_KEY_PKCS8_BASE64 `
        --repo $Repository
    if ($LASTEXITCODE -ne 0) {
        throw 'Caricamento del secret GitHub non riuscito.'
    }
    Write-Host "Secret aggiornato per $Repository con $KeyId ($fingerprint)."
}
finally {
    $privateBase64 = $null
    if ($privateBytes) {
        [Array]::Clear($privateBytes, 0, $privateBytes.Length)
    }
    if ($key) { $key.Dispose() }
}
