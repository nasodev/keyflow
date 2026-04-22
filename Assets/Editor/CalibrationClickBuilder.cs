using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KeyFlow.Editor
{
    // Generates Assets/Audio/calibration_click.wav — a short non-piano click
    // for the calibration overlay. Deterministic (seeded RNG) so repeated
    // runs produce byte-identical output.
    public static class CalibrationClickBuilder
    {
        public const int SampleRate = 48000;
        public const int DurationSamples = 960; // 20 ms @ 48 kHz
        public const int Channels = 1;
        public const int BitsPerSample = 16;
        public const int Seed = 1;
        public const float PeakLinear = 0.501f;  // ~-6 dBFS
        public const float HighPassCutoffHz = 500f;
        public const float DecayTauSamples = 192f; // ~4 ms
        public const int AttackSamples = 24;       // ~0.5 ms linear fade-in

        public const string AssetPath = "Assets/Audio/calibration_click.wav";

        public static byte[] GenerateWavBytes()
        {
            int dataSize = DurationSamples * Channels * (BitsPerSample / 8);
            byte[] result = new byte[44 + dataSize];

            // RIFF header
            WriteAscii(result, 0, "RIFF");
            WriteInt32(result, 4, 36 + dataSize); // file size - 8
            WriteAscii(result, 8, "WAVE");

            // fmt chunk
            WriteAscii(result, 12, "fmt ");
            WriteInt32(result, 16, 16);                             // PCM chunk size
            WriteInt16(result, 20, 1);                              // PCM format
            WriteInt16(result, 22, (short)Channels);
            WriteInt32(result, 24, SampleRate);
            WriteInt32(result, 28, SampleRate * Channels * (BitsPerSample / 8)); // byte rate
            WriteInt16(result, 32, (short)(Channels * (BitsPerSample / 8)));     // block align
            WriteInt16(result, 34, (short)BitsPerSample);

            // data chunk
            WriteAscii(result, 36, "data");
            WriteInt32(result, 40, dataSize);

            // Data body: seeded noise → 1-pole HP → attack/decay envelope → peak-scale → int16 LE.
            var rng = new System.Random(Seed);
            // 1-pole HP: y[n] = α (y[n-1] + x[n] - x[n-1]); α = RC / (RC + dt)
            // where RC = 1 / (2π fc), dt = 1 / fs.
            float rc = 1f / (2f * Mathf.PI * HighPassCutoffHz);
            float dt = 1f / SampleRate;
            float alpha = rc / (rc + dt);
            float prevRaw = 0f;
            float prevHp  = 0f;
            for (int i = 0; i < DurationSamples; i++)
            {
                float raw = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = alpha * (prevHp + raw - prevRaw);
                prevHp = hp;
                prevRaw = raw;

                float attack = i < AttackSamples ? (i + 1) / (float)AttackSamples : 1f;
                float decay  = Mathf.Exp(-i / DecayTauSamples);
                float value  = hp * attack * decay * PeakLinear;

                int sample = Mathf.Clamp(Mathf.RoundToInt(value * 32767f), -32768, 32767);
                int offset = 44 + i * 2;
                result[offset + 0] = (byte)(sample & 0xFF);
                result[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return result;
        }

        private static void WriteAscii(byte[] dst, int offset, string s)
        {
            for (int i = 0; i < s.Length; i++) dst[offset + i] = (byte)s[i];
        }

        private static void WriteInt32(byte[] dst, int offset, int value)
        {
            dst[offset + 0] = (byte)(value & 0xFF);
            dst[offset + 1] = (byte)((value >> 8) & 0xFF);
            dst[offset + 2] = (byte)((value >> 16) & 0xFF);
            dst[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] dst, int offset, short value)
        {
            dst[offset + 0] = (byte)(value & 0xFF);
            dst[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        [MenuItem("KeyFlow/Build Calibration Click")]
        public static void Build()
        {
            EnsureFolder("Assets/Audio");

            byte[] bytes = GenerateWavBytes();
            File.WriteAllBytes(AssetPath, bytes);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(AssetPath) as AudioImporter;
            if (importer == null)
            {
                Debug.LogError($"[KeyFlow] No AudioImporter for {AssetPath}.");
                return;
            }

            importer.forceToMono = true;
            var defaults = importer.defaultSampleSettings;
            defaults.loadType = AudioClipLoadType.DecompressOnLoad;
            defaults.preloadAudioData = true;
            defaults.compressionFormat = AudioCompressionFormat.PCM; // short transient; avoid Vorbis smearing
            defaults.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            defaults.sampleRateOverride = (uint)SampleRate;
            importer.defaultSampleSettings = defaults;

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();

            Debug.Log($"[KeyFlow] CalibrationClickBuilder wrote {AssetPath} ({bytes.Length} bytes).");
        }
    }
}
