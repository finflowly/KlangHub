# KlangHub — Cast-Receiver

Was der Fernseher zeigt, wenn KlangHub auf ihn streamt. Zwei Ausbaustufen, dieselbe Registrierung:

Beide Varianten liegen hier fertig — registriert wird **eine** davon:

| | Custom Receiver (`index.html`) | Styled Media Receiver (`klanghub.css`) |
|---|---|---|
| Registrierter Typ | Custom Receiver, **Receiver Application URL** | Styled Media Receiver, **Skin URL** |
| Was man gestalten kann | die komplette Seite | Hintergrund, Start-Logo, Leerlauf-Bild, Fortschrittsbalken, Wasserzeichen |
| Eigener App-Name statt „Default Media Receiver" | ja | ja |
| Eigenes Text-Layout | ja | nein |
| Live-Pegelmeter (geplant) | ja | nein |
| Multiroom-Synchronität (geplant) | ja — das ist der eigentliche Grund | nein |

Empfehlung: **Custom Receiver**. Der Styled-Weg bleibt als einfache Alternative bestehen.

Der Plan für die zweite Stufe steht in [`docs/PLAN-MULTIROOM-SYNC.md`](../docs/PLAN-MULTIROOM-SYNC.md).

## Inhalt

```
receiver/
  index.html            ← Custom Receiver: DIESE URL wird registriert
  klanghub.css          ← Alternative: Skin-Datei für den Styled Media Receiver
  assets/
    background.png      1920×1080  hinter der laufenden Wiedergabe
    splash.png          1920×1080  Leerlauf-Bildschirm (mit Wortmarke)
    logo.png             900×520   während der Receiver startet (transparent)
    watermark.png        260×260   dezent während der Wiedergabe (transparent)
```

Die Bilder werden aus denselben Farbtokens gezeichnet wie die App (`Classes/Theme.cs`); das Skript dazu
liegt in `tools/make-receiver-assets.ps1`.

## Einrichten

1. **Veröffentlichen.** Erst `git push`, dann in den Repository-Einstellungen unter *Pages* als Quelle den
   Standard-Branch (hier `master`) und den Ordner `/ (root)` wählen. Danach liegt die Seite unter
   `https://<konto>.github.io/<repo>/receiver/` — die Groß- und Kleinschreibung des Repository-Namens zählt.
   Kurz im Browser aufrufen: Es muss die dunkle Seite mit den Ringen erscheinen, nicht eine 404-Seite.
2. **Registrieren.** Auf https://cast.google.com/publish anmelden, Entwicklerkonto anlegen (einmalig 5 USD),
   *Add New Application* → **Custom Receiver**. Im Formular:
   - **Name:** `KlangHub` — das steht später auf dem Fernseher.
   - **Receiver Application URL:** `https://<konto>.github.io/<repo>/receiver/` (zeigt auf `index.html`).
   - **Guest Mode:** aus. Erlaubt Casting von Geräten außerhalb des WLANs; KlangHub nutzt das nicht.
   - **Google Cast for Audio:** **an**. Ohne dieses Häkchen lädt der Receiver **nicht** auf reinen
     Audio-Geräten — also weder auf einem Google Home noch auf einer Soundbar.
   - **Package Name:** leer (es gibt keine Android-TV-App).

   Google vergibt daraufhin eine **App-ID** aus acht Zeichen.
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
