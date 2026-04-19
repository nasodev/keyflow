using UnityEngine;

namespace KeyFlow
{
    public class AudioSamplePool : MonoBehaviour
    {
        [SerializeField] private int channels = 16;
        [SerializeField] private AudioClip defaultClip;

        private AudioSource[] sources;
        private int nextIndex;

        public int Count => sources?.Length ?? 0;

        private void Awake()
        {
            if (sources == null) Initialize(channels);
        }

        public void Initialize(int channelCount)
        {
            sources = new AudioSource[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.loop = false;
                src.priority = 64;
                sources[i] = src;
            }
        }

        public void InitializeForTest(int channels) => Initialize(channels);

        public AudioSource NextSource()
        {
            var src = sources[nextIndex];
            nextIndex = (nextIndex + 1) % sources.Length;
            return src;
        }

        public void PlayOneShot(AudioClip clip = null)
        {
            var src = NextSource();
            src.PlayOneShot(clip ?? defaultClip);
        }
    }
}
