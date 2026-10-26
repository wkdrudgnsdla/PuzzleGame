using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleGame.Editor
{
    public static class ProjectionPrototypeTools
    {
        public const string ScenePath = "Assets/_Game/Scenes/ProjectionPrototype.unity";

        [MenuItem("Puzzle Game/Open Projection Prototype")]
        public static void OpenPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
                CreatePrototypeScene();

            Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>();
        }

        [MenuItem("Puzzle Game/Rebuild Prototype Scene")]
        public static void RebuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild Projection Prototype",
                "기존 프로토타입 씬의 맵 배치를 기본 상태로 다시 만듭니다. 직접 편집한 씬 내용이 바뀝니다.",
                "Rebuild", "Cancel"))
                return;

            CreatePrototypeScene();
        }

        // Used with -executeMethod. Existing scene contents are preserved on repeat runs.
        public static void CreateSceneForAutomation()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ConfigureBuildScene();
                AssetDatabase.SaveAssets();
                return;
            }

            CreatePrototypeScene();
        }

        public static void BuildStandaloneForAutomation()
        {
            CreateSceneForAutomation();

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var outputDirectory = Path.Combine(projectRoot, "Builds", "ProjectionPrototype");
            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outputDirectory, "ProjectionPrototype.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Projection prototype build failed: " + report.summary.result);

            Debug.Log("Projection prototype build: " + report.summary.outputPath);
        }

        private static void CreatePrototypeScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Projection Prototype");
            var prototype = root.AddComponent<ProjectionPrototype>();
            prototype.SurfaceTemplate = LoadOrCreateMaterial("PrototypeSurface", "Universal Render Pipeline/Lit", false);
            prototype.FlatTemplate = LoadOrCreateMaterial("PrototypeFlat", "Universal Render Pipeline/Unlit", true);

            Material platformA = LoadOrCreateMaterial("PrototypePlatformA", "Universal Render Pipeline/Lit", false,
                new Color(0.13f, 0.63f, 0.70f));
            Material platformB = LoadOrCreateMaterial("PrototypePlatformB", "Universal Render Pipeline/Lit", false,
                new Color(0.94f, 0.57f, 0.22f));
            Material exitMaterial = LoadOrCreateMaterial("PrototypeExit", "Universal Render Pipeline/Unlit", true,
                new Color(0.49f, 0.96f, 0.63f));

            Transform map = CreateTransform("Map", root.transform, Vector3.zero);
            Transform platforms = CreateTransform("Platforms", map, Vector3.zero);
            CreatePlatform(platforms, "Platform A", 1, new Vector3(0f, -0.5f, 0f),
                new Vector3(4f, 1f, 3f), platformA);
            CreatePlatform(platforms, "Platform B", 2, new Vector3(8f, -0.5f, 4f),
                new Vector3(4f, 1f, 3f), platformB);

            prototype.MapRoot = map;
            prototype.SpawnPoint = CreateTransform("Spawn Point", map, Vector3.zero);
            prototype.ExitPoint = CreateTransform("Exit Point", map, new Vector3(8f, 0f, 4f));
            prototype.CameraFocusPoint = CreateTransform("Camera Focus", map, new Vector3(4f, 1.1f, 2f));

            GameObject exitMarker = CreateCube("Exit Marker", prototype.ExitPoint, new Vector3(0f, 0.07f, 0f),
                new Vector3(0.85f, 0.1f, 0.85f), exitMaterial);
            UnityEngine.Object.DestroyImmediate(exitMarker.GetComponent<Collider>());
            GameObject exitBeacon = CreateCube("Exit Beacon", prototype.ExitPoint, new Vector3(0f, 0.7f, 0f),
                new Vector3(0.12f, 1.25f, 0.12f), exitMaterial);
            UnityEngine.Object.DestroyImmediate(exitBeacon.GetComponent<Collider>());

            Transform sunTransform = CreateTransform("Prototype Sun", map, Vector3.zero);
            sunTransform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            Light sun = sunTransform.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.None;

            ProjectionHudAuthoring.CreateHud(prototype);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save prototype scene to " + ScenePath);

            ConfigureBuildScene();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Projection prototype scene saved: " + ScenePath);
        }

        private static void ConfigureBuildScene()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != ScenePath) scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Transform CreateTransform(string objectName, Transform parent, Vector3 localPosition)
        {
            var result = new GameObject(objectName);
            result.layer = ProjectionPrototype.World3DLayer;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            return result.transform;
        }

        private static GameObject CreateCube(string objectName, Transform parent, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = objectName;
            result.layer = ProjectionPrototype.World3DLayer;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static void CreatePlatform(Transform parent, string objectName, int id,
            Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject platform = CreateCube(objectName, parent, localPosition, scale, material);
            platform.AddComponent<ProjectableBox>().Initialize(id, material.color);
        }

        private static Material LoadOrCreateMaterial(string materialName, string shaderName, bool doubleSided,
            Color? color = null)
        {
            const string materialsDirectory = "Assets/_Game/Materials";
            Directory.CreateDirectory(materialsDirectory);
            AssetDatabase.Refresh();

            var materialPath = materialsDirectory + "/" + materialName + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Required URP shader was not found: " + shaderName);

            var material = new Material(shader) { name = materialName };
            if (color.HasValue)
            {
                material.color = color.Value;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color.Value);
            }
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);
            if (doubleSided && material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);

            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}
