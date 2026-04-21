using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;

namespace KeyFlow.Editor
{
    public static class W6SamplesWireup
    {
        private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string PianoFolder = "Assets/Audio/piano";

        private static readonly string[] SampleNames =
        {
            "C2v10", "Ds2v10", "Fs2v10", "A2v10",
            "C3v10", "Ds3v10", "Fs3v10", "A3v10",
            "C4v10", "Ds4v10", "Fs4v10", "A4v10",
            "C5v10", "Ds5v10", "Fs5v10", "A5v10",
            "C6v10",
        };

        [MenuItem("KeyFlow/W6 Samples Wireup")]
        public static void Wire()
        {
            // 1. Load the 17 clips in the expected order.
            var clips = new AudioClip[SampleNames.Length];
            for (int i = 0; i < SampleNames.Length; i++)
            {
                string path = $"{PianoFolder}/{SampleNames[i]}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogError($"[W6Wireup] Missing sample: {path}. Aborting.");
                    return;
                }
                clips[i] = clip;
            }

            // 2. Open the gameplay scene.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Wire AudioSamplePool.pitchSamples.
            var pool = Object.FindFirstObjectByType<AudioSamplePool>();
            if (pool == null)
            {
                Debug.LogError("[W6Wireup] AudioSamplePool not found in scene.");
                return;
            }
            var poolSo = new SerializedObject(pool);
            var pitchSamplesProp = poolSo.FindProperty("pitchSamples");
            pitchSamplesProp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                pitchSamplesProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            poolSo.FindProperty("baseMidi").intValue = 36;
            poolSo.FindProperty("stepSemitones").intValue = 3;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Wire TapInputHandler.judgmentSystem.
            var tapInput = Object.FindFirstObjectByType<TapInputHandler>();
            var judgment = Object.FindFirstObjectByType<JudgmentSystem>();
            if (tapInput == null || judgment == null)
            {
                Debug.LogError("[W6Wireup] TapInputHandler or JudgmentSystem missing.");
                return;
            }
            var tapSo = new SerializedObject(tapInput);
            tapSo.FindProperty("judgmentSystem").objectReferenceValue = judgment;
            tapSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. Wire SettingsScreen credits label — create a Text child if needed.
            var settings = Object.FindFirstObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            if (settings == null)
            {
                Debug.LogWarning("[W6Wireup] SettingsScreen not found; skipping credits label wire-up.");
            }
            else
            {
                var settingsSo = new SerializedObject(settings);
                var creditsProp = settingsSo.FindProperty("creditsLabel");
                if (creditsProp.objectReferenceValue == null)
                {
                    var creditsGo = new GameObject("CreditsLabel", typeof(RectTransform), typeof(Text));
                    creditsGo.transform.SetParent(settings.transform, false);
                    var rt = creditsGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.05f, 0.02f);
                    rt.anchorMax = new Vector2(0.95f, 0.08f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var txt = creditsGo.GetComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    txt.fontSize = 18;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    txt.text = UIStrings.CreditsSamples;
                    creditsProp.objectReferenceValue = txt;
                }
                settingsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 6. Save.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[W6Wireup] Done. Samples + JudgmentSystem ref + Credits label wired.");
        }
    }
}
