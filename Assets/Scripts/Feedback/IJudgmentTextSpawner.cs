using UnityEngine;

namespace KeyFlow.Feedback
{
    public interface IJudgmentTextSpawner
    {
        void Spawn(Judgment judgment, Vector3 worldPos);
    }
}
