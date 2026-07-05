# KlangHub — Hardware-Abnahme (Premium Chromecast 2026 Sprint)

**Build:** `dist/KlangHub-Release-f4f9e6d.zip` (framework-abhängig, läuft auf dem maschinenweiten .NET-10-Runtime).
**Zweck:** die eine Sache abnehmen, die ich ohne Geräte nicht selbst testen kann. Alles Code-seitige ist grün
(140 Tests, App bootet, Artwork-Endpunkt über echten Socket verifiziert, FLAC verlustfrei + ~440× Echtzeit).

Bei Problemen: **Optionen → „Log device communication" AN** (blendet den versteckten **Log**-Tab ein; Logs im
`txtLog`-Textfeld, nicht auf Platte). Danach „Scan again for devices" bzw. Cast starten und die Zeilen ablesen.

---

> **Neu in diesem Build (`f4f9e6d`, nach HW-Test #2):** **FLAC als Out-of-box-Default** (24-bit HiFi, verlustfrei,
> KOMPRIMIERT) — löst den Enchant-`ERROR 102` (OOM bei 32-bit unkomprimiertem LPCM) · Zombie-id-Reconcile
> vervollständigt · **Deutsch** (Default bei deutschem OS) · 10 s Puffer · Version 0.0.0.1. **Bitte die alten
> Settings löschen** (`%LOCALAPPDATA%\KlangHub` — oder egal, der Build startet ohnehin frisch) damit FLAC +
> Deutsch als Default greifen.

## A. Discovery — 5 Geräte, egal ob IPv4 oder IPv6
1. App starten, ~30 s warten.
2. **Erwartung:** 5 Kacheln — Enchant, Google TV, TCL TV, Soundbar, the multi-room group (Gruppe).
   **DHCP-Move-Test (BUG A, neu):** Enchant im Betrieb stromlos machen + wieder einstecken (bekommt ggf. eine
   neue IP). **Erwartung jetzt:** die alte Kachel migriert auf die neue IP — **keine 6. „Error"-Kachel** mehr,
   kein Dauer-Reconnect-Spam. Falls doch ein Zombie bleibt: Log-Tab an → die `eureka: … id=`-Zeile des Enchant
   kopieren (zeigt, ob er eine stabile mDNS-`id=` trägt — davon hängt der Reconcile ab).
3. **Cross-Service-IPv4-Bridge ist jetzt aktiv** (`9ade996`): die App browst zusätzlich `_airplay`/`_raop` und
   recovert die Enchant-IPv4 aus seinem AirPlay-Dienst (geteilter `fd1a…`-Host). **Der Enchant sollte damit
   auch dann erscheinen, wenn sein `_googlecast` gerade IPv6-only annonciert.**
4. **Falls der Enchant TROTZDEM fehlt:** Log-Tab an → „Scan again" → prüfen:
   - Gibt es eine **`mDNS-bridge [_airplay/_raop] learned IPv4 … for hosts=[fd1a…]`**-Zeile? Wenn ja, aber der
     Enchant kommt nicht → sein `_googlecast`-`fd1a…`-Host weicht vom AirPlay-Host ab (die `mDNS-svc`-Zeilen
     beider Dienste kopieren, damit ich das Matching anpasse).
   - Gibt es KEINE `mDNS-bridge …learned IPv4…`-Zeile (nur IPv6 auch bei AirPlay) → dann ist es **wirklich
     IPv6-only überall** → nächster Schritt = **echtes IPv6-Audio** (großer Milestone: Dual-Stack-Listener +
     `[v6]`-Stream-URL + pro-Gerät-URL). In beiden Fällen: die `mDNS-svc [src][type] … addrs=[ip(family),…]`-
     Zeilen für den Enchant (`_googlecast` vs. `_airplay`/`_raop`) kopieren. Details: Checkpoint §4/§9.

## B. Codec — FLAC (der Kern-Test dieses Builds)
Default nach Erststart = **FLAC (24-bit, verlustfrei HiFi, komprimiert)**. Grund: 32-bit unkomprimiertes LPCM
warf am Enchant nach ~45–70 s `ERROR 102` (Empfänger-OOM); FLAC ist verlustfrei UND komprimiert → soll auf
ALLEN Geräten sauber laufen, auch am kleinen Enchant.
1. **Der entscheidende Test:** auf **jedes** der 5 Geräte casten (Enchant, Soundbar, Google TV, TCL TV) und
   **mindestens 3–5 Minuten** laufen lassen. **Erwartung:** durchgehend sauber, **kein** `ERROR 102`, **kein**
   Rauschen, **keine** Unterbrechungen.
2. **Falls ein Gerät FLAC ablehnt** (`LOAD_FAILED`) oder abbricht: sag mir welches — dann ist das Netz **WAV
   24-bit** (im Picker), und wir prüfen 16-bit oder eine native FLAC-Variante für dieses Gerät.
3. Zum Vergleich gern: **WAV 24-bit** auf dem Enchant testen — spielt es länger als 32-bit, aber kürzer/gleich
   wie FLAC? (bestätigt, dass „unkomprimiert" das Problem ist, nicht die Bittiefe).

## C. Premium-TV-Screen
1. Auf einen **TV** casten (mit dem FLAC-Default).
2. **Erwartung:** das gebrandete **„KlangHub — Now Playing"**-Bild (dunkler Verlauf, bernsteinfarbene Klang-Ringe,
   Wortmarke) **fullscreen** + Titel/Untertitel — **nicht** der generische „Default Media Receiver"-Screen.
3. Das Bild wird von KlangHub selbst geliefert (`http://<sender-ip>:<port>/artwork.png`, image/png, 1280×1280).

## D. Robustheit
1. **Reconnect:** während der Wiedergabe kurz WLAN am Gerät trennen/Standby → nach Rückkehr sollte KlangHub die
   Wiedergabe wieder aufnehmen (Backoff 5→10→20→30 s, Reset bei Playing).
2. **Stop/Skip:** Stop, dann erneut Play auf demselben Gerät → sauberer Neustart, kein „Socket-Junk" (RST-on-stop).

---

## Ergebnis-Rückmeldung (das brauche ich)
- **A:** Erscheinen 5 Geräte? Bleibt beim Enchant-Stromlos-Test die Zombie-Kachel weg?
- **B (Kern):** Spielt **FLAC durchgehend sauber (3–5 Min)** auf ALLEN Geräten, besonders am **Enchant** —
  **kein ERROR 102 / Rauschen** mehr? Falls ein Gerät FLAC ablehnt: welches?
- **C:** Erscheint das gebrandete TV-Bild?
- **D:** UI auf Deutsch? Reconnect + sauberer Stop ok?

Nächster Sprint (nach FLAC-Bestätigung): Opus (noch paketverlust-resistenter) · adaptives Buffer-Monitoring ·
Enchant-IPv6 (Bridge vs. Full-IPv6) · eigener animierter Receiver.
