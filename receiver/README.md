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

1. **Veröffentlichen — erledigt.** Pages steht auf `master`, Ordner `/ (root)`. Die kanonische Adresse
   für dieses Repository lautet:

   ```
   https://finflowly.github.io/KlangHub/receiver/
   ```

   **Stand 2026-09-07 antwortet sie mit 200** (`Last-Modified` 2026-09-06, `max-age=600`). Die Groß-
   und Kleinschreibung des Repository-Namens zählt. Ein `git push` auf `master` **ist** die
   Veröffentlichung der neuen Bühne — nichts neu registrieren, nur den Cache abwarten. Umziehen darf
   die Adresse nicht: sie steht in der Cast-Konsole.
2. **Registrieren.** Auf https://cast.google.com/publish anmelden, Entwicklerkonto anlegen (einmalig 5 USD),
   *Add New Application* → **Custom Receiver**. Im Formular:
   - **Name:** `KlangHub` — das steht später auf dem Fernseher.
   - **Receiver Application URL:** `https://finflowly.github.io/KlangHub/receiver/` (zeigt auf `index.html`).
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

- Die Seite wird **aus dem Internet** geladen — und zwar vom **Cast-Gerät**, nicht von KlangHub. Es holt
  `index.html` von der registrierten HTTPS-Adresse, dazu Googles CAF-SDK von `www.gstatic.com` und die
  Schriften Fraunces und Inter Tight von `fonts.googleapis.com` / `fonts.gstatic.com`. Keine dieser
  Dateien liegt in diesem Repository; sie werden zur Laufzeit geladen und nicht mitgeliefert. Der
  Audiostream bleibt dabei lokal im WLAN, aber der Start braucht eine Verbindung.
- Ohne eingetragene App-ID entfällt das alles: dann läuft die Wiedergabe über Googles
  Standard-Empfänger und ohne diese Seite. KlangHub selbst fragt beim Start allerdings in jedem Fall
  GitHub nach einer neueren Version — siehe [SECURITY.md](../SECURITY.md). „Vollständig offline" wäre
  also zu viel gesagt.
- Die App-ID hängt am Google-Konto dessen, der registriert. Wer das Projekt forkt, trägt seine eigene ein —
  deshalb steht sie in den Einstellungen und nicht im Code.

## Bühnen-Nachrichten tragen ein Session-Token

Cast authentifiziert Sender nicht. Wer im selben Netz ist, kann sich in eine laufende Session hängen und
Nachrichten in `urn:x-cast:de.klanghub.stage` schicken — und der Namespace steht in einem öffentlichen
Repository. Möglich war damit: beliebiger Text und ein beliebiges Bild in Vollbild auf dem Fernseher,
dazu ein HTTP-Aufruf des Cast-Geräts an einen fremden Server. Keine Codeausführung — aller Text läuft
über `textContent`, nie über `innerHTML` —, aber Defacement im Wohnzimmer.

**So ist es zu:** Beim `LOAD` legt der Sender einen frischen Zufallswert in `media.customData.stageToken`.
Diese Nachricht geht über den Media-Namespace an genau das Gerät, auf das gecastet wird; sonst sieht sie
niemand. Jede spätere Bühnen-Nachricht führt denselben Wert im Feld `token` mit, und der Receiver verwirft
alles andere kommentarlos — auch `state`- und `position`-Nachrichten, denn eine gefälschte „Pause" ist eine
Fehlermeldung, die keine ist.

| | |
|---|---|
| Erzeugt | `StageToken.Create()` — 128 Bit aus dem kryptographischen Generator, URL-sicher |
| Lebensdauer | **ein `LOAD`**. Jede neue Wiedergabe bekommt einen neuen Wert |
| Sender-Seite | `ChromeCastMessages.RequireToken` — eine Bühnen-Nachricht **ohne** Token wird nicht gebaut, sondern wirft |
| Empfänger-Seite | `fromUs()` in `index.html`, aufgerufen als Erstes im Message-Listener |
| Gelernt wird er | im `LOAD`-Interceptor, nicht im `LOAD_START`-Event — eine Nachricht kann im selben Atemzug eintreffen |

**Ohne gesehenes `LOAD` gilt die Sperre nicht.** Das war bis 2026-09-07 umgekehrt — `sessionToken`
null hieß: **jede** Nachricht fällt durch —, und es war die falsche Richtung. Eine Bühne, die ein
`LOAD` verpasst hat (Neuladen der Seite, ein Gerät, das den Interceptor nicht durchreicht, eine
Session, die nicht von uns gestartet wurde), verwarf danach *alles* und blieb für immer stumm: keine
Titelwechsel, kein Cover, nichts. Ein stiller Fernseher ist kein Sicherheitsgewinn, sondern ein
kaputtes Produkt.

Die Regel lautet jetzt: **Token gelernt → streng. Token nie gelernt → durchlassen.** Sobald ein `LOAD`
gesehen wurde — und das ist der Normalfall, KlangHub schickt bei jeder Wiedergabe eines mit —, wird
jede Nachricht ohne den passenden Wert kommentarlos verworfen, wie zuvor. Das Fenster, das offen
bleibt, ist genau die Zeitspanne, in der ohnehin nichts von uns läuft.

Weiterhin gilt, was der Audit gebracht hat: Cover-URLs nur `http`/`https`, Metadatenfelder auf 512 Zeichen
begrenzt, Text ausschließlich über `textContent`.

Bewusst **nicht** dasselbe Geheimnis wie `SessionSecret` (das in den Artwork-URLs steht): Das gilt für den
ganzen Programmlauf und steht in Adressen. Ein gemeinsamer Wert hätte bedeutet, dass das Auslesen an einer
Stelle die andere mit aufschließt.

### Was die Tests davon prüfen — und was nicht

`StageMessageTests` hält die Sender-Seite fest: dass alle drei Nachrichtenarten das Token tragen, dass ohne
Token nichts gebaut wird, dass das `LOAD` es übergibt, und dass zwei Sessions nicht denselben Wert bekommen.
`ReceiverStageTests` liest `index.html` als Text und hält die Form der Sperre fest — Interceptor vorhanden,
`fromUs` als erste Anweisung im Listener, kein `innerHTML`, Cover-URL auf `http`/`https` beschränkt.

Das beweist, dass die Sperre nicht gelöscht oder umgangen wurde. Es beweist **nicht**, dass sie auf einem
Gerät funktioniert. Dafür gibt es keine Abkürzung; die Checkliste unten ist der Weg.

## Das Cover eines Stücks gehört diesem Stück

**Der Fehler, um den es geht:** Clementine spielt eine M4A mit eingebettetem Cover, der Fernseher zeigt
es — richtig. Danach ein Webradio ohne Bild: der Ton wechselt, **das Cover der M4A bleibt stehen**.
Minutenlang, über fremde Titel hinweg. Falsch, und die Art von Falsch, die man dem Gerät glaubt.

Drei Ursachen, alle drei behoben, alle drei einzeln ausreichend gewesen:

1. **Der Sender vergaß das Bild nie.** `CurrentCover` wurde nur gesetzt, wenn die Kaskade einen
   **Dateipfad** hatte. Ein Stream hat keinen (`ClementineSong.LocalPath` gibt bei allem, was nicht
   `file://` ist, `null` zurück — richtig so). Also lief der Cover-Sucher nicht, und was zuletzt darin
   stand, blieb darin stehen. `NowPlayingService` **vergisst** das Bild jetzt bei jedem neuen Stück,
   bevor es ein neues sucht, und nimmt zusätzlich das Bild an, das eine Quelle selbst mitbringt —
   Clementines `Art` kam bis dahin bis in `NowPlayingTrack.CoverBytes` und wurde dort nie gelesen.
2. **Die Adresse blieb dieselbe, also war für den Fernseher nichts passiert.** Die Bühne bekam immer
   eine Cover-URL: `artwork.png` von diesem Rechner, die ohne echtes Cover das KlangHub-Bild
   ausliefert. Gleiche URL wie eben → `setCover` kehrt sofort um → altes Bild. Die Bühne bekommt jetzt
   **gar keine** Adresse, wenn es kein Bild gibt (`StageCover.Url`), und zeigt dann die Ringe. Der
   **Der Cast-`LOAD` folgt derselben Regel** — und das war beim ersten Anlauf falsch. Der Gedanke
   „`metadata.images` behält `artwork.png`, damit ein reines Audiogerät ein gebrandetes Bild hat" ist
   genau die dritte Falle noch einmal: `LOAD_START` liest `images[0]`, und der Fernseher hätte beim
   Verbinden erst das KlangHub-Bild als Album gezeigt — mit Schleier und Farbpalette —, bis
   Sekundenbruchteile später die erste Bühnen-Nachricht die Ringe nachreicht. Ein Zwischenbild, das
   wie eine Platte aussieht und keine ist. `GetStreamMediaInfo` nimmt jetzt `StageCoverUrl()`; ohne
   Bild trägt der LOAD **kein** `images`, und `GetLoadMessage` lässt das Feld dann ganz weg.
   Branding gehört auf die Ringe und die Wortmarke, nicht in ein Feld, das „Cover dieses Stücks"
   heißt.
3. **Der Receiver räumte bei einem neuen Stück alles auf außer dem Bild.** `newTrack` leerte Titel,
   Artist, Album, Qualität und Dauer, aber `if (update.cover !== undefined)` hieß: ein **fehlendes**
   Feld ändert nichts — und der Sender lässt leere Felder weg. Jetzt gilt:

   | | |
   |---|---|
   | `newTrack: true` | `setCover(update.cover \|\| '')` — kein Feld heißt **weg damit** |
   | Merge (`newTrack` fehlt/falsch) | fehlendes Feld heißt **behalten** — ein ICY-Titelwechsel darf das Bild *dieses* Stücks nicht löschen |

   Dazu: ein `LOAD` ohne `images` erbt nicht mehr die Bilder des `LOAD` davor (`loadedMedia` wird
   immer überschrieben), und eine Dauer, die nicht mitkommt, nimmt den Fortschrittsbalken mit. Ein
   Balken, der bei einem Livestream eine Restzeit behauptet, ist erfundene Information.

**Der Fallback ist die Ringgrafik.** Nicht das letzte Album, nicht ein Notenschlüssel, nicht das
KlangHub-Logo. `?demo=radio` zeigt deshalb ebenfalls die Ringe — vorher malte die Demo sich ein
Kunst-Cover und konnte den Fehler gar nicht zeigen, den sie zeigen sollte.

### Webradio: ein ICY-Titel wird auseinandergenommen

Ein Sender schickt oft nur eine Zeile: `Massive Attack - Teardrop`. `IcyTitle.Split` trennt sie —
**nur wenn es eindeutig ist**: genau ein ` - `, beide Seiten nicht leer, und nur, solange die Quelle
selbst keinen Artist genannt hat.

Getrennt wird am **Separator ` - ` (Leerzeichen, Bindestrich, Leerzeichen)**, nicht an Bindestrichen.
Der Unterschied ist der ganze Punkt: `Jean-Michel Jarre - Oxygene` hat zwei Bindestriche und **einen**
Separator, wird also richtig getrennt; `AC/DC - T.N.T.` und `Simon - Ballad Of A Well-Known Gun`
ebenso. Wer stattdessen Bindestriche zählt, verliert den halben Radioabend und macht aus Jean-Michel
einen Klumpen im Titel. `A - B - C` bleibt ganz, weil es dort **zwei** Separatoren gibt und niemand
weiß, welcher gemeint ist. Lieber eine ungetrennte Zeile als ein falsch halbierter Songname.

Getrennt wird im **Sender**, nicht auf der Bühne — die Online-Suche unten braucht dieselbe Trennung,
und zwei Implementierungen derselben Regel gehen irgendwann auseinander.

### Ein Cover aus dem Netz, für Radio, das keines mitschickt

Ohne das bleibt Webradio dauerhaft bei den Ringen. Also fragt KlangHub — **auf dem Windows-Rechner,
nicht auf dem Fernseher**. Der Stick würde pro Haushalt einen Katalog hämmern, CORS nähme uns die
Farbpalette, und Cache und Drossel gehören dorthin, wo es einen Prozess gibt, der lange lebt.

**Gefragt wird MusicBrainz, geholt wird vom Cover Art Archive.** Warum nicht der bessere Katalog:
iTunes hat die höchste Trefferquote und erlaubt sein Artwork nur zur Förderung des Stores, mit Badge;
Deezers Bedingungen sind für persönliche Anwendungen geschrieben und verbieten die Verknüpfung mit
einer fremden Marke; Last.fm und Spotify brauchen Schlüssel, und ein Schlüssel gehört nicht in ein
öffentliches Repository. Die Entscheidung steht in
[`docs/THIRD-PARTY-LICENSES.md`](../docs/THIRD-PARTY-LICENSES.md) und wird nicht umgedreht.

Gefragt wird nur, wenn **alles** davon gilt:

- Artist **und** Titel stehen fest (nach dem ICY-Split).
- Es gibt kein Bild aus Tags, Ordner, Clementines `Art` oder SMTC.
- Es ist kein Dateipfad im Spiel — eine Datei ist lokal zu klären.
- Es sieht nach einem Stück aus: kein `://`, kein `www.`, nicht bloß ein Sendername.

Und dann höchstens einmal: der Schlüssel ist `artist|title`, normalisiert (Kleinschreibung, Akzente
weg, Klammerzusätze und `feat.`-Anhänge weg, Whitespace zusammengezogen). Jede Antwort wird gemerkt —
**Fehlschläge eingeschlossen**, sonst fragt eine Senderrotation dieselbe Frage jede Stunde neu. Gemerkt
wird in `%LocalAppData%\KlangHub\radio-covers.json`: klein, nur Adressen, und beim Deinstallieren weg.

Ein Treffer muss zum Stück passen — Artist *und* Titel normalisiert gleich, sonst nichts. Von den
Releases gewinnt ein offizielles Album vor einer Single vor allem anderen. **Im Zweifel kein Cover.**
Lieber die Ringe als das falsche Album auf 65 Zoll.

Höchstens eine Anfrage gleichzeitig, gut eine Sekunde Abstand, identifizierender User-Agent, und bei
`503`/`429` fünf Minuten Ruhe. Das ist es, worum MusicBrainz bittet, und es ist der Grund, warum kein
Schlüssel nötig ist.

Die gefundene Adresse geht als **Merge** an die Bühne, nicht als neuer Schnitt: das Cover blendet sich
weich zum laufenden Titel ein. Ist das Stück inzwischen weiter, wird die Antwort verworfen — gemerkt
wird sie trotzdem. Die Palette wird per `crossOrigin = anonymous` gelesen; schickt das Archiv keine
CORS-Kopfzeile, bleibt die Standardfarbe und das Bild wird trotzdem gezeigt.

## Abnahme auf echter Hardware — erste Sitzung am 2026-09-06

Gelaufen auf einem Google TV Streamer, 16:55–17:11 Uhr, sechzehn Minuten ohne Abbruch. Was das
Protokoll belegt:

- [x] `https://finflowly.github.io/KlangHub/receiver/` liefert **200** (29.339 Bytes, CAF-SDK, unser
      Namespace, CSP — roh nachgeprüft, nicht nur der Titel).
- [x] App-ID eingetragen, beide Fernseher als Testgeräte „Ready For Testing", Anwendung **veröffentlicht**.
- [x] Der Fernseher lädt **KlangHub**: `appType:"WEB"`, `displayName:"KlangHub"`, und in den Namespaces
      steht `urn:x-cast:de.klanghub.stage`. Nicht der Default Media Receiver.
- [x] Ton: das Gerät holt den Stream (`Connection added from <tv>:57736`), `MEDIA_STATUS BUFFERING` →
      `PLAYING`, Position läuft bis `t=989.3s` durch.
- [x] **Titelwechsel ohne Tonunterbrechung** — der eigentliche Beweis für Phase 1. Vier Wechsel gingen
      über den Namespace, es gab **genau einen** LOAD, und die Wiedergabeposition lief dabei
      ununterbrochen weiter. Kein Nachladen, keine Stille.
- [x] Das Session-Token wandert: `customData.stageToken` im LOAD, derselbe Wert in jeder
      `state`- und `track`-Nachricht.
- [x] Clementine als Leitfall: Artist, Titel und Cover kamen aus der Kaskade, nicht aus Cast-Tags.

Noch offen, weil nur ein Augenpaar es bestätigen kann:

- [ ] Wie die Bühne aussieht: Cover groß, Titel dominant, unscharfer Fond in der Farbe der Platte.
- [ ] Pause: dunkelt ab, Uhr erscheint, Session bleibt.
- [ ] Ein Stück, von dem nur der Dateiname bekannt ist, und das weiche Nachziehen der Tags.
- [ ] TCL und Enchant (bisher nur der Streamer).

### Was die erste Sitzung gezeigt hat — und was daraus wurde

Vier Befunde, alle vier nachgearbeitet. Die Ursachen stehen hier, weil sie beim nächsten Mal Zeit
sparen; die Begründungen im Detail stehen an den Zeilen, die es betrifft.

- **Der Fernseher aktualisierte nicht von selbst.** Die `track`-Nachrichten gingen nachweislich raus
  (17:16:39, 17:16:50); Titel und Cover erschienen erst auf einen Tastendruck der Fernbedienung.
  Die Worte waren nie falsch — sie standen auf `opacity: 0`. Ein Titelwechsel setzt die Klasse
  `changing`, sobald die Nachricht ankommt, und **nur** ein `setTimeout` schrieb danach die neuen
  Worte und nahm die Klasse wieder weg. Alles andere auf dieser Bühne schreibt synchron, und genau
  deshalb traf es ausschließlich Titelwechsel — und deshalb war die Bühne leer statt veraltet.

  Der Schnitt hängt jetzt nicht mehr an einer Uhr: er lässt die beiden Uhren gegeneinander laufen,
  die ein Browser hat — den Animationsrahmen, der dem Zeichnen folgt, und den Timer, der das nicht
  tut —, und die schnellere beendet ihn. `settle()` ist idempotent, die langsamere findet nichts mehr
  vor. Zusätzlich landet **jede** eingehende Nachricht einen überfälligen Schnitt, bevor sie gelesen
  wird: eine Bühne darf einen Atemzug hinter der Musik sein, nicht ein Stück.

  Belegt wurde das, indem dasselbe Skript gegen einen nachgebauten Fernseher lief, mit jeweils einer
  angehaltenen Uhr. Mit der alten Fassung bleibt der Titel leer und `changing` stehen — genau das
  gemeldete Bild. **Welche** Uhr der echte Fernseher angehalten hat, kann nur er selbst sagen: dafür
  meldet die Bühne es jetzt zurück, siehe unten.
- **Jeder Titelwechsel ging zweimal raus**, im selben Millisekundenschritt: `newTrack:false`, dann
  `newTrack:true`. `NowPlayingService` hatte zwei Ereignisse für eine Tatsache und `ApplicationLogic`
  hing an beiden. Jetzt ein Ereignis, `NowPlayingUpdate`, das das Merkmal mitträgt: ein Schnitt ist
  eine Eigenschaft der Änderung, keine zweite Änderung.
- **„0.0 KB in 1.0 s over 0 sends", eine Zeile pro Sekunde**, während `MEDIA_STATUS` PLAYING meldete
  und `currentTime` im Takt der Uhr weiterlief. Die Musik war in Ordnung; das Protokoll beschrieb die
  **vorherige** Verbindung. `StreamingConnection.Dispose` schloss den Socket, aber der Sendethread
  hängt an einer `WeakReference` und endete erst beim Aufräumen durch den GC — bis dahin meldete eine
  tote Verbindung sekündlich, dass sie nichts trägt. Die neue, gesunde Verbindung schwieg, weil ein
  gesunder Strom nichts sagt. Jetzt endet der Thread beim Schließen.
- **`SET_VOLUME` alle fünfzehn Sekunden**, über 150 Nachrichten. Ein Receiver meldet zwei Zahlen, die
  auf dem Draht gleich aussehen: `RECEIVER_STATUS` trägt die **Geräte**-Lautstärke mit `controlType`
  und Schrittweite, `MEDIA_STATUS` die **Medien**-Lautstärke — 1.00 von der LOAD bis zum Sitzungsende,
  ohne Regelart, ohne Schritt. Die zweite wurde wie die erste gelesen: die Karte sah 100 %, fand das
  über der eingestellten Obergrenze und drückte zurück. Der Sicherungsschalter dafür konnte nicht
  auslösen, weil die `RECEIVER_STATUS` dazwischen die echten 23 % meldete und seinen Zähler jedes Mal
  zurücksetzte. Und leiser, aber schlimmer: die Medien-Lautstärke trägt `controlType: null` und löschte
  damit sekündlich die Tatsache, dass dieser Fernseher **fest** eingestellt ist — die eine Tatsache,
  die den ganzen Streit beendet hätte. `MEDIA_STATUS` liefert die Lautstärke jetzt nicht mehr ab.
- **Googles `statusText` bleibt auf dem ersten Stück stehen** („Casting: …"), weil wir bewusst nicht
  neu laden. Sichtbar nur in der Home-App, nicht auf unserer Bühne.

### 2026-09-06, zweite Sitzung: die Bühne blieb leer

Nach dem ersten Push zeigte der Fernseher nur noch die Ringe, gross „KlangHub" und die Marke —
kein Titel, kein Cover. Der erste `stage-report` hat es in einer Zeile beantwortet:

    "tracks":0, "viewport":625, "screen":540, "timerLatest":45, "cutLatest":1,
    "zoneBottom":508, "zoneBelowEdge":-117, "zoneNudge":0, "visibility":"visible"

**`tracks: 0`** — die Bühne hatte keine einzige Titel-Nachricht gelesen. Der Sender schickt eine nur
bei einer *Änderung*; nach einer frischen LOAD weiss die Bühne also nichts, und die einzige Quelle
ist die LOAD selbst. Die trug Titel, Artist, Album und Cover — gelesen wurden sie aber aus
`event.media` des `LOAD_START`-Ereignisses, und das ist auf diesem Gerät leer. `fromMedia` bekam
`undefined`, lieferte lauter leere Felder, und der Titel fiel auf den Markennamen zurück.

Die Medien werden jetzt aus drei Quellen genommen, in dieser Reihenfolge: das Ereignis, dann
`playerManager.getMediaInformation()`, dann die LOAD, die der Interceptor ohnehin gesehen hat. Der
Interceptor ist die verlässlichste davon — er bekommt die Anfrage im Original.

**`timerLatest: 45`, `cutLatest: 1`** — nebenbei widerlegt: die Timer dieses Fernsehers werden *nicht*
gedrosselt, und der Schnitt landet pünktlich. Die Vermutung aus der ersten Sitzung war falsch; der
Fehler lag allein darin, dass der Schnitt an einer einzigen Uhr hing.

**`zoneNudge: 0`, `zoneBelowEdge: -117`, und danach kein weiterer Bericht** — innerhalb der Seite hat
sich nichts bewegt. Das Absacken der unteren Zeile passiert also unterhalb der Seite. Auffällig
bleibt `viewport: 625` gegen `screen: 540`. Die Bühne meldet jetzt **jede** Bewegung der Zeile, nicht
erst das Erreichen des Randes, damit der nächste Lauf das entscheidet.

### Was auf *jedem* Fernseher laufen muss

Die Bühne lief auf einem Google TV Streamer und sah dort richtig aus. Das ist keine Zusage für ein
Gerät von 2015. Drei Dinge, die deshalb bewusst so stehen:

- **Kein modernes CSS ohne Rückfallwert davor.** `color-mix()` braucht Chrome 111, `inset` Chrome 87,
  `clamp()` Chrome 79 — und ein Browser, der eine Funktion nicht kennt, verwirft die **ganze**
  Deklaration. Ohne Rückfall hiesse das: kein Schatten am Cover, keine sichtbare Fortschrittslinie,
  gar kein Schleier und damit leuchtende Ecken. Jede dieser Zeilen steht jetzt zweimal, einfach
  zuerst. `ReceiverCutTests` hält das fest.
- **Das Skript bleibt ES5.** `var`, keine Pfeilfunktionen, keine Template-Literale.
- **Messen darf nie die Sekunde zerreissen, die misst.** `getBoundingClientRect` steckt in einem
  try/catch, und `window.innerHeight` hat einen Rückfall. Fällt die Messung aus, läuft die Bühne
  weiter und meldet nur nichts — vorher hätte es Fortschrittslinie und Schnitt-Wächter mitgerissen.

### Die Selbstauskunft der Bühne ist wieder ausgebaut

Für die zweite Sitzung meldete die Bühne über denselben Namespace `stage-report` zurück, was sie über
sich selbst wusste — Bildabstände, Timer-Verspätung, Sichtbarkeit, gelesene Titel-Nachrichten. Sie hat
ihre Frage beantwortet (siehe oben: der Fernseher hält die Seite für unsichtbar) und ist danach
**entfernt** worden. `ReceiverCutTests` hält fest, dass keines der fünf Wörter — `stage-report`,
`holdTheBottomRow`, `bottomRowNudge`, `sayToSender`, `gapLongest` — wieder in `index.html` steht.
Diagnose-Maschinerie, die niemand mehr liest, ist eine zweite Bühne, die stillschweigend kaputtgeht.

Wer sie noch einmal braucht, baut sie neu und wirft sie danach wieder weg.

### Der Weg dorthin, damit ihn niemand zweimal gehen muss

`LAUNCH_ERROR / NOT_FOUND` kam auf **drei** Geräten — beiden Fernsehern und einem Cast-nativen
Lautsprecher — obwohl App-ID, Receiver-URL und Testgeräte-Eintrag nachweislich korrekt waren. Zwei
Ursachen, nacheinander: auf dem Streamer war ein **anderes Google-Konto** angemeldet als das der
Cast-Konsole; und danach half nur noch **Veröffentlichen**. Der Testgeräte-Weg allein — Seriennummer
eingetragen, „Ready For Testing", fünfzehn Minuten, stromlos — hat auf diesen Geräten nicht gereicht.
Fürs Veröffentlichen verlangt die Konsole Sender Details für mindestens eine Plattform; für einen
Windows-Sender, den das Formular nicht kennt, genügt der **Chrome**-Eintrag mit der Projekt-URL.
