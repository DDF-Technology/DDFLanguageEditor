# Chiavi di firma delle release

La firma del manifesto aggiornamenti usa ECDSA P-256/SHA-256. Non coincide con
la futura firma Authenticode degli eseguibili Windows.

## Chiave attiva

- `keyId`: `official-2026`
- formato pubblico: SubjectPublicKeyInfo DER, Base64 nel trust store
- fingerprint SHA-256:
  `cef7cb71ca0c95d06a4cf8b56d1996fe4e2dea0fd94ee490fa7184753465e204`
- trust store versionato: `ddf-release-trust-store.json`
- secret GitHub Actions: `DDF_RELEASE_SIGNING_KEY_PKCS8_BASE64`

La chiave privata non appartiene al repository. La copia amministrativa locale
è cifrata tramite DPAPI `CurrentUser` e può essere decifrata soltanto dallo
stesso account Windows sullo stesso profilo:

`%USERPROFILE%\.ddf\release-keys\official-2026.pkcs8.dpapi`

`tools/set-release-signing-secret.ps1` verifica formato, fingerprint e account
GitHub prima di inviare la privata via standard input. Non crea file in chiaro e
azzera il buffer binario al termine.

## Pubblicazione

1. La pipeline legge esclusivamente il secret GitHub.
2. `tools/publish-release.ps1` importa PKCS#8 e richiede ECDSA P-256.
3. ZIP e SHA-256 vengono prodotti prima del manifesto.
4. Il manifesto canonico viene firmato con `official-2026`.
5. La pipeline rilegge il manifesto sia con la pubblica derivata sia con il
   trust store realmente incorporato nel Core e confronta il fingerprint;
   interrompe la release se uno dei controlli non riesce.
6. Il consumer usa soltanto le chiavi `active` del trust store incorporato.

## Rotazione

La rotazione deve evitare di rendere invisibili release ancora supportate:

1. generare offline una nuova coppia e un nuovo `keyId`;
2. aggiungere la nuova pubblica come `active`, mantenendo attiva la precedente;
3. distribuire almeno una versione dell'editor contenente entrambe;
4. sostituire il secret GitHub e iniziare a firmare con la nuova chiave;
5. trascorso il periodo di compatibilità, marcare la vecchia come `revoked`;
6. non riutilizzare mai un `keyId` e conservare evidenza dei fingerprint.

In caso di sospetta compromissione, la chiave viene marcata immediatamente
`revoked`; una release firmata soltanto dalla chiave revocata resta non
attendibile. Una perdita della privata senza compromissione richiede comunque
una nuova coppia: la pubblica esistente non consente di ricostruirla.
