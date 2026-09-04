using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.Classes;
using KlangHub.Communication;
using KlangHub.Communication.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// A premium, owner-drawn device tile (the "room card"). Draws its own warm-dark surface, status dot,
    /// format pill, name/subtitle, a live amber level meter while playing, and a compact volume slider + play
    /// button. The neutral-session wiring (state/volume events, play/volume/mute via IPlaybackSession) is
    /// unchanged - only the presentation is new. The card is keyboard-focusable (2-px amber focus ring;
    /// Space/Enter = play, Left/Right = volume). Adds two per-speaker features: a live playing-time and a hard
    /// maximum-volume cap (SpeakerPrefs).
    /// </summary>
    public partial class DeviceControl : UserControl
    {
        private readonly Func<IPlaybackSession> sessionAccessor;
        private readonly IPlaybackSession? session;
        private readonly CastDeviceDescriptor? descriptor;

        private PlaybackState state = PlaybackState.Idle;
        private string statusText = string.Empty;
        private string deviceName = "";
        private int volume;                 // 0..100
        private bool muted;
        private int stepPercent = 5;
        private int maxVolume = 100;        // per-speaker hard cap
        private DateTime? playingSince;     // when this device entered Playing

        private readonly System.Windows.Forms.Timer uiTimer;
        private readonly float[] meterTargets = new float[11];
        private readonly float[] meterHeights = new float[11];
        private readonly Random rnd = new Random();

        // hit regions (computed in OnPaint layout)
        private Rectangle playRect, muteRect, sliderRect, overflowRect, cardRect;
        private Rectangle meterBand, timeBand;   // exact repaint regions for the animation timer
        private bool draggingVolume;
        private bool hovering;
        private Point mouseDownAt;
        private bool downOnBody;

        public DeviceControl(Func<IPlaybackSession> sessionAccessorIn)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.Selectable, true);
            InitializeComponent();
            TabStop = true;   // keyboard-focusable card

            sessionAccessor = sessionAccessorIn;
            try { session = sessionAccessorIn?.Invoke(); }
            catch (InvalidOperationException) { session = null; }
            descriptor = session?.Device;

            for (int i = 0; i < meterHeights.Length; i++) { meterHeights[i] = 0.15f; meterTargets[i] = 0.15f; }
            maxVolume = SpeakerPrefs.GetMaxVolume(descriptor?.Id);

            if (session != null)
            {
                session.StateChanged += OnSessionStateChanged;
                session.VolumeChanged += OnSessionVolumeChanged;
                Disposed += (s, e) =>
                {
                    session.StateChanged -= OnSessionStateChanged;
                    session.VolumeChanged -= OnSessionVolumeChanged;
                };
                deviceName = descriptor!.Name;
                RenderStatus(session.State, session.StatusText);
            }

            uiTimer = new System.Windows.Forms.Timer { Interval = 90 };
            uiTimer.Tick += (s, e) => OnTick();
            uiTimer.Start();
        }

        // ---------------- neutral session observation (unchanged behaviour) ----------------

        public void SetDeviceName(string name)
        {
            if (InvokeRequired) { Invoke(new Action<string>(SetDeviceName), new object[] { name }); return; }
            deviceName = name;
            Invalidate();
        }

        public string GetDeviceName() => deviceName;
        public bool IsGroup => descriptor?.IsGroup ?? false;

        /// <summary>Raised when the user gave this speaker a different room, so the grid can regroup.</summary>
        public event EventHandler? RoomChanged;

        /// <summary>The room this speaker was assigned to, or null while it has none.</summary>
        public string? Room => SpeakerPrefs.GetRoom(descriptor?.Id);

        /// <summary>Current volume in percent, as this card last saw it.</summary>
        public int VolumePercent => volume;

        /// <summary>The per-speaker hard cap - a room fader must never push a card past it.</summary>
        public int MaxVolumePercent => maxVolume;

        /// <summary>The volume step this device itself works in, in percent. Cast endpoints report their
        /// own ("stepInterval"): a television moves in 1 %, a Google Home in 2 %, the Enchant in 4 %. Until
        /// the first status arrives this is the protocol's default of 5 %.</summary>
        public int StepPercent => stepPercent;

        public bool IsMuted => muted;

        /// <summary>Sets this card to an absolute percentage, never past its own hard cap. The room fader
        /// uses it to scale a whole room by one factor.</summary>
        public void SetVolumePercent(int percent)
        {
            int target = Math.Clamp(percent, 0, maxVolume);
            if (target == volume) return;
            volume = target;
            Invalidate();
            TryOnSession(s => s.SetVolume(target / 100f));
        }

        /// <summary>Moves this card's volume by <paramref name="delta"/> percentage points, inside 0..cap.</summary>
        public void NudgeVolume(int delta) => SetVolumePercent(volume + delta);

        /// <summary>Mutes or unmutes this speaker (the room bar mutes a whole room at once).</summary>
        public void SetMuted(bool value)
        {
            if (muted == value) return;
            TryOnSession(s => s.SetMuted(value));
        }
        public string? Id => descriptor?.Id;
        public bool IsPlaying => state == PlaybackState.Playing || state == PlaybackState.Buffering;

        private void OnSessionStateChanged(object? sender, PlaybackState s)
            => RenderStatus(s, session?.StatusText ?? string.Empty);

        private void RenderStatus(PlaybackState newState, string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(new Action<PlaybackState, string>(RenderStatus), new object[] { newState, text }); return; }

            bool nowPlaying = newState == PlaybackState.Playing || newState == PlaybackState.Buffering;
            bool wasPlaying = playingSince.HasValue;
            if (nowPlaying && !wasPlaying) playingSince = DateTime.Now;
            else if (!nowPlaying && wasPlaying) playingSince = null;

            state = newState;
            statusText = text ?? string.Empty;
            Invalidate();
        }

        private void OnSessionVolumeChanged(object? sender, VolumeStatus v) => RenderVolume(v);

        private void RenderVolume(VolumeStatus v)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(new Action<VolumeStatus>(RenderVolume), new object[] { v }); return; }

            int reported = (int)Math.Round(v.Level * 100);
            // Hard cap: if the endpoint reports a level above the per-speaker maximum, push it back down.
            if (reported > maxVolume)
            {
                reported = maxVolume;
                TryOnSession(s => s.SetVolume(maxVolume / 100f));
            }
            volume = reported;
            muted = v.Muted;
            stepPercent = Math.Max(1, (int)Math.Round(v.StepInterval * 100));
            Invalidate();
        }

        // ---------------- timer: meter animation + playing-time ----------------

        private void OnTick()
        {
            if (IsDisposed) return;
            PollForModel();
            bool playing = state == PlaybackState.Playing || state == PlaybackState.Buffering;
            if (playing)
            {
                for (int i = 0; i < meterHeights.Length; i++)
                {
                    if (Math.Abs(meterHeights[i] - meterTargets[i]) < 0.05f)
                        meterTargets[i] = 0.2f + (float)rnd.NextDouble() * 0.8f;
                    meterHeights[i] += (meterTargets[i] - meterHeights[i]) * 0.35f;
                }
                Invalidate(meterBand.IsEmpty ? ClientRectangle : meterBand);   // meter band (exact draw region)
                if (!timeBand.IsEmpty) Invalidate(timeBand);                   // playing-time
            }
            else
            {
                bool changed = false;
                for (int i = 0; i < meterHeights.Length; i++)
                    if (meterHeights[i] > 0.16f) { meterHeights[i] += (0.15f - meterHeights[i]) * 0.3f; changed = true; }
                if (changed) Invalidate(meterBand.IsEmpty ? ClientRectangle : meterBand);
            }
        }

        // ---------------- painting ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // The tile is glass: it shows the app's own backdrop through itself, softly blurred. The slice is
            // anchored to the WINDOW, so the rings line up with the surrounding page instead of restarting
            // inside every card.
            Theme.PaintBackdrop(g, this, blurDivisor: 5, veilScale: 0.78f);   // the pane gathers light

            bool playing = state == PlaybackState.Playing || state == PlaybackState.Buffering;
            bool error = state == PlaybackState.Error;
            bool group = descriptor?.IsGroup == true;
            cardRect = new Rectangle(4, 3, Width - 9, Height - 9);   // inset leaves room for the float shadow
            var cardF = new RectangleF(cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height);

            // soft amber glow behind a playing card (the signature)
            if (playing)
            {
                using var glow = new GraphicsPath();
                glow.AddPath(Theme.RoundedRect(RectangleF.Inflate(cardF, 2, 2), Theme.RadCard), false);
                using var gb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(60, Theme.Amber), SurroundColors = new[] { Color.FromArgb(0, Theme.Amber) } };
                g.FillPath(gb, glow);
            }

            // glass pane: playing = amber-tinted and a little more solid (it has to carry the meter), hovered
            // = lifted and slightly clearer, otherwise the plain surface tint.
            Color surface = playing ? Theme.Blend(Theme.Surface, Theme.Amber, 0.05f) : hovering ? Theme.Raised : Theme.Surface;
            Color borderCol = playing ? Color.FromArgb(170, Theme.Amber) : error ? Color.FromArgb(110, Theme.Ember)
                : hovering ? Color.FromArgb(190, Theme.LineHi) : Color.FromArgb(150, Theme.LineHi);
            int alpha = playing ? Theme.GlassAlpha + 26 : hovering ? Theme.GlassAlpha - 12 : Theme.GlassAlpha;
            Theme.DrawSoftShadow(g, cardF, Theme.RadCard, hovering ? 4f : 3f, hovering ? 60 : 46);
            Theme.FillGlass(g, cardF, Theme.RadCard, surface, borderCol, alpha);

            // left accent bar (amber playing / ember error)
            var accent = playing ? Theme.Amber : error ? Theme.Ember : Color.Transparent;
            if (accent != Color.Transparent)
            {
                using var ab = new SolidBrush(accent);
                using var ap = Theme.RoundedRect(new RectangleF(cardF.X + 1, cardF.Y + 10, 3, cardF.Height - 20), 1.5f);
                g.FillPath(ab, ap);
            }

            // Every row is placed against the CARD, not against the control: the card is inset for its float
            // shadow, so measuring from the control's own edge is what pushed the play button hard against the
            // bottom line. cardTop/cardBottom keep the same 16-px breathing room top and bottom.
            int padL = cardRect.Left + Theme.PadCard, padR = cardRect.Right - Theme.PadCard;
            int cardTop = cardRect.Top, cardBottom = cardRect.Bottom;

            // ---- status row: dot + word (left) ----
            // "Connecting" is the moment between picking a speaker and hearing it - it used to fall through
            // to "Connected", so the card claimed to be ready while nothing was playing yet.
            bool connecting = state is PlaybackState.Connecting or PlaybackState.Loading;
            Color dotColor = playing || connecting ? Theme.Amber : error ? Theme.Ember : Theme.Blue;
            Color labelColor = playing || connecting ? Theme.Amber : error ? Theme.Ember : Theme.Blend(Theme.Slate, Theme.Ivory, 0.35f);
            string statusLabel = StatusWord(state);
            int dotY = cardTop + 18;
            using (var db = new SolidBrush(dotColor)) g.FillEllipse(db, padL, dotY - 4, 8, 8);
            if (playing || connecting) DrawGlowDot(g, padL + 4, dotY, Theme.Amber);
            DrawText(g, statusLabel, Theme.Label, labelColor, padL + 15, dotY - 8);
            float statusRight = padL + 15 + g.MeasureString(statusLabel, Theme.Label).Width;

            // ---- pill (right): format while playing, else the status/action (ember for error) ----
            string pillText = group ? KlangHub.Properties.Strings.Card_Pill_Group_Text
                : playing ? Theme.CurrentFormatLabel
                : connecting ? KlangHub.Properties.Strings.Card_Pill_Connecting_Text
                : error ? KlangHub.Properties.Strings.Card_Pill_Reconnect_Text
                : KlangHub.Properties.Strings.Card_Pill_Ready_Text;
            Color pillFg = playing || connecting ? Theme.Amber : error ? Theme.Ember
                : group ? Theme.Blend(Theme.Slate, Theme.Ivory, 0.35f) : Theme.Slate;
            Color? pillTint = playing ? Theme.Amber : error ? Theme.Ember : (Color?)null;
            // The pill grows with its text and grows LEFTWARDS, straight towards the status word. Some
            // languages need a lot more room than English for "reconnect" (nl "opnieuw verbinden",
            // fi "yhdistä uudelleen"), so it is given whatever is left beside the status and trims itself
            // rather than sliding underneath it.
            DrawPill(g, pillText, pillFg, pillTint, padR, dotY, padR - statusRight - 10);

            // ---- identity row: icon + name + subtitle (+ live playing time) ----
            int icoSize = 36, icoY = cardTop + 40;
            DrawDeviceIcon(g, new Rectangle(padL, icoY, icoSize, icoSize), playing);
            int nameX = padL + icoSize + 12;
            if (playingSince.HasValue)
            {
                var span = DateTime.Now - playingSince.Value;
                string t = span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"mm\:ss");
                var sz = g.MeasureString(t, Theme.Data);
                DrawText(g, t, Theme.Data, Theme.Slate, padR - sz.Width, icoY + 3);
                timeBand = new Rectangle((int)(padR - sz.Width) - 2, icoY, (int)sz.Width + 6, 24);
            }
            else timeBand = Rectangle.Empty;
            DrawText(g, deviceName, Theme.Name, Theme.Ivory, nameX, icoY - 2);
            string sub = Subtitle();
            if (!string.IsNullOrEmpty(sub))
            {
                // A named room gets its symbol right in front of the subtitle, so the room is readable at a
                // glance whether or not the grid is grouped.
                var room = Room;
                float subX = nameX;
                if (!string.IsNullOrWhiteSpace(room))
                {
                    RoomPresets.DrawIcon(g, new RectangleF(nameX, icoY + 22, 14, 14),
                                         room, playing ? Theme.Amber : Theme.Slate);
                    subX += 19;
                }
                DrawText(g, sub, Theme.Small, Theme.Slate, subX, icoY + 22);
            }

            // ---- level meter (the signature) ----
            int meterY = cardTop + 90, meterH = 18, barW = 4, gap = 3, meterX = padL;
            for (int i = 0; i < meterHeights.Length; i++)
            {
                float h = Math.Max(3, meterHeights[i] * meterH);
                var r = new RectangleF(meterX + i * (barW + gap), meterY + (meterH - h), barW, h);
                using var bb = new SolidBrush(playing ? Theme.Blend(Theme.AmberDim, Theme.Amber, meterHeights[i]) : Theme.MeterOff);
                using var bp = Theme.RoundedRect(r, 1.5f);
                g.FillPath(bb, bp);
            }
            meterBand = new Rectangle(meterX - 2, meterY - 2, meterHeights.Length * (barW + gap) + 4, meterH + 4);

            // ---- control row: play + speaker + slider + % + overflow ----
            // One row, vertically centred on itself, sitting a full card inset above the bottom edge.
            const int ctlH = 30;
            int ctlY = cardBottom - Theme.PadCard - ctlH;
            int ctlMid = ctlY + ctlH / 2;

            playRect = new Rectangle(padL, ctlY, 32, ctlH);
            DrawPlayButton(g, playRect, playing);

            muteRect = new Rectangle(playRect.Right + 10, ctlMid - 10, 22, 20);
            DrawSpeakerIcon(g, muteRect, muted);

            int sx = muteRect.Right + 12;
            int pctW = 38, ovW = 22;
            int trackW = padR - sx - pctW - ovW - 12;
            sliderRect = new Rectangle(sx, ctlMid - 5, trackW, 10);
            DrawVolumeSlider(g, sliderRect);

            string pct = muted ? Properties.Strings.Card_Muted_Text : (volume + "%");
            var pctSize = g.MeasureString(pct, Theme.Data);
            DrawText(g, pct, Theme.Data, muted ? Theme.Amber : Theme.Slate,
                     sliderRect.Right + 10, ctlMid - pctSize.Height / 2f);

            overflowRect = new Rectangle(padR - ovW, ctlY, ovW, ctlH);
            DrawOverflow(g, overflowRect);

            // ---- keyboard focus ring ----
            // ShowFocusCues, not just Focused: clicking a tile focuses it too, and a fully opaque amber ring
            // around one card while its neighbours have only the soft playing edge looks like a bug, not like
            // focus. Windows raises this flag once the keyboard has been used to navigate.
            if (Focused && ShowFocusCues) Theme.DrawFocusRing(g, cardF, Theme.RadCard);
        }

        // The model name only exists once mDNS has announced the device - a card built from the persisted
        // device list starts without it. Poll the live session for it (cheap property read, no reconnect),
        // stop as soon as it arrives, and give up after a while so a device that never announces one costs
        // nothing.
        private string? model;
        private int modelPolls;

        private void PollForModel()
        {
            // eureka_info can take a while to answer (and a device rediscovered from the persisted list only
            // gets its details on the next announcement), so keep looking for several minutes rather than
            // giving up after the first seconds. One property read every ~2 s costs nothing.
            if (model != null || modelPolls > 4000 || session == null) return;
            if ((modelPolls++ % 22) != 0) return;                               // every ~2 s at the 90 ms tick
            try
            {
                var m = session.Device?.Model;
                if (!string.IsNullOrWhiteSpace(m)) { model = m; Invalidate(); }
            }
            catch { /* the session may be mid-reconnect - just try again on the next poll */ }
        }

        // The concept's card carries "room · model" under the name. The room IS the device name a Chromecast
        // announces (people name them after the room), so the subtitle adds what the name cannot say: the
        // hardware behind it, straight from the mDNS "md=" record - and for a group, that it is one.
        private string Subtitle()
        {
            if (descriptor?.IsGroup == true)
                return descriptor.MemberCount > 1
                    ? string.Format(KlangHub.Properties.Strings.Card_Subtitle_Group_Text,
                          string.Format(KlangHub.Properties.Strings.Label_RoomSummaryDevicesMany_Text, descriptor.MemberCount))
                    : KlangHub.Properties.Strings.Card_Subtitle_GroupPlain_Text;

            // room · model is the concept's line. The room is whatever the user named (a Chromecast cannot
            // tell us), the model is what the device announces - either half alone is a complete subtitle.
            var room = SpeakerPrefs.GetRoom(descriptor?.Id);
            var m = !string.IsNullOrWhiteSpace(model) ? model : descriptor?.Model;
            if (!string.IsNullOrWhiteSpace(m) && SaysTheSame(m!, deviceName)) m = null;
            if (!string.IsNullOrEmpty(room))
                return string.IsNullOrWhiteSpace(m) ? room! : $"{room} · {m}";

            var sc = statusText?.Trim();
            if (!string.IsNullOrEmpty(sc)) return sc!;
            if (!string.IsNullOrWhiteSpace(m)) return m!;
            return descriptor?.Id != null ? KlangHub.Properties.Strings.Card_Subtitle_Device_Text : string.Empty;
        }

        /// <summary>True when the model would only repeat the device name ("Enchant Speaker" under
        /// "Enchant Speaker") - people name a Chromecast after its room or its model, so the subtitle has to
        /// step back rather than echo the line above it.
        ///
        /// Only an exact match counts. Containment used to qualify too, and that swallowed the very thing
        /// the subtitle exists for: a speaker called "Google Home" announces the model "Google Home Speaker",
        /// which is a longer, more precise name - not an echo. Whatever the model adds, it stays.</summary>
        private static bool SaysTheSame(string model, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            static string Key(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            string a = Key(model), b = Key(name!);
            return a.Length > 0 && a == b;
        }

        /// <summary>
        /// The word on the status row. Every state a listener can tell apart gets its own - in particular
        /// the connecting moment, which used to be indistinguishable from "connected" even though nothing
        /// was playing yet.
        /// </summary>
        private static string StatusWord(PlaybackState s) => s switch
        {
            PlaybackState.Playing => KlangHub.Properties.Strings.Card_Status_Playing_Text,
            PlaybackState.Buffering => KlangHub.Properties.Strings.Card_Status_Buffering_Text,
            PlaybackState.Connecting or PlaybackState.Loading => KlangHub.Properties.Strings.Card_Status_Connecting_Text,
            PlaybackState.Paused => KlangHub.Properties.Strings.Card_Status_Paused_Text,
            PlaybackState.Error => KlangHub.Properties.Strings.Card_Status_Error_Text,
            _ => KlangHub.Properties.Strings.Card_Status_Connected_Text,
        };

        // ---------- drawing helpers ----------

        private static void DrawText(Graphics g, string text, Font f, Color c, float x, float y)
        {
            using var b = new SolidBrush(c);
            using var sf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(text, f, b, x, y, sf);
        }

        private void DrawPill(Graphics g, string text, Color fg, Color? tint, int rightEdge, int centerY,
                              float maxWidth = float.MaxValue)
        {
            if (string.IsNullOrEmpty(text)) return;

            var sz = g.MeasureString(text, Theme.Label);
            float h = 20f;
            float w = Math.Min(sz.Width + 18, Math.Max(46f, maxWidth));
            var r = new RectangleF(rightEdge - w, centerY - h / 2f, w, h);

            if (tint.HasValue)
            {
                Theme.FillRounded(g, r, h / 2f, Color.FromArgb(28, tint.Value));
                Theme.DrawRounded(g, r, h / 2f, Color.FromArgb(120, tint.Value));
            }
            else Theme.DrawRounded(g, r, h / 2f, Theme.Line);

            using var brush = new SolidBrush(fg);
            using var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Trimming = StringTrimming.EllipsisCharacter,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(text, Theme.Label, brush, new RectangleF(r.X + 9, r.Y, r.Width - 18, r.Height), sf);
        }

        private static void DrawGlowDot(Graphics g, int cx, int cy, Color c)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(cx - 8, cy - 8, 16, 16);
            using var pb = new PathGradientBrush(path) { CenterColor = Color.FromArgb(120, c), SurroundColors = new[] { Color.FromArgb(0, c) } };
            g.FillPath(pb, path);
        }

        private void DrawDeviceIcon(Graphics g, Rectangle r, bool playing)
        {
            Theme.FillRounded(g, r, Theme.RadControl, Theme.Ink2);
            Theme.DrawRounded(g, r, Theme.RadControl, playing ? Color.FromArgb(90, Theme.Amber) : Theme.Line);
            using var pen = new Pen(playing ? Theme.Amber : Theme.Slate, 1.6f);
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (descriptor?.IsGroup == true)
            {
                g.DrawEllipse(pen, cx - 8, cy - 5, 8, 8);
                g.DrawEllipse(pen, cx, cy - 5, 8, 8);
            }
            else
            {
                var body = new Rectangle(cx - 6, cy - 9, 12, 18);
                using var bp = Theme.RoundedRect(body, 3);
                g.DrawPath(pen, bp);
                g.DrawEllipse(pen, cx - 3, cy, 6, 6);
            }
        }

        private void DrawPlayButton(Graphics g, Rectangle r, bool playing)
        {
            var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            if (playing) { Theme.FillRounded(g, rf, Theme.RadControl, Theme.Amber); }
            else { Theme.FillRounded(g, rf, Theme.RadControl, Theme.Ink2); Theme.DrawRounded(g, rf, Theme.RadControl, Theme.Line); }
            var col = playing ? Theme.OnAmber : Theme.Ivory;
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using var b = new SolidBrush(col);
            if (playing)
            {
                g.FillRectangle(b, cx - 5, cy - 6, 3, 12);
                g.FillRectangle(b, cx + 2, cy - 6, 3, 12);
            }
            else
            {
                var pts = new[] { new PointF(cx - 4, cy - 6), new PointF(cx - 4, cy + 6), new PointF(cx + 6, cy) };
                g.FillPolygon(b, pts);
            }
        }

        private void DrawSpeakerIcon(Graphics g, Rectangle r, bool isMuted)
        {
            using var pen = new Pen(isMuted ? Theme.Amber : Theme.Slate, 1.6f) { LineJoin = LineJoin.Round };
            int x = r.X, cy = r.Y + r.Height / 2;
            var body = new[] { new PointF(x, cy - 3), new PointF(x + 4, cy - 3), new PointF(x + 9, cy - 7), new PointF(x + 9, cy + 7), new PointF(x + 4, cy + 3), new PointF(x, cy + 3) };
            g.DrawPolygon(pen, body);
            if (isMuted)
            {
                g.DrawLine(pen, x + 12, cy - 4, x + 18, cy + 4);
                g.DrawLine(pen, x + 18, cy - 4, x + 12, cy + 4);
            }
            else
            {
                g.DrawArc(pen, x + 8, cy - 6, 8, 12, -55, 110);
            }
        }

        private void DrawVolumeSlider(Graphics g, Rectangle r)
        {
            float trackY = r.Y + r.Height / 2f;
            var trackRect = new RectangleF(r.X, trackY - 2.5f, r.Width, 5);
            Theme.FillRounded(g, trackRect, 2.5f, Theme.Ink2);
            Theme.DrawRounded(g, trackRect, 2.5f, Theme.Line);

            // per-speaker hard cap marker
            if (maxVolume < 100)
            {
                float capX = r.X + r.Width * (maxVolume / 100f);
                using var cap = new Pen(Color.FromArgb(120, Theme.Ember), 1.5f);
                g.DrawLine(cap, capX, r.Y + 1, capX, r.Y + r.Height - 1);
            }

            float fillW = r.Width * (Math.Min(volume, maxVolume) / 100f);
            if (fillW > 3)
            {
                var fr = new RectangleF(r.X, trackY - 2.5f, fillW, 5);
                using var lg = new LinearGradientBrush(fr, muted ? Theme.Slate2 : Theme.AmberDim, muted ? Theme.Slate : Theme.Amber, LinearGradientMode.Horizontal);
                using var fp = Theme.RoundedRect(fr, 2.5f);
                g.FillPath(lg, fp);
            }
            float tx = r.X + fillW;
            using (var tb = new SolidBrush(Theme.Ivory)) g.FillEllipse(tb, tx - 6, trackY - 6, 12, 12);
            using (var tp = new Pen(Color.FromArgb(70, Theme.Amber), 3f)) g.DrawEllipse(tp, tx - 6, trackY - 6, 12, 12);
        }

        private void DrawOverflow(Graphics g, Rectangle r)
        {
            using var b = new SolidBrush(hovering ? Theme.Slate : Theme.Slate2);
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            for (int i = -1; i <= 1; i++) g.FillEllipse(b, cx - 1.5f, cy + i * 6 - 1.5f, 3, 3);
        }

        // ---------------- interaction ----------------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (draggingVolume) { ApplyVolumeFromX(e.X); return; }

            if (e.Button == MouseButtons.Left && downOnBody &&
                (Math.Abs(e.X - mouseDownAt.X) > 6 || Math.Abs(e.Y - mouseDownAt.Y) > 6))
            {
                downOnBody = false;
                DoDragDrop(this, DragDropEffects.Move);   // reorder
                return;
            }

            bool overCtl = playRect.Contains(e.Location) || overflowRect.Contains(e.Location) || muteRect.Contains(e.Location) || sliderRect.Contains(e.Location);
            Cursor = overCtl ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();   // clicking the card gives it keyboard focus
            mouseDownAt = e.Location;
            downOnBody = false;

            if (SliderHit(e.Location)) { draggingVolume = true; ApplyVolumeFromX(e.X); }
            else if (!playRect.Contains(e.Location) && !overflowRect.Contains(e.Location) && !muteRect.Contains(e.Location))
                downOnBody = true;   // candidate for drag-reorder / name click
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (draggingVolume) { draggingVolume = false; return; }
            // A right-click is the shortcut to the details card. A double-click cannot be: clicking the card
            // body toggles playback, so a second click would start and stop the music on the way there.
            if (e.Button == MouseButtons.Right) { ShowSpeakerDetails(); return; }
            if (e.Button != MouseButtons.Left) return;

            if (playRect.Contains(e.Location)) TryOnSession(s => s.TogglePlayStop());
            else if (muteRect.Contains(e.Location)) TryOnSession(s => s.SetMuted(!s.Volume.Muted));
            else if (overflowRect.Contains(e.Location)) ShowSpeakerOptions();
            else if (downOnBody && Math.Abs(e.X - mouseDownAt.X) < 6 && Math.Abs(e.Y - mouseDownAt.Y) < 6)
                TryOnSession(s => s.TogglePlayStop());   // click the card body toggles play (familiar behaviour)
            downOnBody = false;
        }

        // keyboard: card focusable, Space/Enter toggles play, Left/Right nudge volume
        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Space:
                case Keys.Enter:
                    TryOnSession(s => s.TogglePlayStop()); e.Handled = true; break;
                case Keys.Left:
                    ApplyVolumeAbsolute(Math.Max(0, volume - stepPercent) / 100f); e.Handled = true; break;
                case Keys.Right:
                    ApplyVolumeAbsolute(Math.Min(100, volume + stepPercent) / 100f); e.Handled = true; break;
            }
        }

        protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }

        private bool SliderHit(Point p) =>
            p.X >= sliderRect.X - 6 && p.X <= sliderRect.Right + 6 && Math.Abs(p.Y - (sliderRect.Y + sliderRect.Height / 2)) <= 12;

        private void ApplyVolumeFromX(int x)
        {
            float t = (x - sliderRect.X) / (float)sliderRect.Width;
            ApplyVolumeAbsolute(Math.Clamp(t, 0, 1));
        }

        /// <summary>Sets this card's volume to an absolute 0..1 level, honouring its own hard cap. Used both by
        /// the card's own slider drag and by the header's master-volume fader (which applies the same level to
        /// every card).</summary>
        public void ApplyVolumeAbsolute(float t)
        {
            int pct = (int)Math.Round(Math.Clamp(t, 0, 1) * 100);
            pct = Math.Min(pct, maxVolume);   // hard cap
            if (pct == volume) return;
            volume = pct;
            Invalidate();
            TryOnSession(s => s.SetVolume(pct / 100f));
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovering = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovering = false; Cursor = Cursors.Default; Invalidate(); }

        /// <summary>The read-only card: everything the speaker announces about itself. The facts are read
        /// from the live session rather than kept here, because a device's details fill in over the first
        /// seconds - the firmware and the network arrive with the setup endpoint, well after the name.</summary>
        private void ShowSpeakerDetails()
        {
            IReadOnlyList<Core.Casting.DeviceFact>? details = null;
            try { details = (sessionAccessor?.Invoke() ?? session)?.Device?.Details; }
            catch (InvalidOperationException) { details = descriptor?.Details; }

            // The volume step is the one fact the provider cannot supply: it does not come from the
            // announcement or the setup endpoint but from the live control channel, which only a running
            // session has. So the card gets it from here, after everything the device announced.
            var rows = new List<Core.Casting.DeviceFact>(details ?? descriptor?.Details
                                                         ?? (IReadOnlyList<Core.Casting.DeviceFact>)Array.Empty<Core.Casting.DeviceFact>());
            if (rows.Count > 0)
                rows.Add(new Core.Casting.DeviceFact(Core.Casting.DeviceFactKind.VolumeStep, stepPercent + " %"));

            using var card = new SpeakerDetailsPopup(deviceName, rows);
            card.ShowAt(PointToScreen(new Point(Math.Max(0, (Width - 460) / 2), 24)));
        }

        private void ShowSpeakerOptions()
        {
            using var popup = new SpeakerOptionsPopup(deviceName, maxVolume, SpeakerPrefs.GetRoom(descriptor?.Id));
            var loc = PointToScreen(new Point(overflowRect.Left - 230, overflowRect.Bottom + 4));
            var answer = popup.ShowAt(loc);
            if (answer == DialogResult.OK || answer == DialogResult.Retry)
            {
                var roomBefore = SpeakerPrefs.GetRoom(descriptor?.Id);
                maxVolume = popup.MaxVolume;
                SpeakerPrefs.SetMaxVolume(descriptor?.Id, maxVolume);
                SpeakerPrefs.SetRoom(descriptor?.Id, popup.Room);
                if (volume > maxVolume) TryOnSession(s => s.SetVolume(maxVolume / 100f));
                Invalidate();
                if (!string.Equals(roomBefore ?? string.Empty, popup.Room, StringComparison.CurrentCultureIgnoreCase))
                    RoomChanged?.Invoke(this, EventArgs.Empty);
            }

            if (answer == DialogResult.Retry)
                ShowSpeakerDetails();
        }

        private void TryOnSession(Func<IPlaybackSession, Task> action)
        {
            if (sessionAccessor == null) return;
            try { _ = action(sessionAccessor()); }
            catch (InvalidOperationException) { }
        }

        // ---------------- drag-drop (reorder) target side, unchanged ----------------

        private void DeviceControl_DragOver(object sender, DragEventArgs e)
        {
            if (e == null) return;
            e.Effect = DragDropEffects.All;
        }

        private void DeviceControl_DragDrop(object sender, DragEventArgs e)
        {
            if (sender is DeviceControl dc && dc.ParentForm is IMainForm mf)
                mf.DoDragDrop(sender, e);
        }
    }
}
