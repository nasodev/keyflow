using System.IO;
using UnityEditor;
using UnityEngine;

namespace KeyFlow.Editor
{
    // Applies shared AudioImporter settings to every WAV under Assets/Audio/piano/.
    // Must run as an AssetPostprocessor so settings stick through re-imports
    // (including fresh checkouts of the worktree).
    public class PianoSampleImportPostprocessor : AssetPostprocessor
    {
        private const string PianoFolder = "Assets/Audio/piano/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(PianoFolder)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;

            var defaults = importer.defaultSampleSettings;
            defaults.loadType = AudioClipLoadType.DecompressOnLoad;
            defaults.compressionFormat = AudioCompressionFormat.Vorbis;
            defaults.quality = 0.60f;
            defaults.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            defaults.sampleRateOverride = 48000;
            // preloadAudioData moved to SampleSettings in Unity 6
            defaults.preloadAudioData = true;
            importer.defaultSampleSettings = defaults;
        }

        // Called via -executeMethod to force-reimport all piano WAVs so
        // OnPreprocessAudio runs and .meta files are fully serialized.
        public static void ForceReimportPianoSamples()
        {
            var wavFiles = Directory.GetFiles(PianoFolder, "*.wav");
            foreach (var file in wavFiles)
            {
                // Normalize path separators for Unity's asset database
                var path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[KeyFlow] Reimported: {path}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[KeyFlow] ForceReimportPianoSamples complete: {wavFiles.Length} files.");
        }
    }
}
