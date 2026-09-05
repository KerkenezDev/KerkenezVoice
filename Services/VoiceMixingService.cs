using System;
using System.IO;
using KokoroSharp.Core;

namespace KerkenezVoice.Services
{
    public class VoiceMixingService
    {
        private readonly ModelManagerService _modelManager;

        public VoiceMixingService(ModelManagerService modelManager)
        {
            _modelManager = modelManager;
        }

        public (bool success, string message, float[,,]? tensor) MixVoices(
            KokoroVoice voiceA,
            KokoroVoice voiceB,
            double ratio,
            string newVoiceName,
            string operation = "mix")
        {
            try
            {
                if (voiceA?.Features == null || voiceB?.Features == null)
                    return (false, "One or both voices are missing features.", null);

                float[,,] featA = voiceA.Features;
                float[,,] featB = voiceB.Features;

                int d0 = featA.GetLength(0);
                int d1 = featA.GetLength(1);
                int d2 = featA.GetLength(2);

                float[,,] mixed = new float[d0, d1, d2];
                float r = (float)Math.Clamp(ratio, 0.0, 1.0);
                string op = operation.ToLowerInvariant();

                for (int i = 0; i < d0; i++)
                {
                    for (int j = 0; j < d1; j++)
                    {
                        if (op == "mix")
                        {
                            // Norm-preserving interpolation to prevent vector shrinkage at 50/50
                            float sumSqA = 0f;
                            float sumSqB = 0f;
                            float sumSqMix = 0f;

                            for (int k = 0; k < d2; k++)
                            {
                                float v1 = featA[i, j, k];
                                float v2 = featB[i, j, k];
                                sumSqA += v1 * v1;
                                sumSqB += v2 * v2;

                                float m = v1 * (1.0f - r) + v2 * r;
                                mixed[i, j, k] = m;
                                sumSqMix += m * m;
                            }

                            float normA = MathF.Sqrt(sumSqA);
                            float normB = MathF.Sqrt(sumSqB);
                            float normMix = MathF.Sqrt(sumSqMix);
                            float targetNorm = normA * (1.0f - r) + normB * r;

                            if (normMix > 1e-6f && targetNorm > 1e-6f)
                            {
                                float scale = targetNorm / normMix;
                                for (int k = 0; k < d2; k++)
                                {
                                    mixed[i, j, k] *= scale;
                                }
                            }
                        }
                        else
                        {
                            for (int k = 0; k < d2; k++)
                            {
                                float v1 = featA[i, j, k];
                                float v2 = featB[i, j, k];

                                switch (op)
                                {
                                    case "add":
                                        mixed[i, j, k] = v1 + v2 * r;
                                        break;
                                    case "subtract":
                                        mixed[i, j, k] = v1 - v2 * r;
                                        break;
                                    case "multiply":
                                        mixed[i, j, k] = v1 * (1.0f - r) + (v1 * v2) * r;
                                        break;
                                    case "divide":
                                        mixed[i, j, k] = v1 * (1.0f - r) + (v1 / (v2 + 1e-6f)) * r;
                                        break;
                                    default:
                                        mixed[i, j, k] = v1 * (1.0f - r) + v2 * r;
                                        break;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(newVoiceName))
                {
                    string clean = string.Join("_", newVoiceName.Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                    string safeName = Path.GetFileNameWithoutExtension(clean);
                    string outPath = Path.Combine(_modelManager.CustomVoicesDirectory, $"{safeName}.bin");

                    int totalFloats = d0 * d1 * d2;
                    byte[] bytes = new byte[totalFloats * sizeof(float)];
                    Buffer.BlockCopy(mixed, 0, bytes, 0, bytes.Length);
                    File.WriteAllBytes(outPath, bytes);
                }

                return (true, "Mixed successfully.", mixed);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public bool DeleteCustomVoice(string voiceName)
        {
            try
            {
                string clean = string.Join("_", voiceName.Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string safeName = Path.GetFileNameWithoutExtension(clean);
                string outPath = Path.Combine(_modelManager.CustomVoicesDirectory, $"{safeName}.bin");
                if (File.Exists(outPath))
                {
                    File.Delete(outPath);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
