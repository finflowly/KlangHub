using System;
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
    /// unchanged from the previous control - only the presentation is new. Adds two per-speaker features:
    /// a live playing-time and a hard maximum-volume cap (SpeakerPrefs).
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
        private bool draggingVolume;
        private bool hovering;
        private Point mouseDownAt;
        private bool downOnBody;

        public DeviceControl(Func<IPlaybackSession> sessionAccessorIn)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            InitializeComponent();

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
            bool playing = state == PlaybackState.Playing || state == PlaybackState.Buffering;
            if (playing)
            {
                for (int i = 0; i < meterHeights.Length; i++)
                {
                    if (Math.Abs(meterHeights[i] - meterTargets[i]) < 0.05f)
                        meterTargets[i] = 0.2f + (float)rnd.NextDouble() * 0.8f;
                    meterHeights[i] += (meterTargets[i] - meterHeights[i]) * 0.35f;
                }
                Invalidate(new Rectangle(0, 84, Width, 26));   // meter band
                Invalidate(new Rectangle(Width - 90, 36, 90, 24)); // playing-time
            }
            else
            {
                bool changed = false;
                for (int i = 0; i < meterHeights.Length; i++)
                    if (meterHeights[i] > 0.16f) { meterHeights[i] += (0.15f - meterHeights[i]) * 0.3f; changed = true; }
                if (changed) Invalidate(new Rectangle(0, 84, Width, 26));
            }
        }

        // ---------------- painting ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Ink);

            bool playing = state == PlaybackState.Playing || state == PlaybackState.Buffering;
            bool error = state == PlaybackState.Error;
            cardRect = new Rectangle(2, 2, Width - 5, Height - 5);
            var cardF = new RectangleF(cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height);

            // glow behind a playing card
            if (playing)
            {
                using var glow = new GraphicsPath();
                glow.AddPath(Theme.RoundedRect(RectangleF.Inflate(cardF, 2, 2), 15), false);
                using var gb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(60, Theme.Amber), SurroundColors = new[] { Color.FromArgb(0, Theme.Amber) } };
                g.FillPath(gb, glow);
            }

            // card surface
            Theme.FillRounded(g, cardF, 13, playing ? Blend(Theme.Surface, Theme.Amber, 0.05f) : Theme.Surface);
            Theme.DrawRounded(g, cardF, 13, playing ? Color.FromArgb(150, Theme.Amber) : (hovering ? Theme.LineHi : Theme.Line), 1.2f);

            // left accent bar
            var accent = playing ? Theme.Amber : error ? Theme.Ember : Color.Transparent;
            if (accent != Color.Transparent)
            {
                using var ab = new SolidBrush(accent);
                using var ap = Theme.RoundedRect(new RectangleF(cardF.X + 1, cardF.Y + 10, 3, cardF.Height - 20), 1.5f);
                g.FillPath(ab, ap);
            }

            int padL = 18, padR = Width - 18;

            // ---- status row ----
            Color dotColor = playing ? Theme.Amber : error ? Theme.Ember : Theme.Blue;
            string statusLabel = StatusWord(state);
            var dotY = 20;
            using (var db = new SolidBrush(dotColor)) g.FillEllipse(db, padL, dotY - 4, 8, 8);
            if (playing) DrawGlowDot(g, padL + 4, dotY, Theme.Amber);
            DrawText(g, statusLabel, Theme.Label, playing ? Theme.Amber : error ? Theme.Ember : Color.FromArgb(0xA7, 0xB4, 0xC8), padL + 15, dotY - 8);

            // format / group pill (right)
            string pillText = descriptor?.IsGroup == true ? "Gruppe"
                : playing ? Theme.CurrentFormatLabel
                : error ? "erneut verbinden" : "bereit";
            Color pillFg = descriptor?.IsGroup == true ? Color.FromArgb(0xA7, 0xB4, 0xC8)
                : playing ? Theme.Amber : Theme.Slate;
            DrawPill(g, pillText, pillFg, playing, padR, dotY);

            // ---- identity row ----
            int icoSize = 36, icoY = 44;
            DrawDeviceIcon(g, new Rectangle(padL, icoY, icoSize, icoSize), playing);
            int nameX = padL + icoSize + 12;
            // playing time (right of name row)
            if (playingSince.HasValue)
            {
                var span = DateTime.Now - playingSince.Value;
                string t = span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"mm\:ss");
                var sz = g.MeasureString(t, Theme.Data);
                DrawText(g, t, Theme.Data, Theme.Slate, padR - sz.Width, icoY + 2);
            }
            DrawText(g, deviceName, Theme.Name, Theme.Ivory, nameX, icoY + 1);
            string sub = Subtitle();
            if (!string.IsNullOrEmpty(sub))
                DrawText(g, sub, Theme.Small, Theme.Slate, nameX, icoY + 22);

            // ---- level meter ----
            int meterY = 92, meterH = 20, barW = 4, gap = 3, meterX = padL;
            for (int i = 0; i < meterHeights.Length; i++)
            {
                float h = Math.Max(3, meterHeights[i] * meterH);
                var r = new RectangleF(meterX + i * (barW + gap), meterY + (meterH - h), barW, h);
                Color c = playing ? Theme.Amber : Color.FromArgb(0x24, 0x2C, 0x36);
                using var bb = new SolidBrush(playing ? Blend(Theme.AmberDim, Theme.Amber, meterHeights[i]) : c);
                using var bp = Theme.RoundedRect(r, 1.5f);
                g.FillPath(bb, bp);
            }

            // ---- control row ----
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
            DrawText(g, pct, Theme.Small, muted ? Theme.Amber : Theme.Slate, sliderRect.Right + 8, ctlY + 6);

            overflowRect = new Rectangle(padR - ovW, ctlY, ovW, 28);
            DrawOverflow(g, overflowRect);
        }

        private string Subtitle()
        {
            if (descriptor?.IsGroup == true) return "Multiroom-Gruppe";
            var sc = statusText?.Trim();
            if (state == PlaybackState.Error && !string.IsNullOrEmpty(sc)) return sc!;
            if (!string.IsNullOrEmpty(sc)) return sc!;
            return descriptor?.Id != null ? "Cast-Gerät" : string.Empty;
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

        private void DrawPill(Graphics g, string text, Color fg, bool amber, int rightEdge, int centerY)
        {
            var sz = g.MeasureString(text, Theme.Label);
            var w = sz.Width + 18; var h = 19f;
            var r = new RectangleF(rightEdge - w, centerY - 4 - h / 2 + 4, w, h);
            if (amber) { Theme.FillRounded(g, r, h / 2, Theme.AmberSoft); Theme.DrawRounded(g, r, h / 2, Color.FromArgb(72, Theme.Amber)); }
            else Theme.DrawRounded(g, r, h / 2, Theme.Line);
            DrawText(g, text, Theme.Label, fg, r.X + 9, r.Y + 2.5f);
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
            Theme.FillRounded(g, r, 9, Theme.Ink2);
            Theme.DrawRounded(g, r, 9, playing ? Color.FromArgb(90, Theme.Amber) : Theme.Line);
            using var pen = new Pen(playing ? Theme.Amber : Theme.Slate, 1.6f);
            int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (descriptor?.IsGroup == true)
            {
                g.DrawEllipse(pen, cx - 8, cy - 5, 8, 8);
                g.DrawEllipse(pen, cx, cy - 5, 8, 8);
            }
            else
            {
                // speaker: rounded body + cone
                var body = new Rectangle(cx - 6, cy - 9, 12, 18);
                using var bp = Theme.RoundedRect(body, 3);
                g.DrawPath(pen, bp);
                g.DrawEllipse(pen, cx - 3, cy, 6, 6);
            }
        }

        private void DrawPlayButton(Graphics g, Rectangle r, bool playing)
        {
            var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            if (playing) { Theme.FillRounded(g, rf, 8, Theme.Amber); }
            else { Theme.FillRounded(g, rf, 8, Theme.Ink2); Theme.DrawRounded(g, rf, 8, Theme.Line); }
            var col = playing ? Color.FromArgb(0x19, 0x13, 0x08) : Theme.Ivory;
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

            // dimmed region above the per-speaker cap
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

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
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
