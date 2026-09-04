# Was ein Cast-Gerät über sich preisgibt

Gemessen am 4. September 2026 an allen vier Geräten im Haus — TCL-Fernseher, Samsung-Soundbar,
Google Home und Harman-Kardon-Enchant. Keine Vermutungen: jede Zeile hier ist eine Antwort, die
tatsächlich über das Netz kam.

## Die drei Quellen

| Quelle | Was sie liefert | Wer antwortet |
|---|---|---|
| **mDNS-TXT-Record** | `md` Modell · `fn` Name · `ca` Fähigkeiten · `ve` Protokollversion · `rs` Aktivität · `st` Zustand · `id` Geräte-ID · `ic` Symbolpfad | **alle vier** |
| **eureka_info** (`:8008/setup/eureka_info?options=detail`) | Firmware, Sprache, Laufzeit, Update-Zustand, WLAN, MAC, Standort | alle vier — aber der Block `device_info` nur bei **einem** |
| **DIAL** (`:8008/ssdp/device-desc.xml`) | Hersteller und Modell als UPnP | **nur der Fernseher**; die drei Audiogeräte antworten mit 404 |

## Woher der Modellname kommt

Aus dem mDNS-Record, und nur von dort. Die Werte, wie sie ankamen:

```
md=Smart TV Pro          fn=TCL TV           ca=264709
md=Q995GD                fn=Soundbar         ca=199172
md=Google Home Speaker   fn=Google Home      ca=198660
md=Enchant Speaker       fn=Enchant Speaker  ca=198660
```

Damit ist die naheliegende Frage beantwortet: Der Enchant **verrät „Harman Kardon" nirgends**. Weder im
mDNS-Record noch in eureka_info noch über DIAL. Er nennt sich „Enchant Speaker", und das ist alles, was
er über sein Modell sagt. Ebenso wenig gibt es eine Seriennummer vom Gehäuse — die `id` ist eine UUID,
keine aufgedruckte Nummer.

Der Fernseher ist die Ausnahme: er beantwortet DIAL und nennt dort `<manufacturer>TCL</manufacturer>`.
Deshalb steht bei ihm ein Hersteller in den Details und bei den Lautsprechern keiner.

## Die Fähigkeiten-Bitmaske `ca`

Google beschreibt nur die unteren sechs Bits (Videoausgang, Videoeingang, Audioausgang, Audioeingang,
Entwicklermodus, Gruppenmitglied). Die Geräte setzen mehr. Gemessen:

| Gerät | `ca` | benannte Bits | ungenannte Bits |
|---|---|---|---|
| TCL-Fernseher | 264709 | Videoausgang, Audioausgang | 9, 11, 18 |
| Soundbar | 199172 | Audioausgang | 9, 11, 16, 17 |
| Google Home / Enchant | 198660 | Audioausgang | 11, 16, 17 |

**Videoausgang** ist damit die einzige verlässliche Art, einen Bildschirm von einem Lautsprecher zu
unterscheiden, ohne Google zu fragen — genau so entsteht die Zeile „Typ" in den Gerätedetails. Die
ungenannten Bits werden bewusst nicht geraten.

## Was auf keinem Weg zu bekommen ist

- **Der Raum.** Er steht ausschließlich in Googles Home Graph. Deshalb vergibt ihn KlangHub selbst und
  merkt ihn sich in `speakers.json`.
- **Die unterstützten Codecs.** Das Cast-Senderprotokoll hat keine Frage dafür. Erst im **eigenen
  Empfänger** lässt sich `canDisplayType('audio/flac')` aufrufen und die Antwort über einen eigenen
  Namespace zurückschicken — siehe [`PLAN-MULTIROOM-SYNC.md`](PLAN-MULTIROOM-SYNC.md). Bis dahin zeigt
  die Detailkarte die vom Cast-Standard garantierten Formate und sagt in einer Fußnote genau das.
- **Das Gerätesymbol.** `ic=/setup/icon.png` leitet auf ein generisches Google-Bild um, auf jedem Gerät
  dasselbe.

## Nebenbefund: die native Lautstärke-Schrittweite

Der Kontrollkanal (`GET_STATUS` im Empfänger-Namespace) liefert neben `level` und `muted` auch
`stepInterval` — und der ist je Gerät verschieden: Fernseher 0,01 · Soundbar und Google Home 0,02 ·
Enchant 0,04. Ein Feinregler, der in den Schritten des Geräts läuft statt in festen fünf Prozent,
könnte diesen Wert benutzen. Noch nicht umgesetzt, aber notiert.
