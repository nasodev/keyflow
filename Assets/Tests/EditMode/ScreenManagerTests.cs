using NUnit.Framework;
using UnityEngine;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Tests.EditMode
{
    public class ScreenManagerTests
    {
        private GameObject mgr, mainRoot, gameplayRoot, results;
        private GameObject settingsGO, pauseGO, calibGO;
        private GameObject startCanvas;
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
            startCanvas = new GameObject("start");

            sm = mgr.AddComponent<ScreenManager>();
            SetPrivate(sm, "startRoot", startCanvas);
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
            foreach (var go in new[] { mgr, mainRoot, gameplayRoot, results, settingsGO, pauseGO, calibGO, startCanvas })
                if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Replace_Main_ActivatesOnlyMainRoot()
        {
            sm.Replace(AppScreen.Main);
            Assert.IsTrue(mainRoot.activeSelf);
            Assert.IsFalse(gameplayRoot.activeSelf);
            Assert.IsFalse(results.activeSelf);
            Assert.IsFalse(startCanvas.activeSelf);
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

        [Test]
        public void Replace_Start_ActivatesOnlyStartRoot()
        {
            sm.Replace(AppScreen.Start);
            Assert.IsTrue(startCanvas.activeSelf);
            Assert.IsFalse(mainRoot.activeSelf);
            Assert.IsFalse(gameplayRoot.activeSelf);
            Assert.IsFalse(results.activeSelf);
            Assert.AreEqual(AppScreen.Start, sm.Current);
        }

        [Test]
        public void Replace_FromStartToMain_DeactivatesStart()
        {
            sm.Replace(AppScreen.Start);
            sm.Replace(AppScreen.Main);
            Assert.IsFalse(startCanvas.activeSelf);
            Assert.IsTrue(mainRoot.activeSelf);
        }

        [Test]
        public void HandleBack_FromMain_GoesToStart()
        {
            sm.Replace(AppScreen.Main);
            sm.HandleBack();
            Assert.AreEqual(AppScreen.Start, sm.Current);
        }

        [Test]
        public void Replace_MainToGameplay_AppliesProfileBackground()
        {
            SessionProfile.Current = Profile.Soyoon;
            int applyCount = 0;
            Profile? lastApplied = null;
            var spyHost = new GameObject("spy");
            var spy = spyHost.AddComponent<BackgroundSwitcherSpy>();
            spy.OnApply = p => { applyCount++; lastApplied = p; };
            SetPrivate(sm, "backgroundSwitcher", spy);

            sm.Replace(AppScreen.Main);
            Assert.AreEqual(0, applyCount);

            sm.Replace(AppScreen.Gameplay);
            Assert.AreEqual(1, applyCount);
            Assert.AreEqual(Profile.Soyoon, lastApplied);

            SessionProfile.Current = Profile.Nayoon;  // cleanup for next test
            Object.DestroyImmediate(spyHost);
        }

        private static void SetPrivate(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(target, value);
        }
    }

    public class BackgroundSwitcherSpy : KeyFlow.Feedback.BackgroundSwitcher
    {
        public System.Action<Profile> OnApply;
        public override void Apply(Profile p) { OnApply?.Invoke(p); }
    }
}
