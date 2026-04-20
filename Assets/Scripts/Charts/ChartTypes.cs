using System;
using System.Collections.Generic;

namespace KeyFlow
{
    public enum NoteType { TAP, HOLD }

    [Serializable]
    public class ChartNote
    {
        public int t;
        public int lane;
        public int pitch;
        public NoteType type;
        public int dur;
    }

    [Serializable]
    public class ChartDifficulty
    {
        public int totalNotes;
        public List<ChartNote> notes;
    }

    [Serializable]
    public class ChartData
    {
        public string songId;
        public string title;
        public string composer;
        public int bpm;
        public int durationMs;
        public Dictionary<Difficulty, ChartDifficulty> charts;
    }

    public class ChartValidationException : Exception
    {
        public ChartValidationException(string message) : base(message) { }
    }
}
