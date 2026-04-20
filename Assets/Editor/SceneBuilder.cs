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

        [MenuItem("KeyFlow/Build W2 Gameplay Scene")]
        public static void BuildScene()
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
            var judgmentLine = BuildJudgmentLine(whiteSprite);
            BuildManagers(
                camera, pianoClip, notePrefab,
                out var audioSync, out var samplePool, out var tapInput, out var judgmentSystem);
            BuildHUD(audioSync, tapInput, samplePool, judgmentSystem);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log($"[KeyFlow] W2 4-lane portrait scene built: {ScenePath}");
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
            out JudgmentSystem judgmentSystem)
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

            var spawnerGO = new GameObject("Spawner");
            spawnerGO.transform.SetParent(managers.transform);
            var spawner = spawnerGO.AddComponent<NoteSpawner>();
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

        private static GameObject BuildNotePrefab(Sprite sprite)
        {
            var go = new GameObject("Note");
            go.transform.localScale = new Vector3(0.8f, 0.4f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.95f, 0.85f, 1);
            sr.sortingOrder = 1;
            go.AddComponent<NoteController>();

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
    }
}
