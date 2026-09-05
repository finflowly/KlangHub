# KlangHub — Arbeitsregeln

## 1. Dieses Repository ist öffentlich. Nichts Privates darf hinein.

**Das ist die wichtigste Regel dieses Projekts, und sie steht bewusst an erster Stelle.**

Alles, was committet wird, ist für jeden lesbar — dauerhaft. Ein Umschreiben der Historie hilft nur,
solange nichts gepusht wurde. Danach ist es draußen.

### Was niemals in eine getrackte Datei gehört

| | Stattdessen |
|---|---|
| Klarnamen von Personen | **„Neo & Trinity"** — der einzige Name, der auftauchen darf |
| E-Mail-Adressen | `noreply@…`, `…@example.invalid` (RFC 2606) |
| Benutzerverzeichnisse (`C:\Users\<name>`) | `%USERPROFILE%` bzw. `$env:USERPROFILE` |
| MAC-Adressen echter Geräte | erfundene, siehe Liste in `RepositoryPrivacyTests` |
| Cast-App-IDs, Konto-Kennungen, Schlüssel | erfundene Beispielwerte; echte gehören in die Einstellungen |
| Selbst vergebene Raum- und Gruppennamen | neutral umschreiben („die Multiroom-Gruppe") |
| Screenshots, Logdateien, PDFs | gar nicht — sie zeigen IP-Adressen, Gerätelisten, Fensterinhalte |

**Produktnamen sind erlaubt** (Harman Kardon Enchant, TCL C735, Samsung HW-Q995GD). Das sind
technische Angaben, keine privaten. Ebenso Beobachtungen aus echten Messungen — nur ohne den
Namen dessen, der gemessen hat: „reported from real hardware" statt „X hat berichtet".

### Wie es abgesichert ist — drei Schichten

1. **`.gitignore`** schließt `/*.png`, `/*.jpg`, `/*.pdf`, `/*.log` und `*.log`/`logs/` aus.
   *Aber:* `.gitignore` wirkt **nicht** auf bereits getrackte Dateien. Acht Screenshots waren
   deshalb jahrelang im Repo, ohne dass es jemandem auffiel.
2. **`Source/KlangHub.Tests/RepositoryPrivacyTests.cs`** läuft bei **jedem Testlauf** über alles,
   was git trackt, und prüft die *Form* privater Daten: Mailadressen, Benutzerverzeichnisse,
   MAC-Adressen, Screenshots. Es prüft bewusst keine konkreten Namen — eine Liste verbotener Namen
   in einer öffentlichen Datei würde genau das veröffentlichen, was sie schützen soll.
3. **`.githooks/pre-commit`** ruft `tools/check-no-private-data.ps1` vor jedem Commit. Dieses Skript
   liest zusätzlich eine **Wortliste mit den echten Namen** aus
   `%LOCALAPPDATA%\KlangHub\private-words.txt`. Diese Datei liegt **außerhalb des Repos** und darf
   niemals hineinwandern.

   Einmal pro Klon einrichten: `git config core.hooksPath .githooks`

### Was zu tun ist, wenn etwas gefunden wird

1. **Nicht committen.** Erst bereinigen.
2. Prüfen, ob es auch in der **Historie** steht: `git log -S"<begriff>" --all`
3. Falls ja und **noch nichts gepusht** wurde: mit `git filter-repo` tilgen (siehe unten).
   Falls bereits gepusht: dem Maintainer sagen — dann ist es öffentlich und eine Entscheidung,
   die ihm gehört, nicht dir.
4. Den Begriff in die private Wortliste aufnehmen, damit er nicht wiederkommt.

### Vor einem Push immer

```powershell
tools\check-no-private-data.ps1          # ganzes Repo
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Source/KlangHub.Tests/KlangHub.Tests.csproj
```

**Gepusht wird nur auf ausdrückliche Ansage des Maintainers.** Bei einem öffentlichen Repo ist
einmal veröffentlicht endgültig.

---

## 2. Versionierung

Beginnt bei **0.0.1**. Drei Stellen müssen zusammenpassen:

- `Source/KlangHub/KlangHub.csproj` → `<Version>`
- `Source/KlangHub/Properties/AssemblyInfo.cs` → `AssemblyVersion`, `AssemblyFileVersion`
- `installer/KlangHub.iss` → `AppVersion`, `VersionInfoVersion`, `SourceDir`

Der Publish-Ordner heißt entsprechend `publish\KlangHub-<version>-win-x64`.

---

## 3. Bauen und testen

**Falle:** `dotnet` im PATH ist eine reine Runtime **ohne SDK**. Das SDK liegt benutzerlokal:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Source/KlangHub.Tests/KlangHub.Tests.csproj
& "$env:USERPROFILE\.dotnet\dotnet.exe" build Source/KlangHub/KlangHub.csproj -c Release
```

Weitere Fallen:
- Das Testprotokoll ist **UTF-16** — `grep` findet darin nichts, mit `Get-Content` lesen.
- `--filter` wird von der Testing-Platform ignoriert (MTP0001).
- Die Warnung „Multilingual App Toolkit is unavailable" beim Build ist vorbestehend und harmlos.

---

## 4. Arbeitsweise

- **Test zuerst.** Jede Verhaltensänderung beginnt mit einem Test, der aus dem richtigen Grund rot ist.
- **Neue Texte in alle 24 Sprachen.** Eine halb übersetzte Oberfläche ist ein Rückschritt.
- **Kommentare erklären das Warum**, nicht das Was — insbesondere, welcher reale Fehler zu einer
  Zeile geführt hat. Diese Begründungen sind der wertvollste Teil des Codes und dürfen beim
  Anonymisieren nicht verloren gehen, nur der Name.
- **Keine Lautstärke-Änderungen beim Testen.** Nie Klicks auslösen, die reale Lautsprecher lauter
  stellen können.
- **Keine selbst kompilierten Wegwerf-Programme** in Temp-Verzeichnissen: Virenscanner schlagen
  darauf an (heuristisch, unsignierte .NET-Datei mit Socket). Diagnose läuft über die Testsuite.
