using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    // Per-popup lifecycle animator. Pool pre-instantiates 12 GameObjects each
    // with this component + a Text component; JudgmentTextPool calls Activate
    // and the popup drives its own scale-punch, y-rise, and alpha-fade until
    // t >= 1 at which point it self-deactivates.
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Text))]
    public class JudgmentTextPopup : MonoBehaviour
    {
        private const float PunchEndT = 0.22f;   // scale returns to 1.0 by 22% of lifetime
        private const float FadeStartT = 0.55f;  // alpha starts fading at 55% of lifetime
        private const float PunchPeak = 1.3f;    // initial scale at t=0

        private Text text;
        private RectTransform rt;
        private float startTime;
        private float lifetime;
        private float yRiseUnits;
        private Color baseColor;
        private float baseLocalY;

        private void Awake()
        {
            text = GetComponent<Text>();
            rt = GetComponent<RectTransform>();
        }

        // Called by the pool. `startTime` is `Time.time` in production; tests
        // pass 0 and use TickForTest with a simulated time.
        public void Activate(float startTime, float lifetime, float yRiseUnits, Color color)
        {
            // Awake only runs on active GameObjects; tests may call Activate
            // before the first implicit enable. Lazy-init here as a safety net.
            if (text == null) text = GetComponent<Text>();
            if (rt == null) rt = GetComponent<RectTransform>();

            this.startTime = startTime;
            this.lifetime = lifetime;
            this.yRiseUnits = yRiseUnits;
            this.baseColor = color;
            this.baseLocalY = rt.anchoredPosition.y;

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

            // Scale punch: 1.3 -> 1.0 across first 22% of lifetime, then steady.
            float scale = t < PunchEndT
                ? Mathf.Lerp(PunchPeak, 1.0f, t / PunchEndT)
                : 1.0f;
            rt.localScale = new Vector3(scale, scale, 1f);

            // Y rise: linear across full lifetime. baseLocalY is the anchoredPosition.y
            // captured at Activate time so pooled reuse starts from the correct spawn point.
            float yOffset = yRiseUnits * t;
            var ap = rt.anchoredPosition;
            ap.y = baseLocalY + yOffset;
            rt.anchoredPosition = ap;

            // Alpha fade: 1.0 -> 0.0 across last 45% of lifetime.
            float alpha = t < FadeStartT
                ? 1.0f
                : Mathf.Lerp(1.0f, 0.0f, (t - FadeStartT) / (1f - FadeStartT));
            var c = baseColor;
            c.a = alpha;
            text.color = c;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        // Test hook. Drives the same Tick path that Update calls in play mode,
        // but with a caller-supplied simulated time so EditMode tests don't
        // depend on Time.time progression. Guarded to strip from IL2CPP
        // release builds, matching LaneGlowController's test-hook convention.
        internal void TickForTest(float simulatedTime) => Tick(simulatedTime);
#endif
    }
}
