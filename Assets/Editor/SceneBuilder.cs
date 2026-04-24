using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.UI;
using KeyFlow.Calibration;
using KeyFlow.Charts;
using KeyFlow.Feedback;

namespace KeyFlow.Editor
{
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string NotePrefabPath = "Assets/Prefabs/Note.prefab";
        private const string WhiteSpritePath = "Assets/Sprites/white.png";
        private const string StarFilledPath = "Assets/Sprites/star_filled.png";
        private const string StarEmptyPath = "Assets/Sprites/star_empty.png";
        private const string ThumbsDir = "Assets/StreamingAssets/thumbs";
        private const string LockedThumbPath = "Assets/StreamingAssets/thumbs/locked.png";
        private const string FurEliseThumbPath = "Assets/StreamingAssets/thumbs/fur_elise.png";

        // Portrait layout (camera orthographic size 8 → 9 world-unit-wide viewport at 9:16 aspect)
        private const float LaneAreaWidth = 9f;       // world units (W6 SP6: full-screen at ortho=8, aspect 9:16)
        private const float SpawnY = 6.5f;
        private const float JudgmentY = -5f;           // ~81% down the viewport (spec §4.3 target: 80%)

        // W6 SP1 multi-pitch Salamander samples (folded in via SP7 from deleted W6SamplesWireup)
        private const string PianoFolder = "Assets/Audio/piano";
        private static readonly string[] SampleNames =
        {
            "C2v10", "Ds2v10", "Fs2v10", "A2v10",
            "C3v10", "Ds3v10", "Fs3v10", "A3v10",
            "C4v10", "Ds4v10", "Fs4v10", "A4v10",
            "C5v10", "Ds5v10", "Fs5v10", "A5v10",
            "C6v10",
        };

        [MenuItem("KeyFlow/Build W4 Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Sprites");
            EnsureFolder("Assets/StreamingAssets");
            EnsureFolder(ThumbsDir);

            var whiteSprite = EnsureWhiteSprite();
            EnsureStarSprite(true);
            EnsureStarSprite(false);
            EnsureThumbnailAssets();
            var pianoClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/piano_c4.wav");
            if (pianoClip == null)
            {
                Debug.LogError("[KeyFlow] Missing Assets/Audio/piano_c4.wav. Aborting.");
                return;
            }

            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/calibration_click.wav");
            if (clickClip == null)
            {
                Debug.LogError("[KeyFlow] Missing Assets/Audio/calibration_click.wav. Run KeyFlow/Build Calibration Click first. Aborting.");
                return;
            }

            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background_gameplay.png");
            if (bgSprite == null)
            {
                Debug.LogError("[KeyFlow] Missing Assets/Sprites/background_gameplay.png. Aborting.");
                return;
            }

            var pitchSamples = LoadPitchSamples();
            if (pitchSamples == null) return;

            var notePrefab = BuildNotePrefab(whiteSprite);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GameplayScene";

            var camera = BuildMainCamera();
            CreateEventSystem();

            BuildBackgroundCanvas(bgSprite, camera);

            // GameplayRoot groups all gameplay-only objects so ScreenManager
            // can toggle them wholesale.
            var gameplayRoot = new GameObject("GameplayRoot");

            BuildLaneDividers(whiteSprite, gameplayRoot.transform);
            BuildJudgmentLine(whiteSprite, gameplayRoot.transform);
            BuildManagers(
                camera, pianoClip, pitchSamples, whiteSprite, notePrefab, gameplayRoot.transform,
                out var audioSync, out var samplePool, out var tapInput,
                out var judgmentSystem, out var spawner, out var holdTracker);
            BuildFeedbackPipeline(judgmentSystem, gameplayRoot.transform);
            var hudPauseButton = BuildHUD(audioSync, tapInput, samplePool, judgmentSystem, whiteSprite, gameplayRoot.transform);

            var calibration = BuildCalibrationOverlay(whiteSprite, clickClip, audioSync);
            var resultsScreen = BuildResultsCanvas(whiteSprite);
            BuildGameplayController(calibration, audioSync, spawner, judgmentSystem, holdTracker, resultsScreen, gameplayRoot.transform);

            var mainCanvas = BuildMainCanvas(whiteSprite);
            var mainScreen = mainCanvas.GetComponent<MainScreen>();
            var pauseScreen = BuildPauseCanvas(whiteSprite, audioSync);
            var settingsScreen = BuildSettingsCanvas(whiteSprite, calibration);

            // Wire HUD pause button -> PauseScreen, MainScreen -> Settings overlay
            SetField(hudPauseButton, "pauseOverlay", pauseScreen);
            SetField(mainScreen, "settingsOverlay", settingsScreen);

            var screenManagerGO = new GameObject("ScreenManager");
            var screenMgr = screenManagerGO.AddComponent<ScreenManager>();
            SetField(screenMgr, "mainRoot", mainCanvas);
            SetField(screenMgr, "gameplayRoot", gameplayRoot);
            SetField(screenMgr, "resultsCanvas", resultsScreen.gameObject);
            SetField(screenMgr, "calibrationOverlay", calibration);
            SetField(screenMgr, "pauseOverlay", pauseScreen);
            SetField(screenMgr, "settingsOverlay", settingsScreen);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log($"[KeyFlow] W4 scene built: {ScenePath}");
        }

        private static GameObject CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            return go;
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

        private static void BuildLaneDividers(Sprite sprite, Transform parent)
        {
            float leftEdge = -LaneAreaWidth / 2f;
            for (int i = 0; i <= LaneLayout.LaneCount; i++)
            {
                float x = leftEdge + i * (LaneAreaWidth / LaneLayout.LaneCount);
                var go = new GameObject($"LaneDivider_{i}");
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.position = new Vector3(x, 0, 0);
                go.transform.localScale = new Vector3(0.02f, 20f, 1);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.8f, 0.9f, 1.0f, 0.3f); // W6 SP6: blue-tinted white, low alpha, harmonizes with new background
                sr.sortingOrder = -1;
            }
        }

        private static GameObject BuildJudgmentLine(Sprite sprite, Transform parent)
        {
            var go = new GameObject("JudgmentLine");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = new Vector3(0, JudgmentY, 0);
            go.transform.localScale = new Vector3(LaneAreaWidth, 0.12f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.9f, 0.95f, 1.0f, 0.5f); // W6 SP6: subtle white, α=0.5 — visible timing guide without fighting background
            sr.sortingOrder = 0;
            return go;
        }

        private static LaneGlowController BuildLaneGlow(
            Sprite whiteSprite,
            Transform managersParent,
            AudioSyncManager audioSync)
        {
            if (whiteSprite == null)
            {
                Debug.LogError("BuildLaneGlow: whiteSprite is null; lane glow will not render.");
                return null;
            }

            var root = new GameObject("LaneGlow");
            root.transform.SetParent(managersParent, worldPositionStays: false);
            var controller = root.AddComponent<LaneGlowController>();

            var sprites = new SpriteRenderer[LaneLayout.LaneCount];
            float tileWidth = LaneAreaWidth / LaneLayout.LaneCount;

            // Glow sits in the TAP ZONE below the judgment line — the empty lane
            // area between the judgment line and the camera bottom. Placing it
            // below-and-separate from the note tiles (instead of at JudgmentY where
            // the tiles live) means it's never visually buried.
            // JudgmentY = -3, camera bottom ≈ -5 → tap zone 2 units tall, midpoint -4.
            const float TapZoneMidY = -4f;
            const float TapZoneHeight = 2.0f;
            for (int i = 0; i < LaneLayout.LaneCount; i++)
            {
                var go = new GameObject($"Glow_{i}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(LaneLayout.LaneToX(i, LaneAreaWidth), TapZoneMidY, 0);
                go.transform.localScale = new Vector3(tileWidth, TapZoneHeight, 1);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = whiteSprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                // sortingOrder = 2 so the glow still wins against incidental overlap;
                // but by design the glow is in the tap zone (y < -3) while tiles
                // scroll through y ≥ -3 only up to their bottom-at-judgment position.
                sr.sortingOrder = 2;
                sprites[i] = sr;
            }

            SetArrayField(controller, "glowSprites", sprites);
            SetField(controller, "audioSync", audioSync);
            return controller;
        }

        private static void BuildManagers(
            Camera camera,
            AudioClip pianoClip,
            AudioClip[] pitchSamples,
            Sprite whiteSprite,
            GameObject notePrefab,
            Transform parent,
            out AudioSyncManager audioSync,
            out AudioSamplePool samplePool,
            out TapInputHandler tapInput,
            out JudgmentSystem judgmentSystem,
            out NoteSpawner spawner,
            out HoldTracker holdTracker)
        {
            var managers = new GameObject("Managers");
            managers.transform.SetParent(parent, worldPositionStays: false);

            var audioSyncGO = new GameObject("AudioSync");
            audioSyncGO.transform.SetParent(managers.transform);
            audioSyncGO.AddComponent<AudioSource>();
            audioSync = audioSyncGO.AddComponent<AudioSyncManager>();

            var samplePoolGO = new GameObject("SamplePool");
            samplePoolGO.transform.SetParent(managers.transform);
            samplePool = samplePoolGO.AddComponent<AudioSamplePool>();
            SetField(samplePool, "defaultClip", pianoClip);
            SetArrayField(samplePool, "pitchSamples", pitchSamples);
            SetField(samplePool, "baseMidi", 36);
            SetField(samplePool, "stepSemitones", 3);

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
            SetField(tapInput, "judgmentSystem", judgmentSystem);

            var holdTrackerGO = new GameObject("HoldTracker");
            holdTrackerGO.transform.SetParent(managers.transform);
            holdTracker = holdTrackerGO.AddComponent<HoldTracker>();
            SetField(holdTracker, "tapInput", tapInput);
            SetField(holdTracker, "audioSync", audioSync);
            SetField(holdTracker, "judgmentSystem", judgmentSystem);

            var laneGlow = BuildLaneGlow(whiteSprite, managers.transform, audioSync);
            if (laneGlow != null) SetField(holdTracker, "laneGlow", laneGlow);
            SetField(holdTracker, "audioPool", samplePool);

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

        private static void BuildFeedbackPipeline(
            JudgmentSystem judgmentSystem, Transform parent)
        {
            var feedbackRoot = new GameObject("FeedbackPipeline");
            feedbackRoot.transform.SetParent(parent, false);

            var presets = AssetDatabase.LoadAssetAtPath<FeedbackPresets>(
                "Assets/ScriptableObjects/FeedbackPresets.asset");
            var hitPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/Prefabs/Feedback/hit.prefab");
            var missPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(
                "Assets/Prefabs/Feedback/miss.prefab");

            if (presets == null || hitPrefab == null || missPrefab == null)
            {
                Debug.LogError("SceneBuilder: Feedback assets missing. Run 'KeyFlow/Build Feedback Assets' first.");
                return;
            }

            var hapticsGo = new GameObject("HapticService");
            hapticsGo.transform.SetParent(feedbackRoot.transform, false);
            var hapticService = hapticsGo.AddComponent<HapticService>();
            SetField(hapticService, "presets", presets);

            var particlesGo = new GameObject("ParticlePool");
            particlesGo.transform.SetParent(feedbackRoot.transform, false);
            var particlePool = particlesGo.AddComponent<ParticlePool>();
            SetField(particlePool, "hitPrefab", hitPrefab);
            SetField(particlePool, "missPrefab", missPrefab);
            SetField(particlePool, "presets", presets);

            var dispatcherGo = new GameObject("FeedbackDispatcher");
            dispatcherGo.transform.SetParent(feedbackRoot.transform, false);
            var dispatcher = dispatcherGo.AddComponent<FeedbackDispatcher>();
            SetField(dispatcher, "judgmentSystem", judgmentSystem);
            SetField(dispatcher, "hapticService", hapticService);
            SetField(dispatcher, "particlePool", particlePool);
        }

        private static HUDPauseButton BuildHUD(
            AudioSyncManager audioSync,
            TapInputHandler tapInput,
            AudioSamplePool samplePool,
            JudgmentSystem judgmentSystem,
            Sprite whiteSprite,
            Transform parent)
        {
            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(parent, worldPositionStays: false);
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
            text.raycastTarget = false;

            var meter = canvasGO.AddComponent<LatencyMeter>();
            SetField(meter, "hudText", text);
            SetField(meter, "audioSync", audioSync);
            SetField(meter, "tapInput", tapInput);
            SetField(meter, "samplePool", samplePool);
            SetField(meter, "judgmentSystem", judgmentSystem);

            // Pause button (top-right)
            var pauseBtnGO = new GameObject("PauseButton");
            pauseBtnGO.transform.SetParent(canvasGO.transform, false);
            var pauseBtnRT = pauseBtnGO.AddComponent<RectTransform>();
            pauseBtnRT.anchorMin = new Vector2(1, 1);
            pauseBtnRT.anchorMax = new Vector2(1, 1);
            pauseBtnRT.pivot = new Vector2(1, 1);
            pauseBtnRT.anchoredPosition = new Vector2(-20, -20);
            pauseBtnRT.sizeDelta = new Vector2(80, 80);
            var pauseBtnImg = pauseBtnGO.AddComponent<Image>();
            pauseBtnImg.sprite = whiteSprite;
            pauseBtnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.75f);
            pauseBtnGO.AddComponent<Button>();

            var pauseLabelGO = new GameObject("Label");
            pauseLabelGO.transform.SetParent(pauseBtnGO.transform, false);
            var pauseLabelRT = pauseLabelGO.AddComponent<RectTransform>();
            pauseLabelRT.anchorMin = Vector2.zero;
            pauseLabelRT.anchorMax = Vector2.one;
            pauseLabelRT.offsetMin = Vector2.zero;
            pauseLabelRT.offsetMax = Vector2.zero;
            var pauseLabel = pauseLabelGO.AddComponent<Text>();
            pauseLabel.text = "II";
            pauseLabel.fontSize = 36;
            pauseLabel.color = Color.white;
            pauseLabel.alignment = TextAnchor.MiddleCenter;
            pauseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // W6 SP6 combo HUD: large centered-top number, hidden at combo=0
            var comboGO = new GameObject("ComboText", typeof(RectTransform));
            comboGO.transform.SetParent(canvasGO.transform, false);
            var comboRT = comboGO.GetComponent<RectTransform>();
            comboRT.anchorMin = new Vector2(0.5f, 1f);
            comboRT.anchorMax = new Vector2(0.5f, 1f);
            comboRT.pivot = new Vector2(0.5f, 1f);
            comboRT.anchoredPosition = new Vector2(0, -100); // below the existing progress bar
            comboRT.sizeDelta = new Vector2(400, 140);

            var comboText = comboGO.AddComponent<Text>();
            comboText.text = "0";
            comboText.fontSize = 96;
            comboText.color = Color.white;
            comboText.alignment = TextAnchor.MiddleCenter;
            comboText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            comboText.raycastTarget = false;
            comboText.enabled = false; // starts hidden; ComboHUD.Update flips on first non-zero combo

            var comboHUD = comboGO.AddComponent<ComboHUD>();
            SetField(comboHUD, "judgmentSystem", judgmentSystem);
            SetField(comboHUD, "comboText", comboText);

            return pauseBtnGO.AddComponent<HUDPauseButton>();
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
            promptText.text = UIStrings.CalibrationPrompt;
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
            btnText.text = UIStrings.CalibrationStart;
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

        private static void BuildBackgroundCanvas(Sprite bgSprite, Camera cam)
        {
            var canvasGO = new GameObject("BackgroundCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            // ScreenSpaceCamera (not Overlay!) so world-space gameplay objects
            // (notes, lane dividers, judgment line) render IN FRONT of the bg.
            // Overlay canvases always paint over world-space regardless of
            // sortingOrder. Camera mode puts the canvas in 3D space at
            // planeDistance, which z-sorts naturally behind the gameplay.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 50f; // far in front of camera but behind nothing gameplay-facing
            canvas.sortingOrder = -100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>(); // present for Canvas completeness; Image's raycastTarget is false below

            var imgGO = new GameObject("BackgroundImage", typeof(RectTransform));
            imgGO.transform.SetParent(canvasGO.transform, false);
            var rt = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = imgGO.AddComponent<Image>();
            img.sprite = bgSprite;
            img.preserveAspect = false; // uniform stretch, no letterboxing
            img.raycastTarget = false;  // don't absorb taps — they go to gameplay
        }

        private static GameObject BuildMainCanvas(Sprite whiteSprite)
        {
            var starFilled = AssetDatabase.LoadAssetAtPath<Sprite>(StarFilledPath);
            var starEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(StarEmptyPath);

            var canvasGO = new GameObject("MainCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Background fill
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            // Header
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(canvasGO.transform, false);
            var headerRT = headerGO.AddComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 120);
            headerRT.anchoredPosition = Vector2.zero;
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            var titleGO = new GameObject("TitleText");
            titleGO.transform.SetParent(headerGO.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(0, 0.5f);
            titleRT.pivot = new Vector2(0, 0.5f);
            titleRT.anchoredPosition = new Vector2(24, 0);
            titleRT.sizeDelta = new Vector2(400, 60);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "KeyFlow";
            titleText.fontSize = 42;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var settingsBtnGO = new GameObject("SettingsButton");
            settingsBtnGO.transform.SetParent(headerGO.transform, false);
            var settingsBtnRT = settingsBtnGO.AddComponent<RectTransform>();
            settingsBtnRT.anchorMin = new Vector2(1, 0.5f);
            settingsBtnRT.anchorMax = new Vector2(1, 0.5f);
            settingsBtnRT.pivot = new Vector2(1, 0.5f);
            settingsBtnRT.anchoredPosition = new Vector2(-24, 0);
            settingsBtnRT.sizeDelta = new Vector2(80, 80);
            var settingsBtnImg = settingsBtnGO.AddComponent<Image>();
            settingsBtnImg.sprite = whiteSprite;
            settingsBtnImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var settingsButton = settingsBtnGO.AddComponent<Button>();

            var settingsBtnTextGO = new GameObject("Label");
            settingsBtnTextGO.transform.SetParent(settingsBtnGO.transform, false);
            var settingsBtnTextRT = settingsBtnTextGO.AddComponent<RectTransform>();
            settingsBtnTextRT.anchorMin = Vector2.zero;
            settingsBtnTextRT.anchorMax = Vector2.one;
            settingsBtnTextRT.offsetMin = Vector2.zero;
            settingsBtnTextRT.offsetMax = Vector2.zero;
            var settingsBtnText = settingsBtnTextGO.AddComponent<Text>();
            settingsBtnText.text = "⚙";
            settingsBtnText.fontSize = 42;
            settingsBtnText.color = Color.white;
            settingsBtnText.alignment = TextAnchor.MiddleCenter;
            settingsBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ScrollView
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(canvasGO.transform, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = new Vector2(0, -120); // leave room for Header
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = new Color(0, 0, 0, 0.01f); // must have Image for Mask
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;

            // Card template (inactive; MainScreen clones this)
            var cardTemplate = BuildCardTemplate(contentGO.transform, whiteSprite, starFilled, starEmpty);
            cardTemplate.SetActive(false);

            // MainScreen controller
            var mainScreen = canvasGO.AddComponent<MainScreen>();
            SetField(mainScreen, "cardContainer", contentGO.transform);
            SetField(mainScreen, "cardPrefab", cardTemplate);
            SetField(mainScreen, "settingsButton", settingsButton);
            SetField(mainScreen, "starFilled", starFilled);
            SetField(mainScreen, "starEmpty", starEmpty);
            // settingsOverlay wires in Task 16.

            return canvasGO;
        }

        private static GameObject BuildCardTemplate(
            Transform parent, Sprite whiteSprite, Sprite starFilled, Sprite starEmpty)
        {
            // Anchor-based layout — card is full-width of container.
            // Content columns: [Thumb 112] [Center flex] [Right 160]
            var cardGO = new GameObject("CardTemplate");
            cardGO.transform.SetParent(parent, false);
            var cardRT = cardGO.AddComponent<RectTransform>();
            // Top-stretch anchor so VerticalLayoutGroup on Content can stack
            // cards top-to-bottom. VLG assigns anchoredPosition.
            cardRT.anchorMin = new Vector2(0, 1);
            cardRT.anchorMax = new Vector2(1, 1);
            cardRT.pivot = new Vector2(0.5f, 1);
            cardRT.sizeDelta = new Vector2(0, 150);
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.sprite = whiteSprite;
            cardImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var cardLE = cardGO.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 150;
            cardLE.minHeight = 150;
            var canvasGroup = cardGO.AddComponent<CanvasGroup>();

            // Thumbnail (left, vertically centered)
            var thumbGO = new GameObject("Thumbnail");
            thumbGO.transform.SetParent(cardGO.transform, false);
            var thumbRT = thumbGO.AddComponent<RectTransform>();
            thumbRT.anchorMin = new Vector2(0, 0.5f);
            thumbRT.anchorMax = new Vector2(0, 0.5f);
            thumbRT.pivot = new Vector2(0, 0.5f);
            thumbRT.anchoredPosition = new Vector2(12, 0);
            thumbRT.sizeDelta = new Vector2(112, 112);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.color = Color.white;
            thumbImg.preserveAspect = true;

            // Title (top of middle column)
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(cardGO.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0, 1);
            titleRT.anchoredPosition = new Vector2(136, -12);
            titleRT.offsetMax = new Vector2(-172, titleRT.offsetMax.y);
            titleRT.sizeDelta = new Vector2(titleRT.sizeDelta.x, 34);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "Title";
            titleText.fontSize = 26;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.raycastTarget = false;

            // Composer (below title)
            var composerGO = new GameObject("Composer");
            composerGO.transform.SetParent(cardGO.transform, false);
            var composerRT = composerGO.AddComponent<RectTransform>();
            composerRT.anchorMin = new Vector2(0, 1);
            composerRT.anchorMax = new Vector2(1, 1);
            composerRT.pivot = new Vector2(0, 1);
            composerRT.anchoredPosition = new Vector2(136, -52);
            composerRT.offsetMax = new Vector2(-172, composerRT.offsetMax.y);
            composerRT.sizeDelta = new Vector2(composerRT.sizeDelta.x, 26);
            var composerText = composerGO.AddComponent<Text>();
            composerText.text = "Composer";
            composerText.fontSize = 18;
            composerText.color = new Color(0.75f, 0.75f, 0.8f, 1f);
            composerText.alignment = TextAnchor.UpperLeft;
            composerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            composerText.verticalOverflow = VerticalWrapMode.Truncate;
            composerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            composerText.raycastTarget = false;

            // Stars row (bottom of middle column)
            var starImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGO = new GameObject($"Star{i}");
                starGO.transform.SetParent(cardGO.transform, false);
                var starRT = starGO.AddComponent<RectTransform>();
                starRT.anchorMin = new Vector2(0, 0);
                starRT.anchorMax = new Vector2(0, 0);
                starRT.pivot = new Vector2(0, 0);
                starRT.anchoredPosition = new Vector2(136 + i * 38, 14);
                starRT.sizeDelta = new Vector2(32, 32);
                var starImg = starGO.AddComponent<Image>();
                starImg.sprite = starEmpty;
                starImg.preserveAspect = true;
                starImg.raycastTarget = false;
                starImages[i] = starImg;
            }

            // Right column: Easy (top), Normal (bottom), stacked vertically
            var easyButton = BuildCardButtonAnchored(
                cardGO.transform, whiteSprite, "Easy",
                new Color(0.25f, 0.6f, 0.4f, 1f),
                new Vector2(1, 1), new Vector2(-12, -14), new Vector2(150, 56));
            var normalButton = BuildCardButtonAnchored(
                cardGO.transform, whiteSprite, "Normal",
                new Color(0.6f, 0.5f, 0.2f, 1f),
                new Vector2(1, 0), new Vector2(-12, 14), new Vector2(150, 56));

            var view = cardGO.AddComponent<SongCardView>();
            SetField(view, "thumbnailImage", thumbImg);
            SetField(view, "titleText", titleText);
            SetField(view, "composerText", composerText);
            SetField(view, "easyButton", easyButton);
            SetField(view, "normalButton", normalButton);
            SetField(view, "canvasGroup", canvasGroup);
            SetField(view, "starFilled", starFilled);
            SetField(view, "starEmpty", starEmpty);
            SetArrayField(view, "starImages", starImages);

            return cardGO;
        }

        // Anchor-based button. Pivot follows anchor so anchoredPosition is an inset from that corner.
        private static Button BuildCardButtonAnchored(
            Transform parent, Sprite whiteSprite, string label, Color color,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var btnGO = new GameObject(label + "Button");
            btnGO.transform.SetParent(parent, false);
            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor; // e.g. top-right anchor -> pivot top-right -> inset from that corner
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = btnGO.AddComponent<Image>();
            img.sprite = whiteSprite;
            img.color = color;
            var btn = btnGO.AddComponent<Button>();

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 22;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
            return btn;
        }

        private static SettingsScreen BuildSettingsCanvas(
            Sprite whiteSprite, CalibrationController calibration)
        {
            var canvasGO = new GameObject("SettingsCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);

            CreateCenteredText(canvasGO.transform, "TitleText", UIStrings.SettingsTitle, 48,
                new Vector2(0.5f, 0.9f), new Vector2(680, 80));

            // Close (X) button top-right
            var closeBtnGO = new GameObject("CloseButton");
            closeBtnGO.transform.SetParent(canvasGO.transform, false);
            var closeBtnRT = closeBtnGO.AddComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 1);
            closeBtnRT.anchorMax = new Vector2(1, 1);
            closeBtnRT.pivot = new Vector2(1, 1);
            closeBtnRT.anchoredPosition = new Vector2(-20, -20);
            closeBtnRT.sizeDelta = new Vector2(80, 80);
            var closeBtnImg = closeBtnGO.AddComponent<Image>();
            closeBtnImg.sprite = whiteSprite;
            closeBtnImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            var closeButton = closeBtnGO.AddComponent<Button>();

            var closeLblGO = new GameObject("Label");
            closeLblGO.transform.SetParent(closeBtnGO.transform, false);
            var closeLblRT = closeLblGO.AddComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero;
            closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero;
            closeLblRT.offsetMax = Vector2.zero;
            var closeLbl = closeLblGO.AddComponent<Text>();
            closeLbl.text = "×";
            closeLbl.fontSize = 40;
            closeLbl.color = Color.white;
            closeLbl.alignment = TextAnchor.MiddleCenter;
            closeLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // SFX volume slider
            CreateCenteredText(canvasGO.transform, "SfxLabel", UIStrings.SfxVolumeLabel, 28,
                new Vector2(0.5f, 0.75f), new Vector2(600, 50));
            var sfxSlider = BuildSlider(canvasGO.transform, whiteSprite,
                new Vector2(0.5f, 0.7f), new Vector2(560, 40));

            // Note speed slider
            CreateCenteredText(canvasGO.transform, "SpeedLabel", UIStrings.NoteSpeedLabel, 28,
                new Vector2(0.5f, 0.6f), new Vector2(600, 50));
            var speedSlider = BuildSlider(canvasGO.transform, whiteSprite,
                new Vector2(0.5f, 0.55f), new Vector2(560, 40));
            var speedValueText = CreateCenteredText(canvasGO.transform, "SpeedValue", "2.0", 26,
                new Vector2(0.5f, 0.5f), new Vector2(200, 40));

            // Haptics toggle
            CreateCenteredText(canvasGO.transform, "HapticsLabel", "Haptics", 28,
                new Vector2(0.35f, 0.45f), new Vector2(300, 50));
            var hapticsToggle = BuildToggle(canvasGO.transform, whiteSprite,
                new Vector2(0.65f, 0.45f), new Vector2(60, 60));

            // Recalibrate button
            var recalButton = BuildPrimaryButton(canvasGO.transform, whiteSprite,
                UIStrings.RecalibrateButton, new Vector2(0.5f, 0.35f), new Color(0.2f, 0.55f, 0.75f, 1f));

            // Version label bottom-right
            var versionGO = new GameObject("VersionLabel");
            versionGO.transform.SetParent(canvasGO.transform, false);
            var versionRT = versionGO.AddComponent<RectTransform>();
            versionRT.anchorMin = new Vector2(1, 0);
            versionRT.anchorMax = new Vector2(1, 0);
            versionRT.pivot = new Vector2(1, 0);
            versionRT.anchoredPosition = new Vector2(-20, 20);
            versionRT.sizeDelta = new Vector2(300, 40);
            var versionText = versionGO.AddComponent<Text>();
            versionText.text = "";
            versionText.fontSize = 20;
            versionText.color = new Color(0.7f, 0.7f, 0.75f, 1f);
            versionText.alignment = TextAnchor.MiddleRight;
            versionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var screen = canvasGO.AddComponent<SettingsScreen>();
            SetField(screen, "sfxVolumeSlider", sfxSlider);
            SetField(screen, "noteSpeedSlider", speedSlider);
            SetField(screen, "noteSpeedValueLabel", speedValueText);
            SetField(screen, "recalibrateButton", recalButton);
            SetField(screen, "closeButton", closeButton);
            SetField(screen, "versionLabel", versionText);
            SetField(screen, "hapticsToggle", hapticsToggle);
            SetField(screen, "calibration", calibration);

            // CC-BY Salamander credit Text, anchored to bottom of SettingsCanvas (W6 SP7: folded in from W6SamplesWireup)
            var creditsGo = new GameObject("CreditsLabel", typeof(RectTransform), typeof(Text));
            creditsGo.transform.SetParent(canvasGO.transform, false);
            var creditsRT = creditsGo.GetComponent<RectTransform>();
            creditsRT.anchorMin = new Vector2(0.05f, 0.02f);
            creditsRT.anchorMax = new Vector2(0.95f, 0.08f);
            creditsRT.offsetMin = Vector2.zero;
            creditsRT.offsetMax = Vector2.zero;
            var creditsText = creditsGo.GetComponent<Text>();
            creditsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            creditsText.fontSize = 18;
            creditsText.alignment = TextAnchor.MiddleCenter;
            creditsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            creditsText.text = UIStrings.CreditsSamples;
            SetField(screen, "creditsLabel", creditsText);

            return screen;
        }

        private static Slider BuildSlider(
            Transform parent, Sprite whiteSprite, Vector2 anchor, Vector2 size)
        {
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(parent, false);
            var sliderRT = sliderGO.AddComponent<RectTransform>();
            sliderRT.anchorMin = anchor;
            sliderRT.anchorMax = anchor;
            sliderRT.pivot = new Vector2(0.5f, 0.5f);
            sliderRT.sizeDelta = size;

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.25f);
            bgRT.anchorMax = new Vector2(1, 0.75f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = whiteSprite;
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1, 0.75f);
            fillAreaRT.offsetMin = new Vector2(10, 0);
            fillAreaRT.offsetMax = new Vector2(-10, 0);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = whiteSprite;
            fillImg.color = new Color(0.3f, 0.65f, 0.9f, 1f);

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(30, 40);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.sprite = whiteSprite;
            handleImg.color = Color.white;

            var slider = sliderGO.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Toggle BuildToggle(
            Transform parent, Sprite whiteSprite, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject("Toggle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var toggle = go.AddComponent<Toggle>();

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRT = bgGo.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = whiteSprite;
            bgImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            toggle.targetGraphic = bgImg;

            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(bgGo.transform, false);
            var checkRT = checkGo.AddComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.1f, 0.1f);
            checkRT.anchorMax = new Vector2(0.9f, 0.9f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.sprite = whiteSprite;
            checkImg.color = new Color(0.4f, 0.85f, 0.5f, 1f);
            toggle.graphic = checkImg;

            return toggle;
        }

        private static PauseScreen BuildPauseCanvas(Sprite whiteSprite, AudioSyncManager audioSync)
        {
            var canvasGO = new GameObject("PauseCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.8f);

            CreateCenteredText(canvasGO.transform, "TitleText", UIStrings.Paused, 56, new Vector2(0.5f, 0.75f), new Vector2(680, 90));

            var resumeButton = BuildPrimaryButton(canvasGO.transform, whiteSprite,
                UIStrings.Resume, new Vector2(0.5f, 0.5f), new Color(0.2f, 0.6f, 0.9f, 1f));
            var quitButton = BuildPrimaryButton(canvasGO.transform, whiteSprite,
                UIStrings.QuitToMain, new Vector2(0.5f, 0.35f), new Color(0.6f, 0.3f, 0.3f, 1f));

            var pauseScreen = canvasGO.AddComponent<PauseScreen>();
            SetField(pauseScreen, "resumeButton", resumeButton);
            SetField(pauseScreen, "quitButton", quitButton);
            SetField(pauseScreen, "audioSync", audioSync);
            return pauseScreen;
        }

        private static Button BuildSizedButton(
            Transform parent, Sprite whiteSprite, string label, Vector2 anchor, Vector2 size, Color color)
        {
            var btnGO = new GameObject(label + "Button");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = anchor;
            btnRT.anchorMax = anchor;
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = size;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = whiteSprite;
            btnImg.color = color;
            var btn = btnGO.AddComponent<Button>();

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return btn;
        }

        private static Button BuildPrimaryButton(
            Transform parent, Sprite whiteSprite, string label, Vector2 anchor, Color color)
        {
            var btnGO = new GameObject(label + "Button");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = anchor;
            btnRT.anchorMax = anchor;
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(440, 100);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = whiteSprite;
            btnImg.color = color;
            var btn = btnGO.AddComponent<Button>();

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 32;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return btn;
        }

        private static ResultsScreen BuildResultsCanvas(Sprite whiteSprite)
        {
            var starFilled = AssetDatabase.LoadAssetAtPath<Sprite>(StarFilledPath);
            var starEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(StarEmptyPath);

            var canvasGO = new GameObject("ResultsCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 1f);

            var title = CreateCenteredText(canvasGO.transform, "TitleText", UIStrings.SongComplete, 52,
                new Vector2(0.5f, 0.88f), new Vector2(680, 80));

            // 3 star images centered around y=0.72
            var starImages = new Image[3];
            float centerY = 0.72f;
            for (int i = 0; i < 3; i++)
            {
                var starGO = new GameObject($"Star{i}");
                starGO.transform.SetParent(canvasGO.transform, false);
                var starRT = starGO.AddComponent<RectTransform>();
                starRT.anchorMin = new Vector2(0.5f, centerY);
                starRT.anchorMax = new Vector2(0.5f, centerY);
                starRT.pivot = new Vector2(0.5f, 0.5f);
                starRT.sizeDelta = new Vector2(96, 96);
                starRT.anchoredPosition = new Vector2((i - 1) * 120f, 0);
                var img = starGO.AddComponent<Image>();
                img.sprite = starEmpty;
                img.preserveAspect = true;
                starImages[i] = img;
            }

            var scoreText = CreateCenteredText(canvasGO.transform, "ScoreText",
                string.Format(UIStrings.ScoreFmt, 0), 44,
                new Vector2(0.5f, 0.56f), new Vector2(680, 70));
            var maxCombo = CreateCenteredText(canvasGO.transform, "MaxComboText",
                string.Format(UIStrings.MaxComboFmt, 0), 32,
                new Vector2(0.5f, 0.48f), new Vector2(680, 60));
            var breakdown = CreateCenteredText(canvasGO.transform, "BreakdownText",
                string.Format(UIStrings.BreakdownFmt, 0, 0, 0, 0), 26,
                new Vector2(0.5f, 0.42f), new Vector2(680, 60));
            var newRecord = CreateCenteredText(canvasGO.transform, "NewRecordLabel",
                UIStrings.NewRecord, 34,
                new Vector2(0.5f, 0.34f), new Vector2(680, 60));
            newRecord.color = new Color(1f, 0.85f, 0.25f, 1f);
            newRecord.gameObject.SetActive(false);

            var retryBtn = BuildSizedButton(canvasGO.transform, whiteSprite,
                UIStrings.Retry, new Vector2(0.3f, 0.18f), new Vector2(300, 96),
                new Color(0.2f, 0.6f, 0.9f, 1f));
            var homeBtn = BuildSizedButton(canvasGO.transform, whiteSprite,
                UIStrings.Home, new Vector2(0.7f, 0.18f), new Vector2(300, 96),
                new Color(0.35f, 0.35f, 0.4f, 1f));

            var screen = canvasGO.AddComponent<ResultsScreen>();
            SetField(screen, "titleText", title);
            SetField(screen, "starFilled", starFilled);
            SetField(screen, "starEmpty", starEmpty);
            SetField(screen, "scoreText", scoreText);
            SetField(screen, "maxComboText", maxCombo);
            SetField(screen, "breakdownText", breakdown);
            SetField(screen, "newRecordLabel", newRecord);
            SetField(screen, "retryButton", retryBtn);
            SetField(screen, "homeButton", homeBtn);
            SetArrayField(screen, "starImages", starImages);
            return screen;
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

        private static GameplayController BuildGameplayController(
            CalibrationController calibration,
            AudioSyncManager audioSync,
            NoteSpawner spawner,
            JudgmentSystem judgmentSystem,
            HoldTracker holdTracker,
            ResultsScreen resultsScreen,
            Transform parent)
        {
            var go = new GameObject("GameplayController");
            go.transform.SetParent(parent, worldPositionStays: false);
            var ctrl = go.AddComponent<GameplayController>();
            SetField(ctrl, "calibration", calibration);
            SetField(ctrl, "audioSync", audioSync);
            SetField(ctrl, "spawner", spawner);
            SetField(ctrl, "judgmentSystem", judgmentSystem);
            SetField(ctrl, "holdTracker", holdTracker);
            SetField(ctrl, "resultsScreen", resultsScreen);
            return ctrl;
        }

        private static GameObject BuildNotePrefab(Sprite sprite)
        {
            var go = new GameObject("Note");
            go.transform.localScale = new Vector3(LaneAreaWidth / LaneLayout.LaneCount, 0.4f, 1);
            // = (9f/4, 0.4f, 1) = (2.25f, 0.4f, 1). Tiles fill one lane width edge-to-edge.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.08f, 0.08f, 0.12f, 1); // W6 SP6: near-black matches reference; palette aligned with camera.backgroundColor
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

        private static Sprite EnsureStarSprite(bool filled)
        {
            string path = filled ? StarFilledPath : StarEmptyPath;
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color fill = filled ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outer = 28f, inner = 12f;
            Vector2[] verts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? outer : inner;
                float a = Mathf.PI / 2f - i * Mathf.PI / 5f;
                verts[i] = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), verts))
                    pixels[y * size + x] = fill;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        private static void EnsureThumbnailAssets()
        {
            if (!File.Exists(LockedThumbPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var p = new Color[size * size];
                Color bg = new Color(0.2f, 0.2f, 0.22f, 1f);
                Color stripe = new Color(0.3f, 0.3f, 0.32f, 1f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    p[y * size + x] = (((x + y) / 8) % 2 == 0) ? bg : stripe;
                tex.SetPixels(p); tex.Apply();
                File.WriteAllBytes(LockedThumbPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(LockedThumbPath);
            }
            if (!File.Exists(FurEliseThumbPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var p = new Color[size * size];
                Color bg = new Color(0.15f, 0.2f, 0.4f, 1f);
                Color fg = new Color(0.95f, 0.9f, 0.7f, 1f);
                for (int i = 0; i < p.Length; i++) p[i] = bg;
                for (int y = 32; y < 96; y++) for (int x = 48; x < 56; x++) p[y * size + x] = fg;
                for (int y = 88; y < 96; y++) for (int x = 48; x < 80; x++) p[y * size + x] = fg;
                for (int y = 60; y < 68; y++) for (int x = 48; x < 72; x++) p[y * size + x] = fg;
                tex.SetPixels(p); tex.Apply();
                File.WriteAllBytes(FurEliseThumbPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(FurEliseThumbPath);
            }
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

        private static void SetField(Object target, string name, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}"); return; }
            prop.intValue = value;
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

        private static AudioClip[] LoadPitchSamples()
        {
            var clips = new AudioClip[SampleNames.Length];
            for (int i = 0; i < SampleNames.Length; i++)
            {
                string path = $"{PianoFolder}/{SampleNames[i]}.wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogError($"[KeyFlow] Missing pitch sample: {path}. Aborting.");
                    return null;
                }
                clips[i] = clip;
            }
            return clips;
        }
    }
}
