using UnityEngine;

namespace KeyFlow.Feedback
{
    public readonly struct JudgmentFeedbackEvent
    {
        public readonly Judgment Kind;
        public readonly Vector3 WorldPos;

        public JudgmentFeedbackEvent(Judgment kind, Vector3 worldPos)
        {
            Kind = kind;
            WorldPos = worldPos;
        }
    }
}
