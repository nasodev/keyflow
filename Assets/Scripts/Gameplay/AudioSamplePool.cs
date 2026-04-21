using UnityEngine;

namespace KeyFlow
{
    public class AudioSamplePool : MonoBehaviour
    {
        [SerializeField] private int channels = 16;
        [SerializeField] private AudioClip defaultClip;

        [Header("Pitch Sample Map")]
        [SerializeField] private AudioClip[] pitchSamples;
        [SerializeField] private int baseMidi = 36;
        [SerializeField] private int stepSemitones = 3;

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
            src.pitch = 1f;
            src.PlayOneShot(clip ?? defaultClip);
        }

        public void PlayForPitch(int midiPitch)
        {
            var (clip, ratio) = ResolveSample(midiPitch, pitchSamples, baseMidi, stepSemitones);
            if (clip == null)
            {
                PlayOneShot();
                return;
            }
            var src = NextSource();
            src.pitch = ratio;
            src.PlayOneShot(clip);
        }

        public static (AudioClip clip, float pitchRatio) ResolveSample(
            int midiPitch,
            AudioClip[] pitchSamples,
            int baseMidi,
            int stepSemitones)
        {
            if (pitchSamples == null || pitchSamples.Length == 0) return (null, 1f);

            int hi = baseMidi + (pitchSamples.Length - 1) * stepSemitones;
            int p = System.Math.Clamp(midiPitch, baseMidi, hi);

            int baseIdx = (p - baseMidi) / stepSemitones;
            int sampleMidi = baseMidi + baseIdx * stepSemitones;
            int offset = p - sampleMidi;

            if (offset == 2 && baseIdx + 1 < pitchSamples.Length)
            {
                baseIdx += 1;
                sampleMidi = baseMidi + baseIdx * stepSemitones;
                offset = -1;
            }

            float ratio = Mathf.Pow(2f, offset / 12f);
            return (pitchSamples[baseIdx], ratio);
        }
    }
}
