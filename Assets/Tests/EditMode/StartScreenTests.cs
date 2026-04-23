using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class StartScreenTests
    {
        private GameObject mgr, startCanvas, mainRoot, gameplayRoot, results;
        private GameObject nayoonBtnGO, soyoonBtnGO;
        private ScreenManager sm;
        private StartScreen startScreen;
        private Button nayoonBtn, soyoonBtn;

        private class TestOverlay : OverlayBase { }

        [SetUp]
        public void Setup()
        {
            SessionProfile.Current = Profile.Nayoon;

            mgr = new GameObject("sm");
            startCanvas = new GameObject("start");
            mainRoot = new GameObject("main");
            gameplayRoot = new GameObject("gameplay");
            results = new GameObject("results");

            var settings = new GameObject("settings").AddComponent<TestOverlay>();
            var pause = new GameObject("pause").AddComponent<TestOverlay>();
            var calib = new GameObject("calib").AddComponent<TestOverlay>();

            sm = mgr.AddComponent<ScreenManager>();
            SetPrivate(sm, "startRoot", startCanvas);
            SetPrivate(sm, "mainRoot", mainRoot);
            SetPrivate(sm, "gameplayRoot", gameplayRoot);
            SetPrivate(sm, "resultsCanvas", results);
            SetPrivate(sm, "settingsOverlay", settings);
            SetPrivate(sm, "pauseOverlay", pause);
            SetPrivate(sm, "calibrationOverlay", calib);

            // EditMode tests don't reliably fire Awake; set singleton by hand so
            // StartScreen.Select can reach this sm via ScreenManager.Instance.
            typeof(ScreenManager).GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { sm });

            nayoonBtnGO = new GameObject("nayoon"); nayoonBtn = nayoonBtnGO.AddComponent<Button>();
            soyoonBtnGO = new GameObject("soyoon"); soyoonBtn = soyoonBtnGO.AddComponent<Button>();

            var startGO = new GameObject("startScreen");
            startScreen = startGO.AddComponent<StartScreen>();
            SetPrivate(startScreen, "nayoonButton", nayoonBtn);
            SetPrivate(startScreen, "soyoonButton", soyoonBtn);
        }

        [TearDown]
        public void Teardown()
        {
            SessionProfile.Current = Profile.Nayoon;
            foreach (var go in new[] { mgr, startCanvas, mainRoot, gameplayRoot, results, nayoonBtnGO, soyoonBtnGO })
                if (go != null) Object.DestroyImmediate(go);
            foreach (var overlay in GameObject.FindObjectsByType<TestOverlay>(FindObjectsSortMode.None))
                Object.DestroyImmediate(overlay.gameObject);
            foreach (var s in GameObject.FindObjectsByType<StartScreen>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
        }

        [Test]
        public void SelectNayoon_SetsProfileNayoon()
        {
            SessionProfile.Current = Profile.Soyoon;
            startScreen.InvokeSelectForTest(Profile.Nayoon);
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }

        [Test]
        public void SelectSoyoon_SetsProfileSoyoon()
        {
            startScreen.InvokeSelectForTest(Profile.Soyoon);
            Assert.AreEqual(Profile.Soyoon, SessionProfile.Current);
        }

        [Test]
        public void SelectNayoon_ReplacesToMain()
        {
            sm.Replace(AppScreen.Start);
            startScreen.InvokeSelectForTest(Profile.Nayoon);
            Assert.AreEqual(AppScreen.Main, sm.Current);
            Assert.IsTrue(mainRoot.activeSelf);
            Assert.IsFalse(startCanvas.activeSelf);
        }

        [Test]
        public void SelectSoyoon_ReplacesToMain()
        {
            sm.Replace(AppScreen.Start);
            startScreen.InvokeSelectForTest(Profile.Soyoon);
            Assert.AreEqual(AppScreen.Main, sm.Current);
        }

        private static void SetPrivate(object t, string name, object v) =>
            t.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
              .SetValue(t, v);
    }
}
