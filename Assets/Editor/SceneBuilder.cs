using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow.UI;

namespace KeyFlow.Editor
{
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string NotePrefabPath = "Assets/Prefabs/Note.prefab";
        private const string WhiteSpritePath = "Assets/Sprites/white.png";

        // Portrait layout (camera orthographic size 8 → 9 world-unit-wide viewport at 9:16 aspect)
        private const float LaneAreaWidth = 4f;       // world units
        private const float SpawnY = 6.5f;
        private const float JudgmentY = -5f;           // ~81% down the viewport (spec §4.3 target: 80%)

        [MenuItem("KeyFlow/Build W3 Gameplay Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Sprites");

            var whiteSprite = EnsureWhiteSprite();
            var pianoClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/piano_c4.wav");
            if (pianoClip == null)
            {
                Debug.LogError("[KeyFlow] Missing Assets/Audio/piano_c4.wav. Aborting.");
                return;
            }

            var notePrefab = BuildNotePrefab(whiteSprite);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GameplayScene";

            var camera = BuildMainCamera();
            BuildLaneDividers(whiteSprite);
            BuildJudgmentLine(whiteSprite);
            BuildManagers(
                camera, pianoClip, notePrefab,
                out var audioSync, out var samplePool, out var tapInput,
                out var judgmentSystem, out var spawner, out var holdTracker);
            BuildHUD(audioSync, tapInput, samplePool, judgmentSystem);

            var calibration = BuildCalibrationOverlay(whiteSprite, pianoClip, audioSync);
            var completionPanel = BuildCompletionPanel(whiteSprite);
            BuildGameplayController(calibration, audioSync, spawner, judgmentSystem, completionPanel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log($"[KeyFlow] W3 scene built: {ScenePath}");
        }

        private static Camera BuildMainCamera()
        {
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 0, -10);
            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cam.AddComponent<AudioListener>();
            return camera;
        }

        private static void BuildLaneDividers(Sprite sprite)
        {
            float leftEdge = -LaneAreaWidth / 2f;
            for (int i = 0; i <= LaneLayout.LaneCount; i++)
            {
                float x = leftEdge + i * (LaneAreaWidth / LaneLayout.LaneCount);
                var go = new GameObject($"LaneDivider_{i}");
                go.transform.position = new Vector3(x, 0, 0);
                go.transform.localScale = new Vector3(0.02f, 20f, 1);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
                sr.sortingOrder = -1;
            }
        }

        private static GameObject BuildJudgmentLine(Sprite sprite)
        {
            var go = new GameObject("JudgmentLine");
            go.transform.position = new Vector3(0, JudgmentY, 0);
            go.transform.localScale = new Vector3(LaneAreaWidth, 0.12f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.2f, 0.9f, 1.0f, 1);
            sr.sortingOrder = 0;
            return go;
        }

        private static void BuildManagers(
            Camera camera,
            AudioClip pianoClip,
            GameObject notePrefab,
            out AudioSyncManager audioSync,
            out AudioSamplePool samplePool,
            out TapInputHandler tapInput,
            out JudgmentSystem judgmentSystem,
            out NoteSpawner spawner,
            out HoldTracker holdTracker)
        {
            var managers = new GameObject("Managers");

            var audioSyncGO = new GameObject("AudioSync");
            audioSyncGO.transform.SetParent(managers.transform);
            audioSyncGO.AddComponent<AudioSource>();
            audioSync = audioSyncGO.AddComponent<AudioSyncManager>();

            var samplePoolGO = new GameObject("SamplePool");
            samplePoolGO.transform.SetParent(managers.transform);
            samplePool = samplePoolGO.AddComponent<AudioSamplePool>();
            SetField(samplePool, "defaultClip", pianoClip);

            var tapInputGO = new GameObject("TapInput");
            tapInputGO.transform.SetParent(managers.transform);
            tapInput = tapInputGO.AddComponent<TapInputHandler>();
            SetField(tapInput, "samplePool", samplePool);
            SetField(tapInput, "audioSync", audioSync);
            SetField(tapInput, "mainCamera", camera);
            SetField(tapInput, "laneAreaWidth", LaneAreaWidth);

            var judgmentGO = new GameObject("JudgmentSystem");
            judgmentGO.transform.SetParent(managers.transform);
            judgmentSystem = judgmentGO.AddComponent<JudgmentSystem>();
            SetField(judgmentSystem, "tapInput", tapInput);

            var holdTrackerGO = new GameObject("HoldTracker");
            holdTrackerGO.transform.SetParent(managers.transform);
            holdTracker = holdTrackerGO.AddComponent<HoldTracker>();
            SetField(holdTracker, "tapInput", tapInput);
            SetField(holdTracker, "audioSync", audioSync);
            SetField(holdTracker, "judgmentSystem", judgmentSystem);

            // Reverse wire: JudgmentSystem needs HoldTracker to hand off HOLD start taps.
            SetField(judgmentSystem, "holdTracker", holdTracker);

            var spawnerGO = new GameObject("Spawner");
            spawnerGO.transform.SetParent(managers.transform);
            spawner = spawnerGO.AddComponent<NoteSpawner>();
            SetField(spawner, "notePrefab", notePrefab);
            SetField(spawner, "audioSync", audioSync);
            SetField(spawner, "judgmentSystem", judgmentSystem);
            SetField(spawner, "laneAreaWidth", LaneAreaWidth);
            SetField(spawner, "spawnY", SpawnY);
            SetField(spawner, "judgmentY", JudgmentY);
        }

        private static void BuildHUD(
            AudioSyncManager audioSync,
            TapInputHandler tapInput,
            AudioSamplePool samplePool,
            JudgmentSystem judgmentSystem)
        {
            var canvasGO = new GameObject("HUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var textGO = new GameObject("HUDText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(680, 260);

            var text = textGO.AddComponent<Text>();
            text.text = "Initializing...";
            text.fontSize = 22;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var meter = canvasGO.AddComponent<LatencyMeter>();
            SetField(meter, "hudText", text);
            SetField(meter, "audioSync", audioSync);
            SetField(meter, "tapInput", tapInput);
            SetField(meter, "samplePool", samplePool);
            SetField(meter, "judgmentSystem", judgmentSystem);
        }

        private static CalibrationController BuildCalibrationOverlay(
            Sprite whiteSprite, AudioClip clickSample, AudioSyncManager audioSync)
        {
            var canvasGO = new GameObject("CalibrationCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background panel
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.8f);

            // Prompt text
            var promptGO = new GameObject("PromptText");
            promptGO.transform.SetParent(canvasGO.transform, false);
            var promptRT = promptGO.AddComponent<RectTransform>();
            promptRT.anchorMin = new Vector2(0.5f, 0.7f);
            promptRT.anchorMax = new Vector2(0.5f, 0.7f);
            promptRT.pivot = new Vector2(0.5f, 0.5f);
            promptRT.sizeDelta = new Vector2(680, 200);
            var promptText = promptGO.AddComponent<Text>();
            promptText.text = "화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요.";
            promptText.fontSize = 32;
            promptText.color = Color.white;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            promptText.verticalOverflow = VerticalWrapMode.Overflow;
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Beat indicators: 8 circles in horizontal row
            var indicators = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                var dotGO = new GameObject($"Beat{i}");
                dotGO.transform.SetParent(canvasGO.transform, false);
                var dotRT = dotGO.AddComponent<RectTransform>();
                dotRT.anchorMin = new Vector2(0.5f, 0.5f);
                dotRT.anchorMax = new Vector2(0.5f, 0.5f);
                dotRT.pivot = new Vector2(0.5f, 0.5f);
                dotRT.sizeDelta = new Vector2(50, 50);
                // Spread across reference width 720: spacing ~70px, centered
                float xOffset = (i - 3.5f) * 70f;
                dotRT.anchoredPosition = new Vector2(xOffset, 0);
                var img = dotGO.AddComponent<Image>();
                img.sprite = whiteSprite;
                img.color = Color.gray;
                indicators[i] = img;
            }

            // Start button
            var btnGO = new GameObject("StartButton");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.3f);
            btnRT.anchorMax = new Vector2(0.5f, 0.3f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(300, 100);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = whiteSprite;
            btnImg.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            var startButton = btnGO.AddComponent<Button>();

            var btnTextGO = new GameObject("StartText");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnText = btnTextGO.AddComponent<Text>();
            btnText.text = "Start";
            btnText.fontSize = 36;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 8 AudioSources (one per click) so PlayScheduled can overlap reliably
            var clickSources = new AudioSource[8];
            for (int i = 0; i < 8; i++)
            {
                var srcGO = new GameObject($"Click{i}");
                srcGO.transform.SetParent(canvasGO.transform, false);
                var src = srcGO.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.clip = clickSample;
                clickSources[i] = src;
            }

            var controller = canvasGO.AddComponent<CalibrationController>();
            SetField(controller, "clickSample", clickSample);
            SetField(controller, "audioSync", audioSync);
            SetField(controller, "promptText", promptText);
            SetField(controller, "startButton", startButton);
            SetArrayField(controller, "clickSources", clickSources);
            SetArrayField(controller, "beatIndicators", indicators);

            return controller;
        }

        private static CompletionPanel BuildCompletionPanel(Sprite whiteSprite)
        {
            var canvasGO = new GameObject("CompletionCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);

            var title = CreateCenteredText(canvasGO.transform, "TitleText", "SONG COMPLETE", 48, new Vector2(0.5f, 0.85f), new Vector2(680, 80));
            var score = CreateCenteredText(canvasGO.transform, "ScoreText", "Score: 0", 40, new Vector2(0.5f, 0.7f), new Vector2(680, 80));
            var combo = CreateCenteredText(canvasGO.transform, "ComboText", "Max Combo: 0", 32, new Vector2(0.5f, 0.6f), new Vector2(680, 60));
            var breakdown = CreateCenteredText(canvasGO.transform, "BreakdownText", "P:0 G:0 G:0 M:0", 26, new Vector2(0.5f, 0.5f), new Vector2(680, 60));
            var stars = CreateCenteredText(canvasGO.transform, "StarsText", "---", 48, new Vector2(0.5f, 0.4f), new Vector2(680, 80));

            // Restart button
            var btnGO = new GameObject("RestartButton");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.2f);
            btnRT.anchorMax = new Vector2(0.5f, 0.2f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(300, 100);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = whiteSprite;
            btnImg.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            var restartButton = btnGO.AddComponent<Button>();

            var btnTextGO = new GameObject("RestartText");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnText = btnTextGO.AddComponent<Text>();
            btnText.text = "Restart";
            btnText.fontSize = 36;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = canvasGO.AddComponent<CompletionPanel>();
            SetField(panel, "titleText", title);
            SetField(panel, "scoreText", score);
            SetField(panel, "comboText", combo);
            SetField(panel, "breakdownText", breakdown);
            SetField(panel, "starsText", stars);
            SetField(panel, "restartButton", restartButton);

            return panel;
        }

        private static Text CreateCenteredText(
            Transform parent, string name, string initial, int fontSize,
            Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.text = initial;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static void BuildGameplayController(
            CalibrationController calibration,
            AudioSyncManager audioSync,
            NoteSpawner spawner,
            JudgmentSystem judgmentSystem,
            CompletionPanel completionPanel)
        {
            var go = new GameObject("GameplayController");
            var ctrl = go.AddComponent<GameplayController>();
            SetField(ctrl, "calibration", calibration);
            SetField(ctrl, "audioSync", audioSync);
            SetField(ctrl, "spawner", spawner);
            SetField(ctrl, "judgmentSystem", judgmentSystem);
            SetField(ctrl, "completionPanel", completionPanel);
        }

        private static GameObject BuildNotePrefab(Sprite sprite)
        {
            var go = new GameObject("Note");
            go.transform.localScale = new Vector3(0.8f, 0.4f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.95f, 0.85f, 1);
            sr.sortingOrder = 1;
            var noteCtrl = go.AddComponent<NoteController>();
            SetField(noteCtrl, "spriteRenderer", sr);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, NotePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Sprite EnsureWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(WhiteSpritePath);
            var importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 4;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetField(Object target, string name, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetField(Object target, string name, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.floatValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetArrayField(Object target, string name, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
