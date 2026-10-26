using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace PuzzleGame
{
    /// <summary>Playable, self-contained proof of the shared 3D / projected 2D world.</summary>
    public sealed class ProjectionPrototype : MonoBehaviour
    {
        public const int World3DLayer = 8;
        public const int World2DLayer = 9;
        public const int Player3DLayer = 10;
        public const int Player2DLayer = 11;

        public Material SurfaceTemplate;
        public Material FlatTemplate;

        [Header("Scene map")]
        public Transform MapRoot;
        public Transform SpawnPoint;
        public Transform ExitPoint;
        public Transform CameraFocusPoint;
        public ProjectionPrototypeHud Hud;
        public StageRoute Route;

        [Header("Mouse orbit")]
        [SerializeField, Min(0.01f)] private float dragDegreesPerPixel = 0.25f;
        [SerializeField, Min(1f)] private float dragSmoothing = 20f;
        [SerializeField, Range(65f, 90f)] private float verticalOrbitLimitDegrees = 90f;
        [SerializeField, Range(20f, 80f)] private float topDownPitchThreshold = 50f;

        [Header("Camera zoom and pan")]
        [SerializeField, Min(0.1f)] private float minZoomSize = 1.5f;
        [SerializeField, Min(0.1f)] private float maxZoomSize = 20f;
        [SerializeField, Min(0.1f)] private float initialZoomSize = 4.4f;
        [SerializeField, Min(0.01f)] private float wheelZoomSpeed = 0.15f;
        [SerializeField, Min(1f)] private float zoomSmoothing = 16f;
        [SerializeField, Min(0.01f)] private float panSensitivity = 1f;
        [SerializeField, Min(1f)] private float panSmoothing = 20f;

        private readonly List<ProjectableBox> terrain = new List<ProjectableBox>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly Collider[] overlapBuffer = new Collider[16];
        private ProjectionFrame frame;
        private ProjectionWorldBuilder projectionBuilder;
        private PrototypePlayer3D player3D;
        private PrototypePlayer2D player2D;
        private Camera camera3D;
        private Camera camera2D;
        private Transform projectionRoot;
        private Transform returnMarker;
        private Transform goal2D;
        private GameObject sidePlayerVisual;
        private GameObject topPlayerVisual;
        private Material[] projectedMaterials;
        private PhysicsMaterial2D frictionless;
        private ProjectableBox support;
        private Vector3 lastAnchor;
        private Vector2? moveInputOverride;
        private bool jumpInputOverride;
        private bool initialized;
        private float messageUntil;
        private string message;
        private bool draggingView;
        private bool projectionDirty;
        private Vector2 lastDragPosition;
        private float currentViewYaw;
        private float targetViewYaw;
        private float currentViewPitch;
        private float targetViewPitch;
        private bool panningCamera;
        private Vector2 lastPanPosition;
        private Vector3 currentPanOffset;
        private Vector3 targetPanOffset;
        private float currentZoomSize;
        private float targetZoomSize;

        private Vector3 Spawn => SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
        private Vector3 Goal => ExitPoint != null ? ExitPoint.position : new Vector3(8f, 0f, 4f);
        private Vector3 CameraFocus => (CameraFocusPoint != null ? CameraFocusPoint.position
            : new Vector3(4f, 1.1f, 2f)) + currentPanOffset;

        public bool Is2D { get; private set; }
        public float YawDegrees => frame.Yaw;
        public float PitchDegrees => frame.Pitch;
        public float ZoomSize => currentZoomSize;
        public bool IsTopDownView => Mathf.Abs(PitchDegrees) >= topDownPitchThreshold;
        public bool IsTopDown2D => Is2D && IsTopDownView;
        public IReadOnlyList<ProjectableBox> Terrain => terrain;
        public bool HasWon { get; private set; }
        public bool IsGrounded => initialized && (Is2D ? IsTopDown2D
            ? FindTopDownSupport(player2D.Foot, out _) != null
            : player2D.IsGrounded : player3D.IsGrounded);
        public int CurrentSupportId => support != null ? support.Id : -1;
        public Vector3 PlayerWorldFoot => !initialized ? Spawn :
            !Is2D ? player3D.Foot : ReturnPreviewFoot ?? lastAnchor;
        public Vector2 PlayerProjectedFoot => !initialized ? Vector2.zero :
            Is2D ? player2D.Foot : frame.Project(player3D.Foot);
        public Vector3? ReturnPreviewFoot => TryGetReturnFoot(out Vector3 foot) ? foot : (Vector3?)null;
        public bool CanReturnTo3D => Is2D && TryGetReturnFoot(out _);
        public string StatusMessage
        {
            get
            {
                if (!initialized) return "월드를 준비하는 중입니다.";
                if (Time.unscaledTime < messageUntil) return message;
                if (HasWon) return "성공! 2D에서 건넌 이동이 실제 3D 위치에 반영됐습니다. Backspace: 다시 시작";
                if (IsTopDown2D) return CanReturnTo3D
                    ? "탑뷰 2D · WASD로 바닥을 이동하고 R로 현재 위치에서 3D로 돌아가세요."
                    : "탑뷰 2D · 복귀 공간을 확보하려면 바닥 안쪽으로 조금 이동하세요.";
                if (!IsGrounded) return "공중에서는 전환할 수 없습니다. 발판에 착지하세요.";
                if (Is2D) return CanReturnTo3D ? "복귀 가능 · R을 눌러 현재 발판의 3D 위치로 돌아가세요."
                    : "복귀 공간이 부족합니다. 발판 안쪽으로 조금 더 이동하세요.";
                if (IsTopDownView) return "탑뷰 시점 · R로 중력 없는 2D 평면 이동에 들어가세요.";
                return "좌클릭 드래그로 카메라 이동 · 우클릭 드래그로 회전 · 휠로 줌 · R로 차원 전환";
            }
        }

        private void Start()
        {
            if (!InitializeWorld()) { enabled = false; return; }
            initialized = true;
            if (Route != null) Route.Initialize(this);
            ResetPrototype();
        }

        private bool InitializeWorld()
        {
            if (MapRoot == null)
            {
                Debug.LogError("Assign the scene's Map object to ProjectionPrototype.MapRoot before playing.", this);
                return false;
            }
            var usedIds = new HashSet<int>();
            ProjectableBox[] placedBoxes = MapRoot.GetComponentsInChildren<ProjectableBox>();
            var reservedIds = new HashSet<int>();
            foreach (ProjectableBox box in placedBoxes) if (box.Id > 0) reservedIds.Add(box.Id);
            int nextId = 1;
            foreach (ProjectableBox box in placedBoxes)
            {
                int id = box.Id;
                if (id <= 0 || usedIds.Contains(id))
                {
                    while (usedIds.Contains(nextId) || reservedIds.Contains(nextId)) nextId++;
                    id = nextId++;
                }
                usedIds.Add(id);
                box.Initialize(id, box.BodyColor);
                terrain.Add(box);
            }
            if (terrain.Count == 0)
            {
                Debug.LogError("Add a BoxCollider and ProjectableBox to a platform under Map before playing.", this);
                return false;
            }
            foreach (Transform placed in MapRoot.GetComponentsInChildren<Transform>(true))
                placed.gameObject.layer = World3DLayer;
            frame = new ProjectionFrame(Vector3.zero, 0f);
            frictionless = new PhysicsMaterial2D("Prototype frictionless") { friction = 0f, bounciness = 0f };

            projectionRoot = NewChild("Generated 2D world", transform);
            projectionBuilder = projectionRoot.gameObject.AddComponent<ProjectionWorldBuilder>();
            projectedMaterials = new Material[terrain.Count];
            for (int i = 0; i < terrain.Count; i++)
                projectedMaterials[i] = CreateMaterial(terrain[i].BodyColor, true);

            var playerObject = new GameObject("Player 3D", typeof(CharacterController));
            playerObject.layer = Player3DLayer;
            playerObject.transform.SetParent(transform, false);
            player3D = playerObject.AddComponent<PrototypePlayer3D>();
            GameObject visual3D = CreatePrimitive("Player visual", PrimitiveType.Capsule,
                playerObject.transform, Vector3.up * 0.9f, new Vector3(0.6f, 0.9f, 0.6f),
                CreateMaterial(new Color(0.90f, 0.96f, 1f), false), Player3DLayer);
            Destroy(visual3D.GetComponent<Collider>());

            var playerObject2D = new GameObject("Player 2D", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            playerObject2D.layer = Player2DLayer;
            playerObject2D.transform.SetParent(transform, false);
            player2D = playerObject2D.AddComponent<PrototypePlayer2D>();
            playerObject2D.GetComponent<CapsuleCollider2D>().sharedMaterial = frictionless;
            GameObject visual2D = CreatePrimitive("Player silhouette", PrimitiveType.Capsule,
                playerObject2D.transform, Vector3.zero, new Vector3(0.6f, 0.9f, 0.06f),
                CreateMaterial(new Color(0.93f, 0.99f, 1f), true), Player2DLayer);
            Destroy(visual2D.GetComponent<Collider>());
            sidePlayerVisual = visual2D;
            topPlayerVisual = CreatePrimitive("Top view player", PrimitiveType.Sphere,
                playerObject2D.transform, Vector3.zero, new Vector3(0.6f, 0.6f, 0.06f),
                visual2D.GetComponent<Renderer>().sharedMaterial, Player2DLayer);
            Destroy(topPlayerVisual.GetComponent<Collider>());
            topPlayerVisual.SetActive(false);

            camera3D = CreateCamera("3D camera", (1 << World3DLayer) | (1 << Player3DLayer));
            camera3D.gameObject.tag = "MainCamera";
            camera3D.gameObject.AddComponent<AudioListener>();
            camera2D = CreateCamera("2D projection camera", (1 << World2DLayer) | (1 << Player2DLayer));

            Material goalMaterial = CreateMaterial(new Color(0.49f, 0.96f, 0.63f), true);
            GameObject marker = CreatePrimitive("Safe 3D return preview", PrimitiveType.Cube, transform,
                Vector3.zero, new Vector3(0.65f, 0.035f, 0.65f), goalMaterial, World3DLayer);
            Destroy(marker.GetComponent<Collider>());
            returnMarker = marker.transform;
            returnMarker.gameObject.SetActive(false);

            goal2D = CreatePrimitive("Projected exit marker", PrimitiveType.Cube, projectionRoot,
                Vector3.zero, new Vector3(0.22f, 1.15f, 0.08f), goalMaterial, World2DLayer).transform;
            Destroy(goal2D.GetComponent<Collider>());
            if (Hud != null) Hud.Initialize(this);
            return true;
        }

        private Camera CreateCamera(string cameraName, int mask)
        {
            var cameraObject = new GameObject(cameraName, typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera result = cameraObject.GetComponent<Camera>();
            result.orthographic = true;
            result.orthographicSize = ClampZoom(initialZoomSize);
            result.nearClipPlane = 0.1f;
            result.farClipPlane = 100f;
            result.cullingMask = mask;
            result.clearFlags = CameraClearFlags.SolidColor;
            result.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
            result.allowHDR = false;
            return result;
        }

        private Material CreateMaterial(Color color, bool flat)
        {
            Material template = flat ? FlatTemplate : SurfaceTemplate;
            Shader shader = template != null ? template.shader : Shader.Find(flat ?
                "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = template != null ? new Material(template) : new Material(shader);
            material.name = "Prototype " + (flat ? "flat" : "surface");
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            if (flat && material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            ownedMaterials.Add(material);
            return material;
        }

        private static Transform NewChild(string childName, Transform parent)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material, int layer)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = objectName;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.layer = layer;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private void Update()
        {
            if (!initialized) return;
            Keyboard keyboard = Keyboard.current;
            Vector2 movement = moveInputOverride ?? ReadMovement(keyboard);
            bool jump = jumpInputOverride || (keyboard != null &&
                keyboard.spaceKey.wasPressedThisFrame);
            jumpInputOverride = false;
            if (keyboard != null)
            {
                if (keyboard.backspaceKey.wasPressedThisFrame) { ResetPrototype(); return; }
                if (keyboard.escapeKey.wasPressedThisFrame && !Application.isEditor) Application.Quit();
            }

            UpdateMouseOrbit();
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) TryToggleMode();
            if (draggingView || panningCamera) { movement = Vector2.zero; jump = false; }

            if (Is2D) player2D.SetInput(movement, jump);
            else player3D.Tick(movement, frame, jump, Time.deltaTime);
            UpdateSupport();
            if (!IsTopDown2D && (Is2D ? player2D.Foot.y : player3D.Foot.y) < -5f)
            {
                ResetPlayerAt(Route != null ? Route.RespawnPosition : Spawn);
                Notify("낙하했습니다. 마지막 도달 지점으로 돌아왔습니다.");
            }
            if (!Is2D && support != null && IsGrounded &&
                Vector3.Distance(player3D.Foot, Goal) < 0.8f && (Route == null || Route.IsComplete)) HasWon = true;

            Vector3? preview = ReturnPreviewFoot;
            returnMarker.gameObject.SetActive(Is2D && preview.HasValue);
            if (preview.HasValue) returnMarker.position = preview.Value + Vector3.up * 0.02f;
        }

        private void UpdateMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pointer = mouse.position.ReadValue();
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    float steps = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
                    if (!Mathf.Approximately(steps, 0f))
                        targetZoomSize = ClampZoom(targetZoomSize * Mathf.Exp(-steps * wheelZoomSpeed));
                }
                if (!Is2D && !overUi && mouse.rightButton.wasPressedThisFrame)
                {
                    StopMouseOrbit();
                    draggingView = true;
                    lastDragPosition = pointer;
                    targetViewYaw = currentViewYaw;
                    targetViewPitch = currentViewPitch;
                }
                if (!overUi && mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.isPressed)
                {
                    StopMouseOrbit();
                    panningCamera = true;
                    lastPanPosition = pointer;
                    targetPanOffset = currentPanOffset;
                }
                if (draggingView)
                {
                    // Pointer displacement already measures this frame's movement; do not multiply by deltaTime.
                    Vector2 delta = pointer - lastDragPosition;
                    float orbitSensitivity = dragDegreesPerPixel * Mathf.Min(1f,
                        currentZoomSize / Mathf.Max(0.1f, initialZoomSize));
                    targetViewYaw += delta.x * orbitSensitivity;
                    targetViewPitch = Mathf.Clamp(targetViewPitch - delta.y * orbitSensitivity,
                        -verticalOrbitLimitDegrees, verticalOrbitLimitDegrees);
                    lastDragPosition = pointer;
                    if (!mouse.rightButton.isPressed) draggingView = false;
                }
                if (panningCamera)
                {
                    Vector2 delta = pointer - lastPanPosition;
                    float unitsPerPixel = 2f * currentZoomSize / Mathf.Max(1, camera3D.pixelHeight);
                    targetPanOffset -= (frame.Right * delta.x + frame.Up * delta.y) * unitsPerPixel * panSensitivity;
                    lastPanPosition = pointer;
                    if (!mouse.leftButton.isPressed) panningCamera = false;
                }
            }
            else if (draggingView || panningCamera) StopMouseOrbit();

            float remaining = targetViewYaw - currentViewYaw;
            float pitchRemaining = targetViewPitch - currentViewPitch;
            if (!Is2D && (Mathf.Abs(remaining) > 0.001f || Mathf.Abs(pitchRemaining) > 0.001f))
            {
                // Keep the drag angle unwrapped so a stroke can pass through 0/360 without reversing.
                float blend = 1f - Mathf.Exp(-dragSmoothing * Time.unscaledDeltaTime);
                float next = Mathf.Abs(remaining) < 0.01f ? targetViewYaw
                    : Mathf.Lerp(currentViewYaw, targetViewYaw, blend);
                currentViewPitch = Mathf.Abs(pitchRemaining) < 0.01f ? targetViewPitch
                    : Mathf.Lerp(currentViewPitch, targetViewPitch, blend);
                ApplyView(next, false);
            }
            // Rebuild the derived 2D world when the view settles or R confirms it.
            if (!draggingView && projectionDirty && Mathf.Abs(targetViewYaw - currentViewYaw) <= 0.001f
                && Mathf.Abs(targetViewPitch - currentViewPitch) <= 0.001f)
            {
                currentViewYaw = targetViewYaw = YawDegrees;
                RebuildProjection();
            }

            targetZoomSize = ClampZoom(targetZoomSize);
            float nextZoom = Mathf.Lerp(currentZoomSize, targetZoomSize,
                1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime));
            nextZoom = ClampZoom(Mathf.Abs(nextZoom - targetZoomSize) < 0.001f ? targetZoomSize : nextZoom);
            Vector3 nextPan = Vector3.Lerp(currentPanOffset, targetPanOffset,
                1f - Mathf.Exp(-panSmoothing * Time.unscaledDeltaTime));
            if ((nextPan - targetPanOffset).sqrMagnitude < 0.000001f) nextPan = targetPanOffset;
            if (nextZoom != currentZoomSize || nextPan != currentPanOffset)
            {
                currentZoomSize = nextZoom;
                currentPanOffset = nextPan;
                UpdateCameraPose();
            }
        }

        private void StopMouseOrbit()
        {
            draggingView = false;
            panningCamera = false;
            targetViewYaw = currentViewYaw;
            targetViewPitch = currentViewPitch;
            targetPanOffset = currentPanOffset;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StopMouseOrbit();
        }

        private static Vector2 ReadMovement(Keyboard keyboard)
        {
            if (keyboard == null) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)), 1f);
        }

        private void UpdateSupport()
        {
            if (IsTopDown2D)
            {
                support = FindTopDownSupport(player2D.Foot, out Vector3 worldFoot);
                if (support != null) lastAnchor = worldFoot;
                return;
            }
            if (!IsGrounded) { support = null; return; }
            if (!Is2D)
            {
                support = null;
                Vector3 foot = player3D.Foot;
                foreach (ProjectableBox box in terrain)
                {
                    Bounds bounds = box.SourceCollider.bounds;
                    if (Mathf.Abs(foot.y - bounds.max.y) < 0.12f &&
                        foot.x >= bounds.min.x - 0.02f && foot.x <= bounds.max.x + 0.02f &&
                        foot.z >= bounds.min.z - 0.02f && foot.z <= bounds.max.z + 0.02f)
                    { support = box; lastAnchor = foot; break; }
                }
                return;
            }
            if (IsProjectedSupport(support)) return;
            support = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (ProjectableBox box in terrain)
            {
                if (!IsProjectedSupport(box)) continue;
                Vector3 anchor;
                float distance = ProjectionMath.TryGetProjectedSurface(box.SourceCollider, frame,
                    player2D.Foot.x, out float surfaceHeight, out _) &&
                    ProjectionMath.TryResolveBoxTop(box.SourceCollider, frame,
                        new Vector2(player2D.Foot.x, surfaceHeight), 0f, lastAnchor, out anchor)
                    ? (anchor - lastAnchor).sqrMagnitude : float.PositiveInfinity;
                if (support == null || distance < nearestDistance - 0.001f ||
                    (Mathf.Abs(distance - nearestDistance) < 0.001f && box.Id < support.Id))
                { support = box; nearestDistance = distance; }
            }
        }

        private bool IsProjectedSupport(ProjectableBox box)
        {
            return box != null && box.ProjectedCollider != null &&
                player2D.IsSupportedBy(box.ProjectedCollider);
        }

        private bool TryGetReturnFoot(out Vector3 foot)
        {
            foot = default;
            if (!initialized || !Is2D || !IsGrounded || support == null) return false;
            if (IsTopDown2D)
            {
                if (!ProjectionMath.TryResolveBoxTopPoint(support.SourceCollider, frame, player2D.Foot,
                    PrototypePlayer3D.Radius + 0.08f, out foot)) return false;
            }
            else
            {
                if (!ProjectionMath.TryGetProjectedSurface(support.SourceCollider, frame, player2D.Foot.x,
                    out float surfaceHeight, out _)) return false;
                if (!ProjectionMath.TryResolveBoxTop(support.SourceCollider, frame,
                    new Vector2(player2D.Foot.x, surfaceHeight),
                    PrototypePlayer3D.Radius + 0.08f, lastAnchor, out foot)) return false;
            }
            // Shrink slightly and lift off the floor: touching the support is allowed.
            float radius = PrototypePlayer3D.Radius - 0.015f;
            Vector3 bottom = foot + Vector3.up * (PrototypePlayer3D.Radius + 0.025f);
            Vector3 top = foot + Vector3.up * (PrototypePlayer3D.Height - PrototypePlayer3D.Radius);
            return Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer,
                1 << World3DLayer, QueryTriggerInteraction.Ignore) == 0;
        }

        public bool TryToggleMode()
        {
            if (!initialized) return false;
            UpdateSupport();
            if (!IsGrounded || support == null)
            { Notify("발판에 착지한 상태에서 R을 눌러주세요."); return false; }
            if (!Is2D)
            {
                // R commits the direction currently visible, including during a drag.
                StopMouseOrbit();
                // Re-read the source geometry on entry as well as on view changes.
                RebuildProjection();
                Vector2 projectedFoot = frame.Project(player3D.Foot);
                if (IsTopDownView)
                {
                    Vector3 floorPoint = player3D.Foot;
                    floorPoint.y = support.SourceCollider.bounds.max.y;
                    projectedFoot = frame.Project(floorPoint);
                }
                else
                {
                    if (!ProjectionMath.TryGetProjectedSurface(support.SourceCollider, frame, projectedFoot.x,
                        out float surfaceHeight, out Vector2 surfaceNormal))
                    { Notify("이 각도에는 설 수 있는 투영 발판이 없습니다. 시점을 조금 바꿔주세요."); return false; }
                    projectedFoot.y = surfaceHeight + PrototypePlayer3D.Radius *
                        (1f / Mathf.Max(surfaceNormal.y, 0.1f) - 1f);
                }
                projectionRoot.gameObject.SetActive(true);
                player2D.gameObject.SetActive(true);
                player2D.SetSimulated(false);
                player2D.SetTopDown(IsTopDownView, ConstrainTopDownMove);
                sidePlayerVisual.SetActive(!IsTopDownView);
                topPlayerVisual.SetActive(IsTopDownView);
                player2D.Teleport(projectedFoot);
                if (player2D.HasBlockingOverlap())
                {
                    player2D.gameObject.SetActive(false);
                    projectionRoot.gameObject.SetActive(false);
                    Notify("투영된 지형이 몸과 겹칩니다. 위치나 시점을 바꿔주세요.");
                    return false;
                }
                lastAnchor = player3D.Foot;
                player3D.gameObject.SetActive(false);
                player2D.SetSimulated(true);
                Is2D = true;
                Notify(IsTopDown2D ? "탑뷰 2D · WASD로 바닥 안에서 자유롭게 이동하세요."
                    : "사이드뷰 2D · 화면에서 연결된 발판을 걸어서 건널 수 있습니다.");
            }
            else
            {
                if (!TryGetReturnFoot(out Vector3 foot))
                { Notify("이 위치에는 3D 복귀 공간이 없습니다. 발판 안쪽으로 이동하세요."); return false; }
                player2D.SetSimulated(false);
                player2D.gameObject.SetActive(false);
                projectionRoot.gameObject.SetActive(false);
                player3D.gameObject.SetActive(true);
                player3D.Teleport(foot);
                lastAnchor = foot;
                Is2D = false;
                Notify("3D 모드 · 이동한 발판의 실제 깊이로 돌아왔습니다.");
            }
            camera3D.enabled = !Is2D;
            camera2D.enabled = Is2D;
            return true;
        }

        public bool SetViewYaw(float yaw)
        {
            if (!initialized || float.IsNaN(yaw) || float.IsInfinity(yaw)) return false;
            if (Is2D || !IsGrounded)
            { Notify("시점은 3D에서 발판에 서 있을 때 바꿀 수 있습니다."); return false; }
            StopMouseOrbit();
            ApplyView(yaw);
            targetViewYaw = currentViewYaw;
            Notify("시점 " + Mathf.RoundToInt(YawDegrees) + "° · R로 2D 연결을 확인하세요.");
            return true;
        }

        private void ApplyView(float yaw, bool rebuildProjection = true)
        {
            currentViewYaw = yaw;
            frame = new ProjectionFrame(Vector3.zero, Mathf.Repeat(yaw, 360f), currentViewPitch);
            UpdateCameraPose();
            projectionDirty = true;
            if (rebuildProjection) RebuildProjection();
        }

        private void UpdateCameraPose()
        {
            // Orthographic projection keeps every depth aligned with the generated 2D world.
            // Dolly as zoom changes, with enough clearance for terrain near the focus.
            float distance = 25f + currentZoomSize;
            camera3D.transform.SetPositionAndRotation(CameraFocus - frame.Forward * distance,
                Quaternion.LookRotation(frame.Forward, frame.Up));
            camera3D.farClipPlane = Mathf.Max(100f, distance + 50f);
            Vector2 focus = frame.Project(CameraFocus);
            camera2D.transform.SetPositionAndRotation(new Vector3(focus.x, focus.y, -25f), Quaternion.identity);
            camera3D.orthographicSize = currentZoomSize;
            camera2D.orthographicSize = currentZoomSize;
        }

        private float ClampZoom(float size)
        {
            float minimum = Mathf.Max(0.1f, minZoomSize);
            return Mathf.Clamp(size, minimum, Mathf.Max(minimum, maxZoomSize));
        }

        private void OnValidate()
        {
            minZoomSize = Mathf.Max(0.1f, minZoomSize);
            maxZoomSize = Mathf.Max(minZoomSize, maxZoomSize);
            initialZoomSize = Mathf.Clamp(initialZoomSize, minZoomSize, maxZoomSize);
        }

        private void RebuildProjection()
        {
            projectionBuilder.Rebuild(terrain, frame, projectedMaterials, frictionless, World2DLayer, IsTopDownView);
            Vector2 exit = frame.Project(Goal);
            goal2D.localPosition = new Vector3(exit.x, exit.y + (IsTopDownView ? 0f : 0.60f),
                IsTopDownView ? -0.01f : -0.12f);
            goal2D.localScale = IsTopDownView ? new Vector3(0.65f, 0.65f, 0.01f)
                : new Vector3(0.22f, 1.15f, 0.08f);
            projectionDirty = false;
        }

        private ProjectableBox FindTopDownSupport(Vector2 point, out Vector3 worldFoot)
        {
            worldFoot = default;
            if (support != null && ProjectionMath.TryResolveBoxTopPoint(support.SourceCollider,
                frame, point, 0f, out worldFoot)) return support;
            ProjectableBox closest = null;
            float distance = float.PositiveInfinity;
            foreach (ProjectableBox box in terrain)
            {
                if (!ProjectionMath.TryResolveBoxTopPoint(box.SourceCollider, frame, point, 0f,
                    out Vector3 candidate)) continue;
                float candidateDistance = (candidate - lastAnchor).sqrMagnitude;
                if (closest != null && candidateDistance >= distance) continue;
                closest = box;
                worldFoot = candidate;
                distance = candidateDistance;
            }
            return closest;
        }

        private Vector2 ConstrainTopDownMove(Vector2 from, Vector2 desired)
        {
            Vector2 delta = desired - from;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.1f));
            Vector2 step = delta / steps;
            Vector2 position = from;
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = position + step;
                if (FindTopDownSupport(next, out _) != null) { position = next; continue; }
                // Slide along a floor boundary, while keeping every substep on the union of floor faces.
                next = position + new Vector2(step.x, 0f);
                if (FindTopDownSupport(next, out _) != null) position = next;
                next = position + new Vector2(0f, step.y);
                if (FindTopDownSupport(next, out _) != null) position = next;
            }
            return position;
        }

        public void ResetPrototype()
        {
            if (!initialized) return;
            if (Route != null) Route.ResetProgress();
            ResetPlayerAt(Spawn);
            Notify("시작 위치 · 마우스로 시점을 잡고 R로 차원 전환, SPACE로 점프하세요.");
        }

        private void ResetPlayerAt(Vector3 position)
        {
            Is2D = false;
            HasWon = false;
            moveInputOverride = null;
            jumpInputOverride = false;
            player2D.SetSimulated(false);
            player2D.SetTopDown(false);
            sidePlayerVisual.SetActive(true);
            topPlayerVisual.SetActive(false);
            player2D.gameObject.SetActive(false);
            projectionRoot.gameObject.SetActive(false);
            player3D.gameObject.SetActive(true);
            player3D.Teleport(position);
            lastAnchor = position;
            support = null;
            UpdateSupport();
            StopMouseOrbit();
            currentViewPitch = targetViewPitch = 0f;
            currentPanOffset = targetPanOffset = position - Spawn;
            currentZoomSize = targetZoomSize = ClampZoom(initialZoomSize);
            ApplyView(0f);
            targetViewYaw = currentViewYaw;
            camera3D.enabled = true;
            camera2D.enabled = false;
            returnMarker.gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            if (SpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(SpawnPoint.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            }
            if (CameraFocusPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(CameraFocusPoint.position, 0.2f);
            }
        }

        public void SetMoveInputOverride(Vector2? move) { moveInputOverride = move; }
        public void SetJumpInputOverride(bool jump) { jumpInputOverride = jump; }

        private void Notify(string text) { message = text; messageUntil = Time.unscaledTime + 2.8f; }

        private void OnDestroy()
        {
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
            if (frictionless != null) Destroy(frictionless);
        }
    }
}
