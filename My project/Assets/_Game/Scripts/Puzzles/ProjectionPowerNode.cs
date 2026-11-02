using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PuzzleGame
{
    /// <summary>A world-mounted battery whose received charge survives mode and camera changes.</summary>
    public sealed class ProjectionPowerNode : MonoBehaviour
    {
        public string Label = "A";
        public bool StartsPowered;
        public bool IsFinal;
        [Min(0.1f)] public float Radius = 0.3f;
        public UnityEvent OnPowered = new UnityEvent();

        private static readonly Color PoweredColor = new Color(0.04f, 0.68f, 1f);
        private static readonly Color EmptyColor = new Color(1f, 0.83f, 0.09f);
        private static readonly Color HousingColor = new Color(0.12f, 0.15f, 0.19f);
        private readonly List<Part> parts = new List<Part>();
        private Transform worldVisual, projectedVisual;
        private Material coreMaterial, housingMaterial, metalMaterial;
        private Material projectedCoreMaterial, projectedHousingMaterial, projectedMetalMaterial;
        private Light bulbLight;
        private bool visualsReady;
        private Vector3 lastRight, lastUp;
        private Matrix4x4 lastWorldMatrix;
        private bool projectionCurrent;
        private int projectionRequestFrame = -1;
        private bool projectionRequested;

        public bool IsPowered { get; private set; }

        public void Activate()
        {
            if (IsPowered) return;
            IsPowered = true;
            RefreshColors();
            OnPowered.Invoke();
        }

        public void ResetNode()
        {
            IsPowered = StartsPowered;
            projectionRequestFrame = -1;
            projectionRequested = false;
            RefreshColors();
        }

        public void InitializeVisuals()
        {
            if (visualsReady) return;
            visualsReady = true;
            coreMaterial = NewMaterial("Battery colored core", EmptyColor, false);
            housingMaterial = NewMaterial("Battery graphite housing", HousingColor, false);
            metalMaterial = NewMaterial("Battery terminals", new Color(0.5f, 0.56f, 0.62f), false);
            projectedCoreMaterial = NewMaterial("Projected battery core", EmptyColor, true);
            projectedHousingMaterial = NewMaterial("Projected battery housing", HousingColor, true);
            projectedMetalMaterial = NewMaterial("Projected battery terminals", new Color(0.5f, 0.56f, 0.62f), true);
            worldVisual = NewRoot("Mounted power unit 3D", ProjectionPrototype.World3DLayer);
            projectedVisual = NewRoot("Projected mounted power unit", ProjectionPrototype.World2DLayer);

            float r = Radius;
            AddPart("Mounting plinth", PrimitiveType.Cube, new Vector3(0f, -r * 1.4f, 0.02f),
                new Vector3(r * 3.15f, r * 0.53f, r * 2.3f), housingMaterial, projectedHousingMaterial);
            if (IsFinal)
            {
                AddPart("Bulb socket", PrimitiveType.Cylinder, new Vector3(0f, -r * 0.76f, 0f),
                    new Vector3(r * 1.15f, r * 0.48f, r * 1.15f), metalMaterial, projectedMetalMaterial);
                AddPart("Lamp bulb", PrimitiveType.Sphere, new Vector3(0f, r * 0.4f, 0f),
                    Vector3.one * r * 2f, coreMaterial, projectedCoreMaterial);
                var lightObject = new GameObject("Powered bulb light", typeof(Light));
                lightObject.transform.SetParent(worldVisual, false);
                lightObject.transform.localPosition = Vector3.up * r * 0.4f;
                bulbLight = lightObject.GetComponent<Light>();
                bulbLight.type = LightType.Point;
                bulbLight.color = PoweredColor;
                bulbLight.intensity = 1.4f;
                bulbLight.range = 2.4f;
                bulbLight.shadows = LightShadows.None;
                bulbLight.cullingMask = 1 << ProjectionPrototype.World3DLayer;
            }
            else
            {
                AddPart("Battery housing", PrimitiveType.Cube, new Vector3(0f, 0f, 0.035f),
                    new Vector3(r * 2.6f, r * 2.4f, r * 1.8f), housingMaterial, projectedHousingMaterial);
                AddPart("Charge window", PrimitiveType.Cube, new Vector3(0f, r * 0.15f, -r * 0.84f),
                    new Vector3(r * 1.85f, r * 1.6f, r * 0.22f), coreMaterial, projectedCoreMaterial);
                AddPart("Positive terminal", PrimitiveType.Cube, new Vector3(0f, r * 1.43f, 0.02f),
                    new Vector3(r * 0.75f, r * 0.53f, r * 0.85f), metalMaterial, projectedMetalMaterial);
                AddPart("Rear charge cell", PrimitiveType.Cube, new Vector3(0f, r * 0.15f, r * 1.04f),
                    new Vector3(r * 1.85f, r * 1.6f, r * 0.22f), coreMaterial, projectedCoreMaterial);
            }
            AddPart("Identity plate", PrimitiveType.Cube, new Vector3(0f, -r * 1.4f, -r * 1.18f),
                new Vector3(r * 2.5f, r * 0.43f, r * 0.08f), metalMaterial, projectedMetalMaterial);
            RefreshColors();
        }

        public void TickVisuals(ProjectionFrame frame, bool is2D, bool showProjection = true)
        {
            if (!visualsReady) return;
            if (projectionRequestFrame != Time.frameCount)
            {
                projectionRequestFrame = Time.frameCount;
                projectionRequested = false;
            }
            // A relay node can be the first network's target and the next one's supply.
            // Every owner contributes visibility; an inactive owner must not hide it.
            projectionRequested |= is2D && showProjection;
            worldVisual.gameObject.SetActive(!is2D);
            bool visibleProjection = is2D && projectionRequested;
            projectedVisual.gameObject.SetActive(visibleProjection);
            // The 3D unit inherits only its authored anchor; it never faces the camera.
            if (!visibleProjection) return;
            projectedVisual.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 scale = transform.lossyScale;
            projectedVisual.localScale = new Vector3(SafeInverse(scale.x), SafeInverse(scale.y), SafeInverse(scale.z));
            Matrix4x4 matrix = worldVisual.localToWorldMatrix;
            if (!projectionCurrent || lastRight != frame.Right || lastUp != frame.Up || matrix != lastWorldMatrix)
            {
                foreach (Part part in parts) ProjectPart(part, frame);
                lastRight = frame.Right;
                lastUp = frame.Up;
                lastWorldMatrix = matrix;
                projectionCurrent = true;
            }
        }

        private static float SafeInverse(float value) => Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;

        private Transform NewRoot(string rootName, int layer)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            root.layer = layer;
            return root.transform;
        }

        private void AddPart(string partName, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Material projectedMaterial)
        {
            GameObject physical = GameObject.CreatePrimitive(type);
            physical.name = partName;
            physical.layer = ProjectionPrototype.World3DLayer;
            physical.transform.SetParent(worldVisual, false);
            physical.transform.localPosition = position;
            physical.transform.localScale = scale;
            Collider collider = physical.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            physical.GetComponent<Renderer>().sharedMaterial = material;

            var projected = new GameObject(partName + " projection", typeof(MeshFilter), typeof(MeshRenderer));
            projected.layer = ProjectionPrototype.World2DLayer;
            projected.transform.SetParent(projectedVisual, false);
            Mesh source = physical.GetComponent<MeshFilter>().sharedMesh;
            var mesh = new Mesh { name = partName + " flattened mesh" };
            mesh.MarkDynamic();
            Vector3[] vertices = source.vertices;
            mesh.vertices = vertices;
            mesh.triangles = source.triangles;
            projected.GetComponent<MeshFilter>().sharedMesh = mesh;
            projected.GetComponent<MeshRenderer>().sharedMaterial = projectedMaterial;
            parts.Add(new Part { Source = physical.transform, LocalVertices = vertices,
                ProjectedVertices = new Vector3[vertices.Length], Mesh = mesh });
        }

        private static void ProjectPart(Part part, ProjectionFrame frame)
        {
            for (int i = 0; i < part.LocalVertices.Length; i++)
            {
                Vector3 world = part.Source.TransformPoint(part.LocalVertices[i]);
                Vector2 point = frame.Project(world);
                part.ProjectedVertices[i] = new Vector3(point.x, point.y, -0.5f + frame.Depth(world) * 0.001f);
            }
            part.Mesh.vertices = part.ProjectedVertices;
            part.Mesh.RecalculateBounds();
        }

        private void RefreshColors()
        {
            if (!visualsReady) return;
            Color color = IsPowered ? PoweredColor : EmptyColor;
            SetColor(coreMaterial, color);
            SetColor(projectedCoreMaterial, color);
            if (coreMaterial.HasProperty("_EmissionColor"))
            {
                coreMaterial.EnableKeyword("_EMISSION");
                coreMaterial.SetColor("_EmissionColor", IsPowered ? PoweredColor * 0.7f : EmptyColor * 0.12f);
            }
            if (bulbLight != null) bulbLight.enabled = IsPowered;
        }

        private static Material NewMaterial(string materialName, Color color, bool flat)
        {
            Shader shader = Shader.Find(flat ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = materialName };
            SetColor(material, color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        private void OnDrawGizmos()
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = HousingColor;
            Gizmos.DrawWireCube(new Vector3(0f, -Radius * 1.4f, 0.02f),
                new Vector3(Radius * 3.15f, Radius * 0.53f, Radius * 2.3f));
            Gizmos.color = Application.isPlaying ? (IsPowered ? PoweredColor : EmptyColor)
                : StartsPowered ? PoweredColor : EmptyColor;
            if (IsFinal) Gizmos.DrawWireSphere(Vector3.up * Radius * 0.4f, Radius);
            else Gizmos.DrawWireCube(Vector3.zero, new Vector3(Radius * 2.6f, Radius * 2.4f, Radius * 1.8f));
            Gizmos.matrix = previous;
        }

        private void OnDestroy()
        {
            foreach (Part part in parts) if (part.Mesh != null) Destroy(part.Mesh);
            foreach (Material material in new[] { coreMaterial, housingMaterial, metalMaterial,
                projectedCoreMaterial, projectedHousingMaterial, projectedMetalMaterial })
                if (material != null) Destroy(material);
        }

        private sealed class Part
        {
            public Transform Source;
            public Vector3[] LocalVertices, ProjectedVertices;
            public Mesh Mesh;
        }
    }
}
