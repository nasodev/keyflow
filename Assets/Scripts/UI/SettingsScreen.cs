using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Calibration;

namespace KeyFlow.UI
{
    public class SettingsScreen : OverlayBase
    {
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider noteSpeedSlider;
        [SerializeField] private Text noteSpeedValueLabel;
        [SerializeField] private Button recalibrateButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text versionLabel;
        [SerializeField] private Text creditsLabel;
        [SerializeField] private CalibrationController calibration;

        private void Awake()
        {
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxChanged);
            }
            if (noteSpeedSlider != null)
            {
                noteSpeedSlider.minValue = 1.0f;
                noteSpeedSlider.maxValue = 3.0f;
                noteSpeedSlider.onValueChanged.AddListener(OnNoteSpeedChanged);
            }
            if (recalibrateButton != null) recalibrateButton.onClick.AddListener(OnRecalibrate);
            if (closeButton != null) closeButton.onClick.AddListener(Finish);
            if (versionLabel != null)
                versionLabel.text = string.Format(UIStrings.VersionLabelFmt, Application.version);
            if (creditsLabel != null)
                creditsLabel.text = UIStrings.CreditsSamples;
        }

        protected override void OnShown()
        {
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(UserPrefs.SfxVolume);
            if (noteSpeedSlider != null) noteSpeedSlider.SetValueWithoutNotify(UserPrefs.NoteSpeed);
            if (noteSpeedValueLabel != null) noteSpeedValueLabel.text = UserPrefs.NoteSpeed.ToString("F1");
            AudioListener.volume = UserPrefs.SfxVolume;
        }

        private void OnSfxChanged(float v)
        {
            UserPrefs.SfxVolume = v;
            AudioListener.volume = v;
        }

        private void OnNoteSpeedChanged(float v)
        {
            UserPrefs.NoteSpeed = v;
            if (noteSpeedValueLabel != null) noteSpeedValueLabel.text = v.ToString("F1");
        }

        private void OnRecalibrate()
        {
            Finish();
            if (calibration != null) calibration.Begin(onDone: () => Show());
        }
    }
}
