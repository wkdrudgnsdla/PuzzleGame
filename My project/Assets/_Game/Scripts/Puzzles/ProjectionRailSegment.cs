using UnityEngine;

namespace PuzzleGame
{
    /// <summary>A directed, scene-authored section of a projection rail.</summary>
    public sealed class ProjectionRailSegment : MonoBehaviour
    {
        public Transform StartPoint;
        public Transform EndPoint;

        public bool IsValid => StartPoint != null && EndPoint != null &&
            (EndPoint.position - StartPoint.position).sqrMagnitude > 0.0001f;

        public Vector3 Start => StartPoint != null ? StartPoint.position : transform.position;
        public Vector3 End => EndPoint != null ? EndPoint.position : transform.position;
        public Vector3 Direction => End - Start;

        private void OnDrawGizmos()
        {
            if (!IsValid) return;
            Gizmos.color = new Color(0.65f, 0.8f, 0.9f);
            Gizmos.DrawLine(Start, End);
            Gizmos.DrawWireSphere(Start, 0.1f);
            Gizmos.DrawWireSphere(End, 0.1f);
        }
    }
}
