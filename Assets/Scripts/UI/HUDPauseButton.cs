using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    [RequireComponent(typeof(Button))]
    public class HUDPauseButton : MonoBehaviour
    {
        [SerializeField] private OverlayBase pauseOverlay;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (pauseOverlay != null)
                button.onClick.AddListener(() => pauseOverlay.Show());
        }
    }
}
