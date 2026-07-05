using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.Classes;
using KlangHub.Communication;
using KlangHub.Communication.Classes;

namespace KlangHub.UserControls
{
    public partial class DeviceControl : UserControl
    {
        private readonly Func<IPlaybackSession> sessionAccessor;
        private readonly IPlaybackSession? session;
        private readonly CastDeviceDescriptor? descriptor;

        public DeviceControl(Func<IPlaybackSession> sessionAccessorIn)
        {
            InitializeComponent();
            sessionAccessor = sessionAccessorIn;

            // 2.2b-4.5/4.6: DeviceControl is a pure neutral-session consumer. Resolve the stable session
            // once, derive its descriptor for identity, subscribe to observation events, and unsubscribe
            // on Disposed so Device cannot hold a delegate to a disposed control.
            try { session = sessionAccessorIn?.Invoke(); }
            catch (InvalidOperationException) { session = null; }
            descriptor = session?.Device;
            if (session != null)
            {
                session.StateChanged += OnSessionStateChanged;
                session.VolumeChanged += OnSessionVolumeChanged;
                Disposed += (s, e) =>
                {
                    session.StateChanged -= OnSessionStateChanged;
                    session.VolumeChanged -= OnSessionVolumeChanged;
                };
                SetDeviceName(descriptor!.Name);                    // initial name + group
                RenderStatus(session.State, session.StatusText);   // initial status (volume on first event)
            }

            btnDevice.FlatAppearance.MouseOverBackColor = btnDevice.BackColor;
            btnDevice.BackColorChanged += (s, e) =>
            {
                btnDevice.FlatAppearance.MouseOverBackColor = btnDevice.BackColor;
            };
        }

        public void SetDeviceName(string name)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetDeviceName), new object[] { name });
                return;
            }

            btnDevice.Text = name;
            pictureGroup.Visible = descriptor?.IsGroup ?? false;
            toolTipGroup.SetToolTip(pictureGroup, Properties.Strings.Tooltip_Group_Text);
        }

        public string GetDeviceName()
        {
            return btnDevice.Text;
        }

        // 2.2b-4.7: neutral identity exposed for MainForm-owned filtering and removal.
        public bool IsGroup => descriptor?.IsGroup ?? false;
        public string? Id => descriptor?.Id;

        // 2.2b-4.5: neutral status observation, fed by the session's StateChanged event.
        private void OnSessionStateChanged(object? sender, PlaybackState state)
            => RenderStatus(state, session?.StatusText ?? string.Empty);

        // Renders from the neutral PlaybackState (+ StatusText). Documented fidelity loss vs the old
        // DeviceState rendering: LoadingMediaCheckFirewall -> Loading loses MistyRose (the firewall hint
        // survives as text via StatusText), and LoadCancelled -> Idle loses PeachPuff. Colours collapse
        // to: playing/buffering = PaleGreen, error = PeachPuff, everything else = LightGray.
        private void RenderStatus(PlaybackState state, string statusText)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<PlaybackState, string>(RenderStatus), new object[] { state, statusText });
                return;
            }

            lblStatus.Text = $"{Resource.Get(state.ToString())} {statusText}".Trim();

            switch (state)
            {
                case PlaybackState.Buffering:
                case PlaybackState.Playing:
                    SetBackColor(Color.PaleGreen);
                    picturePlayPause.Image = Properties.Resources.Stop;
                    break;
                case PlaybackState.Error:
                    SetBackColor(Color.PeachPuff);
                    picturePlayPause.Image = Properties.Resources.Play;
                    break;
                default:
                    SetBackColor(Color.LightGray);
                    picturePlayPause.Image = Properties.Resources.Play;
                    break;
            }
        }

        public void SetBackColor(Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Color>(SetBackColor), new object[] { color });
                return;
            }

            BackColor = color;
            btnDevice.BackColor = color;
            lblStatus.BackColor = color;
            Update();
        }

        // 2.2b-4.5: neutral volume observation, fed by the session's VolumeChanged event.
        private void OnSessionVolumeChanged(object? sender, VolumeStatus volume) => RenderVolume(volume);

        private void RenderVolume(VolumeStatus volume)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                Invoke(new Action<VolumeStatus>(RenderVolume), new object[] { volume });
                return;
            }

            trbVolume.Value = (int)(volume.Level * 100);
            trbVolume.Enabled = true;
            pictureVolumeMute.Enabled = true;
            int change = (int)(volume.StepInterval * 100);
            if (change < 1) change = 1;   // TrackBar requires >= 1
            trbVolume.LargeChange = change;
            trbVolume.SmallChange = change;
            trbVolume.TickFrequency = change;
            pictureVolumeMute.Image = volume.Muted ? Properties.Resources.Mute : Properties.Resources.Unmute;
        }

        private void TrbVolume_Scroll(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // 2.2b-4.4b: volume via the neutral session (SetVolume == VolumeSet, 1:1).
            TryOnSession(s => s.SetVolume(trbVolume.Value / 100f));
        }

        private void PictureVolumeMute_Click(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // 2.2b-4.4b: mute toggle via the neutral session. Volume.Muted is the lossless bool the old
            // device.VolumeMute() negated, so SetMuted(!Muted) is behaviour-equivalent.
            TryOnSession(s => s.SetMuted(!s.Volume.Muted));
        }

        // Run an action on this device's freshly-resolved session. No provider (designer) or a device
        // that left the registry (CreateSession throws) -> no-op, matching the old disposed-device guard.
        private void TryOnSession(Func<IPlaybackSession, Task> action)
        {
            if (sessionAccessor == null)
                return;
            try { _ = action(sessionAccessor()); }   // 2.2b-M2: fire-and-forget (Chromecast completes synchronously)
            catch (InvalidOperationException) { }
        }

        private void DeviceControl_MouseDown(object sender, MouseEventArgs e)
        {
            var control = sender as Control;
            control!.DoDragDrop(control, DragDropEffects.Move);
        }

        private void DeviceChildControl_MouseDown(object sender, MouseEventArgs e)
        {
            var control = sender as Control;
            control!.DoDragDrop(control.Parent!, DragDropEffects.Move);
        }

        private void DeviceControl_DragOver(object sender, DragEventArgs e)
        {
            if (e == null)
                return;

            e.Effect = DragDropEffects.All;
        }

        private void DeviceControl_DragDrop(object sender, DragEventArgs e)
        {
            if (sender == null || !(sender is DeviceControl))
                return;

            ((IMainForm)((DeviceControl)sender).ParentForm!).DoDragDrop(sender, e);
        }

        private void BtnDevicePlay_Click(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // 2.2b-4.4c: play/stop toggle via the neutral session (TogglePlayStop == OnClickPlayStop, 1:1).
            TryOnSession(s => s.TogglePlayStop());
        }
    }
}
