using NUnit.Framework;
using KeyFlow;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class ChartLoaderTests
    {
        private const string ValidJson = @"{
            ""songId"": ""test_song"",
            ""title"": ""Test"",
            ""composer"": ""Anon"",
            ""bpm"": 120,
            ""durationMs"": 5000,
            ""charts"": {
                ""EASY"": {
                    ""totalNotes"": 2,
                    ""notes"": [
                        {""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 0},
                        {""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 500}
                    ]
                }
            }
        }";

        [Test]
        public void ParseJson_ValidInput_PopulatesAllFields()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            Assert.AreEqual("test_song", c.songId);
            Assert.AreEqual(120, c.bpm);
            Assert.AreEqual(5000, c.durationMs);
            Assert.IsTrue(c.charts.ContainsKey(Difficulty.Easy));
            Assert.AreEqual(2, c.charts[Difficulty.Easy].notes.Count);
        }

        [Test]
        public void ParseJson_Tap_HasZeroDur()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            var tap = c.charts[Difficulty.Easy].notes[0];
            Assert.AreEqual(NoteType.TAP, tap.type);
            Assert.AreEqual(0, tap.dur);
        }

        [Test]
        public void ParseJson_Hold_HasPositiveDur()
        {
            var c = ChartLoader.ParseJson(ValidJson);
            var hold = c.charts[Difficulty.Easy].notes[1];
            Assert.AreEqual(NoteType.HOLD, hold.type);
            Assert.AreEqual(500, hold.dur);
        }

        [Test]
        public void ParseJson_InvalidLane_Throws()
        {
            var bad = ValidJson.Replace(@"""lane"": 0", @"""lane"": 5");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_NegativeLane_Throws()
        {
            var bad = ValidJson.Replace(@"""lane"": 0", @"""lane"": -1");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_TapWithDur_Throws()
        {
            var bad = ValidJson.Replace(
                @"{""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 0}",
                @"{""t"": 1000, ""lane"": 0, ""pitch"": 60, ""type"": ""TAP"", ""dur"": 100}");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_HoldWithZeroDur_Throws()
        {
            var bad = ValidJson.Replace(
                @"{""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 500}",
                @"{""t"": 2000, ""lane"": 3, ""pitch"": 72, ""type"": ""HOLD"", ""dur"": 0}");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_PitchOutOfRange_ClampsSilently()
        {
            var c = ChartLoader.ParseJson(ValidJson.Replace(@"""pitch"": 60", @"""pitch"": 100"));
            Assert.AreEqual(83, c.charts[Difficulty.Easy].notes[0].pitch);
        }

        [Test]
        public void ParseJson_TimeBeyondDuration_Throws()
        {
            var bad = ValidJson.Replace(@"""t"": 1000", @"""t"": 999999");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }

        [Test]
        public void ParseJson_NegativeTime_Throws()
        {
            var bad = ValidJson.Replace(@"""t"": 1000", @"""t"": -10");
            Assert.Throws<ChartValidationException>(() => ChartLoader.ParseJson(bad));
        }
    }
}
