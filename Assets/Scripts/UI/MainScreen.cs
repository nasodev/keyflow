using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using KeyFlow;

namespace KeyFlow.UI
{
    public class MainScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button settingsButton;
        [SerializeField] private OverlayBase settingsOverlay;
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;

        private readonly List<SongCardView> cards = new();

        private IEnumerator Start()
        {
            settingsButton.onClick.AddListener(() => settingsOverlay.Show());
            yield return SongCatalog.LoadAsync();
            PopulateCards();
        }

        public void Refresh()
        {
            foreach (Transform child in cardContainer) Destroy(child.gameObject);
            cards.Clear();
            PopulateCards();
        }

        private void PopulateCards()
        {
            foreach (var entry in SongCatalog.All)
            {
                var go = Instantiate(cardPrefab, cardContainer);
                go.SetActive(true);
                var card = go.GetComponent<SongCardView>();
                SetPrivate(card, "starFilled", starFilled);
                SetPrivate(card, "starEmpty", starEmpty);
                StartCoroutine(LoadThumbnailThenBind(card, entry));
                card.OnDifficultySelected += HandleCardTap;
                cards.Add(card);
            }
        }

        private IEnumerator LoadThumbnailThenBind(SongCardView card, SongEntry entry)
        {
            Sprite sprite = null;
            string path = Path.Combine(Application.streamingAssetsPath, entry.thumbnail);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var req = UnityWebRequestTexture.GetTexture(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
#else
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            yield return null;
#endif
            card.Bind(entry, sprite);
        }

        private void HandleCardTap(string songId, Difficulty difficulty)
        {
            SongSession.CurrentSongId = songId;
            SongSession.CurrentDifficulty = difficulty;
            ScreenManager.Instance.Replace(AppScreen.Gameplay);
        }

        private static void SetPrivate(object t, string name, object v) =>
            t.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
              .SetValue(t, v);
    }
}
