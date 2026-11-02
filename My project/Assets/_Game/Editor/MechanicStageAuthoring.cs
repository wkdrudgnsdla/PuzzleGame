using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleGame.Editor
{
    /// <summary>Creates editable demonstration scenes. Existing scenes are never overwritten.</summary>
    public static class MechanicStageAuthoring
    {
        public const string Folder = "Assets/_Game/Scenes/Mechanics";
        private static readonly string[] Names =
        {
            "Stage06_SplitKey", "Stage07_RailSwitch", "Stage08_PowerRelay", "Stage09_RailPowerRelay"
        };
        private static readonly string[] Titles =
        {
            "06 / 흩어진 열쇠", "07 / 이어지는 레일", "08 / 전력 릴레이", "09 / 레일부터 전구까지"
        };
        private static readonly Color Teal = new Color(0.12f, 0.4f, 0.49f);

        [MenuItem("Puzzle Game/Mechanic Stages/06 Split Key")]
        public static void OpenKey() => Open(0);
        [MenuItem("Puzzle Game/Mechanic Stages/07 Rail Switch")]
        public static void OpenRail() => Open(1);
        [MenuItem("Puzzle Game/Mechanic Stages/08 Power Relay")]
        public static void OpenPower() => Open(2);
        [MenuItem("Puzzle Game/Mechanic Stages/09 Rail + Power Relay")]
        public static void OpenCombined() => Open(3);

        [MenuItem("Puzzle Game/Mechanic Stages/Create Missing Scenes", priority = 100)]
        public static void CreateMissingScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateAllForAutomation();
        }

        private static void Open(int index)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = PathFor(index);
            if (!File.Exists(path)) Create(index);
            else EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        // Scene creation/import only. No gameplay or test runner is invoked.
        public static void CreateAllForAutomation()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            for (int i = 0; i < Names.Length; i++)
                if (!File.Exists(PathFor(i))) Create(i);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < Names.Length; i++)
                if (!scenes.Exists(scene => scene.path == PathFor(i)))
                    scenes.Add(new EditorBuildSettingsScene(PathFor(i), true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Four editable mechanic stages are ready: " + Folder);
        }

        // Upgrades the existing electrical scenes without rebuilding their maps, rail, events or HUD.
        public static void MountPowerStagesForAutomation()
        {
            for (int index = 2; index <= 3; index++)
            {
                string path = PathFor(index);
                if (!File.Exists(path)) Create(index);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ProjectionPrototype prototype = UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>();
                if (prototype == null) throw new InvalidOperationException("Missing prototype in " + path);
                ProjectionPowerNetwork network = prototype.GetComponentInChildren<ProjectionPowerNetwork>(true);
                if (network == null) throw new InvalidOperationException("Missing power network in " + path);
                // Layered scenes already mount each device to its own platform height.
                if (prototype.Route != null) continue;
                MountCircuitObjects(prototype, network);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Power devices and cable halves are mounted on scene blocks.");
        }

        private static string PathFor(int index) => Folder + "/" + Names[index] + ".unity";

        private static void Create(int index)
        {
            if (index >= 1)
            {
                LayeredMechanicStageAuthoring.Create(index);
                return;
            }
            Directory.CreateDirectory(Folder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(Titles[index]);
            ProjectionPrototype prototype = root.AddComponent<ProjectionPrototype>();
            prototype.SurfaceTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeSurface.mat");
            prototype.FlatTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeFlat.mat");
            ProjectionPuzzleStage stage = root.AddComponent<ProjectionPuzzleStage>();
            prototype.PuzzleStage = stage;
            stage.StageTitle = Titles[index];

            Transform map = Point("Map", root.transform, Vector3.zero);
            Transform platforms = Point("Platforms", map, Vector3.zero);
            prototype.MapRoot = map;
            prototype.SpawnPoint = Point("Spawn Point", map, new Vector3(-5.5f, 0.02f, -2.8f));
            prototype.CameraFocusPoint = Point("Camera Focus", map, new Vector3(0f, 1.5f, 0f));
            prototype.ExitPoint = Point("Exit Point", map, new Vector3(6.6f, 0.02f, -1.8f));
            Platform(platforms, "Main Deck", 1, new Vector3(0f, -0.5f, 0f), new Vector3(18f, 1f, 14f), Teal);
            ConfigureZoom(prototype, 7f);
            Transform mechanisms = Point("Mechanisms", map, Vector3.zero);

            if (index == 0)
            {
                stage.Instructions = "3D에서 두 금색 열쇠 조각을 맞추세요. 초록빛이 나면 R → 2D에서 열쇠에 닿아 획득 → 3D 출구.\n우클릭 드래그: 회전 · R: 차원 전환 · A/D: 2D 이동 · Backspace: 전체 초기화";
                var key = Point("Split Key", mechanisms, Vector3.zero).gameObject.AddComponent<ProjectionKeyPuzzle>();
                var answer = new ProjectionFrame(Vector3.zero, 30f);
                Vector3 center = new Vector3(0f, 0.95f, 0f);
                key.FirstJoin = Point("Bow Join", key.transform, center - answer.Forward * 1.75f);
                key.SecondJoin = Point("Teeth Join", key.transform, center + answer.Forward * 1.75f);
                key.FirstJoin.rotation = key.SecondJoin.rotation = Quaternion.Euler(0f, 30f, 0f);
                stage.Mechanics = new ProjectionPuzzleMechanic[] { key };
                ExitMarker(prototype.ExitPoint);
            }
            else if (index == 1)
            {
                stage.Instructions = "3D에서 레일 접합부를 맞춘 뒤 R. 초록 스위치의 왼쪽에서 D로 몸으로 밀어 노란 도착점까지 옮기세요.\n시점을 바꿔 출구 블록을 이어 건넌 뒤 3D로 복귀하세요.";
                ProjectionRailSwitch rail = Rail(mechanisms, 25f, new Vector3(-4f, 0.9f, -2f));
                Platform(platforms, "Raised Exit", 3, new Vector3(7f, 0.65f, -1.8f),
                    new Vector3(1.8f, 1.3f, 2.4f), new Color(0.48f, 0.38f, 0.62f));
                prototype.ExitPoint.position = new Vector3(7f, 1.32f, -1.8f);
                ExitMarker(prototype.ExitPoint);
                stage.Mechanics = new ProjectionPuzzleMechanic[] { rail };
                stage.Rewards = new ProjectionPuzzleReward[0];
            }
            else if (index == 2)
            {
                stage.Instructions = "파란 A의 전선을 노란 B의 전선과 맞추고 R → 파란 에너지가 끝까지 차오르면 B가 충전됩니다.\n3D에서 각도를 바꿔 B→C 충전. 최종 C 전구가 켜지면 출구 해금 → R로 3D 복귀 후 출구에 도착하세요.";
                ProjectionPowerNetwork network = PowerNetwork(mechanisms, false);
                MountCircuitObjects(prototype, network);
                stage.Mechanics = new ProjectionPuzzleMechanic[] { network };
                ExitMarker(prototype.ExitPoint);
            }
            else
            {
                prototype.SpawnPoint.position = new Vector3(-6.5f, 0.02f, -3f);
                stage.Instructions = "먼저 초록 스위치를 몸으로 밀어 노란 레일 끝까지 보내 A 전원을 켜세요.\n각도를 바꿔가며 A→B→C→D 충전. D 전구 점등으로 출구 해금 → 3D에서 출구 도착 시 클리어 · 충전 상태는 유지됩니다.";
                ProjectionRailSwitch rail = Rail(mechanisms, 20f, new Vector3(-5f, 0.9f, -3f));
                ProjectionPowerNetwork network = PowerNetwork(mechanisms, true);
                MountCircuitObjects(prototype, network);
                UnityEventTools.AddPersistentListener(rail.OnCompleted, network.Nodes[0].Activate);
                stage.Mechanics = new ProjectionPuzzleMechanic[] { rail, network };
                ExitMarker(prototype.ExitPoint);
                ConfigureZoom(prototype, 8f);
            }

            Transform lightObject = Point("Stage Sun", map, Vector3.zero);
            lightObject.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light sun = lightObject.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
            ProjectionHudAuthoring.CreateHud(prototype);
            if (!EditorSceneManager.SaveScene(scene, PathFor(index)))
                throw new IOException("Could not save " + PathFor(index));
            Debug.Log("Created " + PathFor(index));
        }

        private static ProjectionRailSwitch Rail(Transform parent, float yaw, Vector3 start)
        {
            var rail = Point("Body Push Rail", parent, Vector3.zero).gameObject.AddComponent<ProjectionRailSwitch>();
            var answer = new ProjectionFrame(Vector3.zero, yaw);
            Vector3 firstEnd = start + answer.Right * 3f;
            Vector3 secondStart = firstEnd + answer.Forward * 3.5f;
            var first = Point("Rail 1", rail.transform, Vector3.zero).gameObject.AddComponent<ProjectionRailSegment>();
            first.StartPoint = Point("Start", first.transform, start);
            first.EndPoint = Point("End Join", first.transform, firstEnd);
            var second = Point("Rail 2", rail.transform, Vector3.zero).gameObject.AddComponent<ProjectionRailSegment>();
            second.StartPoint = Point("Start Join", second.transform, secondStart);
            second.EndPoint = Point("End", second.transform, secondStart + answer.Right * 3f);
            rail.Segments = new[] { first, second };
            rail.GoalPoint = Point("Yellow Destination", rail.transform, second.EndPoint.position);
            rail.ConnectionTolerance = 0.4f;
            rail.AngleTolerance = 15f;
            return rail;
        }

        private static ProjectionPowerNetwork PowerNetwork(Transform parent, bool railPowered)
        {
            var network = Point("Power Relay", parent, Vector3.zero).gameObject.AddComponent<ProjectionPowerNetwork>();
            network.Nodes = railPowered
                ? new[]
                {
                    Node(network.transform, "A", new Vector3(-4f, 0.5f, 0f), false, false),
                    Node(network.transform, "B", new Vector3(0f, 0.5f, 2f), false, false),
                    Node(network.transform, "C", new Vector3(4f, 0.5f, 0f), false, false),
                    Node(network.transform, "D", new Vector3(6f, 0.5f, -3f), false, true)
                }
                : new[]
                {
                    Node(network.transform, "A", new Vector3(-4f, 0.5f, 0f), true, false),
                    Node(network.transform, "B", new Vector3(0.5f, 0.5f, 2f), false, false),
                    Node(network.transform, "C", new Vector3(5f, 0.5f, -2f), false, true)
                };
            network.Wires = new ProjectionWire[network.Nodes.Length - 1];
            float[] angles = railPowered ? new[] { 35f, -30f, 60f } : new[] { 25f, -35f };
            for (int i = 0; i < network.Wires.Length; i++)
            {
                ProjectionPowerNode source = network.Nodes[i];
                ProjectionPowerNode target = network.Nodes[i + 1];
                var wire = Point(source.Label + " to " + target.Label, network.transform, Vector3.zero)
                    .gameObject.AddComponent<ProjectionWire>();
                wire.Source = source;
                wire.Target = target;
                Vector3 midpoint = (source.transform.position + target.transform.position) * 0.5f;
                Vector3 offset = new ProjectionFrame(Vector3.zero, angles[i]).Forward * 2f;
                wire.SourcePath = new[]
                {
                    Point("Source Socket", wire.transform, source.transform.position),
                    Point("Source Join", wire.transform, midpoint - offset)
                };
                wire.TargetPath = new[]
                {
                    Point("Target Join", wire.transform, midpoint + offset),
                    Point("Target Socket", wire.transform, target.transform.position)
                };
                wire.ConnectionTolerance = 0.4f;
                wire.AngleTolerance = 15f;
                wire.SecondsToCharge = 2.5f;
                network.Wires[i] = wire;
            }
            return network;
        }

        private static void MountCircuitObjects(ProjectionPrototype prototype, ProjectionPowerNetwork network)
        {
            Transform platforms = prototype.MapRoot.Find("Platforms");
            if (platforms == null) throw new InvalidOperationException("Missing Platforms group.");
            Transform mounts = platforms.Find("Circuit Mounts");
            if (mounts == null) mounts = Point("Circuit Mounts", platforms, Vector3.zero);
            int id = 100;
            Color panelColor = new Color(0.105f, 0.15f, 0.19f);
            foreach (ProjectionPowerNode node in network.Nodes)
            {
                Vector3 position = node.transform.position;
                position.y = 0.5f; // Assembly bottom is 0.5 below its anchor: it rests on the deck.
                node.transform.position = position;
                MountBlock(mounts, node.Label + " Device Block", id++,
                    new Vector3(position.x, -0.045f, position.z), new Vector3(1.05f, 0.09f, 0.85f), panelColor);
            }
            foreach (ProjectionWire wire in network.Wires)
            {
                MountCableHalf(mounts, wire.name + " Source Block", id++, wire.SourcePath, panelColor);
                MountCableHalf(mounts, wire.name + " Target Block", id++, wire.TargetPath, panelColor);
            }
        }

        private static void MountCableHalf(Transform parent, string name, int id, Transform[] path, Color color)
        {
            if (path == null || path.Length == 0) return;
            var bounds = new Bounds(path[0].position, Vector3.zero);
            foreach (Transform point in path)
            {
                Vector3 position = point.position;
                position.y = 0.1f; // Cable sits on the block surface and connects to the housing's bottom port.
                point.position = position;
                bounds.Encapsulate(position);
            }
            MountBlock(parent, name, id, new Vector3(bounds.center.x, -0.035f, bounds.center.z),
                new Vector3(Mathf.Max(0.8f, bounds.size.x + 0.45f), 0.08f,
                    Mathf.Max(0.8f, bounds.size.z + 0.45f)), color);
        }

        private static void MountBlock(Transform parent, string name, int id, Vector3 worldPosition,
            Vector3 size, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.position = worldPosition;
                existing.localScale = size;
                return;
            }
            Platform(parent, name, id, parent.InverseTransformPoint(worldPosition), size, color);
        }

        private static ProjectionPowerNode Node(Transform parent, string label, Vector3 position,
            bool powered, bool final)
        {
            var node = Point(label + (final ? " Final Lamp" : " Battery"), parent, position)
                .gameObject.AddComponent<ProjectionPowerNode>();
            node.Label = label;
            node.StartsPowered = powered;
            node.IsFinal = final;
            return node;
        }

        private static void ConfigureZoom(ProjectionPrototype prototype, float size)
        {
            var serialized = new SerializedObject(prototype);
            serialized.FindProperty("initialZoomSize").floatValue = size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Point(string name, Transform parent, Vector3 localPosition)
        {
            var point = new GameObject(name);
            point.layer = ProjectionPrototype.World3DLayer;
            point.transform.SetParent(parent, false);
            point.transform.localPosition = localPosition;
            return point.transform;
        }

        private static GameObject Platform(Transform parent, string name, int id, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.layer = ProjectionPrototype.World3DLayer;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            block.AddComponent<ProjectableBox>().Initialize(id, color);
            return block;
        }

        private static void ExitMarker(Transform parent)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Exit Beacon";
            beacon.layer = ProjectionPrototype.World3DLayer;
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = Vector3.up * 0.6f;
            beacon.transform.localScale = new Vector3(0.28f, 0.6f, 0.28f);
            UnityEngine.Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.GetComponent<Renderer>().sharedMaterial = MaterialFor(new Color(0.4f, 1f, 0.6f));
        }

        private static Material MaterialFor(Color color)
        {
            string path = "Assets/_Game/Materials/Mechanic_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

    }
}
