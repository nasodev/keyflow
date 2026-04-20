using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace KeyFlow
{
    public static class SongCatalog
    {
        private static SongEntry[] loaded = System.Array.Empty<SongEntry>();

        public static IReadOnlyList<SongEntry> All => loaded;

        public static SongEntry[] ParseJson(string json)
        {
            var dto = JsonConvert.DeserializeObject<CatalogDto>(json);
            if (dto == null) throw new System.FormatException("Null catalog");
            if (dto.songs == null) throw new System.FormatException("Missing 'songs' array");
            foreach (var s in dto.songs)
            {
                if (string.IsNullOrEmpty(s.id))
                    throw new System.FormatException("Entry missing 'id'");
            }
            return dto.songs;
        }

        public static bool TryGet(string id, out SongEntry entry)
        {
            foreach (var s in loaded)
            {
                if (s.id == id) { entry = s; return true; }
            }
            entry = null;
            return false;
        }

        public static IEnumerator LoadAsync()
        {
            const string file = "catalog.kfmanifest";
#if UNITY_ANDROID && !UNITY_EDITOR
            string url = Path.Combine(Application.streamingAssetsPath, file);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new System.IO.FileNotFoundException($"catalog load failed: {req.error}");
                loaded = ParseJson(req.downloadHandler.text);
            }
#else
            string path = Path.Combine(Application.streamingAssetsPath, file);
            loaded = ParseJson(File.ReadAllText(path));
            yield break;
#endif
        }

        // Public for EditMode tests (test assembly can't see internal).
        public static void SetForTesting(SongEntry[] entries) =>
            loaded = entries ?? System.Array.Empty<SongEntry>();
    }
}
