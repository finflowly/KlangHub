# KlangHub — UI-Konzept „Die warme HiFi-Konsole" (2026-07-05)

Verbindliches Design-Dokument für den Premium-UI-Redesign. Referenz-Mockup: `UI Konzept.png` im Repo-Root.
Visuelles Konzept-Board (live, animiert): Artifact `klanghub-konzept-board` (konzept-v1).

## Leitidee
KlangHub ist eine **HiFi-Konsole**, kein Fenster mit Steuerelementen. Warm-dunkler Korpus, **ein**
Akzent (Bernstein), Karten die **glühen wenn sie spielen**. Reines WinForms, owner-drawn.
- **Signatur:** die spielende Karte (Glow + Akzent-Kante + live laufender Pegel). Alles andere ruhig.
- **Disziplin:** Bernstein nur „spielt" & Fokus · Cyan = verbunden · Ember = nur Fehler · sonst Elfenbein/Slate auf Tinte.

## Entscheidungen (von the maintainer bestätigt 2026-07-05)
1. **Vokabular:** „Räume / Einstellungen / Protokoll" bleibt (Hausstil, per `GermanLocalizationTests` fixiert).
   Konzept-Wort „Geräte" gilt als überholt. Alle Ausreißer (Header „Geräte", Karten „Cast-Gerät") auf „Räume" vereinheitlichen.
2. **Theme:** Warm-Dunkel = **feste Identität**. „Dunkles Design"-Schalter fliegt raus, kein Pfad rendert je generisches Hell.
   Echtes Light-Theme = späteres eigenes Milestone.
3. **Umfang:** Volle Überarbeitung (Karten, Einstellungen, Kopfleiste, Rahmen, Sprache), phasenweise mit Checkpoints.

## Kernbefund des Audits
Fundament ist gut: Karten (`DeviceControl`) + 4 Spezial-Controls sind bereits sauber owner-drawn GDI+ über `Theme.cs`.
Die Abweichungen sind **konzentriert**, nicht flächig — v.a. Chrome/Rand, Kopf-Komposition, die Einstellungen-Seite (reitet
noch auf Stock-WinForms-Controls), Copy.

## Design-System (Ziel-Tokens)
| Token | Jetzt | Ziel | Rolle |
|---|---|---|---|
| Amber | #EBB65A | **#E8B65A** | Akzent |
| Blue→Cyan | #6C89B8 | **#4C9EBB** | verbunden |
| Slate | #8A94A6 | **#8A9AA6** | sekundär |
| Ember | #E5734B | **#E5734D** | Fehler |
| Ink/Surface/Raised/Ivory | — | (schon exakt) | Grund/Karte/Hover/Text |
Neu: `MeterOff`-Token (unlit). Metrics: Grid 8 · Pad 16 · Gap 16 · Radius Karte 14 / Control 9 / Pille 999 · Hover +2px · Fokus 2px Amber.
Typo: Segoe UI Variable, **Semibold per Familienname** (kein FontStyle.Bold), größere Skala, getrackte Versal-Labels (per-Glyph), Tabular-Ziffern statt Consolas.
Neue Theme-Helfer: `DrawGlow`, `DrawFocusRing`, `DrawAmberSlider`, `DrawBrandMark`, `DrawSpeakerGlyph`, `Blend`, `DrawTrackedLabel`; Pen/Brush-Cache.

## Status-Modell (Farbe UND Form)
- **Wiedergabe:** Amber-Punkt (glüht) + 3px Amber-Kante + Glow + Pegel läuft; Pille = Format (stabil).
- **Verbunden:** Cyan-Punkt, „bereit", Meter grau.
- **Nicht erreichbar:** Ember-Punkt + Ember-Kante + Ember-„erneut verbinden" + Grund/Backoff.
Kernprinzip: **Format-Pille zustandsstabil**, Status wandert in Punkt+Wort+Kante.

## Umsetzungsreihenfolge (Quick Wins zuerst)
Jede Phase: 140 Tests grün · Smoke-Test · Checkpoint. Native Titelleisten-Chrome braucht the maintainer's HW-Bestätigung.

### Phase 0 — Quick Wins (Stunden, null Funktionsrisiko)
- **Weißer Rand weg (2 Ursachen):** `DwmChrome.cs:43` DWM-Border `AmberDim` → `Ink`. TabControl-Body-Kante: Quick-Fix überpinseln (voll: Phase 3).
- **Palette-Hex** korrigieren (`Theme.cs:22,25,28,29` + AmberSoft/Tint-Basis).
- **Dark fest an:** `GetDarkMode()` → true erzwingen, `chkDarkMode` ausblenden (voller Ausbau: Light-Pfade später entfernen).
- **Chrome-Reste:** `HelpButton=false` (`Designer:925`), `linkHelp.BackColor=White` + `lblLagExperimental=SystemColors.Control` + `MS Sans Serif`-Fonts killen (`Designer:361,605,799`).
- **Copy:** Credit-Zeile + „Klangquelle"→„Audioquelle" (+ `MessageBox_NoRecordingDevices`).

### Phase 1 — Token-Fundament (½–1 Tag)
`Theme.cs`: Metrics + Semibold-Familie + größere Skala + Helfer (Glow/Focus/TrackedLabel/AmberSlider/BrandMark/Blend) + Pen/Brush-Cache.

### Phase 2 — Die Karte (1–2 Tage) — `DeviceControl.cs`
Format-Pille immer sichtbar · Status/Backoff links/Untertitel · Error-Pille Ember (`:208`) · Blue→Cyan · Hover +2px auf Raised + Schatten · 2px Amber-Fokusring · Name Semibold größer · Grid (Pad 16/Rad 14) · Timer-Invalidate-Rechtecke fixen (`:146`) · Literale (#A7B4C8/#242C36/#191308) → Tokens · Karten-Copy → RESX.

### Phase 3 — Kopfleiste (1–2 Tage)
form-level HeaderBar (persistent über Tabs) · globales VU raus · Filter (`cmbFilterDevices`) + Scan in den Kopf · Tabs owner-drawn / Body-Border weg · Room-Summary → RESX + „Räume"/„spielt".

### Phase 4 — Einstellungen neu (2–3 Tage)
Neue owner-drawn Controls: `ToggleSwitch`, `SelectField` (dunkle Popup-Liste, kein heller Pfeil), `TextField`, `SettingsSection` (Card r14/Pad16). `TableLayoutPanel`-Raster. Designter Footer. GroupBox + MS Sans Serif + Stock-Controls raus.

### Phase 5 — Feinschliff (1–2 Tage)
PerMonitorV2-DPI (`Program.cs`) · Protokoll als farbcodierte Konsole · `SpeakerOptionsPopup` Region+Schatten · Reduced-Motion · Dedupe (Slider/Blend/BrandMark aus Controls extrahieren).

## Copy-Pass (DE, du-Ansprache, warm/präzise)
| Ort | Vorher → Nachher |
|---|---|
| Audioquelle | Klangquelle → **Audioquelle** (+ „Keine Audioquelle gefunden") |
| Diagnose | Geräte-Protokoll aufzeichnen → **Verbindungsprotokoll führen** |
| Empfänger | Anzeigename auf dem Empfänger → **Name auf Empfängern** (+ Hilfszeile) |
| Puffer | Klangpuffer (Sekunden) → **Puffer** (Einheit ins Feld) |
| Scan | Räume neu suchen → **Erneut suchen** (im Kopf) |
| Credit | Mit ♥ gemacht von … — 2026 → **entworfen & entwickelt von Neo & Trinity · 2026** |
| Kopf-Titel | „5 Geräte · 1 spielen" (hardcoded) → **„5 Räume · 1 spielt"** (RESX) |
| Karte | „Cast-Gerät" → **Raum · Modell** |
„Klangqualität" bleibt (echtes HiFi-Deutsch).

## Umsetzungsstand (2026-07-05)
Alle Phasen **0–5 umgesetzt & committet** auf Branch `redesign/premium-ui` (7 Commits; master unberührt).
Jede Phase: Build 0 Fehler + 140 Tests grün; Full-Solution-Build grün.

**Bewusste Abweichungen / offene visuelle Iteration:**
- **Pille zustandsabhängig** (Format beim Spielen, sonst bereit/Gruppe/erneut verbinden) — folgt dem verbindlichen
  `UI Konzept.png` (nicht der „immer Format"-Vereinfachung des Konzept-Boards).
- **Phase 3 Header:** globales VU raus + Vokabular gefixt; der tiefe Umbau (eine form-level Zeile + owner-drawn
  Tab-Deck, Filter/Suche in den Kopf) ist **für die visuelle Iteration zurückgestellt** — blind zu riskant.
- **Phase 4 Einstellungen:** Toggles + Dark-Selects via Subclassing (alle Bindungen erhalten). Der vollständige
  Umbau in Section-Cards/`TableLayoutPanel` bleibt für den visuellen Pass.
- **Karten-Copy** (Status/Untertitel) noch als DE-Literale — RESX-Lokalisierung (EN/FR) ist Folgeschritt.
- **Protokoll** noch Roh-Konsole — farbcodierte Konsole = Folgeschritt.

**Braucht the maintainer's Auge (Sandbox hat kein Display):** weißer Rand weg?, Titelleiste als nahtloser Block?,
Karten-Zustände/Glow/Fokus?, Toggles + Combo-Chevron in den Einstellungen?, DPI-Schärfe.

## Visueller Pass (2026-09-04) — Tab-Deck, Fensterrand, Einstellungen

Der zurückgestellte Teil aus Phase 3/4 ist jetzt umgesetzt, gegen den echten laufenden Build verifiziert
(Screenshots der Räume- und der Einstellungen-Seite, normal + maximiert).

**1. Tab-Deck & weißer Rand.** Das stock `TabControl` ist raus. Es zeichnete einen nativen Body-Frame um
seine Seiten, den Owner-Drawing prinzipiell nicht erreicht — genau die helle Haarlinie, die das ganze Fenster
umrahmte. Ersatz: `UserControls/ConsoleTabStrip` (owner-drawn Band, aktiver Tab = angehobene Ink-Fläche mit
2-px-Bernstein-Oberkante) über drei gewöhnlichen `Panel`-Seiten, die der Strip zeigt/versteckt. Dazu
`Form.Padding = 0` — der Streifen sitzt bündig auf der Fensterkante, wie im Ziel-Mockup.

**2. Kopfzeile.** Der zweite Wortmarken-Header entfällt (die Marke lebt in der Titelleiste: Icon + Caption).
Die Zusammenfassungszeile *ist* jetzt die Kopfzeile: Text hart links, Master-Fader + „Räume neu suchen" hart
rechts (`.apptop` des Konzepts). Auch die Caption „DEINE RÄUME" über dem Raster fällt weg — die Summary
benennt bereits, was folgt.

**3. Einstellungen.** Die Seite wird zur Laufzeit in Konsolen-Karten neu aufgebaut
(`MainForm.SetupSettingsPage`); alle vorhandenen Controls werden umgehängt, keine Bindung ändert sich.
Zwei `CardPanel` („Klangprofil & Verbindung" als `TableLayoutPanel`-Raster, „Verhalten & Komfort" als
Toggle-Liste mit Haarlinien), darunter ein ruhiger Footer. Die Spalte ist auf ~920 px gedeckelt, damit
Label und Schalter Paare bleiben.

**4. Was WinForms nicht hergibt — und wie es gelöst ist.**
- Eine `ComboBox` zeichnet ihren 1-px-Rahmen aus dem eigenen `WM_PAINT`; Über-Zeichnen gewann nur an den
  Ecken. Lösung: `DarkComboBox.ClipToWell()` gibt ihr eine gerundete `Region` 2 px innen — der native Rand
  erreicht den Bildschirm nie. Der Rahmen kommt vom `FieldFrame` darunter, der Chevron aus dem Overdraw.
- Owner-Draw einer Combo malt **beide** Flächen: geschlossenes Feld *und* Listenzeilen. Beide gleich zu
  füllen war der Grund, warum jedes Feld als helle Box in der eigenen Mulde saß — jetzt Ink-2 für das Feld,
  Surface/Raised für die Liste (`DrawItemState.ComboBoxEdit`).
- Native Scrollbars: `DwmChrome.UseDarkScrollbars` (`DarkMode_Explorer`) statt weißer Rinne.
- `PillButton` zeigt seinen Fokusring nur bei Tastaturnavigation (`ShowFocusCues`), sonst sah der
  Erst-Fokus wie eine Primär-Aktion aus.

## Zweiter visueller Pass (2026-09-04) — Marken-Backdrop, Glas, Kopfzeile

**5. Das TV-Bild ist jetzt der App-Hintergrund.** `Resources/artwork.png` — dasselbe Bild, das der Chromecast
als `/artwork.png` holt und vollflächig auf den Fernseher legt — liegt hinter der ganzen App
(`Theme.PaintBackdrop`). Es ist an das **Fenster** verankert, nicht an das jeweilige Panel: jede Fläche malt
ihre eigene Scheibe desselben Bildes, also laufen die Ringe nahtlos über Werkzeugleiste, Raster und Karten
hinweg. Skalierung ist rein dynamisch (Cover × Zoom), daher passt sich der Hintergrund jeder Fenstergröße an;
`RepaintBackdrop()` zeichnet beim Resize alle Flächen neu, weil Standard-Container das von sich aus nicht tun.

Der Bildausschnitt ist bewusst gewählt: Das Artwork ist fürs TV komponiert (Ringe um die Mitte, Wortmarke
darunter). Ungeschnitten säße diese Wortmarke quer im Fenster und läse sich als zweites, konkurrierendes Logo.
Deshalb wird das Bild vergrößert und so gerahmt, dass das Fenster **kurz vor** dem leuchtenden Zentrum endet —
sichtbar bleibt das ruhige Ringband, unter einem von oben nach unten dichter werdenden Ink-Schleier.

**6. Glassmorphism.** Geräte-Kacheln und Einstellungs-Karten sind Glasscheiben: Sie zeigen den Backdrop
weichgezeichnet durch sich hindurch (GDI+ kann kein Gaussian-Blur — die Scheibe wird verkleinert gerendert und
bikubisch wieder hochskaliert, was genau den Milchglas-Effekt ergibt), darüber ein durchscheinender Farbton mit
vertikalem Verlauf, eine helle Lichtkante an der Oberkante und ein weicher Schlagschatten. Hinter Glas ist der
Schleier bewusst dünner (`veilScale`) — eine Scheibe sammelt Licht. Die spielende Kachel wird etwas dichter
(sie muss das Pegel-Meter tragen), die überfahrene etwas klarer. Der Downsample-Puffer wird wiederverwendet
statt pro Frame allokiert.

**7. Kopfzeile & Kachel-Untertitel.** Die Zusammenfassung folgt dem Konzept-Board: „Geräte im Heimnetz · N
spielen" über „N Räume · M Gruppen · Format · verlustfrei ans ganze Haus" — vollständig lokalisiert (DE/EN/FR)
statt hart deutsch. Die Kachel nennt unter dem Namen die Hardware: `DiscoveredDevice.ModelName` liest zuerst
`eureka_info` (`device_info.model_name`, mit Hersteller-Präfix wenn er fehlt), sonst den mDNS-`md=`-Eintrag.
Damit dieser überhaupt ankommt, reicht `Devices.SetDeviceInformation` das TXT-Record jetzt über die
eureka-Grenze weiter (vorher ging es dort verloren — auch im Fallback für Geräte ohne `eureka_info`, also
genau für Fernseher und Soundbars). Wiederholt das Modell nur den Gerätenamen, tritt der Untertitel zurück.

**8. Aus dem Review übernommen:** akkumulierendes Padding in `ClampSettingsWidth` (die Einstellungen-Spalte
schrumpfte beim Ziehen der Fensterkante gegen 0), toter Idempotenz-Guard in `SetupRoomSummary`, eine
überzählige Trennlinie über der ersten Toggle-Zeile, überschriebene Textfarben der Summary-Labels,
Combo-Höhe im Feld-Well, Ctrl+Tab-Navigation (kam vorher vom `TabControl`), Karten-Captions bei Sprachwechsel,
Font-Leak in `PillButton`.

## Release 1.0 (2026-09-04) — 24 Sprachen, Installer, Raum & Modell

**Version & Signatur.** `1.0.0`, Fußzeile „Version 1.0", Credit „Neo & Trinity · 2026". Die Anzeige kürzt
`Major.Minor`, solange die Patch-Stelle 0 ist — das angehängte „.0" ist auf einer About-Zeile nur Rauschen.

**Standard ab Werk: WAV 16-bit, 10 s Puffer.** CD-Qualität, unkomprimiert, und das Format, das *jeder*
Cast-Empfänger anstandslos nimmt — der sichere Boden, nicht die Decke: 24-bit und FLAC sitzen einen Klick
weiter. Die 10 Sekunden Empfänger-Polster überstehen WLAN-Jitter, ohne dass es bei Musik jemand merkt.

**Raum und Modell auf der Kachel.** Recherche am lebenden Netz (eureka_info + DIAL auf allen vier Geräten):
Ein Chromecast kennt seinen **Raum nicht** — `eureka_info` liefert Name, Build, Netz; das mDNS-TXT liefert das
Modell (`md=`); der Raum liegt allein in Googles Home Graph in der Cloud, hinter einem Konto. Statt ihn zu
erfinden, ist er jetzt **pro Gerät benennbar** (⋮ auf der Kachel → „Raum"), gespeichert in `speakers.json`
neben der Max-Lautstärke. Die Kachel zeigt dann „Wohnzimmer · Q995GD"; fehlt eines von beidem, trägt das
andere die Zeile allein. Wiederholt das Modell nur den Gerätenamen, tritt es zurück. Damit das Modell
überhaupt ankommt, reicht `Devices.SetDeviceInformation` das TXT-Record jetzt über die eureka-Grenze — vorher
ging es dort verloren, also genau bei Fernsehern und Soundbars, die kein `eureka_info` beantworten.

**Kachel-Padding.** Jede Zeile misst jetzt gegen die **Karte**, nicht gegen das Control: die Karte ist für
ihren Schwebeschatten eingerückt, weshalb der Play-Knopf vorher hart auf der unteren Linie saß. Die Kachel ist
20 px höher, die Steuerzeile sitzt eine volle Karten-Einrückung über der Unterkante.

**24 EU-Amtssprachen.** Je eine `Strings.<code>.resx` (109 Keys, per Satellite-Assembly), die Sprachwahl
listet alle unter ihrem **Endonym** („Deutsch", „Ελληνικά") — eine Liste, die sich nicht mit der UI-Sprache
ändert, damit man aus einer versehentlich gewählten Sprache wieder herausfindet. Eine Systemsprache außerhalb
der 24 landet auf Englisch statt auf einem fehlenden Satelliten.

**Das Fernsehbild spricht mit.** Das Now-Playing-Artwork war ein festes PNG mit englischem Claim — ein
griechischer Nutzer bekam eine griechische App und einen englischen Fernseher. Es wird jetzt zur Laufzeit
gezeichnet (`Classes/ArtworkRenderer`), aus denselben Tokens wie die App, mit der Tagline aus den Ressourcen,
je Sprache einmal gerendert und gecacht; ein Sprachwechsel verwirft den Cache. Das eingebettete PNG bleibt
Fallback.

**Installer** (`installer/KlangHub.iss`, Inno Setup, Ausgabe `dist/KlangHub-1.0-Setup.exe`, ~6 MB).
- Erkennt die Windows-Sprache und spricht sie (21 der 24 EU-Sprachen haben eine Inno-Übersetzung; für
  Irisch und Maltesisch läuft das Setup englisch, die App selbst trotzdem in ihrer Sprache).
- **Die Sprachfrage steht auf der ersten Wizard-Seite**, nicht im nackten System-Dialog davor — sie bestimmt,
  womit KlangHub gestartet wird (`--lang=`), und ist im Markenlook.
- Trägt die App-Optik: ink-dunkler Wizard, elfenbeinfarbene Schrift, ein Bernstein-Akzent, das vollständige
  Logo (gezeichnet, nicht beschnitten), dunkle Titelleiste über dieselben DWM-Attribute wie das App-Fenster,
  dunkle Scrollbars — auch im Deinstallationsfenster.
- **Keine Lizenz-Zustimmungsseite.** Inno zeigt die Lizenz in einem RichEdit, das die Textfarbe pro Zeichen
  führt — weder `Font.Color` noch eine RTF-Farbtabelle überlebten dort das Laden, der Text blieb fast schwarz
  auf Tinte. MIT verlangt ohnehin keine Zustimmung, sondern dass die Lizenz *mitgeliefert* wird: `LICENSE.txt`
  und `THIRD-PARTY-LICENSES.md` werden jetzt neben der App installiert, wo man sie auch lesen kann.
- **Prüft die .NET Desktop Runtime** und lädt sie bei Bedarf automatisch von Microsoft (`aka.ms`), bevor
  installiert wird; Microsofts Installer holt sich die Rechte selbst, weshalb das Setup ohne Adminrechte
  auskommt (Installation pro Benutzer, kein UAC, kein grauer Modus-Dialog).

## Räume als Struktur (2026-09-04) — Vorauswahl, Gruppierung, Raum-Fader

**Raum-Vorauswahl.** Das ⋮-Popup einer Kachel bietet jetzt die zwanzig Räume, aus denen ein Zuhause meist
besteht — Wohnzimmer, Küche, Bad, Loft, Wintergarten, Partyraum … — als aufklappbare Liste, und man kann
trotzdem frei tippen (`ComboBoxStyle.DropDown`). Die Identitäten sind fest und englisch (`RoomPresets.Ids`),
übersetzt wird nur die **Beschriftung**, als *eine* Ressourcenzeile pro Sprache, deren Einträge über die
Position zu den Identitäten gehören. Das hält zwanzig Räume × vierundzwanzig Sprachen bei einer Zeile je
Sprache statt 480 Schlüsseln — der Preis ist, dass eine Übersetzung mit einem Eintrag zu wenig alles um eins
verschieben und die Küche „Bad" nennen würde. Genau das prüft ein Test für jede Sprache.

**Ein Symbol je Raum.** Vierzehn gezeichnete Glyphen (Sofa, Topf, Bett, Dusche, Schreibtisch, Teller, Tür,
Drachen, Sonnenschirm, Baum, Haus, Loft-Dach, Bügel, Rolltor, Discokugel); alles andere — auch ein selbst
getippter Raum — bekommt seine Initiale im Ring. Immer passend, nie ein falsches Bild. Ein Raum, der auf
Deutsch benannt wurde, behält sein Symbol nach dem Sprachwechsel: `IdFor` sucht in allen 24 Sprachen.

**Gruppierte Ansicht.** Der Umschalter „Nach Räumen" in der Kopfzeile ordnet das Raster in Blöcke: eine
Raumleiste über voller Breite, darunter die Kacheln dieses Raums. Technisch bleibt es dasselbe
`FlowLayoutPanel` — die Leiste wird eingefügt und mit `SetFlowBreak` umbrochen, sodass das Layout weiter
mitfließt und an den Kacheln nichts geändert werden musste. Räume alphabetisch, „Noch kein Raum" zuletzt:
das ist eine To-do-Liste, kein Raum. Die Wahl wird mit den übrigen Einstellungen gespeichert.

**Der Raum-Fader ist relativ.** Ein Raum will selten *einen* Pegel — der Küchenlautsprecher steht auf 40 %,
der am Fenster auf 15 %. Ziehen verschiebt deshalb **jeden** Lautsprecher um dieselbe Anzahl Punkte und
erhält damit die Balance, statt den Raum auf einen Wert einzuebnen. Die harte Obergrenze jeder Kachel gilt
weiter: Ein Raum auf 100 % sprengt die Wohnung nicht, der auf 23 % gedeckelte Lautsprecher bleibt bei 23 %.
Dazu Stummschaltung für den ganzen Raum und −/+ für zwei Punkte Feinschliff (Pfeiltasten und `M` ebenso).
