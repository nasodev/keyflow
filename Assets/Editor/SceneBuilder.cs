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

        [MenuItem("KeyFlow/Build W1 PoC Scene")]
        public static void BuildPoCScene()
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

            BuildMainCamera();
            var judgmentLine = BuildJudgmentLine(whiteSprite);
            var spawnPoint = BuildSpawnPoint();
            BuildManagers(pianoClip, notePrefab, spawnPoint, judgmentLine,
                out var audioSync, out var samplePool, out var tapInput);
            BuildHUD(audioSync, tapInput, samplePool);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log($"[KeyFlow] W1 PoC scene built: {ScenePath}");
        }

        private static void BuildMainCamera()
        {
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 0, -10);

            var camera = cam.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            cam.AddComponent<AudioListener>();
        }

        private static GameObject BuildJudgmentLine(Sprite sprite)
        {
            var go = new GameObject("JudgmentLine");
            go.transform.position = new Vector3(0, -3, 0);
            go.transform.localScale = new Vector3(10, 0.2f, 1);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.0f, 0.9f, 1.0f, 1.0f);
            sr.sortingOrder = 0;
            return go;
        }

        private static GameObject BuildSpawnPoint()
        {
            var go = new GameObject("SpawnPoint");
            go.transform.position = new Vector3(0, 4, 0);
            return go;
        }

        private static void BuildManagers(
            AudioClip pianoClip,
            GameObject notePrefab,
            GameObject spawnPoint,
            GameObject judgmentPoint,
            out AudioSyncManager audioSync,
            out AudioSamplePool samplePool,
            out TapInputHandler tapInput)
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

            var spawnerGO = new GameObject("Spawner");
            spawnerGO.transform.SetParent(managers.transform);
            var spawner = spawnerGO.AddComponent<NoteSpawner>();
            SetField(spawner, "notePrefab", notePrefab);
            SetField(spawner, "spawnPoint", spawnPoint.transform);
            SetField(spawner, "judgmentPoint", judgmentPoint.transform);
            SetField(spawner, "audioSync", audioSync);
        }

        private static void BuildHUD(
            AudioSyncManager audioSync,
            TapInputHandler tapInput,
            AudioSamplePool samplePool)
        {
            var canvasGO = new GameObject("HUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var textGO = new GameObject("HUDText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(500, 160);

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
        }

        private static GameObject BuildNotePrefab(Sprite sprite)
        {
            var go = new GameObject("Note");
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
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
            if (prop == null)
            {
                Debug.LogError($"[KeyFlow] Field '{name}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
