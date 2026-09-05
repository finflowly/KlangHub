# KlangHub

**One music. Every room.**

*[English version](README.md) — die englische Fassung ist die maßgebliche.*

Der native Multiroom-Hub für Windows. Er spielt heute in jeden Raum des Hauses, und seine Architektur
ist so gebaut, dass weitere Protokolle als Provider dazukommen und nicht als Umbau.

`Version 0.0.1` · Windows 10/11 · .NET 10 · Chromecast

Das ist ein Fundament, kein 1.0. Was drinsteht, läuft an echter Hardware; was nicht geht, steht
weiter unten ausdrücklich da, statt zwischen den Zeilen zu fehlen.

## Warum es das gibt

- Windows hat keinen ernsthaften nativen Multiroom-Hub. Den Desktop zu casten ist entweder ein
  Browser-Tab oder ein Werkzeug, das vergisst, was gerade läuft.
- Wer vom Desktop castet, soll Metadaten, Räume und Qualität behalten — und nicht eine verlustfreie
  Sammlung auf einen namenlosen Strom ohne Titel auf dem Bildschirm reduzieren.
- Protokolle müssen austauschbar sein, ohne die Anwendung neu zu schreiben.

## Was 0.0.1 tatsächlich kann

- **Aufnehmen**: Desktop-Ton oder ein Mikrofon-/Line-In-Eingang (WASAPI-Loopback).
- **Finden**: Chromecast- und Google-Cast-Geräte sowie Gerätegruppen über mDNS.
- **Mehrere Räume gleichzeitig** bespielen, Google-Cast-Gruppen eingeschlossen.
- **Format wählen**: WAV 16/24/32 Bit, FLAC (verlustfrei), MP3 128/320. Eine frische Installation
  streamt FLAC mit zehn Sekunden Puffer.
- **Je Raum steuern** — Wiedergabe, Pause, Stopp, Lautstärke, Stumm — über `IPlaybackSession`, mit
  individueller Lautstärke-Obergrenze und einem Raum-Fader, der die Balance zwischen den
  Lautsprechern erhält.
- **Räume benennen** und die Kacheln nach Räumen gruppieren, samt Raum-Voreinstellungen.
- **Zeigen, was läuft.** Eine Kaskade aus fünf Quellen (Windows-Now-Playing-Sitzung, Clementines
  Network Remote, Datei-Tags, Fenstertitel des Players, Dateiname) einigt sich auf eine Antwort, und
  die reist mit dem Strom mit.
- **Im Infobereich laufen**, mit Windows starten, auf optionale Tastenkürzel hören.
- **24 Sprachen** — alle EU-Amtssprachen, jede vollständig übersetzt, in der Sprachwahl unter ihrem
  eigenen Namen gelistet.

Der Mehr-Provider-Seam steht bereits: AirPlay- und Snapcast-Geräte werden **gefunden und
eingeordnet** und sind bewusst nicht bespielbar — siehe Roadmap.

## Architektur

```
KlangHub (WinForms-Shell, Orchestrator)
   │
   ├─► ICastProvider ── CompositeCastProvider
   │                       ├─ ChromecastProvider   volle Session
   │                       ├─ SnapcastProvider     nur Discovery
   │                       └─ AirPlayProvider      nur Discovery
   │
   ├─► IAudioCaptureEngine → IAudioSink[] → IAudioEncoder → lokaler HTTP-Server
   │
   └─◄ IPlaybackSession    Play · Pause · Stopp · Lautstärke · Stumm · Status

KlangHub.Core       Verträge und Regeln — kein Windows, keine Oberfläche, kein Protokoll
KlangHub.Platform   Chromecast, Audioaufnahme und -kodierung, Windows-Dienste
```

Core referenziert nichts, Platform referenziert Core, die Anwendung beides — und es gibt keinen Weg
zurück. Ein zweites Protokoll kostet eine Klasse und eine Zeile im Kompositionswurzelpunkt.

Das ganze Bild, samt dem, was 0.0.1 bewusst *nicht* abstrahiert:
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) (englisch).

## TV-Bühne

In `receiver/` liegt ein eigener CAF-v3-Web-Receiver: eine ruhige Now-Playing-Anzeige mit Titel,
Cover und Format anstelle von Googles Standard-Empfänger. Er ist eine eigenständige, über HTTPS
ausgelieferte Seite und nicht Teil der Anwendung — der Ton bleibt beim nativen CAF-Player und läuft
nie durch ihn hindurch.

Er liegt vollständig im Repository und ist **nie auf echter Hardware gelaufen**: dafür braucht es
eine gehostete URL und eine registrierte Cast-App-ID. Einrichtung und Stand stehen in
[`receiver/README.md`](receiver/README.md).

## Systemvoraussetzungen

- Windows 10 oder 11 (64 Bit)
- Ein Chromecast-/Google-Cast-Gerät im selben Netz
- Freigabe der Windows-Firewall für das Heimnetzwerk beim ersten Start — ohne sie beginnt keine
  Wiedergabe

Der ausgelieferte Build ist self-contained; die .NET-Laufzeit bringt der Installer mit. Nur wer
selbst baut, braucht das SDK.

> Zwischen Desktop-Bild und Cast-Ton liegt immer eine Verzögerung — die Puffer machen sie
> unvermeidlich. KlangHub ist ein Musik-Hub und kein Weg, den Ton eines Films auf die Lautsprecher
> zu bringen.

## Installation

`KlangHub-0.0.1-Setup.exe` (Inno Setup) installiert pro Benutzer und verlangt keine
Administratorrechte.

**Windows wird warnen — das ist zu erwarten.** Das Setup ist **nicht signiert**; ein
Codesignatur-Zertifikat kostet Geld, das dieses Projekt nicht ausgibt. SmartScreen meldet deshalb
„Der Computer wurde durch Windows geschützt". Der Weg dahinter: **„Weitere Informationen" →
„Trotzdem ausführen"**. Wer das nicht will, baut KlangHub aus der Quelle.

## Selbst bauen und testen

Voraussetzung ist das **.NET 10 SDK**. Alle Projekte zielen auf `net10.0` bzw.
`net10.0-windows10.0.19041.0`; das Windows-SDK-Ziel braucht es für die Now-Playing-Schnittstelle von
Windows.

```
dotnet build Source/KlangHub.sln -c Release
dotnet test Source/KlangHub.Tests/KlangHub.Tests.csproj
```

Das Setup-Programm entsteht aus einem self-contained Publish plus Inno Setup:

```
dotnet publish Source/KlangHub/KlangHub.csproj -c Release -r win-x64 --self-contained true -o publish/KlangHub-0.0.1-win-x64
ISCC.exe installer/KlangHub.iss
```

Erstes Casten, Netzwerkvoraussetzungen und die Fehler, die zuerst auftreten:
[`docs/GETTING-STARTED.md`](docs/GETTING-STARTED.md) (englisch).

## Wo was liegt

| Pfad | Inhalt |
|---|---|
| `Source/KlangHub.Core` | Verträge und Regeln. Kein Windows, keine Oberfläche, kein Protokoll. |
| `Source/KlangHub.Platform` | Chromecast, Audioaufnahme und -kodierung, Windows-Dienste. |
| `Source/KlangHub` | Die WinForms-Shell, die Orchestrierung und der Kompositionswurzelpunkt. |
| `Source/KlangHub.Tests` | 734 Tests über alle drei, samt Datenschutzprüfung über alles, was git trackt. |
| `receiver/` | Der eigene CAF-v3-Web-Receiver für den Fernseher. |
| `installer/` | Das Inno-Setup-Skript und seine Assistentenbilder. |
| `docs/` | Architektur, Einstieg, Casting — und die teuer bezahlten Notizen. |
| `tools/` | PowerShell-Helfer, darunter die Datenschutzprüfung vor jedem Commit. |

## Roadmap

- **Jetzt** — Chromecast härten und den neutralen Gerätedeskriptor bis in die Oberfläche tragen.
- **Als Nächstes** — AirPlay 2. Discovery und Einordnung stehen; eine Session braucht
  HomeKit-Pairing und einen ALAC/RTP-Sender.
- **Danach** — Snapcast. Discovery steht; eine Session braucht einen snapserver-Feed und
  JSON-RPC-Steuerung.
- **Nicht geplant** — Spotify Connect und Tidal Connect.

## Stand

Früh. Schnittstellen können sich noch bewegen, und 0.0.1 ist der erste Schnitt einer Form, nicht ihre
endgültige. Issues zu Architektur und Fehlern sind willkommen; Wünsche nach Protokollen, die nicht auf
der Roadmap stehen, nicht.

Nichts hier gibt sich als fertig aus, was es nicht ist. Der Receiver hat noch keinen Fernseher
gesehen, der Orchestrator hat das App-Projekt noch nicht verlassen, und hinter einer Schnittstelle
für mehrere Audiosenken steht genau eine.

## Mitmachen

Wie hier gearbeitet wird — Test zuerst, neue Texte in allen 24 Sprachen, und die wichtigste Regel
eines öffentlichen Repositories — steht in [CONTRIBUTING.md](CONTRIBUTING.md). Ein Überblick über die
Dokumentation: [docs/README.md](docs/README.md).

Sicherheitslücken bitte nicht als öffentliches Issue melden, sondern wie in [SECURITY.md](SECURITY.md)
beschrieben.

## Lizenz

KlangHub steht unter der MIT-Lizenz, siehe [LICENSE](LICENSE) für den vollständigen Text und die
Namensnennung. Die Codebasis ist ein Fork eines Chromecast-Audio-Streamers von **SamDel** (MIT); die
mitgelieferten Fremdkomponenten und ihre Lizenzen stehen in
[docs/THIRD-PARTY-LICENSES.md](docs/THIRD-PARTY-LICENSES.md).

## Marken und Unabhängigkeit

KlangHub ist ein unabhängiges Open-Source-Projekt und steht in **keiner Verbindung zu Google, Apple,
Spotify, Samsung oder Harman** und wird von diesen weder unterstützt noch zertifiziert.

Produkt- und Protokollnamen werden ausschließlich beschreibend verwendet, um zu sagen, mit welchen
Geräten die Anwendung arbeitet:

- **Chromecast**, **Google Cast** und **Google Home** sind Marken von Google LLC.
- **AirPlay** ist eine Marke von Apple Inc. KlangHub **findet** AirPlay-Geräte im Netzwerk (offene
  mDNS-Ankündigungen), sendet aber **keinen** AirPlay-Audiostrom. Dafür wären Apples
  Pairing-Verfahren und eine MFi-Lizenzierung nötig — beides ist hier weder implementiert noch
  umgangen.
- **Spotify** und **Spotify Connect** sind Marken von Spotify AB. KlangHub unterstützt Spotify
  Connect **nicht**.
- **Snapcast** ist ein Projekt von Johannes Pohl (GPL-3.0). KlangHub findet Snapcast-Geräte im Netz;
  eine Steuerung ist nicht implementiert. Es wird kein Snapcast-Code verwendet oder gelinkt.
