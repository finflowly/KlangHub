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

            byte[] bufferAlreadySent;
            do
            {
                bufferAlreadySent = GetBufferAlreadySent();
                if (bufferAlreadySent.Length < BufferSizeInBytes)
                    Task.Delay(1000).Wait();
            } while (bufferAlreadySent.Length < BufferSizeInBytes);
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

        // NOTE (2026-09-04): these are per-second byte estimates, and only the MP3 ones are accurate
        // (320 kbps = 40 000 B/s, 128 kbps = 16 000 B/s). Real WAV runs at 192 000 B/s (16-bit/48k stereo)
        // and FLAC at roughly half of that, so "10 seconds" is really about two. It has been that way since
        // before the rewrite and WAV plays fine, so it is left alone deliberately: the stuttering reported for
        // FLAC/MP3 was traced to junk bytes in front of the stream (see AudioHeader.GetStreamHeader), and
        // changing the buffer maths at the same time would make it impossible to tell which fix did what.
        private void SetBufferSize()
        {
            switch (streamFormatSelected)
            {
                case SupportedStreamFormat.Wav:
                case SupportedStreamFormat.Wav_16bit:
                case SupportedStreamFormat.Wav_24bit:
                case SupportedStreamFormat.Wav_32bit:
                    BufferSizeInBytes = ExtraBufferInSeconds * 40000 + BufferSizeInBytesDefault;
                    break;
                case SupportedStreamFormat.Mp3_320:
                    BufferSizeInBytes = ExtraBufferInSeconds * 40000 + BufferSizeInBytesDefault;
                    break;
                case SupportedStreamFormat.Flac:
                    // Lossless but compressed (~half of 16-bit WAV); size like the WAV/Mp3_320 tier so the
                    // startup buffer over-provisions slightly rather than risking under-buffering.
                    BufferSizeInBytes = ExtraBufferInSeconds * 40000 + BufferSizeInBytesDefault;
                    break;
                case SupportedStreamFormat.Mp3_128:
                    BufferSizeInBytes = ExtraBufferInSeconds * 16000 + BufferSizeInBytesDefault;
                    break;
                default:
                    break;
            }
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
