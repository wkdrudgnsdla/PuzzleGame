using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PuzzleGame
{
    /// <summary>
    /// A solid slider pushed by the player's body along an ordered rail. Depth-separated
    /// sections connect only when their projected endpoints and directions agree.
    /// </summary>
    public sealed class ProjectionRailSwitch : ProjectionPuzzleMechanic
    {
        [Header("Ordered rail route")]
        public ProjectionRailSegment[] Segments = new ProjectionRailSegment[0];
        [Tooltip("Authored corners connect by exposed endpoints; straight joints also require matching tangents.")]
        public bool AllowCornerJoints;
        [Tooltip("Defaults to the final section's end. This marker must also align before completion.")]
        public Transform GoalPoint;
        [Min(0.01f)] public float ConnectionTolerance = 0.4f;
        [Range(0f, 45f)] public float AngleTolerance = 15f;
        [Header("Authored rail floors (placement metadata)")]
        [Tooltip("Records the rail's supporting floors. Visibility and pushing depend on mode and body contact, not floor IDs.")]
        public ProjectableBox[] OperatingPlatforms = new ProjectableBox[0];

        [Header("Body-pushed switch")]
        [Min(0.2f)] public float SliderSize = 0.72f;
        [Min(0.1f)] public float PushSpeed = 3f;
        [Min(0.01f)] public float ContactTolerance = 0.12f;
        public Color SwitchColor = new Color(0.12f, 0.86f, 0.32f);
        public Color DestinationColor = new Color(1f, 0.91f, 0.05f);
        public UnityEvent OnCompleted = new UnityEvent();
        public UnityEvent OnReset = new UnityEvent();

        private ProjectionPrototype prototype;
        private CapsuleCollider2D playerCollider;
        private Transform visualRoot;
        private Transform switch3D;
        private Transform switch2D;
        private Transform switchMesh2D;
        private Transform goal3D;
        private Transform goal2D;
        private BoxCollider2D sliderCollider;
        private Rigidbody2D sliderBody;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<LineRenderer> rails3D = new List<LineRenderer>();
        private readonly List<LineRenderer> rails2D = new List<LineRenderer>();
        private readonly List<LineRenderer> bridges = new List<LineRenderer>();
        private Material greenMaterial;
        private Material targetMaterial;
        private Material borderMaterial;
        private Material railMaterial;
        private int segmentIndex;
        private float segmentProgress;
        private bool complete;
        private bool connected;
        private bool in2D;
        private bool valid;

        public override bool IsComplete => complete;
        public bool IsAligned => connected;
        public float Progress => Segments == null || Segments.Length == 0 ? 0f
            : (segmentIndex + segmentProgress) / Segments.Length;
        public Vector3 SliderWorldPosition => valid
            ? Vector3.Lerp(Segments[segmentIndex].Start, Segments[segmentIndex].End, segmentProgress)
            : transform.position;
        public override string StatusHint => complete ? "레일 스위치 활성화 완료"
            : !valid ? "레일의 시작점과 끝점을 지정하세요."
            : !in2D ? connected ? "레일 연결 가능 · R로 2D에 들어가 초록 스위치를 몸으로 미세요."
                : "레일 끝이 이어지도록 카메라를 조절하세요."
            : !connected ? "레일이 끊겨 있습니다 · R로 돌아가 접합부를 맞추세요."
            : "초록 스위치를 몸으로 밀어 노란 도착점까지 옮기세요.";

        public override void Initialize(ProjectionPrototype owner)
        {
            base.Initialize(owner);
            prototype = owner;
            valid = Segments != null && Segments.Length > 0;
            if (valid)
                foreach (ProjectionRailSegment segment in Segments)
                    if (segment == null || !segment.IsValid) { valid = false; break; }
            if (!valid) return;

            PrototypePlayer2D player = owner.GetComponentInChildren<PrototypePlayer2D>(true);
            if (player != null) playerCollider = player.GetComponent<CapsuleCollider2D>();
            if (visualRoot == null) BuildVisuals();
            ResetPuzzle();
        }

        public override void ResetPuzzle()
        {
            segmentIndex = 0;
            segmentProgress = 0f;
            complete = false;
            connected = false;
            if (targetMaterial != null) SetMaterialColor(targetMaterial, DestinationColor);
            if (sliderCollider != null) sliderCollider.enabled = false;
            OnReset.Invoke();
        }

        public override void Tick(ProjectionFrame frame, bool is2D, Vector2 moveInput, float deltaTime)
        {
            in2D = is2D;
            if (!valid || visualRoot == null || sliderCollider == null || sliderBody == null) return;
            connected = CurrentConnectionAligned(frame);
            Vector2 position = frame.Project(SliderWorldPosition);

            // The collider stays solid at an unconnected joint. It is never terrain, so the
            // player cannot use the slider as a fake support for returning to the 3D world.
            // Every visible switch can be pushed on contact, regardless of the
            // previous objective or the floor the player most recently stood on.
            sliderCollider.enabled = is2D && !complete;
            PositionSlider(position);
            if (is2D && !complete && deltaTime > 0f && CanPush(frame, moveInput, out float direction))
            {
                Advance(frame, direction * Mathf.Min(PushSpeed, PrototypePlayer3D.Speed)
                    * Mathf.Clamp(deltaTime, 0f, 0.05f));
                position = frame.Project(SliderWorldPosition);
                PositionSlider(position);
                connected = CurrentConnectionAligned(frame);
            }
            RefreshVisuals(frame);
        }

        private bool CanPush(ProjectionFrame frame, Vector2 moveInput, out float direction)
        {
            direction = 0f;
            if (prototype == null || playerCollider == null || !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy) return false;
            Vector2 start = frame.Project(Segments[segmentIndex].Start);
            Vector2 end = frame.Project(Segments[segmentIndex].End);
            Vector2 tangent = end - start;
            if (tangent.sqrMagnitude < 0.01f) return false;
            tangent.Normalize();
            Vector2 input = prototype.IsTopDown2D ? moveInput : new Vector2(moveInput.x, 0f);
            float along = Vector2.Dot(input, tangent);
            if (Mathf.Abs(along) < 0.15f) return false;
            float sign = Mathf.Sign(along);

            Vector2 sliderPosition = frame.Project(SliderWorldPosition);
            Vector2 playerPosition = prototype.PlayerProjectedFoot +
                (prototype.IsTopDown2D ? Vector2.zero : Vector2.up * (PrototypePlayer3D.Height * 0.5f));
            Vector2 toSlider = sliderPosition - playerPosition;
            // Requiring the player's body to be behind the desired direction prevents pulling
            // the slider, pushing from the far side, or activating it with movement at a distance.
            if (Vector2.Dot(toSlider, tangent) * sign < 0.08f) return false;
            float perpendicular = Mathf.Abs(Vector2.Dot(toSlider, new Vector2(-tangent.y, tangent.x)));
            float bodyReach = prototype.IsTopDown2D ? PrototypePlayer3D.Radius
                : Mathf.Abs(tangent.x) * PrototypePlayer3D.Height * 0.5f
                    + Mathf.Abs(tangent.y) * PrototypePlayer3D.Radius;
            if (perpendicular > SliderSize * 0.5f + bodyReach + ContactTolerance) return false;

            // Closest-points distance uses the actual character capsule and the solid slider.
            // The small predictive margin is at most one movement step, never a remote push.
            ColliderDistance2D contact = sliderCollider.Distance(playerCollider);
            float stepMargin = Mathf.Min(0.1f, PrototypePlayer3D.Speed * Time.fixedDeltaTime);
            if (!contact.isOverlapped && contact.distance > ContactTolerance + stepMargin) return false;
            direction = sign * Mathf.Abs(along);
            return true;
        }

        private void Advance(ProjectionFrame frame, float distance)
        {
            int safety = Segments.Length + 1;
            while (Mathf.Abs(distance) > 0.00001f && safety-- > 0)
            {
                ProjectionRailSegment segment = Segments[segmentIndex];
                float length = Vector2.Distance(frame.Project(segment.Start), frame.Project(segment.End));
                if (length < 0.1f) return;
                if (distance > 0f)
                {
                    float remaining = (1f - segmentProgress) * length;
                    if (distance < remaining)
                    {
                        segmentProgress += distance / length;
                        return;
                    }
                    segmentProgress = 1f;
                    distance -= remaining;
                    if (segmentIndex == Segments.Length - 1)
                    {
                        if (GoalAligned(frame)) Complete();
                        return;
                    }
                    // Do not consume or pass the next segment before validating its joint.
                    if (!JointAligned(frame, segmentIndex)) return;
                    segmentIndex++;
                    segmentProgress = 0f;
                }
                else
                {
                    float remaining = segmentProgress * length;
                    if (-distance < remaining)
                    {
                        segmentProgress += distance / length;
                        return;
                    }
                    segmentProgress = 0f;
                    distance += remaining;
                    if (segmentIndex == 0 || !JointAligned(frame, segmentIndex - 1)) return;
                    segmentIndex--;
                    segmentProgress = 1f;
                }
            }
        }

        private void Complete()
        {
            if (complete) return;
            complete = true;
            sliderCollider.enabled = false;
            SetMaterialColor(targetMaterial, SwitchColor);
            OnCompleted.Invoke();
        }

        private bool CurrentConnectionAligned(ProjectionFrame frame)
        {
            ProjectionRailSegment current = Segments[segmentIndex];
            if ((frame.Project(current.End) - frame.Project(current.Start)).sqrMagnitude < 0.01f)
                return false;
            // A later joint may need another camera angle. Only the connection the
            // slider will reach next determines the current alignment feedback.
            return segmentIndex < Segments.Length - 1
                ? JointAligned(frame, segmentIndex) : GoalAligned(frame);
        }

        private bool JointAligned(ProjectionFrame frame, int previous)
        {
            ProjectionRailSegment a = Segments[previous];
            ProjectionRailSegment b = Segments[previous + 1];
            Vector2 directionA = frame.Project(a.End) - frame.Project(a.Start);
            Vector2 directionB = frame.Project(b.End) - frame.Project(b.Start);
            if (AllowCornerJoints)
                return directionA.sqrMagnitude >= .01f && directionB.sqrMagnitude >= .01f &&
                    Vector2.Distance(frame.Project(a.End), frame.Project(b.Start)) <= ConnectionTolerance &&
                    ProjectionConnectionVisibility.IsJoinExposed(frame, a.End, b.Start, -a.Direction, b.Direction);
            // Directed travel must continue forward, not double back into the slider.
            return Vector2.Dot(directionA, directionB) > 0f &&
                ProjectionPuzzleMath.AreConnected(frame, a.End, b.Start, a.Direction, b.Direction,
                    ConnectionTolerance, AngleTolerance) &&
                ProjectionConnectionVisibility.IsJoinExposed(frame, a.End, b.Start, -a.Direction, b.Direction);
        }

        private bool GoalAligned(ProjectionFrame frame)
        {
            ProjectionRailSegment last = Segments[Segments.Length - 1];
            if (!ProjectionConnectionVisibility.IsEndExposed(frame, last.End, -last.Direction)) return false;
            if (GoalPoint == null) return true;
            return Vector2.Distance(frame.Project(last.End), frame.Project(GoalPoint.position)) <= ConnectionTolerance
                && ProjectionConnectionVisibility.IsExposed(frame, GoalPoint.position);
        }

        private void BuildVisuals()
        {
            var root = new GameObject("Generated rail visuals");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
            greenMaterial = NewMaterial("Rail switch green", SwitchColor);
            targetMaterial = NewMaterial("Rail destination yellow", DestinationColor);
            borderMaterial = NewMaterial("Rail dark border", new Color(0.035f, 0.07f, 0.09f));
            railMaterial = NewMaterial("Rail guide", new Color(0.73f, 0.83f, 0.91f));
            for (int i = 0; i < Segments.Length; i++)
            {
                // Paired guide lines read as a rail rather than a wire.
                for (int guide = 0; guide < 2; guide++)
                {
                    rails3D.Add(NewLine("3D rail " + (i + 1), ProjectionPrototype.World3DLayer, railMaterial, 0.09f));
                    rails2D.Add(NewLine("2D rail " + (i + 1), ProjectionPrototype.World2DLayer, railMaterial, 0.09f));
                }
                if (i < Segments.Length - 1)
                    bridges.Add(NewLine("Accepted rail joint " + (i + 1), ProjectionPrototype.World2DLayer,
                        greenMaterial, 0.12f));
            }
            switch3D = NewCube("Green rail switch 3D", ProjectionPrototype.World3DLayer, greenMaterial,
                Vector3.one * SliderSize);
            // A primitive cube contains a BoxCollider until Destroy runs at frame end.
            // Rigidbody2D cannot coexist with that 3D collider, even when it is disabled.
            // Keep physics on a clean root and the primitive mesh on its own child.
            var switchBodyObject = new GameObject("Green rail switch 2D", typeof(Rigidbody2D), typeof(BoxCollider2D));
            switchBodyObject.layer = ProjectionPrototype.Player2DLayer;
            switchBodyObject.transform.SetParent(visualRoot, false);
            switch2D = switchBodyObject.transform;
            sliderBody = switchBodyObject.GetComponent<Rigidbody2D>();
            sliderBody.bodyType = RigidbodyType2D.Kinematic;
            sliderBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            sliderBody.interpolation = RigidbodyInterpolation2D.None;
            sliderBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            sliderCollider = switchBodyObject.GetComponent<BoxCollider2D>();
            sliderCollider.size = Vector2.one * SliderSize;
            sliderCollider.enabled = false;
            if (playerCollider != null) sliderCollider.sharedMaterial = playerCollider.sharedMaterial;
            switchMesh2D = NewCube("Green switch mesh 2D", ProjectionPrototype.Player2DLayer,
                greenMaterial, new Vector3(SliderSize, SliderSize, 0.18f));
            switchMesh2D.SetParent(switch2D, false);
            NewFrame("Switch frame 3D", switch3D, ProjectionPrototype.World3DLayer, borderMaterial);
            NewFrame("Switch frame 2D", switchMesh2D, ProjectionPrototype.Player2DLayer, borderMaterial);
            goal3D = NewCube("Yellow rail destination 3D", ProjectionPrototype.World3DLayer, targetMaterial,
                new Vector3(0.16f, SliderSize * 1.35f, SliderSize * 1.35f));
            goal2D = NewCube("Yellow rail destination 2D", ProjectionPrototype.World2DLayer, targetMaterial,
                new Vector3(0.16f, SliderSize * 1.35f, 0.2f));
        }

        private void RefreshVisuals(ProjectionFrame frame)
        {
            bool showProjection = in2D;
            // Keep the physics root enabled: visibility must not alter the contact
            // lifecycle while the player jumps or stands on another floor.
            switchMesh2D.gameObject.SetActive(showProjection);
            goal2D.gameObject.SetActive(showProjection);
            for (int i = 0; i < Segments.Length; i++)
            {
                ProjectionRailSegment segment = Segments[i];
                Vector2 a = frame.Project(segment.Start);
                Vector2 b = frame.Project(segment.End);
                Vector2 screenDirection = (b - a).normalized;
                Vector2 perpendicular = new Vector2(-screenDirection.y, screenDirection.x) * 0.13f;
                Vector3 perpendicular3D = Vector3.Cross(segment.Direction.normalized,
                    AllowCornerJoints ? Vector3.up : Vector3.forward).normalized * 0.13f;
                if (perpendicular3D.sqrMagnitude < 0.001f) perpendicular3D = Vector3.up * 0.13f;
                for (int guide = 0; guide < 2; guide++)
                {
                    float sign = guide == 0 ? -1f : 1f;
                    SetLine(rails3D[i * 2 + guide], segment.Start + perpendicular3D * sign,
                        segment.End + perpendicular3D * sign);
                    Vector2 offset = perpendicular * sign;
                    rails2D[i * 2 + guide].enabled = showProjection;
                    SetLine(rails2D[i * 2 + guide], To2D(a + offset, -0.3f), To2D(b + offset, -0.3f));
                }
                if (i < Segments.Length - 1)
                {
                    LineRenderer bridge = bridges[i];
                    bridge.enabled = showProjection && JointAligned(frame, i);
                    SetLine(bridge, To2D(b, -0.31f), To2D(frame.Project(Segments[i + 1].Start), -0.31f));
                }
            }
            switch3D.position = SliderWorldPosition;
            Vector3 goal = GoalPoint != null ? GoalPoint.position : Segments[Segments.Length - 1].End;
            goal3D.position = goal;
            ProjectionRailSegment last = Segments[Segments.Length - 1];
            goal3D.rotation = Quaternion.FromToRotation(Vector3.right, last.Direction.normalized);
            goal2D.position = To2D(frame.Project(goal), -0.4f);
            Vector2 finalDirection = frame.Project(last.End) - frame.Project(last.Start);
            goal2D.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg);
        }

        private void PositionSlider(Vector2 position)
        {
            sliderBody.position = position;
            switch2D.position = To2D(position, -0.5f);
            Physics2D.SyncTransforms();
        }

        private Material NewMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = materialName };
            SetMaterialColor(material, color);
            materials.Add(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        private LineRenderer NewLine(string objectName, int layer, Material material, float width)
        {
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(visualRoot, false);
            lineObject.layer = layer;
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private Transform NewCube(string objectName, int layer, Material material, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.layer = layer;
            cube.transform.SetParent(visualRoot, false);
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return cube.transform;
        }

        private void NewFrame(string objectName, Transform target, int layer, Material material)
        {
            LineRenderer frame = NewLine(objectName, layer, material, 0.075f);
            frame.useWorldSpace = false;
            frame.transform.SetParent(target, false);
            frame.positionCount = 4;
            frame.loop = true;
            frame.SetPositions(new[] { new Vector3(-0.5f, -0.5f, -0.55f), new Vector3(0.5f, -0.5f, -0.55f),
                new Vector3(0.5f, 0.5f, -0.55f), new Vector3(-0.5f, 0.5f, -0.55f) });
        }

        private static void SetLine(LineRenderer line, Vector3 a, Vector3 b)
        {
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        private static Vector3 To2D(Vector2 point, float depth) => new Vector3(point.x, point.y, depth);

        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            if (visualRoot != null) Destroy(visualRoot.gameObject);
        }
    }
}
