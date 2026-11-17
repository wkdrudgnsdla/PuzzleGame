using System.Collections.Generic;
using UnityEngine;

namespace PuzzleGame
{
    /// <summary>A directed cable split between two world depths and joined in projected space.</summary>
    public sealed class ProjectionWire : MonoBehaviour
    {
        public ProjectionPowerNode Source;
        public ProjectionPowerNode Target;
        [Tooltip("Ordered from the source node to this cable's first cut end.")]
        public Transform[] SourcePath = new Transform[0];
        [Tooltip("Ordered from the other cut end to the target node.")]
        public Transform[] TargetPath = new Transform[0];
        [Min(0.01f)] public float ConnectionTolerance = 0.4f;
        [Range(0f, 90f)] public float AngleTolerance = 15f;
        [Min(0.1f)] public float SecondsToCharge = 2.5f;
        [Min(0.02f)] public float Width = 0.12f;
        [Header("Supporting floors (placement metadata only)")]
        [Tooltip("Records mounting blocks for authoring. Never restricts visibility or remote charging.")]
        public ProjectableBox[] OperatingPlatforms = new ProjectableBox[0];

        private static readonly Color OffColor = new Color(0.12f, 0.15f, 0.19f);
        private static readonly Color ConnectedColor = new Color(0.34f, 0.43f, 0.5f);
        private static readonly Color EnergyColor = new Color(0.03f, 0.72f, 1f);
        private static readonly Color CutPlugColor = new Color(0.5f, 0.36f, 0.2f);
        private readonly List<Vector3> projectedPoints = new List<Vector3>();
        private readonly List<Vector3> worldPoints = new List<Vector3>();
        private readonly List<Vector3> prefix = new List<Vector3>();
        private Transform worldRoot, projectedRoot;
        private readonly List<Transform> worldSource = new List<Transform>();
        private readonly List<Transform> worldTarget = new List<Transform>();
        private readonly List<Transform> worldSourceEnergy = new List<Transform>();
        private readonly List<Transform> worldTargetEnergy = new List<Transform>();
        private Transform firstCutPlug, secondCutPlug;
        private LineRenderer projectedCable, projectedSource, projectedTarget, projectedEnergy;
        private LineRenderer projectedSourceEnergy, projectedTargetEnergy;
        private Material cableMaterial, energyMaterial, worldCableMaterial, connectorMaterial;
        private bool visualsReady;
        private bool sourceEndExposed, targetEndExposed;
        private bool readinessKnown, wasReadyToCharge, chargingSound;

        public float FillProgress { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsCharging { get; private set; }
        public bool ConnectionLatched { get; private set; }
        public bool IsValid => Source != null && Target != null && Source != Target &&
            HasPoints(SourcePath) && HasPoints(TargetPath);

        public bool CanConnect(ProjectionFrame frame)
        {
            if (!IsValid)
            {
                sourceEndExposed = targetEndExposed = false;
                return false;
            }
            Vector3 firstEnd = SourcePath[SourcePath.Length - 1].position;
            Vector3 secondStart = TargetPath[0].position;
            Vector3 firstTangent = firstEnd - SourcePath[SourcePath.Length - 2].position;
            Vector3 secondTangent = TargetPath[1].position - secondStart;
            sourceEndExposed = ProjectionConnectionVisibility.IsEndExposed(frame, firstEnd, -firstTangent);
            targetEndExposed = ProjectionConnectionVisibility.IsEndExposed(frame, secondStart, secondTangent);
            return sourceEndExposed && targetEndExposed &&
                ProjectionPuzzleMath.AreConnected(frame, firstEnd, secondStart, firstTangent,
                secondTangent, ConnectionTolerance, AngleTolerance);
        }

        public void InitializeVisuals()
        {
            if (visualsReady) return;
            visualsReady = true;
            cableMaterial = NewMaterial("Empty wire", OffColor);
            energyMaterial = NewMaterial("Wire energy", EnergyColor);
            worldCableMaterial = NewMaterial("Physical cable insulation", OffColor, false);
            connectorMaterial = NewMaterial("Cable cut plugs", CutPlugColor, false);
            worldRoot = NewRoot("Wire 3D", ProjectionPrototype.World3DLayer);
            projectedRoot = NewRoot("Wire 2D", ProjectionPrototype.World2DLayer);
            firstCutPlug = NewTubePrimitive("Source cut connector", PrimitiveType.Sphere, connectorMaterial);
            secondCutPlug = NewTubePrimitive("Target cut connector", PrimitiveType.Sphere, connectorMaterial);
            projectedCable = NewLine("Connected path", projectedRoot, cableMaterial, Width);
            projectedSource = NewLine("Source half", projectedRoot, cableMaterial, Width);
            projectedTarget = NewLine("Target half", projectedRoot, cableMaterial, Width);
            projectedEnergy = NewLine("Energy front", projectedRoot, energyMaterial, Width * 0.7f);
            projectedSourceEnergy = NewLine("Stored source energy", projectedRoot, energyMaterial, Width * 0.7f);
            projectedTargetEnergy = NewLine("Stored target energy", projectedRoot, energyMaterial, Width * 0.7f);
        }

        public void ResetWire()
        {
            StopAudio();
            FillProgress = 0f;
            IsConnected = false;
            IsCharging = false;
            ConnectionLatched = false;
            sourceEndExposed = targetEndExposed = false;
            if (!visualsReady) return;
            SetColor(worldCableMaterial, OffColor);
            SetColor(connectorMaterial, CutPlugColor);
        }

        public void Tick(ProjectionFrame frame, bool is2D, float deltaTime, bool canCharge = true,
            bool showProjection = true)
        {
            IsConnected = CanConnect(frame);
            bool readyToCharge = canCharge && IsConnected && Source.IsPowered && !Target.IsPowered;
            if (readinessKnown && !is2D && readyToCharge && !wasReadyToCharge && isActiveAndEnabled)
                GameAudioEvents.Play(SoundCue.WireAligned);
            readinessKnown = isActiveAndEnabled;
            wasReadyToCharge = readyToCharge;
            IsCharging = is2D && readyToCharge;
            if (IsCharging)
            {
                // Only completed batteries latch. An unfinished front waits while the view changes.
                FillProgress = Mathf.MoveTowards(FillProgress, 1f, Mathf.Max(0f, deltaTime) /
                    Mathf.Max(0.1f, SecondsToCharge));
                if (FillProgress >= 1f)
                {
                    Target.Activate();
                    IsCharging = false;
                    SetChargingSound(false);
                    if (isActiveAndEnabled) GameAudioEvents.Play(SoundCue.WireCharged);
                }
            }
            SetChargingSound(IsCharging && isActiveAndEnabled);
            if (Source != null && Target != null && Source.IsPowered && Target.IsPowered)
                FillProgress = 1f;
            // Remember a connection the player has actually used. Moving to the
            // next operating floor must not erase the already discovered circuit.
            if (IsValid && ((is2D && showProjection && IsConnected) || IsCharging ||
                FillProgress > 0f || Target.IsPowered)) ConnectionLatched = true;
            RefreshVisuals(frame, is2D, showProjection || ConnectionLatched, !is2D && readyToCharge);
        }

        private void SetChargingSound(bool charging)
        {
            if (chargingSound == charging) return;
            chargingSound = charging;
            if (charging) GameAudioEvents.Play(SoundCue.WireChargeStart);
            GameAudioEvents.SetLoop(SoundCue.WireChargeLoop, this, charging);
        }

        internal void StopAudio()
        {
            readinessKnown = false;
            SetChargingSound(false);
        }

        private void OnDisable() => StopAudio();

        private void RefreshVisuals(ProjectionFrame frame, bool is2D, bool showProjection, bool showReady)
        {
            if (!visualsReady) return;
            worldRoot.gameObject.SetActive(!is2D && IsValid);
            projectedRoot.gameObject.SetActive(is2D && showProjection && IsValid);
            if (!IsValid) return;
            Color cableColor = IsConnected ? ConnectedColor : OffColor;
            SetColor(cableMaterial, cableColor);
            Color readyColor = ProjectionPuzzleMechanic.AlignmentReadyColor;
            SetColor(worldCableMaterial, showReady ? readyColor : OffColor);
            SetColor(connectorMaterial, showReady ? readyColor : CutPlugColor);
            // Physical cables stay at their authored world coordinates. Their shape and
            // blue conductor never shift toward or turn to face the current camera.
            BuildPath(SourcePath, false, frame, 0f, worldPoints);
            SetTubes(worldSource, worldPoints, Width, worldCableMaterial, Vector3.zero, "Source cable");
            BuildPath(TargetPath, false, frame, 0f, worldPoints);
            SetTubes(worldTarget, worldPoints, Width, worldCableMaterial, Vector3.zero, "Target cable");
            firstCutPlug.position = SourcePath[SourcePath.Length - 1].position;
            secondCutPlug.position = TargetPath[0].position;
            firstCutPlug.localScale = secondCutPlug.localScale = Vector3.one * Width * 1.55f;

            BuildCombined(frame, true, projectedPoints);
            SetLine(projectedCable, projectedPoints);
            projectedCable.enabled = IsConnected;
            projectedSource.enabled = !IsConnected;
            projectedTarget.enabled = !IsConnected;
            SetPath(projectedSource, SourcePath, true, frame, -0.36f);
            SetPath(projectedTarget, TargetPath, true, frame, -0.36f);
            BuildPrefix(projectedPoints, FillProgress, prefix);
            for (int i = 0; i < prefix.Count; i++) prefix[i] += Vector3.back * 0.04f;
            SetLine(projectedEnergy, prefix);
            projectedEnergy.enabled = IsConnected && prefix.Count >= 2 && Source.IsPowered;

            // Keep the ready preview uniformly green. The projected charge front still uses its stored progress.
            float firstLength = PathLength(SourcePath);
            float secondLength = PathLength(TargetPath);
            float filledLength = (firstLength + secondLength) * FillProgress;
            BuildPath(SourcePath, false, frame, 0f, worldPoints);
            BuildPrefix(worldPoints, firstLength > 0f ? Mathf.Clamp01(filledLength / firstLength) : 0f, prefix);
            if (showReady || !Source.IsPowered) prefix.Clear();
            SetTubes(worldSourceEnergy, prefix, Width * 0.65f, energyMaterial,
                Vector3.up * Width * 0.7f, "Charged source conductor");
            BuildPath(SourcePath, true, frame, -0.4f, worldPoints);
            BuildPrefix(worldPoints, firstLength > 0f ? Mathf.Clamp01(filledLength / firstLength) : 0f, prefix);
            SetLine(projectedSourceEnergy, prefix);
            projectedSourceEnergy.enabled = !IsConnected && Source.IsPowered && prefix.Count >= 2;
            BuildPath(TargetPath, false, frame, 0f, worldPoints);
            BuildPrefix(worldPoints, secondLength > 0f
                ? Mathf.Clamp01((filledLength - firstLength) / secondLength) : 0f, prefix);
            if (showReady || !Source.IsPowered) prefix.Clear();
            SetTubes(worldTargetEnergy, prefix, Width * 0.65f, energyMaterial,
                Vector3.up * Width * 0.7f, "Charged target conductor");
            BuildPath(TargetPath, true, frame, -0.4f, worldPoints);
            BuildPrefix(worldPoints, secondLength > 0f
                ? Mathf.Clamp01((filledLength - firstLength) / secondLength) : 0f, prefix);
            SetLine(projectedTargetEnergy, prefix);
            projectedTargetEnergy.enabled = !IsConnected && Source.IsPowered && prefix.Count >= 2;
        }

        private void BuildCombined(ProjectionFrame frame, bool projected, List<Vector3> result)
        {
            result.Clear();
            AppendPath(SourcePath, projected, frame, -0.36f, result);
            AppendPath(TargetPath, projected, frame, -0.36f, result);
        }

        private void SetPath(LineRenderer line, Transform[] path, bool projected, ProjectionFrame frame, float z)
        {
            BuildPath(path, projected, frame, z, worldPoints);
            SetLine(line, worldPoints);
        }

        private static void BuildPath(Transform[] path, bool projected, ProjectionFrame frame,
            float z, List<Vector3> result)
        {
            result.Clear();
            AppendPath(path, projected, frame, z, result);
        }

        private static void AppendPath(Transform[] path, bool projected, ProjectionFrame frame,
            float z, List<Vector3> result)
        {
            foreach (Transform point in path)
            {
                Vector3 position = point.position;
                if (projected)
                {
                    Vector2 p = frame.Project(position);
                    position = new Vector3(p.x, p.y, z);
                }
                result.Add(position);
            }
        }

        private static void BuildPrefix(List<Vector3> path, float fraction, List<Vector3> result)
        {
            result.Clear();
            if (path.Count < 2 || fraction <= 0f) return;
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float remaining = total * Mathf.Clamp01(fraction);
            result.Add(path[0]);
            for (int i = 1; i < path.Count; i++)
            {
                float length = Vector3.Distance(path[i - 1], path[i]);
                if (length <= remaining)
                {
                    result.Add(path[i]);
                    remaining -= length;
                    continue;
                }
                if (length > 0f) result.Add(Vector3.Lerp(path[i - 1], path[i], remaining / length));
                break;
            }
        }

        private static float PathLength(Transform[] path)
        {
            float result = 0f;
            for (int i = 1; i < path.Length; i++) result += Vector3.Distance(path[i - 1].position, path[i].position);
            return result;
        }

        private static bool HasPoints(Transform[] path)
        {
            if (path == null || path.Length < 2) return false;
            foreach (Transform point in path) if (point == null) return false;
            return true;
        }

        private Transform NewRoot(string rootName, int layer)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            root.layer = layer;
            return root.transform;
        }

        private static LineRenderer NewLine(string lineName, Transform parent, Material material, float width)
        {
            var lineObject = new GameObject(lineName, typeof(LineRenderer));
            lineObject.layer = parent.gameObject.layer;
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = material;
            line.positionCount = 0;
            return line;
        }

        private static void SetLine(LineRenderer line, List<Vector3> points)
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        }

        private void SetTubes(List<Transform> tubes, List<Vector3> path, float diameter,
            Material material, Vector3 mountOffset, string partName)
        {
            int count = Mathf.Max(0, path.Count - 1);
            while (tubes.Count < count) tubes.Add(NewTubePrimitive(partName, PrimitiveType.Cylinder, material));
            for (int i = 0; i < tubes.Count; i++)
            {
                Transform tube = tubes[i];
                bool active = i < count;
                Vector3 delta = active ? path[i + 1] - path[i] : Vector3.zero;
                active &= delta.sqrMagnitude > 0.000001f;
                tube.gameObject.SetActive(active);
                if (!active) continue;
                tube.SetPositionAndRotation((path[i] + path[i + 1]) * 0.5f + mountOffset,
                    Quaternion.FromToRotation(Vector3.up, delta.normalized));
                // Unity's cylinder primitive is two units long and one unit in diameter.
                tube.localScale = new Vector3(diameter, delta.magnitude * 0.5f, diameter);
            }
        }

        private Transform NewTubePrimitive(string partName, PrimitiveType type, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.layer = ProjectionPrototype.World3DLayer;
            part.transform.SetParent(worldRoot, false);
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material NewMaterial(string name, Color color, bool flat = true)
        {
            Shader shader = Shader.Find(flat ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name };
            SetColor(material, color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = OffColor;
            DrawPath(SourcePath);
            DrawPath(TargetPath);
            if (HasPoints(SourcePath) && HasPoints(TargetPath))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(SourcePath[SourcePath.Length - 1].position, ConnectionTolerance);
                Gizmos.DrawWireSphere(TargetPath[0].position, ConnectionTolerance);
            }
        }

        private static void DrawPath(Transform[] path)
        {
            if (path == null) return;
            for (int i = 1; i < path.Length; i++)
                if (path[i - 1] != null && path[i] != null) Gizmos.DrawLine(path[i - 1].position, path[i].position);
        }

        private void OnDestroy()
        {
            if (cableMaterial != null) Destroy(cableMaterial);
            if (energyMaterial != null) Destroy(energyMaterial);
            if (worldCableMaterial != null) Destroy(worldCableMaterial);
            if (connectorMaterial != null) Destroy(connectorMaterial);
        }
    }
}
