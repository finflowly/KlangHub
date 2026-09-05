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

`dotnet-stack` ist installiert (`~/.dotnet/tools/dotnet-stack.exe`) und war das Mittel, mit dem der
Start-Deadlock in einem Zug gefunden wurde:

```
dotnet-stack report -p <pid>
```

Bei „die App hängt" lohnt sich das vor jeder Vermutung.
