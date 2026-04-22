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
            // Filled in by Task 3-5.
            return Array.Empty<byte>();
        }

        [MenuItem("KeyFlow/Build Calibration Click")]
        public static void Build()
        {
            // Filled in by Task 6.
            Debug.LogWarning("[KeyFlow] CalibrationClickBuilder.Build not implemented yet.");
        }
    }
}
