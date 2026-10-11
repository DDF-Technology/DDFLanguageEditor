# DDFLanguageEditor.Runtime

Runtime ABI minimale per gli artefatti DDF compilati dallo standalone o dalla
futura CLI. Il contratto `1.2.0` non dipende da Core, WinForms, SDK embedded o
Roslyn.

Contiene controlli numerici DDF, budget e cancellazione, I/O esplicito, `end`,
errori con coordinate sorgente e un provider di capability deny-by-default. Non
contiene cataloghi dell'applicazione host, reflection, COM o caricamento di DLL.
La ABI include inoltre un observer cooperativo opzionale per sequence point,
stack e snapshot immutabili di parametri, locali, globali, array e strutture DDF.
Le strutture generate implementano un contratto esplicito privo di reflection;
profondità, figli e valori totali sono limitati. Se l'observer è assente, il
percorso normale conserva lo stesso comportamento.
