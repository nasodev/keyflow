using UnityEngine;

namespace KeyFlow.Feedback
{
    public class FeedbackDispatcher : MonoBehaviour
    {
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private HapticService hapticService;
        [SerializeField] private ParticlePool particlePool;
        [SerializeField] private JudgmentTextPool textPool;

        private IHapticService haptic;
        private IParticleSpawner particles;
        private IJudgmentTextSpawner textSpawner;

        private void Awake()
        {
            if (haptic == null) haptic = hapticService;
            if (particles == null) particles = particlePool;
            if (textSpawner == null) textSpawner = textPool;
        }

        private void OnEnable()
        {
            if (judgmentSystem == null)
            {
                Debug.LogError("FeedbackDispatcher: judgmentSystem unassigned.");
                return;
            }
            judgmentSystem.OnJudgmentFeedback += Handle;
        }

        private void OnDisable()
        {
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback -= Handle;
        }

        private void Handle(Judgment j, Vector3 worldPos)
        {
            if (UserPrefs.HapticsEnabled && haptic != null) haptic.Fire(j);
            if (particles != null) particles.Spawn(j, worldPos);
            if (textSpawner != null) textSpawner.Spawn(j, worldPos);
        }

        internal void SetDependenciesForTest(
            JudgmentSystem js, IHapticService h, IParticleSpawner p, IJudgmentTextSpawner t)
        {
            // Unsubscribe from any prior judgmentSystem (idempotent; safe when null/never-subscribed).
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback -= Handle;
            judgmentSystem = js;
            haptic = h;
            particles = p;
            textSpawner = t;
            // Re-subscribe with the injected judgmentSystem. EditMode tests often can't rely on
            // OnEnable firing naturally, so explicitly wire up here.
            if (judgmentSystem != null) judgmentSystem.OnJudgmentFeedback += Handle;
        }
    }
}
