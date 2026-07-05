# KlangHub — Hardware-Abnahme (Premium Chromecast 2026 Sprint)

**Build:** `dist/KlangHub-Release-040da3f.zip` (framework-abhängig, läuft auf dem maschinenweiten .NET-10-Runtime).
**Zweck:** die eine Sache abnehmen, die ich ohne Geräte nicht selbst testen kann. Alles Code-seitige ist grün
(139 Tests, App bootet, Artwork-Endpunkt über echten Socket verifiziert, FLAC verlustfrei + ~440× Echtzeit).

Bei Problemen: **Optionen → „Log device communication" AN** (blendet den versteckten **Log**-Tab ein; Logs im
`txtLog`-Textfeld, nicht auf Platte). Danach „Scan again for devices" bzw. Cast starten und die Zeilen ablesen.

---

> **Neu in diesem Build (`040da3f`, nach HW-Test #1):** DHCP-Move-Zombie-Fix (id=-Reconcile) · ConnectError-
> Circuit-Breaker · 48-kHz-Cap + 10 s Puffer (gegen das „Rauschen") · **32-bit WAV als Default** · Version
> 0.0.0.1 · **Deutsch** (Default bei deutschem OS — dieser Build startet als frischer Erststart, da der
> Versions-Bump den `user.config`-Pfad ändert; die UI ist auf deutschem Windows deutsch).

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

## B. Codec — lossless out-of-box + „alles muss funktionieren"
Default nach Erststart = **WAV 16-bit (lossless)**. Auswahl bietet zusätzlich **„FLAC (lossless — recommended)"**.
1. **Speaker (Enchant/Soundbar):** Cast starten → sollte über WAV-16 **und** FLAC sauber spielen.
2. **FLAC-Prüfung (der eine ungetestete Punkt):** Format auf **FLAC** stellen → auf einen Speaker casten.
   - **Spielt es?** → FLAC ist der Premium-Weg; sag Bescheid, ich flippe den **Default auf FLAC** (+ optional
     24-bit-HiFi als Folgeschritt).
   - **Spielt es NICHT / `LOAD_FAILED`?** → WAV-16 bleibt das Netz; dann prüfen wir 24-bit oder natives libFLAC.
3. ⚠️ **TV-Codec-Entscheidung (wichtig, echtes Risiko):** Der Checkpoint notiert, dass **TVs bei rohem WAV
   `LOAD_FAILED`** werfen. Der neue WAV-16-Default könnte den TV-Fall also verschlechtern (früher war MP3 der
   Default und lief auf TVs).
   - Cast auf **Google TV / TCL TV** mit WAV-16 testen. Bei `LOAD_FAILED` → auf **FLAC** wechseln (Cast-Codec,
     verlustfrei) und erneut testen; falls auch das scheitert, **MP3 (320)** als Kompatibilitäts-Fallback.
   - **Sag mir das Ergebnis** — davon hängt ab, ob der beste Default WAV-16, FLAC oder ein TV-Sonderweg ist
     (per-Gerät-Codec = eigener Milestone, da heute EIN Stream an alle geht).

## C. Premium-TV-Screen
1. Auf einen **TV** casten (Codec, der spielt — siehe B3).
2. **Erwartung:** das gebrandete **„KlangHub — Now Playing"**-Bild (dunkler Verlauf, bernsteinfarbene Klang-Ringe,
   Wortmarke) **fullscreen** + Titel/Untertitel — **nicht** der generische „Default Media Receiver"-Screen.
3. Das Bild wird von KlangHub selbst geliefert (`http://<sender-ip>:<port>/artwork.png`, image/png, 1280×1280).

## D. Robustheit
1. **Reconnect:** während der Wiedergabe kurz WLAN am Gerät trennen/Standby → nach Rückkehr sollte KlangHub die
   Wiedergabe wieder aufnehmen (Backoff 5→10→20→30 s, Reset bei Playing).
2. **Stop/Skip:** Stop, dann erneut Play auf demselben Gerät → sauberer Neustart, kein „Socket-Junk" (RST-on-stop).

---

## Ergebnis-Rückmeldung (das brauche ich)
- **A:** Erscheinen 5 Geräte? Falls Enchant fehlt → die `mDNS-svc`-Zeilen (§A.4).
- **B:** Spielt **FLAC** auf einem Speaker? Spielt **WAV-16 auf dem TV** — oder braucht der TV FLAC/MP3? (§B2, §B3)
- **C:** Erscheint das gebrandete TV-Bild?
- **D:** Reconnect + sauberer Stop ok?

Daraus folgt der nächste Sprint: Default-Codec-Flip (WAV↔FLAC) · TV-Codec-Weg · Enchant-IPv6 (Bridge vs.
Full-IPv6) · dann Opus / adaptives Buffer-Monitoring / eigener animierter Receiver.
