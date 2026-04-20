using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using KeyFlow;

namespace KeyFlow.UI
{
    public class CompletionPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text breakdownText;
        [SerializeField] private Text starsText;
        [SerializeField] private Button restartButton;

        private bool shown;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show(ScoreManager score)
        {
            shown = true;
            gameObject.SetActive(true);
            titleText.text = "SONG COMPLETE";
            scoreText.text = $"Score: {score.Score:N0}";
            comboText.text = $"Max Combo: {score.MaxCombo}";
            breakdownText.text =
                $"Perfect: {score.PerfectCount}   Great: {score.GreatCount}   Good: {score.GoodCount}   Miss: {score.MissCount}";
            starsText.text = new string('*', score.Stars) + new string('-', 3 - score.Stars);

            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(Restart);
        }

        private void Update()
        {
            if (!shown) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Restart();
        }

        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
