using KlangHub.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KlangHub.Classes
{
    public class ApplicationBuffer
    {
        private readonly List<ApplicationBufferItem> applicationBuffer = new List<ApplicationBufferItem>();
        private AudioFormat waveFormat = new AudioFormat(44100, 16, 2);
        private int reduceLagThreshold;
        private SupportedStreamFormat streamFormatSelected;
        private bool startBufferSend;

        /// <summary>Head-room the ring buffer always keeps on top of the user's setting.</summary>
        private const double BaseBufferSeconds = 2.0;

        /// <summary>
        /// How long the first byte may be held back while the start-up buffer fills. The buffer is a cushion
        /// for the receiver, but a receiver that gets nothing at all gives up and reloads - which is what it
        /// did for minutes on MP3, where the old byte-based threshold meant waiting for half a minute of
        /// audio. Whatever has accumulated by this deadline is sent, and the rest simply follows.
        /// </summary>
        private const double MaxStartupWaitSeconds = 4.0;

        /// <summary>
        /// The largest cushion that can actually be delivered. StreamingConnection hands a block to a
        /// fixed-size buffer whose Add is all-or-nothing, so a cushion bigger than that block is not merely
        /// trimmed - it is dropped entirely, and the receiver starts with nothing at all. That is the
        /// "spinner goes round, then it reloads" symptom, arrived at from the opposite direction.
        /// Twelve seconds of stereo 24-bit is 3.5 MB and fits easily; 5.1 at 32-bit would not.
        /// </summary>
        private const double MaxStartupBytes = KlangHub.Streaming.StreamingConnection.StreamBufferBytes * 0.8;

        private const double BufferSizeInBytesDefault = 350000;
        private double BufferSizeInBytes = BufferSizeInBytesDefault;
        private int ExtraBufferInSeconds = 0;

        /// <summary>
        /// Send the startup buffer, a buffer containing the past x seconds.
        /// </summary>
        /// <param name="device"></param>
        public void SendStartupBuffer(IDevice device, SupportedStreamFormat streamFormatIn)
        {
            if (device == null)
                return;

            streamFormatSelected = streamFormatIn;
            SetBufferSize();

            // Wait for the cushion, but never longer than MaxStartupWaitSeconds: hand the receiver what is
            // there and let the rest stream in behind it. Polling every 100 ms rather than every second keeps
            // the wait from rounding up to whole seconds on top of that.
            var deadline = DateTime.UtcNow.AddSeconds(MaxStartupWaitSeconds);
            byte[] bufferAlreadySent = GetBufferAlreadySent();
            while (bufferAlreadySent.Length < BufferSizeInBytes && DateTime.UtcNow < deadline)
            {
                Task.Delay(100).Wait();
                bufferAlreadySent = GetBufferAlreadySent();
            }

            device.OnRecordingDataAvailable(bufferAlreadySent, waveFormat, reduceLagThreshold, streamFormatSelected);
            startBufferSend = true;
        }

        /// <summary>
        /// Return if the satrt buffer is send.
        /// </summary>
        public bool IsStartBufferSend()
        {
            return startBufferSend;
        }

        /// <summary>
        /// Set the extra buffer value.
        /// </summary>
        /// <param name="extraBufferInSecondsIn"></param>
        public void SetExtraBufferInSeconds(int extraBufferInSecondsIn)
        {
            ExtraBufferInSeconds = extraBufferInSecondsIn;
        }

        /// <summary>
        /// Add bytes to the application buffer.
        /// </summary>
        public void AddToBuffer(byte[] dataToSend, AudioFormat formatIn, int reduceLagThresholdIn, SupportedStreamFormat streamFormatIn)
        {
            waveFormat = formatIn;
            reduceLagThreshold = reduceLagThresholdIn;
            streamFormatSelected = streamFormatIn;
            KeepABuffer(dataToSend);
            SetBufferSize();
        }

        /// <summary>
        /// Sizes the ring buffer in SECONDS of the selected format, using that format's real byte rate
        /// (<see cref="StreamRate"/>). The old code used two constants for every format, so "10 seconds"
        /// meant two seconds of WAV - and thirty-two seconds of MP3 128, which no receiver waits for.
        /// </summary>
        private void SetBufferSize()
            => BufferSizeInBytes = StartupBufferBytes(waveFormat, streamFormatSelected, ExtraBufferInSeconds);

        /// <summary>Pure, so the floor and the ceiling can be tested without a device.</summary>
        internal static double StartupBufferBytes(AudioFormat format, SupportedStreamFormat streamFormat, int extraSeconds)
        {
            double wanted = StreamRate.BytesForSeconds(format, streamFormat, extraSeconds + BaseBufferSeconds);
            return Math.Clamp(
                wanted,
                BufferSizeInBytesDefault / 4,   // a floor, so a tiny format still buffers something
                MaxStartupBytes);               // a ceiling, so what we build can actually be delivered
        }

        /// <summary>
        /// Clear the buffer.
        /// </summary>
        public void ClearBuffer()
        {
            if (applicationBuffer == null)
                return;

            lock (applicationBuffer)
            {
                applicationBuffer.Clear();
            }
            startBufferSend = false;
        }

        /// <summary>
        /// Keep an application buffer.
        /// </summary>
        /// <param name="dataToSend"></param>
        private void KeepABuffer(byte[] dataToSend)
        {
            if (applicationBuffer == null)
                return;

            lock (applicationBuffer)
            {
                applicationBuffer.Insert(0, new ApplicationBufferItem { Data = dataToSend });

                while (applicationBuffer.Sum(x => x.Data.Length) - applicationBuffer.Last().Data.Length > BufferSizeInBytes)
                {
                    applicationBuffer.RemoveAt(applicationBuffer.Count - 1);
                }
            }
        }

        /// <summary>
        /// Get the data in the application buffer that's already send to the devices.
        /// </summary>
        private byte[] GetBufferAlreadySent()
        {
            if (applicationBuffer == null)
                return new byte[0];

            IEnumerable<byte> buffer = new List<byte>();
            lock (applicationBuffer)
            {
                for (int i = applicationBuffer.Count - 1; i >= 0; i--)
                {
                    buffer = buffer.Concat(applicationBuffer[i].Data);
                }
            }

            return buffer.ToArray();
        }
    }
}
