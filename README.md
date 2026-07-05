# KlangHub

**Die All-in-One Multiroom-Audio-Lösung für Windows.**
Chromecast, Spotify Connect und mehr – zentral auf deinem PC.

KlangHub nimmt den Ton deines Windows-Desktops (oder eines Mikrofon-/Line-In-Eingangs)
auf und streamt ihn ins lokale Netzwerk an deine Wiedergabegeräte – für synchronen
Multiroom-Sound aus einer einzigen Anwendung.

## Status

KlangHub befindet sich in aktiver Weiterentwicklung: eigenständige, modulare
Premium-Anwendung mit sauberer Schichtentrennung, Erweiterbarkeit für weitere
Streaming-Protokolle (u. a. Spotify Connect) und einer modernen, markeneigenen
"Hi-Fi-Konsolen"-Oberfläche.

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
und die Namensnennung.
