using System.Collections.Generic;
using UnityEngine;

namespace PuzzleGame
{
    public sealed class ProjectionWorldBuilder : MonoBehaviour
    {
        private readonly List<GameObject> proxies = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        public void Rebuild(IReadOnlyList<ProjectableBox> terrain, ProjectionFrame frame,
            Material[] materials, PhysicsMaterial2D physicsMaterial, int layer, bool topDown = false)
        {
            Clear();
            for (int index = 0; index < terrain.Count; index++)
            {
                ProjectableBox source = terrain[index];
                Vector2[] hull = topDown ? ProjectionMath.ProjectBoxTop(source.SourceCollider, frame)
                    : ProjectionMath.ProjectBox(source.SourceCollider, frame);
                if (hull.Length < 3) continue;

                var proxy = new GameObject("Projection - Platform " + source.Id);
                proxy.transform.SetParent(transform, false);
                proxy.layer = layer;
                var collider = proxy.AddComponent<PolygonCollider2D>();
                collider.SetPath(0, hull);
                collider.sharedMaterial = physicsMaterial;
                collider.isTrigger = topDown;
                source.ProjectedCollider = collider;

                var vertices = new Vector3[hull.Length];
                var triangles = new int[(hull.Length - 2) * 3];
                for (int i = 0; i < hull.Length; i++) vertices[i] = hull[i];
                for (int i = 0; i < hull.Length - 2; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 2;
                    triangles[i * 3 + 2] = i + 1;
                }
                var mesh = new Mesh { name = "Projected platform " + source.Id };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                proxy.AddComponent<MeshRenderer>().sharedMaterial = materials[index];
                proxies.Add(proxy);
                meshes.Add(mesh);
            }
            Physics2D.SyncTransforms();
        }

        private void Clear()
        {
            foreach (GameObject proxy in proxies)
            {
                // Disable immediately: Destroy is deferred until the end of the frame.
                if (proxy != null) { proxy.SetActive(false); Destroy(proxy); }
            }
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            proxies.Clear();
            meshes.Clear();
        }

        private void OnDestroy() { Clear(); }
    }
}
