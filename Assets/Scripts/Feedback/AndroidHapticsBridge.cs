using UnityEngine;

namespace KeyFlow.Feedback
{
    /// <summary>
    /// Thin static wrapper around Android Vibrator + VibrationEffect (API 26+).
    /// All calls compile to no-op outside UNITY_ANDROID or inside UNITY_EDITOR.
    /// </summary>
    public static class AndroidHapticsBridge
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass vibrationEffectClass;
        private static bool initialized;
        private static bool available;

        private static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                using var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null) { available = false; return; }
                bool hasVib = vibrator.Call<bool>("hasVibrator");
                if (!hasVib) { available = false; return; }
                vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                available = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[KeyFlow] Haptics init failed: {e.Message}");
                available = false;
            }
        }

        public static AndroidJavaObject CreateOneShot(int durationMs, int amplitude)
        {
            EnsureInit();
            if (!available) return null;
            return vibrationEffectClass.CallStatic<AndroidJavaObject>(
                "createOneShot", (long)durationMs, amplitude);
        }

        public static void Vibrate(AndroidJavaObject effect)
        {
            EnsureInit();
            if (!available || effect == null) return;
            vibrator.Call("vibrate", effect);
        }
#else
        public static object CreateOneShot(int durationMs, int amplitude) => null;
        public static void Vibrate(object effect) { /* no-op */ }
#endif
    }
}
