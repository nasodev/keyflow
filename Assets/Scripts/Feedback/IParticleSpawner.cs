using UnityEngine;

namespace KeyFlow.Feedback
{
    public interface IParticleSpawner
    {
        void Spawn(Judgment judgment, Vector3 worldPos);
    }
}
