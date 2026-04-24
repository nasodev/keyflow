using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class JudgmentTextPoolTests
    {
        private static (GameObject root, JudgmentTextPool pool, FeedbackPresets presets)
            BuildPool(int size = 12)
        {
            var root = new GameObject("textCanvas");
            root.AddComponent<RectTransform>();
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var presets = ScriptableObject.CreateInstance<FeedbackPresets>();
            presets.perfectTextColor = new Color(1f, 0f, 0f, 1f);
            presets.greatTextColor   = new Color(0f, 1f, 0f, 1f);
            presets.goodTextColor    = new Color(0f, 0f, 1f, 1f);
            presets.missTextColor    = new Color(1f, 1f, 0f, 1f);

            var pool = root.AddComponent<JudgmentTextPool>();
            pool.InitializeForTest(presets, poolSize: size, lifetimeSec: 0.45f,
                                   yRiseUnits: 0.36f, fontSize: 48, worldCanvasScale: 1f);
            return (root, pool, presets);
        }

        [Test]
        public void Spawn_FirstCall_ActivatesIndexZero()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            Assert.IsTrue(pool.GetSlotForTest(0).activeSelf);
            for (int i = 1; i < 12; i++)
                Assert.IsFalse(pool.GetSlotForTest(i).activeSelf, $"slot {i} should be inactive");
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_RoundRobin_CyclesThroughPool()
        {
            var (root, pool, presets) = BuildPool();
            for (int i = 0; i < 12; i++) pool.Spawn(Judgment.Perfect, Vector3.zero);
            for (int i = 0; i < 12; i++)
                Assert.IsTrue(pool.GetSlotForTest(i).activeSelf, $"slot {i} should be active after full cycle");
            pool.Spawn(Judgment.Miss, new Vector3(5f, 0f, 0f));
            var slot0Text = pool.GetSlotForTest(0).GetComponent<Text>();
            Assert.AreEqual("MISS", slot0Text.text);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_AppliesPresetColorPerJudgment()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            pool.Spawn(Judgment.Great, Vector3.zero);
            pool.Spawn(Judgment.Good, Vector3.zero);
            pool.Spawn(Judgment.Miss, Vector3.zero);
            Assert.AreEqual(new Color(1f, 0f, 0f, 1f), pool.GetSlotForTest(0).GetComponent<Text>().color);
            Assert.AreEqual(new Color(0f, 1f, 0f, 1f), pool.GetSlotForTest(1).GetComponent<Text>().color);
            Assert.AreEqual(new Color(0f, 0f, 1f, 1f), pool.GetSlotForTest(2).GetComponent<Text>().color);
            Assert.AreEqual(new Color(1f, 1f, 0f, 1f), pool.GetSlotForTest(3).GetComponent<Text>().color);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_SetsTextString_MatchesJudgment()
        {
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, Vector3.zero);
            pool.Spawn(Judgment.Great, Vector3.zero);
            pool.Spawn(Judgment.Good, Vector3.zero);
            pool.Spawn(Judgment.Miss, Vector3.zero);
            Assert.AreEqual("PERFECT", pool.GetSlotForTest(0).GetComponent<Text>().text);
            Assert.AreEqual("GREAT",   pool.GetSlotForTest(1).GetComponent<Text>().text);
            Assert.AreEqual("GOOD",    pool.GetSlotForTest(2).GetComponent<Text>().text);
            Assert.AreEqual("MISS",    pool.GetSlotForTest(3).GetComponent<Text>().text);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_AlwaysPlacesAtCanvasOrigin_IgnoresWorldPos()
        {
            // Popups render at canvas origin (top-center of gameplay, set by
            // SceneBuilder). worldPos is intentionally ignored so the player
            // sees one centered popup regardless of which lane fired the tap.
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Perfect, new Vector3(2.5f, 99f, 0f));
            var rt = pool.GetSlotForTest(0).GetComponent<RectTransform>();
            Assert.AreEqual(0f, rt.anchoredPosition.x, 0.0001f, "x is canvas origin, independent of worldPos.x");
            Assert.AreEqual(0f, rt.anchoredPosition.y, 0.0001f, "y is canvas origin, independent of worldPos.y");
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }

        [Test]
        public void Spawn_Miss_AlsoAtCanvasOrigin_IgnoresWorldPos()
        {
            // Miss can fire with worldPos above or below judgment line (note
            // expired past line, or hold broke mid-tile). Centered position
            // policy applies identically to Miss.
            var (root, pool, presets) = BuildPool();
            pool.Spawn(Judgment.Miss, new Vector3(-1.5f, 50f, 0f));
            var rt = pool.GetSlotForTest(0).GetComponent<RectTransform>();
            Assert.AreEqual(0f, rt.anchoredPosition.x, 0.0001f);
            Assert.AreEqual(0f, rt.anchoredPosition.y, 0.0001f);
            Object.DestroyImmediate(root); Object.DestroyImmediate(presets);
        }
    }
}
