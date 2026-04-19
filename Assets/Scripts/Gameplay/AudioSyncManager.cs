using UnityEngine;

namespace KeyFlow
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSyncManager : MonoBehaviour
    {
        [SerializeField] private double scheduleLeadSec = 0.5;

        private AudioSource bgmSource;
        private double songStartDsp;
        private bool started;

        public double SongStartDspTime => songStartDsp;
        public bool IsPlaying => started;
        public double CalibrationOffsetSec { get; set; } = 0.0;

        public int SongTimeMs =>
            started ? GameTime.GetSongTimeMs(AudioSettings.dspTime, songStartDsp, CalibrationOffsetSec) : 0;

        private void Awake()
        {
            bgmSource = GetComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        public void StartSilentSong()
        {
            songStartDsp = AudioSettings.dspTime + scheduleLeadSec;
            started = true;
        }

        public void StartSong(AudioClip bgm)
        {
            bgmSource.clip = bgm;
            songStartDsp = AudioSettings.dspTime + scheduleLeadSec;
            bgmSource.PlayScheduled(songStartDsp);
            started = true;
        }
    }
}
