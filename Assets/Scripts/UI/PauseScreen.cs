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

        private void OnResume()
        {
            if (audioSync != null) audioSync.Resume();
            Finish();
        }

        private void OnQuit()
        {
            if (audioSync != null) audioSync.Resume();
            Finish();
            ScreenManager.Instance.Replace(AppScreen.Main);
        }
    }
}
