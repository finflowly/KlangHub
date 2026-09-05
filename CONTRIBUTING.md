# Mitarbeiten an KlangHub

Danke für dein Interesse. Vier Regeln, und sie sind kurz, weil sie ernst gemeint sind.

## 1. Dieses Repository ist öffentlich – nichts Privates hinein

Die wichtigste Regel, deshalb steht sie zuerst. Alles, was committet wird, ist dauerhaft für jeden
lesbar; nach einem Push hilft auch kein Umschreiben der Historie mehr.

Niemals in eine getrackte Datei gehören:

| | Stattdessen |
|---|---|
| Klarnamen von Personen | „Neo & Trinity" |
| E-Mail-Adressen | `noreply@…`, `…@example.invalid` |
| Benutzerverzeichnisse (`C:\Users\<name>`) | `%USERPROFILE%` |
| IP- und MAC-Adressen echter Geräte | erfundene Beispielwerte |
| Cast-App-IDs, Konto-Kennungen, Schlüssel | erfundene Beispielwerte |
| Selbst vergebene Raum- und Gruppennamen | neutral umschreiben („die Multiroom-Gruppe") |
| Screenshots, Logdateien, PDFs | gar nicht – sie zeigen Adressen, Gerätelisten, Fensterinhalte |

**Produktnamen sind erlaubt** – sie sind technische Angaben, keine privaten. Ebenso Beobachtungen aus
echten Messungen, nur ohne den Namen dessen, der gemessen hat: „an echter Hardware gemessen" statt
„X hat berichtet". Gerade diese Begründungen sind der wertvollste Teil des Codes; beim Anonymisieren
geht nur der Name verloren, nie das Warum.

Findest du etwas Privates: nicht committen, erst bereinigen. Steht es schon in der Historie
(`git log -S"<begriff>" --all`) und ist noch nichts gepusht, lässt es sich tilgen – ist es bereits
gepusht, sag dem Maintainer Bescheid, das ist seine Entscheidung.

## 2. Einmal pro Klon einrichten

```powershell
git config core.hooksPath .githooks
```

Damit läuft `tools/check-no-private-data.ps1` vor jedem Commit. Das Skript zieht zusätzlich eine
lokale Wortliste heran, die außerhalb des Repositories liegt und niemals hineinwandern darf.

**Vor jedem Push:**

```powershell
tools\check-no-private-data.ps1
dotnet test Source/KlangHub.Tests/KlangHub.Tests.csproj
```

Gepusht wird nur auf ausdrückliche Ansage des Maintainers.

## 3. Test zuerst

Jede Verhaltensänderung beginnt mit einem Test, der aus dem richtigen Grund rot ist – erst dann der
Code, der ihn grün macht. `Source/KlangHub.Tests/RepositoryPrivacyTests.cs` läuft bei jedem Testlauf
über alles, was git trackt, und prüft die Form privater Daten mit.

Kommentare erklären das **Warum**, nicht das Was: insbesondere, welcher reale Fehler zu einer Zeile
geführt hat. Was schon einmal Stunden gekostet hat, gehört nach `docs/BEKANNTE-FALLEN.md`.

## 4. Neue Texte in alle 24 Sprachen

Die Oberfläche liegt vollständig in allen 24 EU-Amtssprachen vor
(`Source/KlangHub.Core/Properties/Strings*.resx`, je 171 Texte). Ein neuer Text gehört in **jede**
dieser Dateien – eine halb übersetzte Oberfläche ist ein Rückschritt. Tests prüfen, dass keine Sprache
zurückfällt.

## Und beim Testen

Keine Klicks auslösen, die reale Lautsprecher lauter stellen können. Ein Fehlklick auf einen
Master-Fader ist niemandes Spaß.
