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
    /// <summary>Authors independent puzzle islands and automatic landing respawn points.</summary>
    public static class LayeredMechanicStageAuthoring
    {
        private static readonly string[] Names = { "Stage07_RailSwitch", "Stage08_PowerRelay", "Stage09_RailPowerRelay" };
        private static readonly Color[] FloorColors =
        {
            new Color(0.12f, 0.46f, 0.55f), new Color(0.72f, 0.46f, 0.18f),
            new Color(0.46f, 0.32f, 0.61f), new Color(0.21f, 0.56f, 0.43f)
        };
        private static readonly Color ExitColor = new Color(0.28f, 0.77f, 0.56f);

        // Explicit rebuild entry point used on a separate authoring copy, never from Play.
        public static void RebuildForAutomation()
        {
            Directory.CreateDirectory(MechanicStageAuthoring.Folder);
            AssetDatabase.Refresh();
            for (int index = 1; index <= 3; index++) Create(index);
            AssetDatabase.SaveAssets();
            Debug.Log("Layered rail and power stages saved: 07, 08, 09. No gameplay was run.");
        }

        public static void Create(int index)
        {
            if (index < 1 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Layered Mechanic Stage " + (index + 6));
            ProjectionPrototype owner = root.AddComponent<ProjectionPrototype>();
            owner.SurfaceTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeSurface.mat");
            owner.FlatTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/PrototypeFlat.mat");
            ProjectionPuzzleStage stage = root.AddComponent<ProjectionPuzzleStage>();
            owner.PuzzleStage = stage;
            owner.MapRoot = Point("Map", root.transform, Vector3.zero);
            Transform platforms = Point("Platforms", owner.MapRoot, Vector3.zero);
            Transform mechanisms = Point("Mechanisms", owner.MapRoot, Vector3.zero);
            owner.SpawnPoint = Point("Spawn Point", owner.MapRoot, Vector3.zero);
            owner.ExitPoint = Point("Exit Point", owner.MapRoot, Vector3.zero);
            owner.CameraFocusPoint = Point("Camera Focus", owner.MapRoot, Vector3.zero);
            var checkpoints = new List<Transform>();

            if (index == 1) LayeredRailStageLayout.Build(owner, stage, platforms, mechanisms, checkpoints);
            else BuildPowerRoute(owner, stage, platforms, mechanisms, checkpoints, index == 3);

            StageRoute route = root.AddComponent<StageRoute>();
            owner.Route = route;
            route.Prototype = owner;
            route.StageTitle = stage.StageTitle;
            route.Checkpoints = checkpoints.ToArray();
            route.RequireCompletionForExit = false;
            route.CheckpointPlatforms = FindLandingPlatforms(owner, checkpoints);
            route.ActivationRadius = 1f;
            route.HeightTolerance = 0.18f;
            Beacon(owner.ExitPoint, "Exit Beacon", new Color(0.4f, 1f, 0.6f), 0.8f);
            var settings = new SerializedObject(owner);
            settings.FindProperty("maxZoomSize").floatValue = 50f;
            settings.ApplyModifiedPropertiesWithoutUndo();

            Transform sunObject = Point("Stage Sun", owner.MapRoot, Vector3.zero);
            sunObject.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light sun = sunObject.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
            ProjectionHudAuthoring.CreateHud(owner);
            if (index >= 2) ValidateLegacyConnections(owner);
            string path = MechanicStageAuthoring.Folder + "/" + Names[index - 1] + ".unity";
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            Debug.Log("Saved layered mechanic stage " + path);
        }

        private static void BuildPowerRoute(ProjectionPrototype owner, ProjectionPuzzleStage stage,
            Transform platforms, Transform mechanisms, List<Transform> checkpoints, bool combined)
        {
            int id = 1;
            ProjectionRailSwitch rail = null;
            Vector3 firstCenter = new Vector3(0f, 9.9f, 0f);
            Island railArrival = null;
            if (combined)
            {
                var view = new ProjectionFrame(Vector3.zero, 20f);
                Vector3 railTop = new Vector3(0f, 15.95f, 0f);
                Island source = MakeIsland(platforms, "Section 01 - Upper Rail Source", railTop,
                    new Vector2(6.8f, 3.8f), FloorColors[0], ref id);
                Vector3 targetCenter = railTop + view.Right * 3f + view.Forward * 6.2f;
                railArrival = MakeIsland(platforms, "Section 02 - Rail Arrival", targetCenter,
                    new Vector2(6.8f, 3.8f), FloorColors[0], ref id);
                rail = MakeRail(mechanisms, source, railArrival, view);
                // Keep the upper rail decks out of the later C->D viewing ray.
                firstCenter = targetCenter + new Vector3(19f, -3.3f, -0.25f);
                owner.SpawnPoint.position = railTop - view.Right * 2.85f + Vector3.up * 0.02f;
                owner.CameraFocusPoint.position = (railTop + targetCenter) * 0.5f + Vector3.up * 1.7f;
            }
            else
            {
                owner.SpawnPoint.position = firstCenter + new Vector3(-1.35f, 0.02f, 0.8f);
                owner.CameraFocusPoint.position = firstCenter + new Vector3(1.5f, 1.7f, 1f);
            }

            float[] yaw = combined ? new[] { 35f, -30f, 60f } : new[] { 25f, -40f };
            float[] heightDelta = combined ? new[] { -3.85f, -2.75f, -3.3f } : new[] { -3.3f, -3.85f };
            float[] depth = combined ? new[] { 14f, 12.5f, 15f } : new[] { 12.5f, 14.5f };
            // Leave a clear projected interval between the small blocks. The cable's
            // unsupported cut ends meet inside that interval, never behind a deck face.
            float[] travel = combined ? new[] { 9.6f, 9.6f, 9.6f } : new[] { 9.6f, 9.6f };
            var islands = new List<Island>();
            var nodes = new List<ProjectionPowerNode>();
            Vector3 center = firstCenter;
            int nodeCount = yaw.Length + 1;
            for (int i = 0; i < nodeCount; i++)
            {
                string label = ((char)('A' + i)).ToString();
                Island island = MakeIsland(platforms, "Power Floor " + label, center,
                    new Vector2(4f, 3.2f), FloorColors[i % FloorColors.Length], ref id);
                islands.Add(island);
                ProjectionPowerNode node = Point(label + (i == nodeCount - 1 ? " Final Lamp" : " Battery"),
                    island.Root, center + Vector3.up * 0.5f).gameObject.AddComponent<ProjectionPowerNode>();
                node.Label = label;
                node.StartsPowered = !combined && i == 0;
                node.IsFinal = i == nodeCount - 1;
                nodes.Add(node);
                Skin(island, label + " Device Mount", center - Vector3.up * 0.025f,
                    new Vector3(1.05f, 0.06f, 0.85f));
                if (i > 0 || combined) AddCheckpoint(island, label + " Landing", checkpoints);
                if (i < yaw.Length)
                {
                    var horizontalView = new ProjectionFrame(Vector3.zero, yaw[i]);
                    center += horizontalView.Right * travel[i] + horizontalView.Forward * depth[i]
                        + Vector3.up * heightDelta[i];
                }
            }

            var network = Point("Floor Power Relay", mechanisms, Vector3.zero).gameObject.AddComponent<ProjectionPowerNetwork>();
            network.Nodes = nodes.ToArray();
            var wires = new List<ProjectionWire>();
            for (int i = 0; i < yaw.Length; i++)
            {
                Island source = islands[i];
                Island target = islands[i + 1];
                var view = new ProjectionFrame(Vector3.zero, yaw[i]);
                float halfLength = travel[i] * 0.5f;
                ProjectionWire wire = Point(nodes[i].Label + " to " + nodes[i + 1].Label,
                    network.transform, Vector3.zero).gameObject.AddComponent<ProjectionWire>();
                wire.Source = nodes[i];
                wire.Target = nodes[i + 1];
                Vector3 sourcePort = source.Center + Vector3.up * 0.24f;
                Vector3 targetPort = target.Center + Vector3.up * 0.24f;
                wire.SourcePath = new[]
                {
                    Point("Source Socket", source.Root, sourcePort),
                    Point("Source Cut End", source.Root, sourcePort + view.Right * halfLength)
                };
                wire.TargetPath = new[]
                {
                    Point("Target Cut End", target.Root, targetPort - view.Right * halfLength),
                    Point("Target Socket", target.Root, targetPort)
                };
                wire.OperatingPlatforms = new[] { source.Deck };
                wire.ConnectionTolerance = 0.4f;
                wire.AngleTolerance = 15f;
                wire.SecondsToCharge = 2.5f;
                wires.Add(wire);
            }
            network.Wires = wires.ToArray();

            if (combined)
            {
                UnityEventTools.AddPersistentListener(rail.OnCompleted, nodes[0].Activate);
                stage.Mechanics = new ProjectionPuzzleMechanic[] { rail, network };
            }
            else stage.Mechanics = new ProjectionPuzzleMechanic[] { network };

            Island last = islands[islands.Count - 1];
            Vector3 exitDelta = combined ? new Vector3(11f, -2.75f, -5f) : new Vector3(11f, -2.75f, -4f);
            Island exit = MakeIsland(platforms, "Lower Exit Landing", last.Center + exitDelta,
                new Vector2(5.8f, 4.4f), ExitColor, ref id);
            AddCheckpoint(exit, "Lower Exit Landing", checkpoints);
            owner.ExitPoint.position = exit.Center + Vector3.right * 1.65f + Vector3.up * 0.02f;
            stage.Rewards = new ProjectionPuzzleReward[0];
            stage.StageTitle = combined ? "09 / 레일과 하강 전력 릴레이" : "08 / 위에서 아래로 전력 릴레이";
            stage.Instructions = combined
                ? "위층 레일을 밀어 A 전원을 켠 뒤 아래 발판으로 내려갑니다. A→B→C→D를 각 발판에서 연결하며 내려오세요.\n기믹은 해당 블록에 서면 바로 작동합니다. 최종 전구 점등 후 아래층 출구에 도착하면 클리어됩니다."
                : "위층 A에서 A→B를 충전한 뒤 아래 B 발판으로 이동하고 B→C를 연결하세요.\n기믹은 해당 블록에 서면 바로 작동하며, 마지막 전구를 켠 후 맨 아래 출구로 내려갑니다.";
            var settings = new SerializedObject(owner);
            settings.FindProperty("initialZoomSize").floatValue = 8.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Island MakeIsland(Transform parent, string name, Vector3 top, Vector2 size, Color color, ref int id)
        {
            Transform group = Point(name, parent, top);
            ProjectableBox deck = Box(group, "Landing Block", top - Vector3.up * 0.45f,
                new Vector3(size.x, 0.9f, size.y), color, id++);
            return new Island { Root = group, Deck = deck, Center = top, Size = size };
        }

        private static ProjectionRailSwitch MakeRail(Transform parent, Island source, Island target, ProjectionFrame view)
        {
            var rail = Point("Upper Rail - A Supply", parent, Vector3.zero).gameObject.AddComponent<ProjectionRailSwitch>();
            Vector3 start = source.Center - view.Right * 1.5f + Vector3.up * 0.9f;
            Vector3 nextStart = target.Center - view.Right * 1.5f + Vector3.up * 0.9f;
            ProjectionRailSegment first = Point("Source Rail", rail.transform, Vector3.zero).gameObject.AddComponent<ProjectionRailSegment>();
            first.StartPoint = Point("Start", source.Root, start);
            first.EndPoint = Point("Join", source.Root, start + view.Right * 3f);
            ProjectionRailSegment second = Point("Arrival Rail", rail.transform, Vector3.zero).gameObject.AddComponent<ProjectionRailSegment>();
            second.StartPoint = Point("Join", target.Root, nextStart);
            second.EndPoint = Point("End", target.Root, nextStart + view.Right * 3f);
            rail.Segments = new[] { first, second };
            rail.GoalPoint = Point("Yellow Destination", target.Root, second.End);
            rail.OperatingPlatforms = new[] { source.Deck, target.Deck };
            rail.ConnectionTolerance = 0.4f;
            rail.AngleTolerance = 15f;
            return rail;
        }

        private static void AddCheckpoint(Island island, string name, List<Transform> checkpoints)
        {
            Transform checkpoint = Point("Checkpoint " + (checkpoints.Count + 1) + " - " + name,
                island.Root, island.Center + Vector3.forward * 1f + Vector3.up * 0.02f);
            Beacon(checkpoint, "Landing Beacon", new Color(1f, 0.72f, 0.2f), 0.65f);
            checkpoints.Add(checkpoint);
        }

        /// <summary>Checks the authored answer view without entering Play mode.</summary>
        public static void ValidateLegacyConnections(ProjectionPrototype owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Physics.SyncTransforms();
            foreach (ProjectionWire wire in owner.GetComponentsInChildren<ProjectionWire>(true))
            {
                if (!wire.IsValid) throw new InvalidOperationException("Invalid legacy wire " + wire.name);
                Vector3 first = wire.SourcePath[wire.SourcePath.Length - 1].position;
                Vector3 second = wire.TargetPath[0].position;
                Vector3 gap = second - first;
                float horizontal = new Vector2(gap.x, gap.z).magnitude;
                if (horizontal < 0.1f) throw new InvalidOperationException("No readable depth gap for " + wire.name);
                float yaw = Mathf.Atan2(gap.x, gap.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(gap.y, horizontal) * Mathf.Rad2Deg;
                var answer = new ProjectionFrame(Vector3.zero, yaw, pitch);
                Vector3 firstInward = wire.SourcePath[wire.SourcePath.Length - 2].position - first;
                Vector3 secondInward = wire.TargetPath[1].position - second;
                if (wire.CanConnect(answer) && ProjectionConnectionVisibility.IsJoinExposed(
                    answer, first, second, firstInward, secondInward)) continue;
                Collider blocker = ProjectionConnectionVisibility.FindJoinOccluder(
                    answer, first, second, firstInward, secondInward);
                string blockerPath = blocker == null ? "unknown" : blocker.name;
                if (blocker != null)
                {
                    Transform parent = blocker.transform.parent;
                    while (parent != null) { blockerPath = parent.name + "/" + blockerPath; parent = parent.parent; }
                }
                throw new InvalidOperationException("Legacy connection is hidden in its answer view: " + wire.name
                    + "; blocker=" + blockerPath + "; first=" + first.ToString("F3")
                    + "; second=" + second.ToString("F3") + "; yaw=" + yaw.ToString("F2")
                    + "; pitch=" + pitch.ToString("F2"));
            }
        }

        private static void Skin(Island island, string name, Vector3 center, Vector3 size)
        {
            GameObject cube = Primitive(island.Root, name, PrimitiveType.Cube, center, size,
                new Color(0.105f, 0.15f, 0.19f));
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }

        private static ProjectableBox Box(Transform parent, string name, Vector3 center, Vector3 size, Color color, int id)
        {
            GameObject cube = Primitive(parent, name, PrimitiveType.Cube, center, size, color);
            ProjectableBox box = cube.AddComponent<ProjectableBox>();
            box.Initialize(id, color);
            return box;
        }

        private static ProjectableBox[] FindLandingPlatforms(ProjectionPrototype owner, List<Transform> checkpoints)
        {
            Physics.SyncTransforms();
            ProjectableBox[] platforms = owner.MapRoot.GetComponentsInChildren<ProjectableBox>(true);
            var result = new ProjectableBox[checkpoints.Count];
            for (int i = 0; i < checkpoints.Count; i++)
            {
                Vector3 foot = checkpoints[i].position;
                foreach (ProjectableBox platform in platforms)
                {
                    if (!platform.isActiveAndEnabled || !platform.SourceCollider.enabled) continue;
                    Bounds bounds = platform.SourceCollider.bounds;
                    if (Mathf.Abs(bounds.max.y - foot.y) <= 0.18f && foot.x >= bounds.min.x &&
                        foot.x <= bounds.max.x && foot.z >= bounds.min.z && foot.z <= bounds.max.z)
                    { result[i] = platform; break; }
                }
                if (result[i] == null) throw new InvalidOperationException("No landing block for " + checkpoints[i].name);
            }
            return result;
        }

        private static Transform Point(string name, Transform parent, Vector3 worldPosition)
        {
            var point = new GameObject(name);
            point.layer = ProjectionPrototype.World3DLayer;
            point.transform.SetParent(parent, false);
            point.transform.position = worldPosition;
            return point.transform;
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 center, Vector3 size, Color color)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.layer = ProjectionPrototype.World3DLayer;
            result.transform.SetParent(parent, false);
            result.transform.position = center;
            result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = Material(color);
            return result;
        }

        private static Material Material(Color color)
        {
            string path = "Assets/_Game/Materials/Layered_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Beacon(Transform point, string name, Color color, float height)
        {
            GameObject beacon = Primitive(point, name, PrimitiveType.Cylinder, point.position + Vector3.up * height * 0.5f,
                new Vector3(0.22f, height * 0.5f, 0.22f), color);
            UnityEngine.Object.DestroyImmediate(beacon.GetComponent<Collider>());
        }

        private sealed class Island
        {
            public Transform Root;
            public ProjectableBox Deck;
            public Vector3 Center;
            public Vector2 Size;
        }
    }
}
