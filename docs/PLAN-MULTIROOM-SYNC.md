# Plan: echte Multiroom-Synchronität (Custom Cast Receiver)

Stand 2026-09-04. Ausgangsfrage: Können wir mit KlangHub unter Windows das erreichen, was ein WiiM zwischen
seinen eigenen Geräten kann — mehrere Lautsprecher und der Fernseher wirklich im Takt?

## Befund

**Warum WiiM/Sonos das können.** Eigene Firmware auf *beiden* Seiten: Der Sender verteilt zeitgestempeltes
Audio, jeder Empfänger richtet seine Wiedergabe an einer gemeinsamen Uhr aus und korrigiert laufend nach.
Ein WiiM synchronisiert dabei nur WiiM-Geräte — einen fremden Fernseher bringt auch er nicht in den Takt.

**Warum unser heutiges Einzel-Casting es nicht kann.** Jedes Gerät bekommt eine eigene HTTP-Verbindung
(`Device.streamingConnection`), und jeder Empfänger hat eigene Uhr, eigenen Puffer, eigenen DAC-Takt. Selbst
bei exakt gleichzeitigem Start laufen sie auseinander: Consumer-Quarze weichen typisch ±50 ppm ab, zwei
Geräte gegenläufig also 100 ppm ≈ **0,36 s Versatz pro Stunde**. Der Default Media Receiver (`CC1AD845`)
bietet keine Schnittstelle für einen Uhrenabgleich. Das ist eine Protokoll-Lücke, kein Fleißproblem.

**Was heute schon synchron ist.** Eine Lautsprechergruppe aus der Google-Home-App erscheint als *ein*
Cast-Ziel; die Synchronisation macht Googles Gruppenleiter im Empfänger, driftkorrigiert. KlangHub castet
dorthin und erbt diese Synchronität. Das bleibt für reine Audio-Setups der beste Weg.

## Stufenplan

### Stufe 1 — Latenz-Offset pro Lautsprecher (klein, sofort nützlich)
Regler 0–500 ms im ⋮-Menü je Gerät, gespeichert neben Raum und Maximallautstärke in `speakers.json`. Da jedes
Gerät seine eigene Verbindung hat, wird ihm ein entsprechend zurückversetzter Ausschnitt des Ringpuffers
geliefert. Löst den praktisch wichtigsten Fall: Ein Fernseher hat wegen seiner Bildverarbeitung eine
**konstante** Zusatzverzögerung (typisch 60–150 ms). Einmal nach Gehör eingestellt, sitzt es.

### Stufe 2 — automatische Grobmessung
`MEDIA_STATUS` liefert je Gerät `currentTime` (wird bereits empfangen, siehe `DeviceCommunication`). Daraus
lässt sich der Versatz schätzen und als Startwert für Stufe 1 vorschlagen. Realistisch ±100–200 ms, weil die
Statusmeldungen selbst zittern — als Vorschlag brauchbar, nicht als Endwert.

### Stufe 3 — eigener Cast Receiver (das eigentliche Ziel)
Eine eigene Empfänger-App (HTML/JS) gibt uns Kontrolle über **beide** Enden:

- **Zeitbasis:** Sender und Empfänger gleichen ihre Uhren über einen eigenen Cast-Namespace ab
  (`urn:x-cast:com.klanghub.sync`), im Prinzip ein kleines NTP über Round-Trip-Messung.
- **Wiedergabe:** Der Receiver spielt nicht „sobald Daten da sind", sondern plant über die Web Audio API auf
  einen exakten Zeitpunkt der gemeinsamen Zeitachse (`AudioBufferSourceNode.start(when)`).
- **Drift:** laufende Korrektur in winzigen Schritten (Resampling bzw. Sample-Einfügen/-Verwerfen).
- **Nebengewinn:** Der Fernseher zeigt dann *unsere* Oberfläche statt „Default Media Receiver" — Name, Icon,
  Layout, und ein **live laufendes Pegelmeter** statt des statischen Artworks.

**Preis:** Der Receiver wird beim Start aus dem Internet geladen. KlangHub läuft heute vollständig offline im
LAN; mit Custom Receiver braucht der Startvorgang eine Internetverbindung. Der Audiostream selbst bleibt lokal.

## Hosting

„Gehostet" heißt: Die Receiver-Seite muss unter einer öffentlichen **HTTPS**-URL liegen, die das Cast-Gerät
lädt. **GitHub Pages** ist dafür ideal und kostenlos — passt zum ohnehin geplanten öffentlichen Repository:

```
klanghub/
  receiver/index.html      ← die Empfänger-App
```
→ `https://<konto>.github.io/klanghub/receiver/`

Diese URL wird bei Google als Receiver-URL hinterlegt. Die Datei ist wenige Kilobyte und wird pro
Wiedergabestart einmal geladen.

## Registrierung (verifiziert 2026-09-04 gegen developers.google.com/cast/docs/registration)

1. **Cast Developer Console:** https://cast.google.com/publish — mit Google-Konto anmelden.
2. **Entwicklerkonto anlegen:** einmalige, **nicht erstattungsfähige Gebühr von 5 USD**.
3. **Anwendung hinzufügen** → Typ wählen:
   - **Custom Receiver** — eigene HTML/JS-App. Für Stufe 3 zwingend.
   - **Styled Media Receiver** — nur eine CSS-Datei, ändert Name und Aussehen, aber **kein eigener Code**,
     also *keine* Synchronisation. Der schnelle Weg, wenn es zunächst nur um Name und Optik geht.
4. **Receiver-URL eintragen** (die GitHub-Pages-Adresse) → Google vergibt die **App-ID** (acht Zeichen).
   Diese ersetzt in `ChromeCastMessages.GetLaunchMessage` die Standard-ID `CC1AD845`.
5. **Testgeräte registrieren:** Seriennummer des Cast-Geräts eintragen (Google-Home-App → Gerät →
   Einstellungen → Geräteinformationen), **15 Minuten warten**, dann das Gerät stromlos machen und neu
   starten. Danach steht der Status auf „Ready for Testing".
6. **Veröffentlichen** (erst nötig, damit die App auf *fremden* Geräten läuft): Anwendungsname, URL,
   Sender-Angaben, Kategorie, Titel (max. 50 Zeichen), Beschreibung (max. 80 Zeichen) und ein Icon in
   512 × 512 px. Jede spätere Änderung erfordert ein erneutes Veröffentlichen.

**Für ein Open-Source-Projekt beachten:** Die App-ID hängt am Google-Konto des Registrierenden. Entweder wird
die App einmal für alle veröffentlicht (dann nutzen alle Nutzer dieselbe gehostete Receiver-Seite), oder
jeder Fork trägt seine eigene App-ID ein. Deshalb sollte die ID in KlangHub **konfigurierbar** sein und nicht
im Code stehen.

## Nächste Schritte

- [ ] App-ID aus dem Code in die Einstellungen holen (Vorbereitung, unabhängig von der Registrierung)
- [ ] Stufe 1: Offset-Regler pro Gerät
- [ ] Receiver-Skelett unter `receiver/` im KlangHub-Look, inkl. GitHub-Pages-Struktur
- [ ] Stufe 3: Sync-Protokoll über eigenen Namespace
- [ ] Stufe 2: automatische Messung als Startwert für Stufe 1
