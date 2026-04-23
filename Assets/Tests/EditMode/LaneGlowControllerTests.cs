using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class LaneGlowControllerTests
    {
        private static (LaneGlowController controller, SpriteRenderer[] sprites, GameObject host)
            Build()
        {
            var host = new GameObject("LaneGlow");
            var sprites = new SpriteRenderer[LaneLayout.LaneCount];
            for (int i = 0; i < LaneLayout.LaneCount; i++)
            {
                var child = new GameObject($"Glow_{i}");
                child.transform.SetParent(host.transform);
                sprites[i] = child.AddComponent<SpriteRenderer>();
                sprites[i].color = new Color(1, 1, 1, 0);
            }
            var ctrl = host.AddComponent<LaneGlowController>();
            ctrl.SetSpritesForTest(sprites);
            return (ctrl, sprites, host);
        }

        [Test]
        public void InitialState_AllAlphaZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.TickForTest();
            for (int i = 0; i < sprites.Length; i++)
                Assert.AreEqual(0f, sprites[i].color.a, 1e-5f, $"lane {i}");
            Object.DestroyImmediate(host);
        }

        [Test]
        public void On_SingleLane_AlphaGreaterThanZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.TickForTest();
            Assert.Greater(sprites[1].color.a, 0f);
            Assert.AreEqual(0f, sprites[0].color.a, 1e-5f);
            Assert.AreEqual(0f, sprites[2].color.a, 1e-5f);
            Assert.AreEqual(0f, sprites[3].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Off_AfterOn_ResetsAlphaToZero()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.TickForTest();
            Assert.Greater(sprites[1].color.a, 0f);

            ctrl.Off(1);
            ctrl.TickForTest();
            Assert.AreEqual(0f, sprites[1].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void OnOn_Idempotent()
        {
            var (ctrl, sprites, host) = Build();
            ctrl.On(1);
            ctrl.On(1);
            ctrl.Off(1);
            ctrl.TickForTest();
            Assert.AreEqual(0f, sprites[1].color.a, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Clear_ResetsAllActiveLanes()
        {
            var (ctrl, sprites, host) = Build();
            for (int i = 0; i < LaneLayout.LaneCount; i++) ctrl.On(i);
            ctrl.TickForTest();
            for (int i = 0; i < LaneLayout.LaneCount; i++)
                Assert.Greater(sprites[i].color.a, 0f);

            ctrl.Clear();
            ctrl.TickForTest();
            for (int i = 0; i < LaneLayout.LaneCount; i++)
                Assert.AreEqual(0f, sprites[i].color.a, 1e-5f);

            Object.DestroyImmediate(host);
        }
    }
}
