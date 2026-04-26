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

        // Pure merge: base entries first (preserve order), then overlay-only
        // entries (preserve order). Same id in both → overlay wins (last write).
        // Caller is responsible for setting isPersonal=true on overlay entries
        // before calling. Either argument may be null.
        public static SongEntry[] MergeOverlay(SongEntry[] basePart, SongEntry[] overlayPart)
        {
            basePart ??= System.Array.Empty<SongEntry>();
            if (overlayPart == null || overlayPart.Length == 0) return basePart;

            var overlayById = new System.Collections.Generic.Dictionary<string, SongEntry>(overlayPart.Length);
            foreach (var e in overlayPart) overlayById[e.id] = e;

            var result = new System.Collections.Generic.List<SongEntry>(basePart.Length + overlayPart.Length);
            var emittedIds = new System.Collections.Generic.HashSet<string>();
            foreach (var e in basePart)
            {
                if (overlayById.TryGetValue(e.id, out var overlayEntry))
                {
                    UnityEngine.Debug.LogWarning($"[KeyFlow] catalog overlay: id '{e.id}' overrides base entry");
                    result.Add(overlayEntry);
                }
                else
                {
                    result.Add(e);
                }
                emittedIds.Add(e.id);
            }
            foreach (var e in overlayPart)
            {
                if (!emittedIds.Contains(e.id)) { result.Add(e); emittedIds.Add(e.id); }
            }
            return result.ToArray();
        }

        public static IEnumerator LoadAsync()
        {
            string baseJson = null;
            string baseError = null;
            yield return ReadStreamingAssetCo("catalog.kfmanifest",
                t => baseJson = t,
                e => baseError = e);
            if (baseJson == null)
                throw new System.IO.FileNotFoundException($"catalog load failed: {baseError}");

            string overlayJson = null;
            yield return ReadStreamingAssetCo("catalog.personal.kfmanifest",
                t => overlayJson = t,
                _ => { /* optional: missing file is the no-overlay case */ });

            var basePart = ParseJson(baseJson);
            var overlayPart = TryParseOverlay(overlayJson);

            loaded = MergeOverlay(basePart, overlayPart);
        }

        // A malformed personal manifest must NOT brick the public catalog — the
        // user could have a typo in a file the public build never sees. On parse
        // failure, log and return null so MergeOverlay falls back to base-only.
        public static SongEntry[] TryParseOverlay(string overlayJson)
        {
            if (string.IsNullOrEmpty(overlayJson)) return null;
            try
            {
                var entries = ParseJson(overlayJson);
                foreach (var e in entries) e.isPersonal = true;
                return entries;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[KeyFlow] catalog.personal.kfmanifest parse failed: {e.Message}");
                return null;
            }
        }

        private static IEnumerator ReadStreamingAssetCo(
            string file,
            System.Action<string> onText,
            System.Action<string> onError)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string url = Path.Combine(Application.streamingAssetsPath, file);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    onError?.Invoke(req.error);
                else
                    onText?.Invoke(req.downloadHandler.text);
            }
#else
            string path = Path.Combine(Application.streamingAssetsPath, file);
            if (!File.Exists(path)) { onError?.Invoke("file not found"); yield break; }
            onText?.Invoke(File.ReadAllText(path));
            yield break;
#endif
        }

        // Public for EditMode tests (test assembly can't see internal).
        public static void SetForTesting(SongEntry[] entries) =>
            loaded = entries ?? System.Array.Empty<SongEntry>();
    }
}
