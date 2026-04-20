using UnityEngine;

namespace KeyFlow.UI
{
    // Base for modal overlays (Settings/Pause/Results/Calibration). Hidden by
    // default: the first OnEnable call deactivates the GameObject so callers
    // must explicitly Show() to reveal. Using OnEnable rather than Awake
    // because SetActive(false) inside Awake is not guaranteed to commit in
    // Unity's EditMode AddComponent flow; OnEnable runs after the component
    // is fully attached.
    public abstract class OverlayBase : MonoBehaviour
    {
        public bool IsVisible { get; private set; }

        private bool initialized;

        protected virtual void OnEnable()
        {
            if (!initialized)
            {
                initialized = true;
                gameObject.SetActive(false);
            }
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            IsVisible = true;
            OnShown();
        }

        public virtual void Finish()
        {
            OnFinishing();
            gameObject.SetActive(false);
            IsVisible = false;
        }

        protected virtual void OnShown() { }
        protected virtual void OnFinishing() { }
    }
}
