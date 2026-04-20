using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;

namespace KeyFlow.UI
{
    public class ResultsScreen : OverlayBase
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text maxComboText;
        [SerializeField] private Text breakdownText;
        [SerializeField] private Text newRecordLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        private const float CountupDuration = 1.5f;

        public System.Action OnRetryPressed;
        public System.Action OnHomePressed;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (homeButton != null) homeButton.onClick.AddListener(OnHome);
        }

        public void Display(ScoreManager score, bool isNewRecord)
        {
            Show();
            titleText.text = UIStrings.SongComplete;
            maxComboText.text = string.Format(UIStrings.MaxComboFmt, score.MaxCombo);
            breakdownText.text = string.Format(UIStrings.BreakdownFmt,
                score.PerfectCount, score.GreatCount, score.GoodCount, score.MissCount);
            newRecordLabel.gameObject.SetActive(isNewRecord);
            if (isNewRecord) newRecordLabel.text = UIStrings.NewRecord;

            retryButton.interactable = false;
            homeButton.interactable = false;

            foreach (var s in starImages) { s.sprite = starEmpty; s.transform.localScale = Vector3.zero; }
            scoreText.text = string.Format(UIStrings.ScoreFmt, 0);

            StartCoroutine(Animate(score.Stars, score.Score));
        }

        private IEnumerator Animate(int stars, int finalScore)
        {
            for (int i = 0; i < stars; i++)
            {
                starImages[i].sprite = starFilled;
                yield return StartCoroutine(ScalePop(starImages[i].transform));
                yield return new WaitForSeconds(0.05f);
            }
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i].transform.localScale == Vector3.zero)
                    starImages[i].transform.localScale = Vector3.one;
            }

            float t = 0;
            while (t < CountupDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / CountupDuration);
                float eased = 1 - Mathf.Pow(1 - u, 2);
                int val = Mathf.FloorToInt(finalScore * eased);
                scoreText.text = string.Format(UIStrings.ScoreFmt, val);
                yield return null;
            }
            scoreText.text = string.Format(UIStrings.ScoreFmt, finalScore);

            retryButton.interactable = true;
            homeButton.interactable = true;
        }

        private IEnumerator ScalePop(Transform tr)
        {
            const float dur = 0.3f;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float s = (u < 0.5f) ? Mathf.Lerp(0, 1.2f, u * 2) : Mathf.Lerp(1.2f, 1.0f, (u - 0.5f) * 2);
                tr.localScale = Vector3.one * s;
                yield return null;
            }
            tr.localScale = Vector3.one;
        }

        private void OnRetry()
        {
            Finish();
            OnRetryPressed?.Invoke();
        }

        private void OnHome()
        {
            Finish();
            if (OnHomePressed != null) OnHomePressed.Invoke();
            else ScreenManager.Instance.Replace(AppScreen.Main);
        }
    }
}
