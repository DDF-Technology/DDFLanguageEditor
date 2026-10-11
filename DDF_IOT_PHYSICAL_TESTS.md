# Gate fisico GPIO, I²C, SPI e PWM

## Scopo

`DdfSystemDeviceIotConnection` usa `System.Device.Gpio` 4.2.0, ma la presenza
del package non garantisce che un computer esponga controller, driver, pin o
bus compatibili. Questa matrice registra quindi supporto verificato, non il
semplice fatto che il progetto compili.

| Sistema | Architettura | Binding/controller | GPIO | I²C | SPI | PWM | Stato |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Windows 10/11 | x64/x86/ARM64 | controller o bridge fornito dal dispositivo | da verificare | da verificare | da verificare | da verificare | non certificato |
| Linux | x64/ARM64/ARM32 | device/driver esposto al provider .NET IoT | da verificare | da verificare | da verificare | da verificare | non certificato |
| macOS | x64/ARM64 | nessun binding scelto | non supportato | non supportato | non supportato | non supportato | escluso finché non esiste un provider esplicito |
| Bridge USB o SDK vendor | dipende dal vendor | adapter `IDdfIotEndpointConnection` dedicato | da verificare | da verificare | da verificare | da verificare | fuori dal bridge predefinito |

Ogni riga promossa deve indicare modello della board/periferica, versione del
sistema, architettura, driver o binding, cablaggio, data, operatore e risultato.
Un esito su una board non autorizza automaticamente board o driver differenti.

## Smoke test volontario

La suite normale usa endpoint simulati e non tocca mai l'hardware. Impostando
`DDF_IOT_SMOKE_KIND`, lo stesso eseguibile dei test avvia invece una singola
prova fisica:

```powershell
$env:DDF_IOT_SMOKE_KIND = 'gpio-input'
$env:DDF_IOT_PIN = '17'
$env:DDF_IOT_PULL = 'up'
dotnet run --project tests/DDFLanguageEditor.Hardware.Tests -c Release
```

Modalità disponibili:

- `gpio-input`: richiede `DDF_IOT_PIN`; pull opzionale `none`, `up`, `down`;
- `gpio-output`: richiede pin, `DDF_IOT_LEVEL` e consenso alla scrittura;
- `i2c-read`: richiede `DDF_IOT_BUS`, `DDF_IOT_ADDRESS` (anche `0xNN`) e
  `DDF_IOT_LENGTH`;
- `spi-loopback`: richiede bus, `DDF_IOT_CS`, `DDF_IOT_CLOCK` e byte
  esadecimali `DDF_IOT_BYTES` separati da virgola;
- `pwm`: richiede `DDF_IOT_CHANNEL`, `DDF_IOT_FREQUENCY` e `DDF_IOT_DUTY`;
  chip e durata sono opzionali.

Le modalità che pilotano un'uscita (`gpio-output`, `spi-loopback`, `pwm`) sono
rifiutate finché non viene impostato esattamente
`DDF_IOT_SMOKE_ALLOW_WRITE=YES`. Il consenso evita attivazioni accidentali,
ma non sostituisce il controllo di tensioni, livelli logici, massa comune,
resistenze, chip-select e limiti elettrici prima del test.

## Criterio di promozione

`ddf.platform.hardware.iot` può diventare `available` soltanto quando:

1. almeno una configurazione reale documentata supera ciascun trasporto che si
   intende dichiarare supportato;
2. disconnessione, riapertura, cancellazione e cleanup sono verificati;
3. driver, binding e redistribuzione sono compatibili con la licenza del
   prodotto;
4. gli ambienti non verificati restano esplicitamente esclusi, senza fallback.
