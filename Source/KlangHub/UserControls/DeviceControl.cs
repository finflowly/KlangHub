using System;
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

            int padL = Theme.PadCard, padR = Width - Theme.PadCard;

            // ---- status row: dot + word (left) ----
            Color dotColor = playing ? Theme.Amber : error ? Theme.Ember : Theme.Blue;
            Color labelColor = playing ? Theme.Amber : error ? Theme.Ember : Theme.Blend(Theme.Slate, Theme.Ivory, 0.35f);
            string statusLabel = StatusWord(state);
            int dotY = 20;
            using (var db = new SolidBrush(dotColor)) g.FillEllipse(db, padL, dotY - 4, 8, 8);
            if (playing) DrawGlowDot(g, padL + 4, dotY, Theme.Amber);
            DrawText(g, statusLabel, Theme.Label, labelColor, padL + 15, dotY - 8);

            // ---- pill (right): format while playing, else the status/action (ember for error) ----
            string pillText = group ? "Gruppe"
                : playing ? Theme.CurrentFormatLabel
                : error ? "erneut verbinden" : "bereit";
            Color pillFg = playing ? Theme.Amber : error ? Theme.Ember
                : group ? Theme.Blend(Theme.Slate, Theme.Ivory, 0.35f) : Theme.Slate;
            Color? pillTint = playing ? Theme.Amber : error ? Theme.Ember : (Color?)null;
            DrawPill(g, pillText, pillFg, pillTint, padR, dotY);

            // ---- identity row: icon + name + subtitle (+ live playing time) ----
            int icoSize = 36, icoY = 44;
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
            DrawText(g, deviceName, Theme.Name, Theme.Ivory, nameX, icoY - 1);
            string sub = Subtitle();
            if (!string.IsNullOrEmpty(sub))
                DrawText(g, sub, Theme.Small, Theme.Slate, nameX, icoY + 22);

            // ---- level meter (the signature) ----
            int meterY = 92, meterH = 20, barW = 4, gap = 3, meterX = padL;
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
            int ctlY = Height - 34;
            playRect = new Rectangle(padL, ctlY, 32, 28);
            DrawPlayButton(g, playRect, playing);

            muteRect = new Rectangle(playRect.Right + 10, ctlY + 4, 22, 20);
            DrawSpeakerIcon(g, muteRect, muted);

            int sx = muteRect.Right + 12;
            int pctW = 34, ovW = 22;
            int trackW = padR - sx - pctW - ovW - 12;
            sliderRect = new Rectangle(sx, ctlY + 9, trackW, 10);
            DrawVolumeSlider(g, sliderRect);

            string pct = muted ? "stumm" : (volume + "%");
            DrawText(g, pct, Theme.Data, muted ? Theme.Amber : Theme.Slate, sliderRect.Right + 8, ctlY + 5);

            overflowRect = new Rectangle(padR - ovW, ctlY, ovW, 28);
            DrawOverflow(g, overflowRect);

            // ---- keyboard focus ring ----
            if (Focused) Theme.DrawFocusRing(g, cardF, Theme.RadCard);
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

            var sc = statusText?.Trim();
            if (!string.IsNullOrEmpty(sc)) return sc!;
            var m = !string.IsNullOrWhiteSpace(model) ? model : descriptor?.Model;
            if (!string.IsNullOrWhiteSpace(m) && !SaysTheSame(m!, deviceName)) return m!;
            return descriptor?.Id != null ? KlangHub.Properties.Strings.Card_Subtitle_Device_Text : string.Empty;
        }

        /// <summary>True when the model would only repeat the device name ("Enchant Speaker" under
        /// "Enchant Speaker") - people name a Chromecast after its room or its model, so the subtitle has to
        /// step back rather than echo the line above it.</summary>
        private static bool SaysTheSame(string model, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            static string Key(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            string a = Key(model), b = Key(name!);
            return a.Length > 0 && b.Length > 0 && (a == b || a.Contains(b) || b.Contains(a));
        }

        private static string StatusWord(PlaybackState s) => s switch
        {
            PlaybackState.Playing => "Wiedergabe",
            PlaybackState.Buffering => "Puffert",
            PlaybackState.Error => "Nicht erreichbar",
            _ => "Verbunden",
        };

        // ---------- drawing helpers ----------

        private static void DrawText(Graphics g, string text, Font f, Color c, float x, float y)
        {
            using var b = new SolidBrush(c);
            using var sf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(text, f, b, x, y, sf);
        }

        private void DrawPill(Graphics g, string text, Color fg, Color? tint, int rightEdge, int centerY)
        {
            var sz = g.MeasureString(text, Theme.Label);
            float w = sz.Width + 18, h = 20f;
            var r = new RectangleF(rightEdge - w, centerY - h / 2f, w, h);
            if (tint.HasValue)
            {
                Theme.FillRounded(g, r, h / 2f, Color.FromArgb(28, tint.Value));
                Theme.DrawRounded(g, r, h / 2f, Color.FromArgb(120, tint.Value));
            }
            else Theme.DrawRounded(g, r, h / 2f, Theme.Line);
            DrawText(g, text, Theme.Label, fg, r.X + 9, r.Y + 3f);
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

        private void ShowSpeakerOptions()
        {
            using var popup = new SpeakerOptionsPopup(deviceName, maxVolume);
            var loc = PointToScreen(new Point(overflowRect.Left - 210, overflowRect.Bottom + 4));
            if (popup.ShowAt(loc) == DialogResult.OK)
            {
                maxVolume = popup.MaxVolume;
                SpeakerPrefs.SetMaxVolume(descriptor?.Id, maxVolume);
                if (volume > maxVolume) TryOnSession(s => s.SetVolume(maxVolume / 100f));
                Invalidate();
            }
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
