using UnityEngine;

namespace KeyFlow
{
    public static class LaneLayout
    {
        public const int LaneCount = 4;

        public static float LaneToX(int lane, float screenWorldWidth)
        {
            float laneWidth = screenWorldWidth / LaneCount;
            float leftEdge = -screenWorldWidth / 2f;
            return leftEdge + laneWidth * (lane + 0.5f);
        }

        public static int XToLane(float x, float screenWorldWidth)
        {
            float laneWidth = screenWorldWidth / LaneCount;
            float leftEdge = -screenWorldWidth / 2f;
            int raw = Mathf.FloorToInt((x - leftEdge) / laneWidth);
            return Mathf.Clamp(raw, 0, LaneCount - 1);
        }
    }
}
