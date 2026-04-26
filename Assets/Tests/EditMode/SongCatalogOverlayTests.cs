using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class SongCatalogOverlayTests
    {
        [Test]
        public void MergeOverlay_NullOverlay_ReturnsBaseUnchanged()
        {
            var basePart = new[]
            {
                new SongEntry { id = "a" },
                new SongEntry { id = "b" }
            };
            var result = SongCatalog.MergeOverlay(basePart, null);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.AreEqual("b", result[1].id);
            Assert.IsFalse(result[0].isPersonal);
            Assert.IsFalse(result[1].isPersonal);
        }

        [Test]
        public void MergeOverlay_BaseAndPersonal_AppendsPersonalEntries()
        {
            var basePart = new[] { new SongEntry { id = "a" } };
            var overlayPart = new[] { new SongEntry { id = "b", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(basePart, overlayPart);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.IsFalse(result[0].isPersonal);
            Assert.AreEqual("b", result[1].id);
            Assert.IsTrue(result[1].isPersonal);
        }

        [Test]
        public void MergeOverlay_DuplicateId_PersonalOverridesBase()
        {
            var basePart = new[] { new SongEntry { id = "a", title = "Public" } };
            var overlayPart = new[] { new SongEntry { id = "a", title = "Personal", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(basePart, overlayPart);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Personal", result[0].title);
            Assert.IsTrue(result[0].isPersonal);
        }

        [Test]
        public void MergeOverlay_NullBase_TreatsAsEmpty()
        {
            var overlayPart = new[] { new SongEntry { id = "a", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(null, overlayPart);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.IsTrue(result[0].isPersonal);
        }
    }
}
