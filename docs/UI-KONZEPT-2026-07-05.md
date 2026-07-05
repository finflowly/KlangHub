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
