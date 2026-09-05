using System;
using System.Collections.Generic;
using KerkenezVoice.Models;

namespace KerkenezVoice.Services
{
    public class AudioProcessingService
    {
        public const int SampleRate = 24000;

        public float[] ProcessAudio(float[] audio, AppSettings config)
        {
            if (audio == null || audio.Length == 0)
                return Array.Empty<float>();

            float[] processed = (float[])audio.Clone();

            // 1. Trim Silence
            if (config.Trim)
            {
                processed = TrimSilence(processed, 0.01f);
                if (processed.Length == 0) return processed;
            }

            // 2. Volume
            if (Math.Abs(config.Volume - 1.0) > 0.001)
            {
                processed = ApplyVolume(processed, (float)config.Volume);
            }

            // 3. Pitch Shift via Resampling
            if (Math.Abs(config.Pitch) > 0.01)
            {
                processed = PitchShiftResample(processed, config.Pitch);
            }

            // 4. Pedalboard FX suite
            if (config.ApplyFx)
            {
                // Distortion
                if (config.DistortionEnabled)
                {
                    processed = ApplyDistortion(processed, config.DistortionDrive);
                }

                // Chorus
                if (config.ChorusEnabled)
                {
                    processed = ApplyChorus(processed, SampleRate, config.ChorusRate, config.ChorusDepth, config.ChorusMix);
                }

                // Phaser
                if (config.PhaserEnabled)
                {
                    processed = ApplyPhaser(processed, SampleRate, config.PhaserRate, config.PhaserDepth, config.PhaserMix);
                }

                // Clipping
                if (config.ClippingEnabled)
                {
                    processed = ApplyClipping(processed, config.ClippingThresh);
                }

                // Bitcrush
                if (config.BitcrushEnabled)
                {
                    processed = ApplyBitcrush(processed, config.BitcrushDepth);
                }

                // GSM Full Rate Compressor / Phone Quality
                if (config.GsmEnabled)
                {
                    processed = ApplyGsm(processed, SampleRate);
                }

                // Highpass Filter
                if (config.HighpassEnabled)
                {
                    processed = ApplyHighPass(processed, SampleRate, config.HighpassFreq);
                }

                // Lowpass Filter
                if (config.LowpassEnabled)
                {
                    processed = ApplyLowPass(processed, SampleRate, config.LowpassFreq);
                }

                // EQ Bass (LowShelf at 250Hz)
                if (Math.Abs(config.EqBass) > 0.01)
                {
                    processed = ApplyLowShelf(processed, SampleRate, 250.0, config.EqBass);
                }

                // EQ Treble (HighShelf at 4000Hz)
                if (Math.Abs(config.EqTreble) > 0.01)
                {
                    processed = ApplyHighShelf(processed, SampleRate, 4000.0, config.EqTreble);
                }

                // Pitch Shift (High Quality without duration change)
                if (config.PitchShiftEnabled && Math.Abs(config.PitchShiftSemitones) > 0.01)
                {
                    processed = PitchShiftResample(processed, config.PitchShiftSemitones);
                }

                // Delay
                if (config.DelayEnabled)
                {
                    processed = ApplyDelay(processed, SampleRate, config.DelayTime, config.DelayFeedback, config.DelayMix);
                }

                // Reverb
                if (config.ReverbEnabled)
                {
                    processed = ApplyReverb(processed, SampleRate, config.ReverbRoomSize, config.ReverbDamping, config.ReverbWetLevel, config.ReverbDryLevel, config.ReverbWidth);
                }

                // Compressor
                if (config.CompEnabled)
                {
                    processed = ApplyCompressor(processed, SampleRate, config.CompThreshold, config.CompRatio, config.CompAttack, config.CompRelease);
                }

                // Limiter
                if (config.LimiterEnabled)
                {
                    processed = ApplyLimiter(processed, SampleRate, config.LimiterThreshold, config.LimiterRelease);
                }

                // Gain
                if (config.GainEnabled && Math.Abs(config.GainDb) > 0.01)
                {
                    processed = ApplyGain(processed, config.GainDb);
                }
            }

            // 5. Normalization
            if (config.Normalize)
            {
                processed = Normalize(processed, 0.98f);
            }

            return processed;
        }

        public static float[] TrimSilence(float[] audio, float threshold = 0.01f)
        {
            int start = 0;
            while (start < audio.Length && Math.Abs(audio[start]) < threshold)
            {
                start++;
            }

            int end = audio.Length - 1;
            while (end >= start && Math.Abs(audio[end]) < threshold)
            {
                end--;
            }

            if (start > end) return Array.Empty<float>();

            int length = end - start + 1;
            float[] trimmed = new float[length];
            Array.Copy(audio, start, trimmed, 0, length);
            return trimmed;
        }

        public static float[] ApplyVolume(float[] audio, float volume)
        {
            float[] result = new float[audio.Length];
            for (int i = 0; i < audio.Length; i++)
            {
                result[i] = audio[i] * volume;
            }
            return result;
        }

        public static float[] PitchShiftResample(float[] audio, double semitones)
        {
            if (Math.Abs(semitones) < 0.001 || audio.Length == 0)
                return audio;

            double factor = Math.Pow(2.0, semitones / 12.0);
            int newLength = (int)Math.Max(1, Math.Round(audio.Length / factor));
            float[] resampled = new float[newLength];

            for (int i = 0; i < newLength; i++)
            {
                double srcIdx = i * factor;
                int idx0 = (int)Math.Floor(srcIdx);
                int idx1 = Math.Min(idx0 + 1, audio.Length - 1);
                double frac = srcIdx - idx0;

                if (idx0 >= audio.Length)
                {
                    resampled[i] = audio[^1];
                }
                else
                {
                    resampled[i] = (float)((1.0 - frac) * audio[idx0] + frac * audio[idx1]);
                }
            }

            return resampled;
        }

        public static float[] ApplyDistortion(float[] audio, double driveDb)
        {
            float[] output = new float[audio.Length];
            double drive = Math.Pow(10.0, driveDb / 20.0);

            for (int i = 0; i < audio.Length; i++)
            {
                double x = audio[i] * drive;
                output[i] = (float)Math.Tanh(x);
            }
            return output;
        }

        public static float[] ApplyClipping(float[] audio, double threshDb)
        {
            float[] output = new float[audio.Length];
            float limit = (float)Math.Pow(10.0, threshDb / 20.0);

            for (int i = 0; i < audio.Length; i++)
            {
                output[i] = Math.Clamp(audio[i], -limit, limit);
            }
            return output;
        }

        public static float[] ApplyBitcrush(float[] audio, double bitDepth)
        {
            float[] output = new float[audio.Length];
            double levels = Math.Pow(2.0, Math.Max(2.0, bitDepth) - 1.0);
            double step = 1.0 / levels;

            for (int i = 0; i < audio.Length; i++)
            {
                output[i] = (float)(Math.Round(audio[i] / step) * step);
            }
            return output;
        }

        public static float[] ApplyGsm(float[] audio, int sampleRate)
        {
            // Bandpass filter 300Hz - 3400Hz phone quality + soft compression
            var filtered = ApplyHighPass(audio, sampleRate, 300);
            filtered = ApplyLowPass(filtered, sampleRate, 3400);

            float[] output = new float[filtered.Length];
            for (int i = 0; i < filtered.Length; i++)
            {
                float x = filtered[i];
                float sign = Math.Sign(x);
                float abs = Math.Abs(x);
                // Non-linear mu-law / A-law style curve
                output[i] = sign * (float)(Math.Log(1.0 + 255.0 * abs) / Math.Log(256.0));
            }
            return output;
        }

        public static float[] ApplyHighPass(float[] audio, int sampleRate, double cutoffHz)
        {
            double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
            double cosw0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * 0.707); // Q = 0.707 Butterworth

            double b0 = (1.0 + cosw0) / 2.0;
            double b1 = -(1.0 + cosw0);
            double b2 = (1.0 + cosw0) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cosw0;
            double a2 = 1.0 - alpha;

            return ApplyBiquad(audio, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static float[] ApplyLowPass(float[] audio, int sampleRate, double cutoffHz)
        {
            double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
            double cosw0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * 0.707);

            double b0 = (1.0 - cosw0) / 2.0;
            double b1 = 1.0 - cosw0;
            double b2 = (1.0 - cosw0) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cosw0;
            double a2 = 1.0 - alpha;

            return ApplyBiquad(audio, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static float[] ApplyLowShelf(float[] audio, int sampleRate, double cutoffHz, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
            double cosw0 = Math.Cos(w0);
            double sinw0 = Math.Sin(w0);
            double alpha = sinw0 / 2.0 * Math.Sqrt((A + 1.0 / A) * (1.0 / 0.707 - 1.0) + 2.0);
            double sqrtA2 = 2.0 * Math.Sqrt(A) * alpha;

            double b0 = A * ((A + 1.0) - (A - 1.0) * cosw0 + sqrtA2);
            double b1 = 2.0 * A * ((A - 1.0) - (A + 1.0) * cosw0);
            double b2 = A * ((A + 1.0) - (A - 1.0) * cosw0 - sqrtA2);
            double a0 = (A + 1.0) + (A - 1.0) * cosw0 + sqrtA2;
            double a1 = -2.0 * ((A - 1.0) + (A + 1.0) * cosw0);
            double a2 = (A + 1.0) + (A - 1.0) * cosw0 - sqrtA2;

            return ApplyBiquad(audio, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static float[] ApplyHighShelf(float[] audio, int sampleRate, double cutoffHz, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
            double cosw0 = Math.Cos(w0);
            double sinw0 = Math.Sin(w0);
            double alpha = sinw0 / 2.0 * Math.Sqrt((A + 1.0 / A) * (1.0 / 0.707 - 1.0) + 2.0);
            double sqrtA2 = 2.0 * Math.Sqrt(A) * alpha;

            double b0 = A * ((A + 1.0) + (A - 1.0) * cosw0 + sqrtA2);
            double b1 = -2.0 * A * ((A - 1.0) + (A + 1.0) * cosw0);
            double b2 = A * ((A + 1.0) + (A - 1.0) * cosw0 - sqrtA2);
            double a0 = (A + 1.0) - (A - 1.0) * cosw0 + sqrtA2;
            double a1 = 2.0 * ((A - 1.0) - (A + 1.0) * cosw0);
            double a2 = (A + 1.0) - (A - 1.0) * cosw0 - sqrtA2;

            return ApplyBiquad(audio, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        private static float[] ApplyBiquad(float[] audio, double b0, double b1, double b2, double a1, double a2)
        {
            float[] output = new float[audio.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

            for (int i = 0; i < audio.Length; i++)
            {
                double x0 = audio[i];
                double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

                output[i] = (float)y0;
                x2 = x1;
                x1 = x0;
                y2 = y1;
                y1 = y0;
            }
            return output;
        }

        public static float[] ApplyChorus(float[] audio, int sampleRate, double rateHz, double depth, double mix)
        {
            float[] output = new float[audio.Length];
            int maxDelaySamples = (int)(0.03 * sampleRate); // 30ms max delay
            float[] buffer = new float[maxDelaySamples + 1];
            int writeIdx = 0;

            double baseDelay = 0.015 * sampleRate; // 15ms base
            double modDepth = depth * 0.010 * sampleRate; // 10ms swing

            for (int i = 0; i < audio.Length; i++)
            {
                buffer[writeIdx] = audio[i];

                double lfo = Math.Sin(2.0 * Math.PI * rateHz * i / sampleRate);
                double currentDelay = baseDelay + lfo * modDepth;

                double readPos = writeIdx - currentDelay;
                while (readPos < 0) readPos += buffer.Length;

                int r0 = (int)readPos;
                int r1 = (r0 + 1) % buffer.Length;
                double frac = readPos - r0;
                float delayedSample = (float)((1.0 - frac) * buffer[r0] + frac * buffer[r1]);

                output[i] = (float)((1.0 - mix) * audio[i] + mix * delayedSample);

                writeIdx = (writeIdx + 1) % buffer.Length;
            }
            return output;
        }

        public static float[] ApplyPhaser(float[] audio, int sampleRate, double rateHz, double depth, double mix)
        {
            float[] output = new float[audio.Length];
            double ap_x1 = 0, ap_y1 = 0;
            double minFreq = 400.0;
            double maxFreq = 4000.0;

            for (int i = 0; i < audio.Length; i++)
            {
                double lfo = 0.5 * (1.0 + Math.Sin(2.0 * Math.PI * rateHz * i / sampleRate));
                double freq = minFreq + lfo * (maxFreq - minFreq) * depth;

                double tan = Math.Tan(Math.PI * freq / sampleRate);
                double a1 = (tan - 1.0) / (tan + 1.0);

                double x0 = audio[i];
                double y0 = a1 * x0 + ap_x1 - a1 * ap_y1;
                ap_x1 = x0;
                ap_y1 = y0;

                output[i] = (float)((1.0 - mix) * audio[i] + mix * y0);
            }
            return output;
        }

        public static float[] ApplyDelay(float[] audio, int sampleRate, double delayTimeSeconds, double feedback, double mix)
        {
            int delaySamples = (int)(delayTimeSeconds * sampleRate);
            if (delaySamples <= 0) return audio;

            float[] output = new float[audio.Length];
            float[] delayBuffer = new float[delaySamples];
            int delayIndex = 0;

            for (int i = 0; i < audio.Length; i++)
            {
                float delayed = delayBuffer[delayIndex];
                output[i] = (float)((1.0 - mix) * audio[i] + mix * delayed);

                delayBuffer[delayIndex] = (float)(audio[i] + delayed * feedback);
                delayIndex = (delayIndex + 1) % delaySamples;
            }
            return output;
        }

        public static float[] ApplyReverb(float[] audio, int sampleRate, double roomSize, double damping, double wetLevel, double dryLevel, double width)
        {
            // Freeverb-style comb and all-pass network
            int[] combTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            int[] allpassTunings = { 556, 441, 341, 225 };

            float[][] combBuffers = new float[combTunings.Length][];
            int[] combIndices = new int[combTunings.Length];
            float[] combFilterState = new float[combTunings.Length];

            for (int c = 0; c < combTunings.Length; c++)
            {
                combBuffers[c] = new float[combTunings[c]];
            }

            float[][] allpassBuffers = new float[allpassTunings.Length][];
            int[] allpassIndices = new int[allpassTunings.Length];
            for (int a = 0; a < allpassTunings.Length; a++)
            {
                allpassBuffers[a] = new float[allpassTunings[a]];
            }

            float feedback = (float)Math.Clamp(roomSize, 0.0, 0.98);
            float damp = (float)Math.Clamp(damping, 0.0, 1.0);

            float[] output = new float[audio.Length];

            for (int i = 0; i < audio.Length; i++)
            {
                float input = audio[i];
                float combSum = 0;

                for (int c = 0; c < combTunings.Length; c++)
                {
                    float[] buf = combBuffers[c];
                    int idx = combIndices[c];
                    float outSample = buf[idx];

                    combFilterState[c] = outSample * (1.0f - damp) + combFilterState[c] * damp;
                    buf[idx] = input + combFilterState[c] * feedback;
                    combIndices[c] = (idx + 1) % buf.Length;

                    combSum += outSample;
                }

                float allpassOut = combSum;
                for (int a = 0; a < allpassTunings.Length; a++)
                {
                    float[] buf = allpassBuffers[a];
                    int idx = allpassIndices[a];
                    float bufOut = buf[idx];

                    float temp = allpassOut + bufOut * 0.5f;
                    buf[idx] = allpassOut;
                    allpassOut = bufOut - temp * 0.5f;
                    allpassIndices[a] = (idx + 1) % buf.Length;
                }

                output[i] = (float)(input * dryLevel + allpassOut * wetLevel * 0.2);
            }

            return output;
        }

        public static float[] ApplyCompressor(float[] audio, int sampleRate, double thresholdDb, double ratio, double attackMs, double releaseMs)
        {
            float[] output = new float[audio.Length];
            double threshold = Math.Pow(10.0, thresholdDb / 20.0);
            double attackCoeff = Math.Exp(-1.0 / (sampleRate * (attackMs / 1000.0)));
            double releaseCoeff = Math.Exp(-1.0 / (sampleRate * (releaseMs / 1000.0)));

            double envelope = 0;

            for (int i = 0; i < audio.Length; i++)
            {
                double inputLevel = Math.Abs(audio[i]);
                if (inputLevel > envelope)
                {
                    envelope = attackCoeff * envelope + (1.0 - attackCoeff) * inputLevel;
                }
                else
                {
                    envelope = releaseCoeff * envelope + (1.0 - releaseCoeff) * inputLevel;
                }

                double gain = 1.0;
                if (envelope > threshold)
                {
                    double envDb = 20.0 * Math.Log10(envelope + 1e-9);
                    double excessDb = envDb - thresholdDb;
                    double compressedExcessDb = excessDb / Math.Max(1.0, ratio);
                    double targetDb = thresholdDb + compressedExcessDb;
                    double targetLevel = Math.Pow(10.0, targetDb / 20.0);
                    gain = targetLevel / (envelope + 1e-9);
                }

                output[i] = (float)(audio[i] * gain);
            }
            return output;
        }

        public static float[] ApplyLimiter(float[] audio, int sampleRate, double thresholdDb, double releaseMs)
        {
            float[] output = new float[audio.Length];
            double threshold = Math.Pow(10.0, thresholdDb / 20.0);
            double releaseCoeff = Math.Exp(-1.0 / (sampleRate * (releaseMs / 1000.0)));
            double gain = 1.0;

            for (int i = 0; i < audio.Length; i++)
            {
                double inputLevel = Math.Abs(audio[i]);
                double targetGain = inputLevel > threshold ? threshold / inputLevel : 1.0;

                if (targetGain < gain)
                {
                    gain = targetGain; // Fast attack
                }
                else
                {
                    gain = releaseCoeff * gain + (1.0 - releaseCoeff) * targetGain;
                }

                output[i] = (float)(audio[i] * gain);
            }
            return output;
        }

        public static float[] ApplyGain(float[] audio, double gainDb)
        {
            float[] output = new float[audio.Length];
            float factor = (float)Math.Pow(10.0, gainDb / 20.0);

            for (int i = 0; i < audio.Length; i++)
            {
                output[i] = audio[i] * factor;
            }
            return output;
        }

        public static float[] Normalize(float[] audio, float targetPeak = 0.98f)
        {
            float max = 0f;
            for (int i = 0; i < audio.Length; i++)
            {
                float abs = Math.Abs(audio[i]);
                if (abs > max) max = abs;
            }

            if (max <= 0f) return audio;

            float scale = targetPeak / max;
            float[] normalized = new float[audio.Length];
            for (int i = 0; i < audio.Length; i++)
            {
                normalized[i] = audio[i] * scale;
            }
            return normalized;
        }
    }
}

