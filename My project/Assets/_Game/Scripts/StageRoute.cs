using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Ordered, scene-authored landing points for the challenge stages.</summary>
    public sealed class StageRoute : MonoBehaviour
    {
        public ProjectionPrototype Prototype;
        public string StageTitle;
        public Transform[] Checkpoints = new Transform[0];
        [Min(0.1f)] public float ActivationRadius = 1.15f;
        [Min(0.01f)] public float HeightTolerance = 0.2f;
        public Color PendingColor = new Color(0.25f, 0.42f, 0.52f);
        public Color CurrentColor = new Color(1f, 0.72f, 0.2f);
        public Color CompletedColor = new Color(0.3f, 0.9f, 0.6f);
        private int completed;
        private MaterialPropertyBlock colors;

        public int CompletedCount => completed;
        public bool IsComplete => completed >= Checkpoints.Length;
        public string ProgressHint => IsComplete ? "출구 활성화" : "체크포인트 " + completed + "/" + Checkpoints.Length;
        public Vector3 RespawnPosition => completed > 0 && Checkpoints[completed - 1] != null
            ? Checkpoints[completed - 1].position + Vector3.up * 0.02f
            : Prototype != null && Prototype.SpawnPoint != null ? Prototype.SpawnPoint.position : Vector3.zero;

        public void Initialize(ProjectionPrototype owner)
        {
            Prototype = owner;
            ResetProgress();
        }

        public void ResetProgress()
        {
            completed = 0;
            RefreshMarkers();
        }

        private void Update()
        {
            if (Prototype == null || IsComplete || Prototype.Is2D || !Prototype.IsGrounded) return;
            Transform point = Checkpoints[completed];
            if (point == null) return;
            Vector3 delta = Prototype.PlayerWorldFoot - point.position;
            if (Mathf.Abs(delta.y) > HeightTolerance ||
                delta.x * delta.x + delta.z * delta.z > ActivationRadius * ActivationRadius) return;
            completed++;
            RefreshMarkers();
        }

        private void RefreshMarkers()
        {
            if (colors == null) colors = new MaterialPropertyBlock();
            for (int i = 0; i < Checkpoints.Length; i++)
            {
                if (Checkpoints[i] == null) continue;
                Color color = i < completed ? CompletedColor : i == completed ? CurrentColor : PendingColor;
                Paint(Checkpoints[i], color);
            }
            if (Prototype != null && Prototype.ExitPoint != null)
                Paint(Prototype.ExitPoint, IsComplete ? CompletedColor : new Color(0.35f, 0.4f, 0.44f));
        }

        private void Paint(Transform target, Color color)
        {
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(colors);
                colors.SetColor("_BaseColor", color);
                colors.SetColor("_Color", color);
                renderer.SetPropertyBlock(colors);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Prototype == null || Prototype.SpawnPoint == null) return;
            Gizmos.color = new Color(1f, 0.75f, 0.25f);
            Vector3 previous = Prototype.SpawnPoint.position;
            foreach (Transform point in Checkpoints)
            {
                if (point == null) continue;
                Gizmos.DrawLine(previous, point.position);
                Gizmos.DrawWireSphere(point.position + Vector3.up * 0.1f, ActivationRadius);
                previous = point.position;
            }
            if (Prototype.ExitPoint != null) Gizmos.DrawLine(previous, Prototype.ExitPoint.position);
        }
    }
}
