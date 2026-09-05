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
   MAC-Adressen, Screenshots — und seit dem Audit auch **Autor und Committer jedes Commits**.
   Das war die Lücke, durch die es passiert ist: die Dateien waren die ganze Zeit sauber.
   Es prüft bewusst keine konkreten Namen — eine Liste verbotener Namen in einer öffentlichen Datei
   würde genau das veröffentlichen, was sie schützen soll.
3. **`.githooks/pre-commit`** ruft `tools/check-no-private-data.ps1` vor jedem Commit. Dieses Skript
   liest zusätzlich eine **Wortliste mit den echten Namen** aus
   `%LOCALAPPDATA%\KlangHub\private-words.txt`. Diese Datei liegt **außerhalb des Repos** und darf
   niemals hineinwandern. **Fehlt sie, verweigert das Skript** — vorher meldete es „No private data
   found", obwohl es keinen einzigen Namen kannte.

   Einmal pro Klon einrichten: `git config core.hooksPath .githooks`

   Das Skript prüft mit `-Staged` den **Index**, nicht die Platte: eine Datei zu stagen und danach
   zu bereinigen brachte die private Fassung vorher an einem grünen Hook vorbei.

### Was zu tun ist, wenn etwas gefunden wird

1. **Nicht committen.** Erst bereinigen.
2. Prüfen, ob es auch in der **Historie** steht: `git log -S"<begriff>" --all`
3. Falls ja und **noch nichts gepusht** wurde: mit `git filter-repo` tilgen (siehe unten).
   Falls bereits gepusht: dem Maintainer sagen — dann ist es öffentlich und eine Entscheidung,
   die ihm gehört, nicht dir.
4. Den Begriff in die private Wortliste aufnehmen, damit er nicht wiederkommt.

### Vor einem Push immer

```powershell
tools\check-no-private-data.ps1           # Arbeitsbaum
tools\check-no-private-data.ps1 -History  # jeder Blob und jeder Commit-Autor. Dauert.
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Source/KlangHub.Tests/KlangHub.Tests.csproj
```

**`-History` ist der wichtige Lauf.** Alle anderen Schichten sehen nur den *aktuellen* Stand. Genau
deshalb hat eine echte MAC-Adresse in drei Commits und einer Commit-Nachricht überlebt, während
jeder Test grün war.

**Gepusht wird nur auf ausdrückliche Ansage des Maintainers.** Bei einem öffentlichen Repo ist
einmal veröffentlicht endgültig.

---

## 2. Versionierung

Beginnt bei **0.0.1**. Drei Stellen müssen zusammenpassen:

- `Source/Directory.Build.props` → `<Version>` — gilt für **alle** Projekte. Vorher stand die Version
  nur in `KlangHub.csproj`, und `KlangHub.Core.dll` und `KlangHub.Platform.dll` gingen als `1.0.0.0`
  in den Installer.
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

**Der Build ist warnungsfrei — Produktion und Tests.** Die frühere Meldung „Multilingual App Toolkit
is unavailable" gibt es nicht mehr; das Toolkit war ein Erbstück mit einem einzigen übersetzbaren
Text und wurde entfernt. Eine neue Warnung ist deshalb immer eine echte.

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
