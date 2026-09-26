# Specifica lessicale DDF 0.12

Stato: normativa per il nucleo lessicale approvato nella `0.12.1`. Direttive e
operatori rimangono candidati finché non vengono chiusi i successivi incrementi
della stessa milestone.

## Testo e confronto

I sorgenti sono UTF-8. Keyword e identificatori sono confrontati per valore
Unicode esatto e distinguono maiuscole e minuscole. DDF non normalizza né rende
equivalenti grafie Unicode differenti; gli strumenti potranno diagnosticarle in
futuro, ma non devono cambiare automaticamente il sorgente.

Gli spazi Unicode separano i token. Le terminazioni di riga accettate dal
documento restano LF e CRLF e vengono normalizzate soltanto dalle preferenze di
salvataggio dell'editor, non dal linguaggio.

## Identificatori

Un identificatore inizia con una lettera Unicode o `_`. I caratteri successivi
possono essere lettere Unicode, cifre Unicode o `_`.

```ebnf
identifier = ( unicode-letter | "_" ),
             { unicode-letter | unicode-digit | "_" } ;
```

La regola consente nomi didattici e applicativi localizzati. Un identificatore
non può coincidere esattamente con una keyword o con `true`/`false`.

## Numeri

I letterali numerici usano esclusivamente le cifre ASCII `0`–`9`. La parte
decimale usa `.` e, quando presente, deve contenere almeno una cifra.

```ebnf
integer-literal = digit, { digit } ;
decimal-literal = digit, { digit }, ".", digit, { digit } ;
digit = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
```

Il segno non appartiene al letterale: `-2` è l'operatore prefisso `-` applicato
a `2`. Basi diverse, separatori, suffissi ed esponenti non appartengono ancora
al nucleo e richiederanno una decisione versionata.

## Booleani

Gli unici letterali booleani canonici sono `true` e `false`. `True` e `False`
sono identificatori ordinari e non vengono più accettati come alias beta.

## Stringhe

Una stringa è racchiusa tra doppi apici e non può attraversare una terminazione
di riga. Sono ammesse soltanto queste sequenze di escape:

| Sequenza | Valore |
| --- | --- |
| `\"` | doppio apice |
| `\\` | barra inversa |
| `\n` | nuova riga nel valore |
| `\r` | ritorno carrello nel valore |
| `\t` | tabulazione nel valore |

Una stringa non chiusa produce `DDF001`; un escape non riconosciuto produce
`DDF005`. Stringhe multilinea e interpolazione non appartengono a questa
versione.

## Commenti

- `//` introduce un commento fino alla fine della riga;
- `/*` apre un commento a blocchi chiuso dal primo `*/`;
- i commenti a blocchi non sono annidati;
- `///` è riconosciuto come commento di documentazione dagli strumenti, ma resta
  lessicalmente un commento di riga.

Un commento a blocchi non terminato produce `DDF002`.

## Direttiva transitoria

`@@'nome'` resta riconosciuta, su una sola riga, per compatibilità con i progetti
beta. Il suo significato definitivo sarà deciso nella `0.12.3`; non deve essere
usata come inclusione testuale. Una direttiva non terminata produce `DDF003`.

## Diagnostiche lessicali

| Codice | Significato |
| --- | --- |
| `DDF001` | stringa non terminata o interrotta dalla fine riga |
| `DDF002` | commento a blocchi non terminato |
| `DDF003` | direttiva di libreria non terminata |
| `DDF004` | carattere non riconosciuto |
| `DDF005` | sequenza di escape non riconosciuta |

## Fonte eseguibile

Le regole sono replicate in `language-packages/ddf/lexicon.json` e verificate
dalla suite Core e da `language-packages/ddf/tests.json`. Ogni modifica normativa
deve aggiornare insieme specifica, schema, pacchetto e test.
