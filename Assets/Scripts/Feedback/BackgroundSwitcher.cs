using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.Feedback
{
    public class BackgroundSwitcher : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite blueBg;
        [SerializeField] private Sprite yellowBg;

        public virtual void Apply(Profile p)
        {
            if (backgroundImage == null) return;
            backgroundImage.sprite = (p == Profile.Soyoon) ? yellowBg : blueBg;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal void SetDependenciesForTest(Image img, Sprite blue, Sprite yellow)
        {
            backgroundImage = img;
            blueBg = blue;
            yellowBg = yellow;
        }
#endif
    }
}
