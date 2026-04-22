using System.IO;
using UnityEditor;
using UnityEngine;
using KeyFlow.Feedback;

namespace KeyFlow.Editor
{
    public static class FeedbackPrefabBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/Feedback";
        private const string SoDir = "Assets/ScriptableObjects";

        [MenuItem("KeyFlow/Build Feedback Assets")]
        public static void Build()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(SoDir);

            BuildHitPrefab();
            BuildMissPrefab();
            BuildPresetsAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KeyFlow] Feedback assets built.");
        }

        private static void BuildHitPrefab()
        {
            var go = new GameObject("hit");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 2.0f;
            main.startSize = 0.35f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            var burst = new ParticleSystem.Burst(0f, 12);
            emission.SetBursts(new[] { burst });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat");

            string path = $"{PrefabDir}/hit.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildMissPrefab()
        {
            var go = new GameObject("miss");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.5f;  // outward burst (distinct fast fade vs hit's 0.45s)
            main.startSize = 0.45f;
            main.startColor = new Color(1f, 0.25f, 0.25f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),
                    new GradientColorKey(new Color(0.5f, 0f, 0f), 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat");

            string path = $"{PrefabDir}/miss.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildPresetsAsset()
        {
            string path = $"{SoDir}/FeedbackPresets.asset";
            if (AssetDatabase.LoadAssetAtPath<FeedbackPresets>(path) != null) return;
            var so = ScriptableObject.CreateInstance<FeedbackPresets>();
            AssetDatabase.CreateAsset(so, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
