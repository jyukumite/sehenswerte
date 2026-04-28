using SehensWerte.Files;
using System.Diagnostics;
using System.Media;

namespace SehensWerte.Controls.Sehens
{
    internal class TraceViewAudioPlayback : IDisposable
    {
        private SoundPlayer? m_Player;
        private Stopwatch m_Stopwatch = new Stopwatch();
        private System.Threading.Timer? m_Timer;
        private readonly int m_StartSample;
        private readonly int m_LengthSamples;
        private readonly double m_SamplesPerSecond;
        private readonly Action m_OnTick;
        private readonly Action m_OnFinished;
        private readonly object m_Lock = new object();
        private bool m_Playing;

        public bool IsPlaying { get { lock (m_Lock) { return m_Playing; } } }

        public int CurrentSampleNumber
        {
            get
            {
                lock (m_Lock)
                {
                    if (!m_Playing) return -1;
                    int progress = (int)(m_Stopwatch.Elapsed.TotalSeconds * m_SamplesPerSecond);
                    if (progress > m_LengthSamples) progress = m_LengthSamples;
                    return m_StartSample + progress;
                }
            }
        }

        public TraceViewAudioPlayback(double[] samples, int startSample, double samplesPerSecond, Action onTick, Action onFinished)
        {
            m_StartSample = startSample;
            m_LengthSamples = samples.Length;
            m_SamplesPerSecond = samplesPerSecond;
            m_OnTick = onTick;
            m_OnFinished = onFinished;

            short[] pcm = samples.Select(x =>
            {
                int s = (int)Math.Round(x * 32768.0);
                return (short)((s < -32768) ? -32768 : ((s > 32767) ? 32767 : s));
            }).ToArray();

            Stream stream = RiffWriter.ToStream(pcm, (int)samplesPerSecond);
            m_Player = new SoundPlayer(stream);
            m_Player.Load();
        }

        public void Play()
        {
            lock (m_Lock)
            {
                if (m_Playing || m_Player == null) return;
                m_Playing = true;
                m_Player.Play();
                m_Stopwatch.Restart();
                double durationMs = m_LengthSamples * 1000.0 / m_SamplesPerSecond;
                m_Timer = new System.Threading.Timer(_ => OnTick(durationMs), null, 0, 33);
            }
        }

        private void OnTick(double durationMs)
        {
            bool finished;
            lock (m_Lock)
            {
                if (!m_Playing) return;
                finished = m_Stopwatch.Elapsed.TotalMilliseconds >= durationMs;
            }
            m_OnTick();
            if (finished)
            {
                Stop();
                m_OnFinished();
            }
        }

        public void Stop()
        {
            SoundPlayer? player;
            System.Threading.Timer? timer;
            lock (m_Lock)
            {
                if (!m_Playing) return;
                m_Playing = false;
                m_Stopwatch.Stop();
                player = m_Player;
                timer = m_Timer;
                m_Timer = null;
            }
            try { player?.Stop(); } catch { }
            timer?.Dispose();
        }

        public void Dispose()
        {
            Stop();
            SoundPlayer? player;
            lock (m_Lock)
            {
                player = m_Player;
                m_Player = null;
            }
            player?.Dispose();
        }
    }
}
