# Bilder des Repositories

Dieser Ordner ist **absichtlich leer**. Er beschreibt, welche Bilder das öffentliche Gesicht von
KlangHub braucht, in welchen Maßen — und was jedes einzelne durchlaufen muss, bevor es hier liegen
darf.

## Warum hier nichts liegt

Ein Screenshot dieser Anwendung zeigt eine Geräteliste, meist eine IP-Adresse, oft selbst vergebene
Raumnamen und regelmäßig den Titel eines Fensters, das jemand offen hatte. Deshalb ist das
Repository an **drei** Stellen dagegen gesichert, und alle drei greifen:

1. `.gitignore` schließt `*.png`, `*.jpg`, `*.bmp`, `*.gif` und `*.pdf` überall aus — mit einer
   kurzen Ausnahmeliste für die Bilder, die die Anwendung selbst mitbringt.
2. `Source/KlangHub.Tests/RepositoryPrivacyTests.cs` lässt bei **jedem Testlauf** nur Pfade zu, die
   auf eine ausdrückliche Erlaubnisliste passen. Ein Bild in `docs/assets/` macht den Testlauf heute
   **rot** — auch dann, wenn `.gitignore` es durchgelassen hätte.
3. `.githooks/pre-commit` prüft den Index vor jedem Commit.

Ein Bild hier einzustellen ist deshalb kein `git add`, sondern eine bewusste Entscheidung in drei
Schritten (siehe unten). Das ist so gewollt.

## Was gebraucht wird

| Datei | Maße | Wofür | Inhalt |
|---|---|---|---|
| `app-shell.png` | Fensterbreite, unskaliert | Hero im README | Die Raumansicht im dunklen Theme, mit laufender Wiedergabe und Cover. |
| `tv-stage.png` | 1920 × 1080 | README, Abschnitt „TV stage" | Der Receiver auf einem echten Fernseher, abfotografiert oder als HDMI-Mitschnitt. |
| `github-social-preview.png` | 1280 × 640 | GitHub → Settings → Social preview | Fast schwarz. Wortmarke `KlangHub`, darunter eine Zeile: `One music. Every room.` Kein Screenshot, kein Glühen, keine Verlaufsorgie. |

Die Einfügestelle für den Hero steht im README als HTML-Kommentar. Wer das Bild liefert, ersetzt den
Kommentar — nicht umgekehrt.

Das Social-Preview-Bild wird **nicht** committet, sondern in den Repository-Einstellungen
hochgeladen. Es wäre das einzige Bild hier ohne privaten Inhalt; es liegt trotzdem besser dort, wo
GitHub es erwartet, statt eine Ausnahme in der Prüfkette zu rechtfertigen.

## Vor jedem Screenshot

- **Dunkles Theme.** Das ist der Auftritt der Anwendung, nicht eine Variante davon.
- **Ein echter Raum, ein echter Titel mit Cover.** Ein leeres Fenster zeigt nichts, was jemanden
  überzeugt.
- **Keine Entwicklerwerkzeuge im Bild**, keine Debug-Overlays, keine Fensterdekoration einer IDE.
- **Kein Klick auf einen Lautstärkeregler.** Beim Aufnehmen wird nichts bedient, was reale
  Lautsprecher lauter stellen kann — ein Fehlklick auf einen Master-Fader ist niemandes Spaß.

## Vor dem Committen — Zeile für Zeile prüfen

- [ ] **Keine IP-Adresse** sichtbar. Auch nicht in der Statuszeile, auch nicht im Protokoll-Reiter.
- [ ] **Keine selbst vergebenen Raum- oder Gruppennamen.** Produktnamen (Harman Kardon Enchant,
      TCL C735) sind erlaubt und technische Angaben; „Schlafzimmer oben" ist es nicht.
- [ ] **Kein Klarname**, nirgends — nicht im Fenstertitel, nicht in einem Pfad, nicht im
      Benutzerordner.
- [ ] **Kein Dateipfad mit `C:\Users\<name>`.**
- [ ] **Nichts aus einem fremden Fenster** am Bildrand.
- [ ] Metadaten des Bildes entfernt (PNG-Chunks, EXIF).

Erst wenn alle sechs Punkte stimmen, sind die drei Schritte fällig:

1. Die Ausnahme in `.gitignore` ergänzen (`!docs/assets/<datei>.png`).
2. Den Pfad in die Erlaubnisliste in `RepositoryPrivacyTests.cs` aufnehmen — mit einem Kommentar,
   der sagt, **wer das Bild geprüft hat und wogegen**.
3. `& "$env:USERPROFILE\.dotnet\dotnet.exe" test Source/KlangHub.Tests/KlangHub.Tests.csproj`
   laufen lassen. Grün heißt: die Prüfkette kennt das Bild jetzt. Sie hat es nicht *angesehen* —
   das bleibt Schritt 0 und ist von Hand zu tun.

Ist ein Bild einmal gepusht, ist es öffentlich. Zurücknehmen geht dann nicht mehr, nur noch
entscheiden, damit zu leben.
