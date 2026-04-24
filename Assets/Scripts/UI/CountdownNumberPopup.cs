using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Text))]
    public class CountdownNumberPopup : MonoBehaviour
    {
        private const float PunchEndT = 0.18f;
        private const float FadeStartT = 0.55f;
        private const float PunchPeak = 1.5f;

        private Text text;
        private RectTransform rt;
        private float startTime;
        private float lifetime;
        private Color baseColor;

        private void Awake()
        {
            text = GetComponent<Text>();
            rt = GetComponent<RectTransform>();
        }

        public void Activate(float startTime, float lifetime, string label, Color color)
        {
            if (text == null) text = GetComponent<Text>();
            if (rt == null) rt = GetComponent<RectTransform>();
            this.startTime = startTime;
            this.lifetime = lifetime;
            this.baseColor = color;
            text.text = label;
            text.color = color;
            rt.localScale = Vector3.one * PunchPeak;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        private void Update()
        {
            Tick(Time.time);
        }

        private void Tick(float now)
        {
            float elapsed = now - startTime;
            if (elapsed <= 0f) return;

            float t = elapsed / lifetime;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }

            float scale = t < PunchEndT
                ? Mathf.Lerp(PunchPeak, 1.0f, t / PunchEndT)
                : 1.0f;
            rt.localScale = new Vector3(scale, scale, 1f);

            float alpha = t < FadeStartT
                ? 1.0f
                : Mathf.Lerp(1.0f, 0.0f, (t - FadeStartT) / (1f - FadeStartT));
            var c = baseColor;
            c.a = alpha;
            text.color = c;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        // Parameter is ABSOLUTE time (same reference frame as `Activate`'s startTime).
        // This differs from CountdownOverlay.TickForTest(simulatedElapsed) which takes
        // elapsed-since-BeginCountdown. Both test files pass startTime=0 so the values
        // happen to match, but the semantics differ by design.
        internal void TickForTest(float simulatedTime) => Tick(simulatedTime);
#endif
    }
}
