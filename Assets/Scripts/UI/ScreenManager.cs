using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyFlow.UI
{
    public enum AppScreen { Main, Gameplay, Results }

    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] private GameObject mainRoot;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject resultsCanvas;

        [SerializeField] private OverlayBase settingsOverlay;
        [SerializeField] private OverlayBase pauseOverlay;
        [SerializeField] private OverlayBase calibrationOverlay;

        public AppScreen Current { get; private set; }

        public event Action<AppScreen> OnReplaced;

        private float lastBackOnMain = -10f;
        private const float DoubleBackWindow = 2.0f;

        public OverlayBase SettingsOverlay => settingsOverlay;
        public OverlayBase PauseOverlay => pauseOverlay;
        public OverlayBase CalibrationOverlay => calibrationOverlay;

        public void Replace(AppScreen target)
        {
            HideAllOverlays();
            if (mainRoot) mainRoot.SetActive(target == AppScreen.Main);
            if (gameplayRoot) gameplayRoot.SetActive(target == AppScreen.Gameplay);
            if (resultsCanvas) resultsCanvas.SetActive(target == AppScreen.Results);
            Current = target;
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
                    if (Time.unscaledTime - lastBackOnMain < DoubleBackWindow)
                        Application.Quit();
                    else
                        lastBackOnMain = Time.unscaledTime;
                    break;
            }
        }

        private void Awake() { Instance = this; }

        // Start runs after all SerializeField refs are wired — safe to flip
        // roots via Replace. Not in Awake because EditMode tests inject
        // refs via reflection after AddComponent + Awake have already run.
        private void Start() { Replace(AppScreen.Main); }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            HandleBack();
        }
    }
}
