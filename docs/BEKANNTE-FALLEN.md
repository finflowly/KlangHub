# Bekannte Fallen

Dinge, die in diesem Projekt schon einmal Stunden gekostet haben. Jede Zeile hier steht, weil der
Fehler nicht dort saß, wo er sich zeigte. Die ausführliche Begründung steht jeweils in der
Commit-Nachricht; hier steht, woran man sie wiedererkennt.

## Audio-Aufnahme (NAudio 3)

**`WasapiRecorder.Dispose()` joint den Capture-Thread ohne Timeout.** `StopRecording()` schreibt nur
ein Flag. Zwischen `StartRecording()` und dem Moment, in dem der Capture-Thread selbst
`captureState = Capturing` schreibt, wird dieses Flag überschrieben — der Stopp geht verloren, die
Schleife endet nie, der Join kommt nie zurück. KlangHub lief genau hinein, weil die Engine beim Bauen
Capture startet und `MainForm_Load` Millisekunden später die Einstellungen anwendet.

*Symptom:* Die App wirkt beim Start abgestürzt — keine Geräteliste, „Suche nach Geräten…" bleibt
stehen, das Fenster reagiert nicht auf das X. Gemessen: drei von sechs Starts.
*Erkennungsmerkmal:* `dotnet-stack report -p <pid>` zeigt den UI-Thread in `Form.OnLoad` →
`LoopbackCaptureEngine.StopRecording` → `WasapiRecorder.Dispose()` → `Thread.Join()`.
*Lösung:* `LoopbackCaptureEngine.StopRecording` hängt die Handler ab, übergibt den Recorder an einen
eigenen Thread und legt den Stopp alle 20 ms nach, bis der Recorder sich als gestoppt meldet — erst
dann der Join. Der Capture-Thread ist `IsBackground`, ein hängender hält den Prozess also nicht am
Leben. (Commit `c3fd380`)

**Regel:** Nie den Capture-Thread vom UI-Thread joinen. Unter NAudio 2 hat `Dispose` nicht gejoint;
dieselben zwei Zeilen waren vor der Migration harmlos.

**`WithLowLatency(true)` heißt REQUIRED, nicht „gerne".** IAudioClient3-Low-Latency verlangt Shared
Mode, Event-Sync, **kein Loopback** und ein Aufnahmeformat identisch zum Mix-Format des Geräts. Ein
Loopback-Caster verletzt zwei dieser Bedingungen prinzipiell. Angefordert = Ausnahme bei jedem Start =
Stille auf allen Lautsprechern. (Commit `d0e2cda`)

## Geräteerkennung (mDNS)

**`ServiceBrowser` muss am Leben gehalten werden.** `MdnsSearch` legte die Browser als lokale
Variablen an und kehrte zurück; danach referenzierte sie nichts mehr. Das funktionierte nur, solange
die Bibliothek sie intern rootete — Tmds.MDns 0.9.1 („robustness improvements to the root timer to
prevent garbage collection") hat genau das geändert.

*Symptom:* „Suche nach Geräten…" für immer, keine Kacheln, nichts im Protokoll.
*Lösung:* Die Browser leben in einem Feld (`DiscoverDevices.browsers`), wie `MdnsDiscovery` es auf der
anderen Seite der App immer schon getan hat. (Commit `b4ba035`)

**Die Platzhalter-MAC `00:00:00:00:00:00` ist keine Identität — und das muss an zwei Stellen gelten.**
Fernseher und manche Lautsprecher melden in `eureka_info` eine MAC aus lauter Nullen. Wer danach
zusammenfasst, klebt fremde Geräte zu einer Kachel zusammen. Nötig war es sowohl in `Devices.GetDevice`
(die Liste fasst nur bei *echten* MACs nach MAC zusammen, sonst nach Endpunkt) als auch in
`ChromecastDeviceId.From` (die Sitzungs-Kennung fällt bei Platzhalter-MAC auf `IP:Port` zurück). Nur eine
der beiden Stellen zu reparieren sieht aus, als wäre es behoben, und ist es nicht.

*Nachtrag:* Der Endpunkt muss **IP und Port** umfassen, nicht nur die IP. Hostet ein Lautsprecher die
Multiroom-Gruppe, dann bedient dieselbe IPv4 sowohl seinen eigenen Empfänger auf `:8009` als auch den
Gruppenleiter auf einem anderen Port — bei Dedup nach IP allein verschwindet der Lautsprecher hinter
seiner eigenen Gruppe (im Protokoll sichtbar als `Discovered device: …` ohne folgendes `Device added:`).

**Ein Gerät, das per DHCP die Adresse wechselt, hinterlässt sonst eine Zombie-Kachel.** Die alte Kachel
bleibt stehen, versucht endlos zu verbinden und blockiert dabei in Zeitüberschreitungen, während daneben
eine zweite Kachel für dieselbe Hardware auftaucht. Der Abgleich läuft über die stabile mDNS-`id=` —
die ging an der `eureka_info`-Grenze verloren und wird jetzt durchgereicht. Zwei Feinheiten gehören dazu:
Eine Ankündigung *ohne* `id=` darf die gemerkte Kennung nicht leeren, und wiederholte Verbindungsfehler
werden zunehmend langsamer nachgefasst (15 → 30 → 60 s), damit ein unerreichbares Gerät das Protokoll
nicht zumüllt.

**Ein Gerät kann seinen Cast-Dienst zeitweise nur über IPv6 ankündigen.** Dann fehlt die IPv4, über die
allein gestreamt wird, und das Gerät verschwindet — obwohl es dieselbe Hardware ist, die Minuten vorher
noch da war. `Ipv4Recovery` merkt sich pro Gerät die IPv4 zu seiner mDNS-`id=` *und* zu jedem IPv6-Host,
der zusammen mit ihr angekündigt wurde (mit begrenzter Haltbarkeit, sonst zeigt der Eintrag nach einem
DHCP-Wechsel ins Leere). Eine reine IPv6-Ankündigung wird darüber wieder auf eine brauchbare IPv4
zurückgeführt. Eine rohe IPv6-Adresse darf **nie** in die Geräteliste wandern: Sie zerlegt die
`eureka_info`-URL, den TLS-Verbindungsaufbau und erzeugt eine Kachel, die sich mit nichts zusammenfassen
lässt. Entweder eine IPv4 wird gefunden, oder die Ankündigung wird übersprungen.

## Cast-Wiedergabe

**`detailedErrorCode: 102` ist kein Netzwerkfehler, sondern der Speicher des Empfängers.** Gemessen an
echter Hardware: Ein kleiner Lautsprecher nahm hochauflösendes, **unkomprimiertes** LPCM an, spielte
etwa eine Minute und warf dann `ERROR 102`, gefolgt von `IDLE(ERROR)` — die App verband neu, lud neu, und
das Spiel begann von vorn. Der Default Media Receiver lässt seinen Puffer nicht einstellen, der einzige
Hebel ist also die Datenrate. Zwei Maßnahmen: Aufnahme auf **48 kHz** deckeln (Cast-Empfänger mischen
ohnehin auf 48 kHz, höher aufgenommenes wird nur wieder heruntergerechnet — halbiert die Rate ohne
hörbaren Verlust) und ein großzügiges Empfänger-Polster. Wer HiFi ohne Risiko will, nimmt ein
komprimiert-verlustfreies Format statt mehr Bits.

**`mediaSessionId` muss aus jedem `MEDIA_STATUS` neu gelesen werden.** Eine einmal gemerkte Kennung ist
nach jedem Neuladen falsch, und Steuerbefehle laufen dann ins Leere.

## Formate und Encoder

**Ein selbstbeschreibendes Format verträgt keinen vorangestellten Header.** MP3 und FLAC bringen ihren
eigenen Kopf mit (MPEG-Frame-Header bzw. `fLaC` + STREAMINFO); nur rohes LPCM braucht einen
RIFF/WAVE-Header. Als vor *jedem* Nicht-WAV-Stream noch ein handgeschriebener „MP3-Header" stand — der
zudem jedes Bit als ganzes Byte schrieb —, mussten die Decoder ihn erst überspringen: Knacken, Aussetzer
oder Verweigerung, je nach Gerät. Das Muster ist unverwechselbar: **alle WAV-Modi laufen, FLAC und beide
MP3-Stufen zicken.** Heute entscheidet `AudioHeader.GetStreamHeader` an einer einzigen Stelle, was
vorangestellt wird.

**Die Startschwelle muss mit der echten Byte-Rate rechnen.** Der erste Bytesatz geht erst raus, wenn der
Puffer gefüllt ist. Wurde diese Schwelle mit einer geratenen Konstante gerechnet, die nur für ein Format
stimmte, wartete der Empfänger bei den anderen ein Vielfaches der eingestellten Sekunden — und lud
zwei-, dreimal neu, bevor überhaupt Ton kam. `StreamRate` liefert die tatsächliche Rate je Format, und
die Wartezeit ist zusätzlich hart gedeckelt: Ein Polster ist gut, aber nicht um den Preis eines
Empfängers, der nie startet.

**`streamType` ist `LIVE`, nicht `BUFFERED`.** Der Stream ist eine endlose Aufnahme ohne Dauer und ohne
Sprungziel. Als `BUFFERED` deklariert hält der Empfänger ihn für eine Datei, zeigt einen
Fortschrittsbalken, der sich nie füllen kann, und darf Bereiche anfordern, die es nicht gibt.

**Ein FLAC-Encoder schreibt seinen Kopf nur einmal — wenn man ihn nicht zurückspulen lässt.** `FlakeWriter`
korrigiert STREAMINFO nachträglich, *falls* der Ausgabestrom `CanSeek` meldet. Für einen endlosen
Live-Stream ist genau das falsch. Ein weiterreichender Strom mit `CanSeek == false` liefert stattdessen
einen fortlaufenden, in sich gültigen FLAC-Strom mit `total_samples = 0`. Ein Datei-Decoder lehnt so
etwas ab, ein Streaming-Empfänger nicht — beim Testen also nicht am Datei-Decoder verzweifeln.

**Kein `ArrayPool` für Audio-Frames.** `ApplicationBuffer` hält die Referenz auf das Frame-Array in einer
rollenden Historie. Ein zurückgegebener Puffer würde diese Historie überschreiben, während sie noch
gelesen wird. Frisches Array je Frame ist hier keine Nachlässigkeit, sondern Voraussetzung; ein echter
Pool bräuchte erst eine andere Eigentümer-Regelung.

**WASAPI im Shared Mode wandelt die Bittiefe wirklich.** Die Sorge, eine angeforderte Bittiefe liefere
nur umetikettierten Float-Mix, ließ sich messen und widerlegen: Die Byte-Mengen skalieren mit der
Bittiefe. Vor der nächsten Vermutung über den Aufnahmepfad lohnt sich dieselbe Messung.

## Cast-Drahtformat (Protobuf)

**proto3 schreibt Default-Werte nicht.** `protocol_version` (CASTV2_1_0 = 0) und `payload_type`
(STRING = 0) sind beide 0 und wären aus jeder Nachricht verschwunden, während der Receiver sie
verlangt. Deshalb ist in `CastChannel.proto` **jedes** Feld `optional` — das stellt explizite Präsenz
wieder her. `CastWireFormatTests` hält das fest. (Commit `c3adda6`)

## Fenster und Icon

**Ein rohes `WM_CLOSE` ist nicht der X-Klick.** WinForms setzt `CloseReason.UserClosing` nur bei
`WM_SYSCOMMAND` mit `SC_CLOSE` (`0x0112` / `0xF060`). Ein per `PostMessage` geschicktes `WM_CLOSE`
liefert `CloseReason.None`, läuft also in den Else-Zweig und beendet die App — der Tray-Zweig ist so
nicht testbar.

**Jeder `dotnet publish` erzeugt eine neue Settings-Identität.** Die Einstellungen liegen unter
`%LOCALAPPDATA%\KlangHub\KlangHub_Url_<hash>\<version>\user.config`, und der Hash wechselt mit jedem
Publish. Ein frisch veröffentlichter Build startet daher mit Standardwerten (u. a.
`MinimizeToTray = false`) und nicht mit den Einstellungen, die man zu testen glaubt.

**Ein Icon skaliert nicht von 256 auf 16.** Die `.ico` war ein einziges 256-px-Motiv, auf jede Größe
heruntergerechnet; bei 16 und 20 px — den Größen für Task-Manager, Taskleiste und Infobereich —
verschwammen die drei konzentrischen Ringe zu einem dunklen Quadrat. `tools/make-app-icon.ps1` zeichnet
jede Größe eigenständig (unter 48 px ein kräftiger Ring statt drei) und schreibt die kleinen Größen als
DIB, nur 128/256 als PNG. Prüfen lässt sich das Ergebnis über den Shell-eigenen Pfad (`SHGetFileInfo`,
`ExtractIconEx`) gegen die gebaute `.exe`, nicht am Quellbild. (Commit `7aef28c`)

**Ein Tray-Symbol landet unter Windows 11 im Überlauf.** Mit `MinimizeToTray` versteckt das X nur das
Fenster; die App scheint dann verschwunden. Ein Ballon sagt einmal pro Programmlauf, wohin sie ging.
(Commit `7f5e445`)

## Diagnose-Werkzeug

`dotnet-stack` (ein .NET-Global-Tool, `dotnet tool install -g dotnet-stack`) war das Mittel, mit dem der
Start-Deadlock in einem Zug gefunden wurde:

```
dotnet-stack report -p <pid>
```

Bei „die App hängt" lohnt sich das vor jeder Vermutung.
