using UnityEngine;
using UnityEngine.UI;

namespace KeyFlow.UI
{
    // Displays live combo count during gameplay. Hides via comboText.enabled
    // (NOT gameObject.SetActive, which would stop Update and trap HUD hidden).
    // Polls judgmentSystem.Score.Combo rather than subscribing to events so
    // runtime gameplay (ScoreManager, JudgmentSystem) stays untouched.
    public class ComboHUD : MonoBehaviour
    {
        [SerializeField] private JudgmentSystem judgmentSystem;
        [SerializeField] private Text comboText;
        private int lastCombo = -1;

        // EditMode test hooks. Internal so KeyFlow.Tests.EditMode sees them
        // via the existing asmdef reference.
        internal int TextAssignmentCount { get; private set; }
        internal void UpdateForTest() => Update();

        private void Update()
        {
            // Filled in by Task 4.
        }
    }
}
