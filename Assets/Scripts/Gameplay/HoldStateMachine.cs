using System.Collections.Generic;

namespace KeyFlow
{
    public enum HoldState { Spawned, Holding, Completed, Broken }

    public struct HoldTransition
    {
        public int id;
        public HoldState newState;
    }

    public class HoldStateMachine
    {
        private class Entry
        {
            public int lane;
            public int startMs;
            public int endMs;
            public HoldState state;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private int nextId = 1;

        public int Register(int lane, int startMs, int endMs)
        {
            int id = nextId++;
            entries[id] = new Entry
            {
                lane = lane,
                startMs = startMs,
                endMs = endMs,
                state = HoldState.Spawned
            };
            return id;
        }

        public HoldState GetState(int id) => entries[id].state;

        public void Clear()
        {
            entries.Clear();
            nextId = 1;
        }

        public void OnStartTapAccepted(int id)
        {
            if (entries.TryGetValue(id, out var e) && e.state == HoldState.Spawned)
                e.state = HoldState.Holding;
        }

        public void Tick(int songTimeMs, HashSet<int> pressedLanes, List<HoldTransition> outTransitions)
        {
            outTransitions.Clear();
            foreach (var kv in entries)
            {
                var e = kv.Value;
                if (e.state != HoldState.Holding) continue;

                if (!pressedLanes.Contains(e.lane))
                {
                    e.state = HoldState.Broken;
                    outTransitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Broken });
                }
                else if (songTimeMs >= e.endMs)
                {
                    e.state = HoldState.Completed;
                    outTransitions.Add(new HoldTransition { id = kv.Key, newState = HoldState.Completed });
                }
            }
        }
    }
}
