using System;
using System.Drawing;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.Classes;
using KlangHub.Communication;
using KlangHub.Communication.Classes;

namespace KlangHub.UserControls
{
    public partial class DeviceControl : UserControl
    {
        private readonly IDevice device;
        private readonly Func<IPlaybackSession> sessionAccessor;
        private readonly IPlaybackSession session;
        private Action PlayPause_Click;

        public DeviceControl(IDevice deviceIn, Func<IPlaybackSession> sessionAccessorIn = null)
        {
            InitializeComponent();
            device = deviceIn;
            sessionAccessor = sessionAccessorIn;

            // 2.2b-4.5: observe the device via the neutral session. Subscribe once to the stable session
            // and unsubscribe on Disposed so Device cannot hold a delegate to a disposed control.
            try { session = sessionAccessorIn?.Invoke(); }
            catch (InvalidOperationException) { session = null; }
            if (session != null)
            {
                session.StateChanged += OnSessionStateChanged;
                session.VolumeChanged += OnSessionVolumeChanged;
                Disposed += (s, e) =>
                {
                    session.StateChanged -= OnSessionStateChanged;
                    session.VolumeChanged -= OnSessionVolumeChanged;
                };
                RenderStatus(session.State, session.StatusText);   // initial render (volume renders on first event)
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
            pictureGroup.Visible = device.IsGroup();
            toolTipGroup.SetToolTip(pictureGroup, Properties.Strings.Tooltip_Group_Text);
        }

        public string GetDeviceName()
        {
            return btnDevice.Text;
        }

        // 2.2b-4.5: neutral status observation, fed by the session's StateChanged event.
        private void OnSessionStateChanged(object sender, PlaybackState state)
            => RenderStatus(state, session?.StatusText ?? string.Empty);

        // Renders from the neutral PlaybackState (+ StatusText). Documented fidelity loss vs the old
        // DeviceState rendering: LoadingMediaCheckFirewall -> Loading loses MistyRose (the firewall hint
        // survives as text via StatusText), and LoadCancelled -> Idle loses PeachPuff. Colours collapse
        // to: playing/buffering = PaleGreen, error = PeachPuff, everything else = LightGray.
        private void RenderStatus(PlaybackState state, string statusText)
        {
            if (device == null || device.IsDisposed())
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

        public void SetClickCallBack(Action playPauseAction)
        {
            PlayPause_Click = playPauseAction;
        }

        // 2.2b-4.5: neutral volume observation, fed by the session's VolumeChanged event.
        private void OnSessionVolumeChanged(object sender, VolumeStatus volume) => RenderVolume(volume);

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
            if (device == null || device.IsDisposed())
                return;

            // 2.2b-4.4b: volume via the neutral session (SetVolume == VolumeSet, 1:1).
            if (sessionAccessor != null)
                TryOnSession(s => s.SetVolume(trbVolume.Value / 100f));
            else
                device.VolumeSet(trbVolume.Value / 100f);
        }

        private void PictureVolumeMute_Click(object sender, EventArgs e)
        {
            if (device == null || device.IsDisposed())
                return;

            // 2.2b-4.4b: mute toggle via the neutral session. Volume.Muted is the same lossless bool
            // that device.VolumeMute() negates, so SetMuted(!Muted) is behaviour-equivalent.
            if (sessionAccessor != null)
                TryOnSession(s => s.SetMuted(!s.Volume.Muted));
            else
                device.VolumeMute();
        }

        // Resolve this device's session and run an action on it. A device that has left the registry
        // makes CreateSession throw; we swallow that to a no-op, matching the disposed-device guard above.
        private void TryOnSession(Action<IPlaybackSession> action)
        {
            try { action(sessionAccessor()); }
            catch (InvalidOperationException) { /* device gone -> no-op */ }
        }

        private void DeviceControl_MouseDown(object sender, MouseEventArgs e)
        {
            var control = sender as Control;
            control.DoDragDrop(control, DragDropEffects.Move);
        }

        private void DeviceChildControl_MouseDown(object sender, MouseEventArgs e)
        {
            var control = sender as Control;
            control.DoDragDrop(control.Parent, DragDropEffects.Move);
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

            ((IMainForm)((DeviceControl)sender).ParentForm).DoDragDrop(sender, e);
        }

        private void BtnDevicePlay_Click(object sender, EventArgs e)
        {
            if (device == null || device.IsDisposed())
                return;

            // 2.2b-4.4c: play/stop toggle via the neutral session (TogglePlayStop == OnClickPlayStop, 1:1).
            if (sessionAccessor != null)
                TryOnSession(s => s.TogglePlayStop());
            else
                PlayPause_Click();   // fallback: unchanged callback
        }
    }
}
