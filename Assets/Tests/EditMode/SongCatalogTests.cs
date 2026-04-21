using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class SongCatalogTests
    {
        private const string ValidJson = @"{
            ""version"": 1,
            ""songs"": [
                { ""id"": ""a"", ""title"": ""Song A"", ""composer"": ""X"",
                  ""thumbnail"": ""thumbs/a.png"", ""difficulties"": [""Easy"",""Normal""],
                  ""chartAvailable"": true },
                { ""id"": ""b"", ""title"": ""(locked)"", ""composer"": ""-"",
                  ""thumbnail"": ""thumbs/locked.png"", ""difficulties"": [],
                  ""chartAvailable"": false }
            ]
        }";

        [Test]
        public void ParseJson_Valid_Returns_Entries_With_Fields()
        {
            var entries = SongCatalog.ParseJson(ValidJson);
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("a", entries[0].id);
            Assert.AreEqual("Song A", entries[0].title);
            Assert.AreEqual(2, entries[0].difficulties.Length);
            Assert.IsTrue(entries[0].chartAvailable);
            Assert.IsFalse(entries[1].chartAvailable);
        }

        [Test]
        public void ParseJson_MissingSongs_Throws()
        {
            Assert.Throws<System.FormatException>(() => SongCatalog.ParseJson(@"{""version"":1}"));
        }

        [Test]
        public void ParseJson_EntryMissingId_Throws()
        {
            string bad = @"{""songs"":[{""title"":""x""}]}";
            Assert.Throws<System.FormatException>(() => SongCatalog.ParseJson(bad));
        }

        [Test]
        public void TryGet_ReturnsHitAndMiss()
        {
            var entries = SongCatalog.ParseJson(ValidJson);
            SongCatalog.SetForTesting(entries);
            Assert.IsTrue(SongCatalog.TryGet("a", out var a));
            Assert.AreEqual("Song A", a.title);
            Assert.IsFalse(SongCatalog.TryGet("missing", out _));
        }
    }
}
