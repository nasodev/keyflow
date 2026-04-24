using UnityEngine;

namespace KeyFlow.UI
{
    public class AudioSourceClickPlayer : IClickPlayer
    {
        private readonly AudioSource source;
        private readonly AudioClip clip;

        public AudioSourceClickPlayer(AudioSource source, AudioClip clip)
        {
            this.source = source;
            this.clip = clip;
        }

        public void Play(float pitch)
        {
            source.pitch = pitch;
            source.PlayOneShot(clip);
        }
    }
}
