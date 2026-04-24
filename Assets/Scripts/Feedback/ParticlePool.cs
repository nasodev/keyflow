using UnityEngine;

namespace KeyFlow.Feedback
{
    public class ParticlePool : MonoBehaviour, IParticleSpawner
    {
        [SerializeField] private ParticleSystem hitPrefab;
        [SerializeField] private ParticleSystem missPrefab;
        [SerializeField] private FeedbackPresets presets;
        [SerializeField] private int hitPoolSize = 16;
        [SerializeField] private int missPoolSize = 4;

        private ParticleSystem[] hitPool;
        private ParticleSystem[] missPool;
        private int hitNextIndex;
        private int missNextIndex;
        private bool ready;

        private void Awake()
        {
            if (hitPrefab == null || missPrefab == null)
            {
                Debug.LogError("ParticlePool: prefab refs unassigned — Spawn calls will no-op.");
                return;
            }
            if (presets == null)
            {
                Debug.LogError("ParticlePool: presets unassigned — tints will be default.");
            }
            hitPool = new ParticleSystem[hitPoolSize];
            for (int i = 0; i < hitPoolSize; i++)
            {
                var ps = Instantiate(hitPrefab, transform);
                ps.gameObject.SetActive(false);
                hitPool[i] = ps;
            }
            missPool = new ParticleSystem[missPoolSize];
            for (int i = 0; i < missPoolSize; i++)
            {
                var ps = Instantiate(missPrefab, transform);
                ps.gameObject.SetActive(false);
                missPool[i] = ps;
            }
            ready = true;
        }

        // Called automatically every time GameplayRoot reactivates (after Results
        // screen → 2nd play). Without this, any ParticleSystem that was mid-emission
        // when the root deactivated resumes on reactivation — producing ghost
        // particle bursts during the SP11 countdown window. Unity pauses
        // ParticleSystem simulation on GameObject deactivation but does NOT clear
        // the simulation state; ps.Clear(true) is required to purge live particles.
        private void OnEnable()
        {
            if (!ready) return;  // pool not yet initialized (Awake runs before OnEnable on first load)
            ClearPoolState();
        }

        private void ClearPoolState()
        {
            for (int i = 0; i < hitPool.Length; i++)
            {
                var ps = hitPool[i];
                if (ps == null) continue;
                ps.Clear(true);
                if (ps.gameObject.activeSelf) ps.gameObject.SetActive(false);
            }
            for (int i = 0; i < missPool.Length; i++)
            {
                var ps = missPool[i];
                if (ps == null) continue;
                ps.Clear(true);
                if (ps.gameObject.activeSelf) ps.gameObject.SetActive(false);
            }
            hitNextIndex = 0;
            missNextIndex = 0;
        }

        public void Spawn(Judgment judgment, Vector3 worldPos)
        {
            if (!ready) return;
            if (judgment == Judgment.Miss)
            {
                SpawnFromPool(missPool, ref missNextIndex, worldPos, null);
            }
            else
            {
                FeedbackPresets.ParticlePreset? preset = presets != null
                    ? presets.GetParticle(judgment)
                    : (FeedbackPresets.ParticlePreset?)null;
                SpawnFromPool(hitPool, ref hitNextIndex, worldPos, preset);
            }
        }

        private static void SpawnFromPool(
            ParticleSystem[] pool, ref int nextIndex, Vector3 worldPos,
            FeedbackPresets.ParticlePreset? preset)
        {
            var ps = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;

            ps.transform.position = worldPos;

            if (preset.HasValue)
            {
                // Struct-copy-safe idiom: assign to local, mutate field, assign back.
                // ParticleSystem.main returns a MainModule struct but its setter applies
                // the change to the underlying system — no GC allocation.
                var main = ps.main;
                main.startColor = preset.Value.tintColor;
                main.startSize = preset.Value.startSize;
                var emission = ps.emission;
                // Burst 0 = the default burst configured on the prefab; rewrite its count.
                var burst = emission.GetBurst(0);
                burst.count = preset.Value.burstCount;
                emission.SetBurst(0, burst);
            }

            if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();
        }
    }
}
