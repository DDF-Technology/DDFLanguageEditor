# Policy degli host DDF Editor SDK

## Ambito fino alla 1.0

L'unico host di riferimento supportato durante la beta è C# su .NET 10 con
WinForms. `DDFLanguageEditor.MinimalHost`, `DDFLanguageEditor.HostDemo` e
`DDFLanguageEditor.DatabaseDemo` sono rispettivamente i riferimenti permanenti
per integrazione minima, isolamento multi-istanza e capacità applicative
complete.

Questo perimetro riguarda il codice dell'applicazione che incorpora l'editor;
non modifica la sintassi DDF, la IR, la VM o la semantica dello script. Core e
contratti pubblici restano il punto autorevole e non possono essere duplicati
in un binding specifico per un altro linguaggio o framework grafico.

## Destinazioni pianificate

VB.NET, F#, WPF e host non-.NET sono pianificati esclusivamente dopo la 1.0.
Non sono oggi supportati, promessi come compatibili o usati come criterio per
allargare le interfacce correnti.

Quando una destinazione verrà aperta dovrà:

1. consumare gli stessi contratti SDK, capability, diagnostiche e schemi
   versionati;
2. superare la stessa matrice di compatibilità binaria, sorgente applicabile e
   dati persistiti;
3. non reimplementare parser, type checker, runtime, debugger o semantica DDF;
4. introdurre soltanto un adapter di hosting/UI o un confine interoperabile
   esplicito;
5. avere lifecycle, threading, errori, packaging e supporto dichiarati prima
   di essere promosso da `post-1.0`.

Per WPF si valuterà un adapter UI sopra il medesimo SDK, non una seconda
superficie semantica. Per host non-.NET il confine dovrà essere versionato e
serializzabile (per esempio processo separato o ABI dedicata): le classi CLR non
verranno esposte direttamente e l'attuale protocollo worker non viene dichiarato
automaticamente API pubblica.

## Gate

`ddf-version-matrix.json` contiene la policy autorevole `hostSupport@1.0`. Il
gate Core verifica i tre progetti C#/.NET 10/WinForms e rifiuta l'introduzione
prematura di `.vbproj`, `.fsproj` o progetti `UseWPF=true`. Ogni futura modifica
del perimetro richiede prima aggiornamento della roadmap, della matrice, di
questa policy e dei contract test.
