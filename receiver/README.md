# KlangHub — Cast-Receiver

Was der Fernseher zeigt, wenn KlangHub auf ihn streamt. Zwei Ausbaustufen, dieselbe Registrierung:

| | Styled Media Receiver (hier fertig) | Custom Receiver (später) |
|---|---|---|
| Was man gestalten kann | Hintergrund, Start-Logo, Leerlauf-Bild, Fortschrittsbalken, Wasserzeichen | die komplette Seite |
| Eigener App-Name statt „Default Media Receiver" | ja | ja |
| Live-Pegelmeter, eigenes Text-Layout | nein | ja |
| Multiroom-Synchronität | nein | ja (das ist der eigentliche Grund) |
| Aufwand | diese CSS-Datei | eigene HTML/JS-App + Sync-Protokoll |

Der Plan für die zweite Stufe steht in [`docs/PLAN-MULTIROOM-SYNC.md`](../docs/PLAN-MULTIROOM-SYNC.md).

## Inhalt

```
receiver/
  klanghub.css          ← die Skin-Datei, ihre URL wird bei Google registriert
  assets/
    background.png      1920×1080  hinter der laufenden Wiedergabe
    splash.png          1920×1080  Leerlauf-Bildschirm (mit Wortmarke)
    logo.png             900×520   während der Receiver startet (transparent)
    watermark.png        260×260   dezent während der Wiedergabe (transparent)
```

Die Bilder werden aus denselben Farbtokens gezeichnet wie die App (`Classes/Theme.cs`); das Skript dazu
liegt in `tools/make-receiver-assets.ps1`.

## Einrichten

1. **Veröffentlichen.** In den Repository-Einstellungen unter *Pages* als Quelle den Branch `main` und den
   Ordner `/ (root)` wählen. Danach liegt die Datei unter
   `https://<konto>.github.io/<repo>/receiver/klanghub.css`.
   Kurz im Browser aufrufen — sie muss als Text erscheinen, mit `https://`.
2. **Registrieren.** Auf https://cast.google.com/publish anmelden, Entwicklerkonto anlegen (einmalig 5 USD),
   *Add New Application* → **Styled Media Receiver**, oben die CSS-URL eintragen. Google vergibt eine
   **App-ID** aus acht Zeichen.
3. **Testgerät freischalten.** Seriennummer des Cast-Geräts eintragen (Google-Home-App → Gerät →
   Einstellungen → Geräteinformationen), **fünfzehn Minuten warten**, dann das Gerät vom Strom trennen und
   neu starten. Danach steht dort „Ready for Testing".
4. **In KlangHub eintragen.** Einstellungen → *Cast-Empfänger* → App-ID. Leer lassen heißt: Googles
   Standard-Empfänger (`CC1AD845`) wie bisher.

Solange die Anwendung nicht veröffentlicht ist, lädt sie **nur auf registrierten Testgeräten**. Für andere
Nutzer des Projekts muss sie bei Google veröffentlicht werden (Titel max. 50 Zeichen, Beschreibung max. 80,
Icon 512 × 512); jede spätere Änderung erfordert erneutes Veröffentlichen.

## Zu wissen

- Die Seite wird **aus dem Internet** geladen. Der Audiostream bleibt lokal im WLAN, aber der Start braucht
  eine Verbindung. Ohne eingetragene App-ID läuft KlangHub weiterhin vollständig offline.
- Die App-ID hängt am Google-Konto dessen, der registriert. Wer das Projekt forkt, trägt seine eigene ein —
  deshalb steht sie in den Einstellungen und nicht im Code.
