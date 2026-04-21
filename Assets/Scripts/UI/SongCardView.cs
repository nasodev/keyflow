using System;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;

namespace KeyFlow.UI
{
    public class SongCardView : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text composerText;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Button easyButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;

        public event Action<string, Difficulty> OnDifficultySelected;

        public void Bind(SongEntry entry, Sprite thumbnail)
        {
            titleText.text = entry.title;
            composerText.text = entry.composer;
            if (thumbnail != null) thumbnailImage.sprite = thumbnail;

            int bestStars = entry.chartAvailable
                ? UserPrefs.GetBestStars(entry.id, Difficulty.Easy)
                : 0;
            for (int i = 0; i < starImages.Length; i++)
                starImages[i].sprite = (i < bestStars) ? starFilled : starEmpty;

            bool easy = entry.chartAvailable && entry.HasDifficulty(Difficulty.Easy);
            bool normal = entry.chartAvailable && entry.HasDifficulty(Difficulty.Normal);
            easyButton.interactable = easy;
            normalButton.interactable = normal;
            canvasGroup.alpha = entry.chartAvailable ? 1f : 0.5f;

            easyButton.onClick.RemoveAllListeners();
            normalButton.onClick.RemoveAllListeners();
            if (easy) easyButton.onClick.AddListener(() => OnDifficultySelected?.Invoke(entry.id, Difficulty.Easy));
            if (normal) normalButton.onClick.AddListener(() => OnDifficultySelected?.Invoke(entry.id, Difficulty.Normal));
        }
    }
}
