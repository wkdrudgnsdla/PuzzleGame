using UnityEngine;

namespace PuzzleGame
{
    public static class ProjectionPuzzleMath
    {
        // Tolerance is in projected world units, so zooming never changes a puzzle's difficulty.
        public static bool AreConnected(ProjectionFrame frame, Vector3 a, Vector3 b,
            Vector3 tangentA, Vector3 tangentB, float distanceTolerance, float angleTolerance)
        {
            if ((frame.Project(a) - frame.Project(b)).sqrMagnitude >
                Mathf.Max(0f, distanceTolerance) * Mathf.Max(0f, distanceTolerance)) return false;
            Vector2 directionA = new Vector2(Vector3.Dot(tangentA, frame.Right), Vector3.Dot(tangentA, frame.Up));
            Vector2 directionB = new Vector2(Vector3.Dot(tangentB, frame.Right), Vector3.Dot(tangentB, frame.Up));
            // Looking down the length of a rail/wire must not turn its collapsed silhouette into a link.
            if (directionA.sqrMagnitude < 0.0025f || directionB.sqrMagnitude < 0.0025f) return false;
            float cosine = Mathf.Abs(Vector2.Dot(directionA.normalized, directionB.normalized));
            return cosine >= Mathf.Cos(Mathf.Clamp(angleTolerance, 0f, 89f) * Mathf.Deg2Rad);
        }
    }
}
