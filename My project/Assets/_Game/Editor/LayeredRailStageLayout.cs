using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PuzzleGame.Editor
{
    /// <summary>Separate rail islands reached by aligning their projected landing surfaces.</summary>
    public static class LayeredRailStageLayout
    {
        private static readonly Color LowerColor = new Color(0.13f, 0.46f, 0.55f);
        private static readonly Color UpperColor = new Color(0.48f, 0.34f, 0.61f);

        public static void Build(ProjectionPrototype owner, ProjectionPuzzleStage stage,
            Transform platforms, Transform mechanisms, List<Transform> checkpoints)
        {
            int id = 1;
            Transform lowerRoom = Point("Section 01 - Lower Rail Islands", platforms, Vector3.zero);
            Vector3 lowerSource = new Vector3(-4f, 0f, 0f);
            var firstView = new ProjectionFrame(Vector3.zero, 15f);
            Vector3 lowerTarget = lowerSource + firstView.Right * 3f + firstView.Forward * 5.5f;
            ProjectableBox firstSource = Deck(lowerRoom, "01 Source Rail Block", ref id, lowerSource,
                new Vector2(6.8f, 3.2f), LowerColor);
            ProjectableBox firstTarget = Deck(lowerRoom, "02 Arrival Rail Block", ref id, lowerTarget,
                new Vector2(6.8f, 3.2f), LowerColor);
            ProjectionRailSwitch first = Rail(mechanisms, "Rail I - Lower Pair", firstView,
                lowerSource, firstSource, firstTarget);

            // Each room has enough vertical clearance at its horizontal rail view.
            // Travelling between rooms uses a different projection, without stairs.
            Vector3 upperSource = lowerTarget + new Vector3(13.25f, 3.6f, 0f);
            Transform upperRoom = Point("Section 02 - Upper Rail Islands", platforms, Vector3.zero);
            var secondView = new ProjectionFrame(Vector3.zero, -35f);
            Vector3 upperTarget = upperSource + secondView.Right * 3f + secondView.Forward * 5.5f;
            ProjectableBox secondSource = Deck(upperRoom, "03 Upper Source Rail Block", ref id, upperSource,
                new Vector2(6.8f, 4.8f), UpperColor);
            ProjectableBox secondTarget = Deck(upperRoom, "04 Upper Arrival Rail Block", ref id, upperTarget,
                new Vector2(6.8f, 4.8f), UpperColor);
            ProjectionRailSwitch second = Rail(mechanisms, "Rail II - Upper Pair", secondView,
                upperSource, secondSource, secondTarget);

            Transform upperCheckpoint = Point("Checkpoint 01 - Upper Rail Landing", upperRoom,
                upperSource - secondView.Right * 2.45f + Vector3.up * 0.02f);
            CheckpointMarker(upperCheckpoint, 1);
            checkpoints.Add(upperCheckpoint);

            Vector3 exitCenter = upperTarget + new Vector3(13.25f, 3.6f, 0f);
            Transform exitRoom = Point("Section 03 - Summit Exit", platforms, Vector3.zero);
            Deck(exitRoom, "05 Summit Landing", ref id, exitCenter, new Vector2(6.4f, 4.2f),
                new Color(0.72f, 0.5f, 0.21f));
            Transform exitCheckpoint = Point("Checkpoint 02 - Summit Landing", exitRoom,
                exitCenter - Vector3.right * 1.55f + Vector3.up * 0.02f);
            CheckpointMarker(exitCheckpoint, 2);
            checkpoints.Add(exitCheckpoint);

            owner.SpawnPoint.position = lowerSource - firstView.Right * 2.85f + Vector3.up * 0.02f;
            owner.ExitPoint.position = exitCenter + Vector3.right * 1.35f + Vector3.up * 0.02f;
            owner.CameraFocusPoint.position = (lowerSource + lowerTarget) * 0.5f + Vector3.up * 1.8f;
            var settings = new SerializedObject(owner);
            settings.FindProperty("initialZoomSize").floatValue = 6.8f;
            settings.FindProperty("maxZoomSize").floatValue = 50f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            stage.StageTitle = "07 / 두 층의 레일 공방";
            stage.Instructions = "시점을 바꿔 블록 윗면을 이어 건너고, 원하는 레일부터 2D에서 몸으로 밀어 해결하세요.\n두 스위치를 완료한 뒤 3D 출구로 이동하세요. 착지대는 복귀 지점을 자동 저장합니다.";
            stage.Mechanics = new ProjectionPuzzleMechanic[] { first, second };
            stage.Rewards = new ProjectionPuzzleReward[0];
        }

        private static ProjectionRailSwitch Rail(Transform parent, string name, ProjectionFrame view,
            Vector3 sourceCenter, ProjectableBox source, ProjectableBox target)
        {
            var rail = Point(name, parent, Vector3.zero).gameObject.AddComponent<ProjectionRailSwitch>();
            Vector3 start = sourceCenter - view.Right * 1.5f + Vector3.up * 0.9f;
            Vector3 firstEnd = start + view.Right * 3f;
            Vector3 nextStart = firstEnd + view.Forward * 5.5f;
            ProjectionRailSegment a = Segment(rail.transform, "Source Track", start, firstEnd);
            ProjectionRailSegment b = Segment(rail.transform, "Arrival Track", nextStart,
                nextStart + view.Right * 3f);
            rail.Segments = new[] { a, b };
            rail.GoalPoint = Point("Yellow Destination", rail.transform, b.End);
            rail.OperatingPlatforms = new[] { source, target };
            rail.ConnectionTolerance = 0.4f;
            rail.AngleTolerance = 15f;
            return rail;
        }

        private static ProjectionRailSegment Segment(Transform parent, string name, Vector3 start, Vector3 end)
        {
            var section = Point(name, parent, Vector3.zero).gameObject.AddComponent<ProjectionRailSegment>();
            section.StartPoint = Point("Start", section.transform, start);
            section.EndPoint = Point("End", section.transform, end);
            return section;
        }

        private static ProjectableBox Deck(Transform parent, string name, ref int id,
            Vector3 topCenter, Vector2 size, Color color)
        {
            return Box(parent, name, id++, topCenter - Vector3.up * 0.5f,
                new Vector3(size.x, 1f, size.y), color);
        }

        private static ProjectableBox Box(Transform parent, string name, int id, Vector3 center,
            Vector3 size, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = ProjectionPrototype.World3DLayer;
            cube.transform.SetParent(parent, false);
            cube.transform.position = center;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = Material(color);
            var box = cube.AddComponent<ProjectableBox>();
            box.Initialize(id, color);
            return box;
        }

        private static Transform Point(string name, Transform parent, Vector3 position)
        {
            var point = new GameObject(name);
            point.layer = ProjectionPrototype.World3DLayer;
            point.transform.SetParent(parent, false);
            point.transform.position = position;
            return point.transform;
        }

        private static void CheckpointMarker(Transform point, int order)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Landing Beacon " + order;
            beacon.layer = ProjectionPrototype.World3DLayer;
            beacon.transform.SetParent(point, false);
            beacon.transform.localPosition = Vector3.up * 0.38f;
            beacon.transform.localScale = new Vector3(0.18f, 0.38f, 0.18f);
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.GetComponent<Renderer>().sharedMaterial = Material(new Color(1f, 0.72f, 0.2f));
        }

        private static Material Material(Color color)
        {
            string path = "Assets/_Game/Materials/Layered_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Layered " + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
