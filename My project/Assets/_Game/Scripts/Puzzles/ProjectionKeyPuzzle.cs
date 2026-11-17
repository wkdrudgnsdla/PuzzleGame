using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PuzzleGame
{
    /// <summary>A split key that becomes a collectible only while its two projected joins align.</summary>
    public sealed class ProjectionKeyPuzzle : ProjectionPuzzleMechanic
    {
        [Header("Split points (local +X follows the key shaft)")]
        public Transform FirstJoin;
        public Transform SecondJoin;
        [Header("Generous alignment in projected world units")]
        [Min(0.01f)] public float ConnectionTolerance = 0.4f;
        [Range(0f, 45f)] public float AngleTolerance = 15f;
        [Min(0.05f)] public float CollectRadius = 0.6f;
        public UnityEvent OnCollected = new UnityEvent();
        public UnityEvent OnReset = new UnityEvent();

        private readonly List<KeyStroke> strokes = new List<KeyStroke>();
        private Material lineMaterial;
        private Transform generated;
        private bool collected;
        private bool aligned;
        private bool alignmentKnown;
        private bool in2D;
        private bool initialized;

        private static readonly Color Gold = new Color(1f, 0.76f, 0.16f);

        public override bool IsComplete => collected;
        public bool IsAligned => aligned;
        public override string StatusHint => collected ? "열쇠 획득 완료 · 출구로 이동하세요."
            : FirstJoin == null || SecondJoin == null ? "열쇠의 두 접합점을 배치하세요."
            : aligned ? in2D ? "완성된 열쇠에 몸을 닿게 하여 획득하세요."
                : "열쇠 정렬 완료 · R로 2D에 들어가 획득하세요."
            : "3D에서 두 열쇠 조각의 접합점을 맞춘 뒤 R로 2D에 들어가세요.";

        public override void Initialize(ProjectionPrototype prototype)
        {
            base.Initialize(prototype);
            if (initialized) return;
            initialized = true;
            var root = new GameObject("Key generated visuals");
            root.transform.SetParent(transform, false);
            generated = root.transform;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader) { name = "Split key gold" };
            lineMaterial.color = Color.white;
            if (lineMaterial.HasProperty("_BaseColor")) lineMaterial.SetColor("_BaseColor", Color.white);
            if (lineMaterial.HasProperty("_Cull")) lineMaterial.SetFloat("_Cull", 0f);
            foreach (Vector3[] path in FirstHalfPaths()) AddStroke(true, path);
            foreach (Vector3[] path in SecondHalfPaths()) AddStroke(false, path);
            ResetPuzzle();
        }

        public override void Tick(ProjectionFrame frame, bool is2D, Vector2 moveInput, float deltaTime)
        {
            in2D = is2D;
            bool wasAligned = aligned;
            aligned = !collected && PrerequisitesComplete && FirstJoin != null && SecondJoin != null &&
                ProjectionPuzzleMath.AreConnected(frame, FirstJoin.position, SecondJoin.position,
                    FirstJoin.right, SecondJoin.right, ConnectionTolerance, AngleTolerance) &&
                FacesSameWay(frame, FirstJoin.right, SecondJoin.right) &&
                FacesSameWay(frame, FirstJoin.up, SecondJoin.up) &&
                ProjectionConnectionVisibility.IsJoinExposed(frame, FirstJoin.position, SecondJoin.position,
                    -FirstJoin.right, SecondJoin.right, 0.3f);
            if (alignmentKnown && !is2D && aligned && !wasAligned) GameAudioEvents.Play(SoundCue.KeyAligned);
            alignmentKnown = true;

            if (initialized) UpdateVisuals(frame, is2D);
            if (!is2D || !aligned || Prototype == null) return;

            Vector2 key = (frame.Project(FirstJoin.position) + frame.Project(SecondJoin.position)) * 0.5f;
            Vector2 foot = Prototype.PlayerProjectedFoot;
            Vector2 nearestBodyPoint = Prototype.IsTopDown2D ? foot : new Vector2(foot.x,
                Mathf.Clamp(key.y, foot.y + PrototypePlayer3D.Radius,
                    foot.y + PrototypePlayer3D.Height - PrototypePlayer3D.Radius));
            float reach = CollectRadius + PrototypePlayer3D.Radius;
            if ((key - nearestBodyPoint).sqrMagnitude > reach * reach) return;
            collected = true;
            aligned = false;
            if (generated != null) generated.gameObject.SetActive(false);
            GameAudioEvents.Play(SoundCue.KeyCollected);
            OnCollected.Invoke();
        }

        public override void ResetPuzzle()
        {
            collected = false;
            aligned = false;
            alignmentKnown = false;
            if (generated != null) generated.gameObject.SetActive(true);
            OnReset.Invoke();
        }

        private bool FacesSameWay(ProjectionFrame frame, Vector3 first, Vector3 second)
        {
            Vector2 a = new Vector2(Vector3.Dot(first, frame.Right), Vector3.Dot(first, frame.Up));
            Vector2 b = new Vector2(Vector3.Dot(second, frame.Right), Vector3.Dot(second, frame.Up));
            // An edge-on key is not a complete key silhouette.
            if (a.sqrMagnitude < 0.01f || b.sqrMagnitude < 0.01f) return false;
            return Vector2.Dot(a.normalized, b.normalized) >= Mathf.Cos(AngleTolerance * Mathf.Deg2Rad);
        }

        private void AddStroke(bool firstHalf, Vector3[] path)
        {
            strokes.Add(new KeyStroke
            {
                FirstHalf = firstHalf,
                LocalPoints = path,
                World = CreateLine(firstHalf ? "Key bow 3D" : "Key teeth 3D", ProjectionPrototype.World3DLayer),
                Projected = CreateLine(firstHalf ? "Key bow 2D" : "Key teeth 2D", ProjectionPrototype.World2DLayer)
            });
        }

        private LineRenderer CreateLine(string name, int layer)
        {
            var child = new GameObject(name);
            child.transform.SetParent(generated, false);
            child.layer = layer;
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.12f;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.startColor = line.endColor = Gold;
            line.sortingOrder = 20;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void UpdateVisuals(ProjectionFrame frame, bool is2D)
        {
            if (generated == null) return;
            generated.gameObject.SetActive(!collected);
            if (collected) return;
            Vector2 midpoint = FirstJoin != null && SecondJoin != null
                ? (frame.Project(FirstJoin.position) + frame.Project(SecondJoin.position)) * 0.5f : Vector2.zero;
            Color color = aligned ? AlignmentReadyColor : Gold;
            lineMaterial.color = color;
            if (lineMaterial.HasProperty("_BaseColor")) lineMaterial.SetColor("_BaseColor", color);
            foreach (KeyStroke stroke in strokes)
            {
                Transform join = stroke.FirstHalf ? FirstJoin : SecondJoin;
                bool valid = join != null;
                stroke.World.enabled = valid && !is2D;
                stroke.Projected.enabled = valid && is2D;
                if (!valid) continue;
                stroke.World.positionCount = stroke.Projected.positionCount = stroke.LocalPoints.Length;
                stroke.World.startColor = stroke.World.endColor = color;
                stroke.Projected.startColor = stroke.Projected.endColor = color;
                Vector2 correction = aligned && is2D ? midpoint - frame.Project(join.position) : Vector2.zero;
                for (int i = 0; i < stroke.LocalPoints.Length; i++)
                {
                    Vector3 world = join.TransformPoint(stroke.LocalPoints[i]);
                    Vector2 projected = frame.Project(world) + correction;
                    stroke.World.SetPosition(i, world);
                    stroke.Projected.SetPosition(i, new Vector3(projected.x, projected.y, -0.18f));
                }
            }
        }

        private static IEnumerable<Vector3[]> FirstHalfPaths()
        {
            var ring = new Vector3[33];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (ring.Length - 1);
                ring[i] = new Vector3(-0.68f + Mathf.Cos(angle) * 0.4f, Mathf.Sin(angle) * 0.4f, 0f);
            }
            yield return ring;
            yield return new[] { new Vector3(-0.28f, 0f, 0f), Vector3.zero };
        }

        private static IEnumerable<Vector3[]> SecondHalfPaths()
        {
            yield return new[] { Vector3.zero, new Vector3(1.1f, 0f, 0f) };
            yield return new[] { new Vector3(0.72f, 0f, 0f), new Vector3(0.72f, -0.32f, 0f),
                new Vector3(0.84f, -0.32f, 0f) };
            yield return new[] { new Vector3(1.1f, 0f, 0f), new Vector3(1.1f, -0.32f, 0f),
                new Vector3(0.98f, -0.32f, 0f) };
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.color = Gold;
            DrawPaths(FirstJoin, FirstHalfPaths());
            DrawPaths(SecondJoin, SecondHalfPaths());
            if (FirstJoin != null) Gizmos.DrawWireSphere(FirstJoin.position, 0.08f);
            if (SecondJoin != null) Gizmos.DrawWireSphere(SecondJoin.position, 0.08f);
        }

        private static void DrawPaths(Transform join, IEnumerable<Vector3[]> paths)
        {
            if (join == null) return;
            foreach (Vector3[] path in paths)
                for (int i = 1; i < path.Length; i++)
                    Gizmos.DrawLine(join.TransformPoint(path[i - 1]), join.TransformPoint(path[i]));
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
            if (generated != null) Destroy(generated.gameObject);
        }

        private sealed class KeyStroke
        {
            public bool FirstHalf;
            public Vector3[] LocalPoints;
            public LineRenderer World;
            public LineRenderer Projected;
        }
    }
}
