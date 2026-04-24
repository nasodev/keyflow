using UnityEngine;

namespace KeyFlow.Feedback
{
    [CreateAssetMenu(fileName = "FeedbackPresets", menuName = "KeyFlow/FeedbackPresets")]
    public class FeedbackPresets : ScriptableObject
    {
        [System.Serializable]
        public struct HapticPreset
        {
            public int durationMs;
            [Range(0, 255)] public int amplitude;
        }

        [System.Serializable]
        public struct ParticlePreset
        {
            public Color tintColor;
            public float startSize;
            public int burstCount;
        }

        [Header("Haptics (VibrationEffect.createOneShot(ms, amplitude))")]
        public HapticPreset perfect = new HapticPreset { durationMs = 15, amplitude = 200 };
        public HapticPreset great   = new HapticPreset { durationMs = 10, amplitude = 120 };
        public HapticPreset good    = new HapticPreset { durationMs = 8,  amplitude = 60  };
        public HapticPreset miss    = new HapticPreset { durationMs = 40, amplitude = 180 };

        [Header("Particles (hit.prefab tint; miss uses prefab-fixed values)")]
        public ParticlePreset perfectParticle = new ParticlePreset {
            tintColor = new Color(1f, 1f, 1f, 1f), startSize = 0.45f, burstCount = 16 };
        public ParticlePreset greatParticle   = new ParticlePreset {
            tintColor = new Color(0.7f, 0.9f, 1f, 1f), startSize = 0.32f, burstCount = 10 };
        public ParticlePreset goodParticle    = new ParticlePreset {
            tintColor = new Color(0.8f, 1f, 0.8f, 1f), startSize = 0.22f, burstCount = 6 };

        [Header("Text popup colors (SP10)")]
        public Color perfectTextColor = new Color(1f, 0.84f, 0f, 1f);      // #FFD700 gold
        public Color greatTextColor   = new Color(0.31f, 0.76f, 1f, 1f);    // #4FC3FF cyan
        public Color goodTextColor    = new Color(0.5f, 0.87f, 0.5f, 1f);   // #7FDF7F green
        public Color missTextColor    = new Color(1f, 0.25f, 0.25f, 1f);    // #FF4040 red

        public HapticPreset GetHaptic(Judgment j) => j switch
        {
            Judgment.Perfect => perfect,
            Judgment.Great   => great,
            Judgment.Good    => good,
            _                => miss,
        };

        public ParticlePreset GetParticle(Judgment j) => j switch
        {
            Judgment.Perfect => perfectParticle,
            Judgment.Great   => greatParticle,
            _                => goodParticle,
            // Miss uses prefab-fixed values — callers route to miss.prefab and ignore this
        };

        public Color GetTextColor(Judgment j) => j switch
        {
            Judgment.Perfect => perfectTextColor,
            Judgment.Great   => greatTextColor,
            Judgment.Good    => goodTextColor,
            _                => missTextColor,
        };
    }
}
