using UnityEngine;

namespace KeyFlow
{
    // Public for EditMode tests; production code should not touch.
    public interface ITimeSource { double DspTime { get; } }

    internal class RealTimeSource : ITimeSource
    {
        public double DspTime => AudioSettings.dspTime;
    }

    [RequireComponent(typeof(AudioSource))]
    public class AudioSyncManager : MonoBehaviour
    {
        [SerializeField] private double scheduleLeadSec = 0.5;

        private AudioSource bgmSource;
        private double songStartDsp;
        private bool started;

        // Pause state
        private bool paused;
        private double pauseStartDsp;

        // Test seam: default to AudioSettings.dspTime; tests inject a manual clock.
        public ITimeSource TimeSource { get; set; } = new RealTimeSource();

        public double SongStartDspTime => songStartDsp;
        public bool IsPlaying => started;
        public bool IsPaused => paused;
        public double CalibrationOffsetSec { get; set; } = 0.0;

        public int SongTimeMs
        {
            get
            {
                if (!started) return 0;
                double nowDsp = paused ? pauseStartDsp : TimeSource.DspTime;
                return GameTime.GetSongTimeMs(nowDsp, songStartDsp, CalibrationOffsetSec);
            }
        }

        private void Awake()
        {
            bgmSource = GetComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        public void StartSilentSong()
        {
            songStartDsp = TimeSource.DspTime + scheduleLeadSec;
            started = true;
            paused = false;
        }

        public void StartSong(AudioClip bgm)
        {
            bgmSource.clip = bgm;
            songStartDsp = TimeSource.DspTime + scheduleLeadSec;
            bgmSource.PlayScheduled(songStartDsp);
            started = true;
            paused = false;
        }

        // Reset between gameplay sessions. SP11 countdown defers StartSilentSong
        // behind a 3-second gap; without Stop(), a retry scenario leaves `started`
        // true from the prior session — NoteSpawner then sees IsPlaying=true with a
        // stale songStartDsp, reports huge SongTimeMs, and spawns every upcoming
        // note at once during the countdown window.
        public void Stop()
        {
            if (paused) AudioListener.pause = false;
            started = false;
            paused = false;
        }

        public void Pause()
        {
            if (paused || !started) return;
            pauseStartDsp = TimeSource.DspTime;
            AudioListener.pause = true;
            paused = true;
        }

        public void Resume()
        {
            if (!paused) return;
            double elapsed = TimeSource.DspTime - pauseStartDsp;
            songStartDsp += elapsed;
            AudioListener.pause = false;
            paused = false;
        }
    }
}
