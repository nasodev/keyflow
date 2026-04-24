using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private Button nayoonButton;
        [SerializeField] private Button soyoonButton;

        private void Awake()
        {
            if (nayoonButton != null) nayoonButton.onClick.AddListener(() => Select(Profile.Nayoon));
            if (soyoonButton != null) soyoonButton.onClick.AddListener(() => Select(Profile.Soyoon));
        }

        private void Select(Profile p)
        {
            SessionProfile.Current = p;
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.Replace(AppScreen.Main);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void InvokeSelectForTest(Profile p) => Select(p);
#endif
    }
}
