# Teilstück A — Now-Playing-Kaskade (Windows-Seite)

Status: **Design, freigegeben zur Umsetzung** (2026-09-05)
Vorgabe: Kapitel 3 des TV-Bühnen-Prompts („Now Playing Intelligence — ohne Metadaten stirbt die Bühne")
Reihenfolge: P0 (fertig) → **A (dies hier)** → B (Live-Transport über eigenen Namespace) → C (Cinematic-Bühne)

---

## 0. Warum dieses Teilstück zuerst kommt

KlangHub castet den Windows-Loopback. Der Stream hat keinen Titel, keinen Künstler, kein Cover und keine
Dauer — er ist eine endlose Tonspur. Heute füllt `ApplicationLogic.GetStreamMediaInfo()` das LOAD deshalb
mit Ersatzwerten:

| Feld | heute |
|---|---|
| Title | der vom Nutzer eingestellte Stream-Titel, sonst `"KlangHub"` |
| Subtitle | ein fester Satz aus den Ressourcen |
| Album | `"KlangHub"` |
| ImageUrl | selbst gerendertes KlangHub-Artwork |

Das ist genau die Bühne, die der Prompt verbietet: „Unknown / leeres Cover / Dateiname in Rohform". **Ohne
echte Metadaten ist jede Gestaltungsarbeit an der Bühne verschwendet** — deshalb A vor C.

A ist außerdem das einzige Teilstück, das **ohne Fernseher vollständig prüfbar** ist. Solange Push, GitHub
Pages und das Testgerät ausstehen, ist es das Einzige, woran ehrlich weitergearbeitet werden kann.

---

## 1. Umfang

**In diesem Teilstück:**

- ein neutrales Datenmodell für „was läuft gerade"
- die Quellen-Kaskade aus Kapitel 3.2, Stufen 1–6
- die Zusammenführungsregeln (Kapitel 3.3): bessere Quelle gewinnt je Feld, leere Felder überschreiben nie
- Trackwechsel-Erkennung, die nicht auf jedes SMTC-Zucken anspringt
- Cover-Jagd inklusive Auslieferung über den vorhandenen HTTP-Server
- Anbindung an `CastMediaMetadata`, sodass **schon der Default Media Receiver** echte Titel zeigt
- Einstellungen für die Quellenwahl

**Nicht in diesem Teilstück:**

- die Bühne selbst, ihre Modi, Lyrics-Darstellung, Pulse (→ C)
- der eigene Cast-Namespace für Live-Aktualisierung ohne Neuladen (→ B)
- akustischer Fingerabdruck (Stufe 6 der Vorlage). Bewusst zurückgestellt: langsam, unsicher, und der
  Prompt selbst nennt ihn „nicht als Default-Show". Das Modell hält den Platz dafür frei, mehr nicht.
- Artist-Bilder (gehören zu Modus D und zur Lizenzfrage, nicht zur Kaskade)

---

## 2. Datenmodell

Neu in `KlangHub.Core/Core/NowPlaying/` — reine Logik, kein Windows, vollständig testbar.

```
NowPlayingTrack        unveränderlich; Title, Artist, Album, Duration?, FilePath?,
                       CoverSource?, Format?, SampleRate?, BitDepth?
                       Jedes Feld darf fehlen. Ein leeres Feld ist eine Aussage: "wissen wir nicht".

MetadataSource         Rangfolge, aufsteigend nach Vertrauen:
                       FileName < NowPlayingFile < SystemMediaControls < FileTags
                       Cover getrennt, siehe §5.

FieldValue<T>          ein Wert plus die Quelle, aus der er stammt. Das ist der Kern: ohne
                       mitgeführte Herkunft kann die Kaskade nicht entscheiden, ob ein neuer
                       Wert einen alten ablösen darf.

NowPlayingCascade      führt Beiträge zusammen und gibt den aktuell besten Track heraus.
                       Kennt keine Datei, kein WinRT, keine Uhr - nimmt Beiträge entgegen
                       und rechnet.

TrackIdentity          normalisierter Vergleichsschlüssel für "ist das ein anderer Titel?"
```

**Warum `FileTags` über `SystemMediaControls` steht:** Die Tags der Datei sind das, was tatsächlich auf der
Platte steht. SMTC gibt weiter, was ein Player gerade meldet — oft gekürzt, manchmal mit Werbezusätzen,
gelegentlich mit dem Dateinamen als Titel. Wo der Dateipfad bekannt ist, sind die Tags die ehrlichere
Quelle. Der Prompt listet die Tags aus demselben Grund als Stufe 1.

**Warum `NowPlayingFile` über `FileName` steht, aber unter SMTC:** Die Datei ist vom Nutzer eingerichtet und
absichtlich befüllt — deutlich mehr wert als ein aus dem Dateinamen geratener Titel. Sie kann aber
veralten, wenn der Helfer hängt, während SMTC live am Player hängt.

---

## 3. Die Quellen

Jede Quelle ist ein `INowPlayingSource`: sie meldet über ein Ereignis einen Beitrag
(`NowPlayingContribution`: Quelle + Track + Zeitstempel) und weiß nichts von den anderen.

### 3.1 Datei-Tags (`FileTagSource`) — Rang 4

Liest ID3, Vorbis, MP4 und FLAC über **`z440.atl.core`** (MIT-Lizenz). Bewusst nicht TagLib# — das ist
LGPL, und das Repo trägt mit dem FLAKE-Encoder bereits eine LGPL-Ausnahme, die es nicht vergrößern soll
(siehe `docs/THIRD-PARTY-LICENSES.md`).

Greift nur, wenn ein Dateipfad bekannt ist. Woher der kommt: aus SMTC (manche Player legen ihn ab), aus
der Now-Playing-Datei (`$file`-Platzhalter) oder aus der Konfiguration eines beobachteten Ordners.

Liefert zusätzlich Format, Samplerate und Bittiefe — das Qualitätszeichen aus Kapitel 4, Modus A.

### 3.2 Windows Now Playing (`SystemMediaControlsSource`) — Rang 3

`Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`. Verifiziert lauffähig
(Spike vom 2026-09-05): Ziel-Framework `net10.0-windows10.0.19041.0` genügt, **kein Zusatzpaket**.

Liefert Titel, Künstler, Album, Miniaturbild, Wiedergabezustand und — entscheidend für die Fortschrittslinie
der Bühne — Position und Dauer über `GetTimelineProperties()`.

**Offene Messung:** Ob *Clementine* auf Windows eine SMTC-Sitzung anlegt, ist unbestätigt. Die Vorlage sagt
„oft unzuverlässig oder gar nicht". Die Kaskade ist genau dafür gebaut; die Messung ändert nichts am
Entwurf, nur an der Erwartung, welche Stufe im Wohnzimmer wirklich greift.

### 3.3 Now-Playing-Datei (`NowPlayingFileSource`) — Rang 2

Eine vom Nutzer hinterlegte Textdatei, die ein Player oder ein kleines Hilfswerkzeug aktuell hält.
Erkannte Formen, wie in der Vorlage verlangt:

- eine Zeile `Artist - Title`
- mehrere Zeilen (`Artist` / `Title` / `Album`)
- Platzhalter-Vorlagen im VLC-Stil: `$artist`, `$title`, `$album`, `$file`

Beobachtet mit `FileSystemWatcher`, **entprellt mit 300 ms**, damit ein halb geschriebener Zwischenstand
nicht als neuer Titel gilt. Kodierung: UTF-8 mit und ohne BOM, UTF-16 und die Systemkodierung — ein
Hilfswerkzeug schreibt, was es will.

### 3.4 Dateiname und Ordner (`FileNameSource`) — Rang 1

Nur Rückfall. Muster: `Artist - Title.mp3`, `01 Title.flac` in einem Ordner `Artist - Album`.
Trackzahlen, Erweiterungen und Trennzeichen werden abgeschnitten.

### 3.5 Clementine

Bekommt **keine eigene Quelle**. Auf Windows gibt es kein MPRIS; was Clementine hergibt, kommt über SMTC,
über die Now-Playing-Datei oder über den Dateipfad und dessen Tags. Was Clementine braucht, ist keine
Sonderbehandlung im Code, sondern **eine verständliche Anleitung in der App** (§7). Niemand soll den Player
wechseln müssen.

---

## 4. Zusammenführung

`NowPlayingCascade.Contribute(contribution)` und `NowPlayingCascade.Current`.

Regeln, jede einzeln testbar:

1. **Feldweise, nicht trackweise.** Kommt der Künstler aus der Now-Playing-Datei und das Album aus den Tags,
   stehen beide nebeneinander.
2. **Höherer Rang gewinnt.** Ein Wert wird nur ersetzt, wenn die neue Quelle mindestens denselben Rang hat.
3. **Leer überschreibt nie.** Meldet SMTC plötzlich einen leeren Künstler, bleibt der bekannte stehen.
   Genau das verhindert das Flackern von Platzhaltern, das der Prompt verbietet.
4. **Gleiche Quelle darf sich korrigieren.** Rang 3 ersetzt Rang 3 — sonst könnte ein Player seinen eigenen
   Titel nie berichtigen.
5. **Ein Trackwechsel räumt ab.** Alle Felder werden verworfen und neu aufgebaut; sonst hängt das Album des
   vorigen Stücks am neuen.

### Trackwechsel

`TrackIdentity` aus normalisiertem Titel + Künstler + Dauer (auf die Sekunde gerundet; fehlt sie, zählt sie
nicht mit). Normalisierung: Kleinschreibung, Mehrfach-Leerzeichen zusammengezogen, Randzeichen entfernt.

Ein Wechsel gilt **nur** als Wechsel, wenn sich die Identität ändert **und** die Änderung nicht bloß eine
Bereicherung des bisherigen Wissens ist: Kommt zu einem bekannten Titel der bis dahin fehlende Künstler
hinzu, ist das **derselbe** Track. Das ist der in der Vorlage beschriebene Fall — erst der Dateiname, zwei
Sekunden später der Künstler — und er darf keinen harten Szenenwechsel auslösen.

Zusätzlich: ein Wechsel des Dateipfads ist immer ein Wechsel, auch bei gleichem Titel (zwei Aufnahmen
desselben Stücks).

---

## 5. Cover

Eigene Kaskade, weil die Quellen andere sind (Kapitel 3.2, Stufe 6):

1. eingebettetes Bild der Datei
2. `cover.jpg` / `folder.jpg` / `Artwork.jpg` / `front.jpg` im Albumordner
3. Miniaturbild aus SMTC
4. Cover-Zwischenspeicher des Players (Clementine legt temporäre Art-Dateien an)
5. das gerenderte KlangHub-Artwork — **nur**, wenn wirklich nichts anderes existiert

Regel aus dem Prompt: „Nie ein generisches Notenschlüssel-Icon als Dauerzustand, wenn irgendein Bild
existiert." Deshalb steht das eigene Artwork ganz unten und wird ersetzt, sobald etwas Besseres auftaucht.

Größe: die längste Kante wird auf 1280 px gebracht, das Seitenverhältnis **nie** verzerrt (Kapitel 6).
Ausgeliefert über den vorhandenen `ArtworkHttp`-Server unter einer Adresse, die sich mit dem Coverinhalt
ändert — sonst zeigt ein Empfänger sein zwischengespeichertes Bild weiter.

---

## 6. Anbindung

`ApplicationLogic.GetStreamMediaInfo()` liest künftig aus der Kaskade:

| Feld | Quelle | Rückfall |
|---|---|---|
| Title | `Track.Title` | Stream-Titel, dann `"KlangHub"` |
| Subtitle | `Track.Artist` | der bisherige feste Satz |
| Album | `Track.Album` | `"KlangHub"` |
| ImageUrl | Cover-Kaskade | gerendertes Artwork |

Damit zeigt **schon der Default Media Receiver** Künstler, Titel und Cover — die Abnahme von Phase 1 hängt
so nicht allein am noch nicht erreichbaren Custom Receiver.

**Korrektur beim Umsetzen (2026-09-05):** Ursprünglich stand hier, bei jedem Trackwechsel werde ein neues
LOAD geschickt. Das ist **verworfen**. Beim Loopback-Stream heißt ein neues LOAD: Verbindung abbauen, Puffer
neu füllen — ein bis drei Sekunden Stille, bei **jedem** Titel. Das ist das genaue Gegenteil von „Trackwechsel
sind weich" (Kapitel 6 der Vorlage) und im Wohnzimmer unzumutbar.

Stattdessen: die gesammelten Metadaten gehen in das LOAD, das ohnehin beim Start der Wiedergabe geschickt
wird. Ein Titelwechsel **während** einer laufenden Sitzung erreicht den Fernseher erst mit Teilstück B, das
den Bildschirm über den eigenen Namespace aktualisiert, ohne den Ton anzufassen. Das ist der einzige weiche
Weg, und es ist der Grund, warum B direkt auf A folgt.

Die strenge Wechsel-Erkennung bleibt trotzdem richtig: sie ist ab B die Grundlage dafür, wann die Bühne
schneiden darf und wann sie nur atmen soll.

---

## 7. Einstellungen

Für Normalnutzer unsichtbar — Standard ist „automatisch das Beste nehmen", alle Quellen an.

Für Power-User, wie in Kapitel 9 verlangt:

- Datei-Tags an/aus
- Windows-Now-Playing an/aus
- Now-Playing-Datei an/aus + Pfad
- ein erklärender Satz ohne Entwicklerbegriffe:
  „Wenn Clementine oder ein anderer Player keine Titel ans Fernsehbild schickt, zeigt KlangHub Künstler und
  Titel aus der Now-Playing-Datei oder aus Windows-Now-Playing."

Abgelegt neben den bestehenden Einstellungen.

---

## 8. Auswirkung auf die Projektdateien

`KlangHub.Platform` und `KlangHub` wechseln von `net10.0-windows` auf **`net10.0-windows10.0.19041.0`**.
`KlangHub.Core` bleibt auf `net10.0` — die Kaskadenlogik darf kein Windows kennen, sonst ist sie nicht
mehr das, was die Tests prüfen können. Neues Paket: `z440.atl.core` (MIT), Eintrag in
`docs/THIRD-PARTY-LICENSES.md`.

---

## 9. Prüfplan

Test-getrieben, in dieser Reihenfolge — jede Stufe für sich grün, bevor die nächste beginnt:

1. `TrackIdentity` — Normalisierung, Bereicherung ≠ Wechsel, Dateipfadwechsel = Wechsel
2. `NowPlayingCascade` — die fünf Regeln aus §4, jede einzeln
3. `NowPlayingFileSource` — die drei Textformen, Platzhalter, Kodierungen, Entprellen
4. `FileNameSource` — die Muster, und dass Unsinn nichts liefert statt Falsches
5. Cover-Kaskade — Reihenfolge, kein Verzerren, Adresse ändert sich mit dem Inhalt
6. `FileTagSource` — gegen echte kleine Testdateien
7. `SystemMediaControlsSource` — dünn gehalten und hinter einer Schnittstelle, weil WinRT im Test nicht
   herstellbar ist; die Umrechnung von WinRT-Werten auf `NowPlayingTrack` wird getrennt geprüft

**Abnahme dieses Teilstücks:** Ein Titel aus einem externen Player erscheint mit Künstler, Titel und Cover
in dem, was KlangHub für das LOAD zusammenstellt — nachgewiesen über die Kaskade, ohne Fernseher. Der
echte Wohnzimmer-Nachweis gehört zur Abnahme von Phase 1 und braucht das Testgerät.
