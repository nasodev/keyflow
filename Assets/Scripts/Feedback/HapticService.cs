using UnityEngine;

namespace KeyFlow.Feedback
{
    public class HapticService : MonoBehaviour, IHapticService
    {
        [SerializeField] private FeedbackPresets presets;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject effectPerfect;
        private AndroidJavaObject effectGreat;
        private AndroidJavaObject effectGood;
        private AndroidJavaObject effectMiss;
#else
        private object effectPerfect, effectGreat, effectGood, effectMiss;
#endif

        private bool ready;

        private void Awake()
        {
            if (presets == null)
            {
                Debug.LogError("HapticService: presets SerializeField unassigned — all Fire() calls will no-op.");
                return;
            }

            // Cache the 4 VibrationEffect Java objects once; reuse on every Fire.
            effectPerfect = AndroidHapticsBridge.CreateOneShot(
                presets.perfect.durationMs, presets.perfect.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectGreat = AndroidHapticsBridge.CreateOneShot(
                presets.great.durationMs, presets.great.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectGood = AndroidHapticsBridge.CreateOneShot(
                presets.good.durationMs, presets.good.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            effectMiss = AndroidHapticsBridge.CreateOneShot(
                presets.miss.durationMs, presets.miss.amplitude)
#if UNITY_ANDROID && !UNITY_EDITOR
                as AndroidJavaObject;
#else
                ;
#endif
            ready = true;
        }

        public void Fire(Judgment judgment)
        {
            if (!ready) return;
            var effect = judgment switch
            {
                Judgment.Perfect => effectPerfect,
                Judgment.Great   => effectGreat,
                Judgment.Good    => effectGood,
                _                => effectMiss,
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidHapticsBridge.Vibrate(effect);
#else
            AndroidHapticsBridge.Vibrate(effect);
#endif
        }
    }
}
