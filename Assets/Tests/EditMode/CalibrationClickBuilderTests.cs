using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using KeyFlow.Editor;

namespace KeyFlow.Tests.EditMode
{
    public class CalibrationClickBuilderTests
    {
        [Test]
        public void GenerateWavBytes_IsDeterministic()
        {
            byte[] first  = CalibrationClickBuilder.GenerateWavBytes();
            byte[] second = CalibrationClickBuilder.GenerateWavBytes();
            Assert.AreEqual(first.Length, second.Length, "Lengths differ across runs.");
            CollectionAssert.AreEqual(first, second, "Bytes differ across runs.");
        }

        [Test]
        public void WavHeader_Is48kMono16BitPcm()
        {
            byte[] b = CalibrationClickBuilder.GenerateWavBytes();
            Assert.GreaterOrEqual(b.Length, 44, "Too short to contain a RIFF header.");
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(b, 8, 4));
            Assert.AreEqual("fmt ", System.Text.Encoding.ASCII.GetString(b, 12, 4));
            Assert.AreEqual(16, System.BitConverter.ToInt32(b, 16), "fmt chunk size should be 16 (PCM).");
            Assert.AreEqual(1,  System.BitConverter.ToInt16(b, 20), "Audio format should be 1 (PCM).");
            Assert.AreEqual(1,  System.BitConverter.ToInt16(b, 22), "Channel count should be 1 (mono).");
            Assert.AreEqual(48000, System.BitConverter.ToInt32(b, 24), "Sample rate should be 48 kHz.");
            Assert.AreEqual(16, System.BitConverter.ToInt16(b, 34), "Bits per sample should be 16.");
            Assert.AreEqual("data", System.Text.Encoding.ASCII.GetString(b, 36, 4));
        }

        [Test]
        public void DataChunk_Is1920Bytes()
        {
            byte[] b = CalibrationClickBuilder.GenerateWavBytes();
            int dataSize = System.BitConverter.ToInt32(b, 40);
            Assert.AreEqual(1920, dataSize, "Data chunk size should be 960 samples × 2 bytes = 1920.");
            Assert.AreEqual(44 + 1920, b.Length, "Total file size = 44-byte header + 1920-byte data.");
        }

        [Test]
        public void PeakBelowZeroDbfs()
        {
            byte[] b = CalibrationClickBuilder.GenerateWavBytes();
            int maxAbs = 0;
            for (int i = 44; i < b.Length; i += 2)
            {
                short s = (short)(b[i] | (b[i + 1] << 8));
                int abs = s < 0 ? -s : s;
                if (abs > maxAbs) maxAbs = abs;
            }
            // Peak target: 0.501 × 32767 ≈ 16416. Allow small headroom for envelope/filter interaction.
            Assert.LessOrEqual(maxAbs, 16500, $"Peak {maxAbs} exceeds -6 dBFS target (~16416).");
            Assert.Greater(maxAbs, 1000, $"Peak {maxAbs} suspiciously low — signal is basically silent.");
        }

        [Test]
        public void Build_ProducesImportableClipWithMonoAndPreload()
        {
            CalibrationClickBuilder.Build();
            AssetDatabase.Refresh();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CalibrationClickBuilder.AssetPath);
            Assert.IsNotNull(clip, "AssetDatabase did not produce an AudioClip from the built WAV.");

            var importer = (AudioImporter)AssetImporter.GetAtPath(CalibrationClickBuilder.AssetPath);
            Assert.IsNotNull(importer, "No AudioImporter bound to the generated asset.");
            Assert.IsTrue(importer.forceToMono, "AudioImporter.forceToMono should be true.");
            Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData, "preloadAudioData should be true.");
        }
    }
}
