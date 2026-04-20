using UnityEngine;
using UnityEngine.UI;
using KeyFlow;

namespace KeyFlow.UI
{
    public class PauseScreen : OverlayBase
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private AudioSyncManager audioSync;

        private void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        }

        protected override void OnShown()
        {
            if (audioSync != null) audioSync.Pause();
        }

        protected override void OnFinishing()
        {
            if (audioSync != null) audioSync.Resume();
        }

        private void OnResume()
        {
            Finish();
        }

        private void OnQuit()
        {
            Finish();
            ScreenManager.Instance.Replace(AppScreen.Main);
        }
    }
}
