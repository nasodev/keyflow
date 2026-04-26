using System;
using Newtonsoft.Json;

namespace KeyFlow
{
    [Serializable]
    public class SongEntry
    {
        public string id;
        public string title;
        public string composer;
        public string thumbnail;
        public string[] difficulties;
        public bool chartAvailable;

        // Set transiently by SongCatalog.LoadAsync from which manifest the entry came from.
        [JsonIgnore]
        public bool isPersonal;

        public bool HasDifficulty(Difficulty d)
        {
            if (difficulties == null) return false;
            string target = d.ToString();
            foreach (var s in difficulties) if (s == target) return true;
            return false;
        }
    }

    [Serializable]
    internal class CatalogDto
    {
        public int version;
        public SongEntry[] songs;
    }
}
