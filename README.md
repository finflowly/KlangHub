# KlangHub

**Die All-in-One Multiroom-Audio-Lösung für Windows.**
Chromecast, Spotify Connect und mehr – zentral auf deinem PC.

KlangHub nimmt den Ton deines Windows-Desktops (oder eines Mikrofon-/Line-In-Eingangs)
auf und streamt ihn ins lokale Netzwerk an deine Wiedergabegeräte – für synchronen
Multiroom-Sound aus einer einzigen Anwendung.

## Status

KlangHub **6.0.0** befindet sich in aktiver Weiterentwicklung. Das Projekt ging aus einem
Fork von [SamDel/ChromeCast-Desktop-Audio-Streamer](https://github.com/SamDel/ChromeCast-Desktop-Audio-Streamer)
(v5.5.0.0) hervor und wird schrittweise zu einer eigenständigen, modularen Premium-Anwendung
ausgebaut: saubere Schichtentrennung, Erweiterbarkeit für weitere Streaming-Protokolle
(u. a. Spotify Connect) und ein Weg zu einer modernen UI.

## Funktionen

- Desktop- oder Mikrofon-Audio erfassen und ins lokale Netzwerk streamen
- Chromecast- / Google-Cast-fähige Geräte automatisch finden (mDNS)
- Mehrere Geräte gleichzeitig sowie Gerätegruppen
- Wählbare Streaming-Formate (WAV 16/24/32 Bit, MP3 128/320)
- Lautstärke- und Wiedergabesteuerung, Systray-Betrieb, Autostart, optionale Tastenkürzel

## Systemvoraussetzungen

- Windows 10/11
- .NET 8 Desktop Runtime
- Beim ersten Start muss die Windows-Firewall für dein Heimnetzwerk (privat/öffentlich)
  freigegeben werden, damit die Wiedergabe funktioniert.

> Hinweis: Zwischen Desktop-Bild und Audio-Wiedergabe besteht systembedingt immer eine
> Latenz (Puffer). KlangHub ist nicht für lippensynchrone Video-Vertonung gedacht.

## Build (Entwickler)

Voraussetzung: **.NET 8 SDK**.

```
dotnet build Source/ChromeCast.Desktop.AudioStreamer/KlangHub.csproj -c Release
```

> Baue das **Projekt**, nicht die Solution – die `.sln` enthält noch ein veraltetes
> Visual-Studio-Installer-Projekt (`Setup.vdproj`), das die .NET-CLI nicht bauen kann.
> Es wird später durch MSIX oder WiX ersetzt.

### Abhängigkeiten

- [NAudio](https://github.com/naudio/NAudio) & [NAudio.Lame](https://github.com/Corey-M/NAudio.Lame) – Audio-Aufnahme & MP3-Encoding
- [Tmds.MDns](https://github.com/tmds/Tmds.MDns) – Geräteerkennung (mDNS)
- [Protocol Buffers](https://github.com/google/protobuf) – Cast-Channel-Protokoll

## Danksagung

KlangHub basiert auf der hervorragenden Arbeit von [SamDel](https://github.com/SamDel) und dem
Projekt [ChromeCast-Desktop-Audio-Streamer](https://github.com/SamDel/ChromeCast-Desktop-Audio-Streamer)
sowie dessen Mitwirkenden.

## Lizenz

Siehe [LICENSE](LICENSE).
