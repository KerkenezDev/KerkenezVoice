using System;
using System.IO;
using NAudio.Wave;

namespace KokoroGUI.Services
{
    public class AudioPlaybackService : IDisposable
    {
        private WaveOutEvent? _waveOut;
        private WaveStream? _currentStream;
        private readonly object _lock = new();

        public event Action? PlaybackStopped;

        public void Play(string filePath, bool blocking = false)
        {
            Stop();

            if (!File.Exists(filePath)) return;

            lock (_lock)
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    var ms = new MemoryStream(fileBytes);
                    _currentStream = new WaveFileReader(ms);

                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_currentStream);
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    _waveOut.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Playback error: {ex.Message}");
                    return;
                }
            }

            if (blocking)
            {
                while (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public void PlayAudioData(float[] samples, int sampleRate = 24000, bool blocking = false)
        {
            Stop();

            if (samples == null || samples.Length == 0) return;

            byte[] byteBuffer = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)Math.Clamp((int)(samples[i] * 32767.0f), short.MinValue, short.MaxValue);
                byteBuffer[i * 2] = (byte)(sample & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            var ms = new MemoryStream(byteBuffer);
            var waveFormat = new WaveFormat(sampleRate, 16, 1);
            var rawStream = new RawSourceWaveStream(ms, waveFormat);

            lock (_lock)
            {
                try
                {
                    _currentStream = rawStream;
                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_currentStream);
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    _waveOut.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PlayAudioData error: {ex.Message}");
                    return;
                }
            }

            if (blocking)
            {
                while (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_waveOut != null)
                {
                    try
                    {
                        _waveOut.PlaybackStopped -= OnPlaybackStopped;
                        _waveOut.Stop();
                        _waveOut.Dispose();
                    }
                    catch { }
                    _waveOut = null;
                }

                if (_currentStream != null)
                {
                    try
                    {
                        _currentStream.Dispose();
                    }
                    catch { }
                    _currentStream = null;
                }
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            lock (_lock)
            {
                if (_waveOut != null)
                {
                    try
                    {
                        _waveOut.PlaybackStopped -= OnPlaybackStopped;
                        _waveOut.Dispose();
                    }
                    catch { }
                    _waveOut = null;
                }

                if (_currentStream != null)
                {
                    try
                    {
                        _currentStream.Dispose();
                    }
                    catch { }
                    _currentStream = null;
                }
            }

            PlaybackStopped?.Invoke();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
