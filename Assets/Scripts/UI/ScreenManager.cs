using System;
using UnityEngine;
using UnityEngine.InputSystem;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.UI
{
    public enum AppScreen { Start, Main, Gameplay, Results }

    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] private GameObject startRoot;
        [SerializeField] private GameObject mainRoot;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject resultsCanvas;

        [SerializeField] private OverlayBase settingsOverlay;
        [SerializeField] private OverlayBase pauseOverlay;
        [SerializeField] private OverlayBase calibrationOverlay;

        [SerializeField] private BackgroundSwitcher backgroundSwitcher;

        public AppScreen Current { get; private set; }

        public event Action<AppScreen> OnReplaced;

        private float lastBackOnStart = -10f;
        private const float DoubleBackWindow = 2.0f;

        public OverlayBase SettingsOverlay => settingsOverlay;
        public OverlayBase PauseOverlay => pauseOverlay;
        public OverlayBase CalibrationOverlay => calibrationOverlay;

        public void Replace(AppScreen target)
        {
            HideAllOverlays();
            if (startRoot)     startRoot.SetActive(target == AppScreen.Start);
            if (mainRoot)      mainRoot.SetActive(target == AppScreen.Main);
            if (gameplayRoot)  gameplayRoot.SetActive(target == AppScreen.Gameplay);
            if (resultsCanvas) resultsCanvas.SetActive(target == AppScreen.Results);
            Current = target;

            if (target == AppScreen.Gameplay && backgroundSwitcher != null)
                backgroundSwitcher.Apply(SessionProfile.Current);

            OnReplaced?.Invoke(target);
        }

        public void ShowOverlay(OverlayBase o) { if (o != null) o.Show(); }
        public void HideOverlay(OverlayBase o) { if (o != null) o.Finish(); }

        public bool AnyOverlayVisible =>
            (settingsOverlay != null && settingsOverlay.IsVisible) ||
            (pauseOverlay != null && pauseOverlay.IsVisible) ||
            (calibrationOverlay != null && calibrationOverlay.IsVisible);

        private void HideAllOverlays()
        {
            if (settingsOverlay != null && settingsOverlay.IsVisible) settingsOverlay.Finish();
            if (pauseOverlay != null && pauseOverlay.IsVisible) pauseOverlay.Finish();
            if (calibrationOverlay != null && calibrationOverlay.IsVisible) calibrationOverlay.Finish();
        }

        public void HandleBack()
        {
            if (settingsOverlay != null && settingsOverlay.IsVisible) { settingsOverlay.Finish(); return; }
            if (calibrationOverlay != null && calibrationOverlay.IsVisible) return;
            if (pauseOverlay != null && pauseOverlay.IsVisible) { pauseOverlay.Finish(); return; }

            switch (Current)
            {
                case AppScreen.Gameplay:
                    if (pauseOverlay != null) pauseOverlay.Show();
                    break;
                case AppScreen.Results:
                    Replace(AppScreen.Main);
                    break;
                case AppScreen.Main:
                    Replace(AppScreen.Start);
                    break;
                case AppScreen.Start:
                    if (Time.unscaledTime - lastBackOnStart < DoubleBackWindow)
                    {
                        Debug.Log("[ScreenManager] Quit requested on double-back from Start");
                        Application.Quit();
                    }
                    else
                    {
                        lastBackOnStart = Time.unscaledTime;
                    }
                    break;
            }
        }

        private void Awake() { Instance = this; }

        private void Start() { Replace(AppScreen.Start); }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            HandleBack();
        }
    }
}
