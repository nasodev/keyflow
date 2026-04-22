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
            if (judgmentSystem == null || comboText == null) return;
            var score = judgmentSystem.Score;
            if (score == null) return;

            int current = score.Combo;
            if (current == lastCombo) return;
            lastCombo = current;

            if (current == 0)
            {
                if (comboText.enabled) comboText.enabled = false;
            }
            else
            {
                if (!comboText.enabled) comboText.enabled = true;
                comboText.text = current.ToString();
                TextAssignmentCount++;
            }
        }
    }
}
