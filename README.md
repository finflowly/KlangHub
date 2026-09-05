# KlangHub

**Die All-in-One Multiroom-Audio-Lösung für Windows.**
Der Ton deines PCs auf allen Lautsprechern im Haus – aus einer Anwendung.

KlangHub nimmt den Ton deines Windows-Desktops (oder eines Mikrofon-/Line-In-Eingangs)
auf und streamt ihn ins lokale Netzwerk an deine Wiedergabegeräte – für synchronen
Multiroom-Sound aus einer einzigen Anwendung.

## Status

KlangHub befindet sich in aktiver Weiterentwicklung. Heute streamt es an
Google-Cast-fähige Geräte. Die Architektur ist auf weitere Protokolle vorbereitet;
AirPlay- und Snapcast-Geräte werden bereits im Netz gefunden, aber noch nicht
bespielt. Was die Anwendung kann, steht unter „Funktionen" – dort steht nichts,
was sie nicht kann.

## Funktionen

- Desktop- oder Mikrofon-Audio erfassen und ins lokale Netzwerk streamen
- Chromecast- / Google-Cast-fähige Geräte automatisch finden (mDNS)
- Mehrere Geräte gleichzeitig sowie Gerätegruppen
- Wählbare Streaming-Formate (WAV 16/24/32 Bit, FLAC verlustfrei, MP3 128/320)
- Lautstärke- und Wiedergabesteuerung je Raum, inkl. individueller Lautstärke-Obergrenze
- Systray-Betrieb, Autostart, optionale Tastenkürzel, Deutsch/Englisch

## Systemvoraussetzungen

- Windows 10/11
- .NET 10 Desktop Runtime
- Beim ersten Start muss die Windows-Firewall für dein Heimnetzwerk (privat/öffentlich)
  freigegeben werden, damit die Wiedergabe funktioniert.

> Hinweis: Zwischen Desktop-Bild und Audio-Wiedergabe besteht systembedingt immer eine
> Latenz (Puffer). KlangHub ist nicht für lippensynchrone Video-Vertonung gedacht.

## Build (Entwickler)

Voraussetzung: **.NET 10 SDK**.

```
dotnet build Source/KlangHub.sln -c Release
```

### Abhängigkeiten

Siehe [docs/THIRD-PARTY-LICENSES.md](docs/THIRD-PARTY-LICENSES.md) für alle mitgelieferten
Komponenten und deren Lizenzen.

## Lizenz

KlangHub steht unter der MIT-Lizenz, siehe [LICENSE](LICENSE) für den vollständigen Text
und die Namensnennung. Die Codebasis ist ein Fork eines Chromecast-Audio-Streamers von
**SamDel** (MIT); die mitgelieferten Fremdkomponenten und ihre Lizenzen stehen in
[docs/THIRD-PARTY-LICENSES.md](docs/THIRD-PARTY-LICENSES.md).

## Marken und Unabhängigkeit

KlangHub ist ein unabhängiges Open-Source-Projekt und steht in **keiner Verbindung zu
Google, Apple, Spotify, Samsung oder Harman** und wird von diesen weder unterstützt noch
zertifiziert.

Produkt- und Protokollnamen werden ausschließlich beschreibend verwendet, um zu sagen,
mit welchen Geräten die Anwendung arbeitet:

- **Chromecast**, **Google Cast** und **Google Home** sind Marken von Google LLC.
- **AirPlay** ist eine Marke von Apple Inc. KlangHub **findet** AirPlay-Geräte im Netzwerk
  (offene mDNS-Ankündigungen), sendet aber **keinen** AirPlay-Audiostrom. Dafür wären
  Apples Pairing-Verfahren und eine MFi-Lizenzierung nötig – beides ist hier weder
  implementiert noch umgangen.
- **Spotify** und **Spotify Connect** sind Marken von Spotify AB. KlangHub unterstützt
  Spotify Connect **nicht**.
- **Snapcast** ist ein Projekt von Johannes Pohl (GPL-3.0). KlangHub findet Snapcast-Geräte
  im Netz; eine Steuerung ist nicht implementiert. Es wird kein Snapcast-Code verwendet
  oder gelinkt.
