using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    public class JudgmentTextPool : MonoBehaviour, IJudgmentTextSpawner
    {
        private static readonly string PerfectStr = "PERFECT";
        private static readonly string GreatStr   = "GREAT";
        private static readonly string GoodStr    = "GOOD";
        private static readonly string MissStr    = "MISS";

        [SerializeField] private FeedbackPresets presets;
        [SerializeField] private int poolSize = 12;
        [SerializeField] private float lifetimeSec = 0.45f;
        [SerializeField] private float yRiseUnits = 0.36f;
        [SerializeField] private int fontSize = 48;
        [SerializeField] private float worldCanvasScale = 0.01f;

        private GameObject[] slots;
        private Text[] texts;
        private RectTransform[] rects;
        private JudgmentTextPopup[] popups;
        private int nextIndex;
        private bool ready;

        private void Awake()
        {
            if (!ready) BuildPool();
        }

        private void BuildPool()
        {
            if (presets == null)
                Debug.LogError("JudgmentTextPool: presets unassigned — spawns will use default colors.");

            slots = new GameObject[poolSize];
            texts = new Text[poolSize];
            rects = new RectTransform[poolSize];
            popups = new JudgmentTextPopup[poolSize];

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"JudgmentText_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 120);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                var t = go.AddComponent<Text>();
                t.text = string.Empty;
                t.font = font;
                t.fontSize = fontSize;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.fontStyle = FontStyle.Bold;

                var popup = go.AddComponent<JudgmentTextPopup>();

                go.SetActive(false);

                slots[i] = go;
                texts[i] = t;
                rects[i] = rt;
                popups[i] = popup;
            }

            ready = true;
        }

        public void Spawn(Judgment judgment, Vector3 worldPos)
        {
            if (!ready) BuildPool();

            int idx = nextIndex;
            nextIndex = (nextIndex + 1) % poolSize;

            texts[idx].text = LookupString(judgment);
            Color color = presets != null
                ? presets.GetTextColor(judgment)
                : Color.white;

            // Canvas sits at (0, judgmentY, 0) in world; canvas-local y=0 is the
            // judgment line. We use worldPos.x as canvas-local x (world units),
            // and fix y at 0 regardless of the worldPos.y the caller provided.
            rects[idx].anchoredPosition = new Vector2(worldPos.x, 0f);

            popups[idx].Activate(Time.time, lifetimeSec, yRiseUnits, color);
        }

        private static string LookupString(Judgment j) => j switch
        {
            Judgment.Perfect => PerfectStr,
            Judgment.Great   => GreatStr,
            Judgment.Good    => GoodStr,
            _                => MissStr,
        };

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        // Test hooks.
        internal void InitializeForTest(
            FeedbackPresets presets, int poolSize, float lifetimeSec,
            float yRiseUnits, int fontSize, float worldCanvasScale)
        {
            this.presets = presets;
            this.poolSize = poolSize;
            this.lifetimeSec = lifetimeSec;
            this.yRiseUnits = yRiseUnits;
            this.fontSize = fontSize;
            this.worldCanvasScale = worldCanvasScale;
            BuildPool();
        }

        internal GameObject GetSlotForTest(int index) => slots[index];
#endif
    }
}
