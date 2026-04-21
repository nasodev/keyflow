using NUnit.Framework;
using UnityEngine;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class ScreenManagerTests
    {
        private GameObject mgr, mainRoot, gameplayRoot, results;
        private GameObject settingsGO, pauseGO, calibGO;
        private ScreenManager sm;
        private TestOverlay settings, pause, calib;

        private class TestOverlay : OverlayBase { }

        [SetUp]
        public void Setup()
        {
            mgr = new GameObject("sm");
            mainRoot = new GameObject("main");
            gameplayRoot = new GameObject("gameplay");
            results = new GameObject("results");
            settingsGO = new GameObject("settings"); settings = settingsGO.AddComponent<TestOverlay>();
            pauseGO = new GameObject("pause"); pause = pauseGO.AddComponent<TestOverlay>();
            calibGO = new GameObject("calib"); calib = calibGO.AddComponent<TestOverlay>();

            sm = mgr.AddComponent<ScreenManager>();
            SetPrivate(sm, "mainRoot", mainRoot);
            SetPrivate(sm, "gameplayRoot", gameplayRoot);
            SetPrivate(sm, "resultsCanvas", results);
            SetPrivate(sm, "settingsOverlay", settings);
            SetPrivate(sm, "pauseOverlay", pause);
            SetPrivate(sm, "calibrationOverlay", calib);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var go in new[] { mgr, mainRoot, gameplayRoot, results, settingsGO, pauseGO, calibGO })
                if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Replace_Main_ActivatesOnlyMainRoot()
        {
            sm.Replace(AppScreen.Main);
            Assert.IsTrue(mainRoot.activeSelf);
            Assert.IsFalse(gameplayRoot.activeSelf);
            Assert.IsFalse(results.activeSelf);
            Assert.AreEqual(AppScreen.Main, sm.Current);
        }

        [Test]
        public void ShowAndHideOverlay_TogglesIndependentlyOfScreen()
        {
            sm.Replace(AppScreen.Main);
            sm.ShowOverlay(settings);
            Assert.IsTrue(settings.IsVisible);
            Assert.IsTrue(mainRoot.activeSelf, "overlay must not deactivate underlying screen");
            sm.HideOverlay(settings);
            Assert.IsFalse(settings.IsVisible);
        }

        [Test]
        public void Replace_HidesAllVisibleOverlays()
        {
            sm.Replace(AppScreen.Main);
            sm.ShowOverlay(settings);
            sm.Replace(AppScreen.Gameplay);
            Assert.IsFalse(settings.IsVisible);
            Assert.IsTrue(gameplayRoot.activeSelf);
        }

        [Test]
        public void HandleBack_OnGameplayWithNoOverlay_ShowsPauseOverlay()
        {
            sm.Replace(AppScreen.Gameplay);
            sm.HandleBack();
            Assert.IsTrue(pause.IsVisible);
        }

        private static void SetPrivate(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(target, value);
        }
    }
}
