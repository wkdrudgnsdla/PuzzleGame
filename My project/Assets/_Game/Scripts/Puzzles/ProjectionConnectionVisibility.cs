using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Tests a real 3D connection before its flattened proxy can claim a visible join.</summary>
    public static class ProjectionConnectionVisibility
    {
        private const float SurfaceClearance = 0.012f;

        public static bool IsExposed(ProjectionFrame frame, Vector3 point, float probeRadius = 0.045f)
        {
            if (!IsFinite(point) || !IsFinite(frame.Forward) || frame.Forward.sqrMagnitude < 0.5f)
                return false;
            float radius = Mathf.Max(0f, probeRadius);
            if (!ClearRay(frame, point)) return false;
            if (radius <= 0f) return true;

            // A center pixel peeking around a block is not a readable connection face.
            return ClearRay(frame, point + frame.Right * radius)
                && ClearRay(frame, point - frame.Right * radius)
                && ClearRay(frame, point + frame.Up * radius)
                && ClearRay(frame, point - frame.Up * radius);
        }

        public static bool IsEndExposed(ProjectionFrame frame, Vector3 end, Vector3 inward,
            float probeLength = 0.22f, float probeRadius = 0.045f)
        {
            if (!IsExposed(frame, end, probeRadius)) return false;
            if (!IsFinite(inward) || inward.sqrMagnitude < 0.000001f) return false;
            Vector3 probe = inward.normalized * Mathf.Max(0f, probeLength);
            // Check the short visible section immediately behind a cut end as well.
            return IsExposed(frame, end + probe * 0.5f, 0f)
                && IsExposed(frame, end + probe, 0f);
        }

        public static bool IsJoinExposed(ProjectionFrame frame, Vector3 first, Vector3 second,
            Vector3 firstInward, Vector3 secondInward, float probeLength = 0.22f,
            float probeRadius = 0.045f)
        {
            return IsEndExposed(frame, first, firstInward, probeLength, probeRadius)
                && IsEndExposed(frame, second, secondInward, probeLength, probeRadius);
        }

        /// <summary>Authoring diagnostic matching IsExposed; returns the first actual solid in the view.</summary>
        public static Collider FindOccluder(ProjectionFrame frame, Vector3 point, float probeRadius = 0.045f)
        {
            if (!IsFinite(point) || !IsFinite(frame.Forward) || frame.Forward.sqrMagnitude < 0.5f)
                return null;
            Collider blocker = FindRayOccluder(frame, point);
            if (blocker != null || probeRadius <= 0f) return blocker;
            float radius = Mathf.Max(0f, probeRadius);
            blocker = FindRayOccluder(frame, point + frame.Right * radius);
            if (blocker != null) return blocker;
            blocker = FindRayOccluder(frame, point - frame.Right * radius);
            if (blocker != null) return blocker;
            blocker = FindRayOccluder(frame, point + frame.Up * radius);
            return blocker != null ? blocker : FindRayOccluder(frame, point - frame.Up * radius);
        }

        public static Collider FindJoinOccluder(ProjectionFrame frame, Vector3 first, Vector3 second,
            Vector3 firstInward, Vector3 secondInward, float probeLength = 0.22f,
            float probeRadius = 0.045f)
        {
            Collider blocker = FindEndOccluder(frame, first, firstInward, probeLength, probeRadius);
            return blocker != null ? blocker
                : FindEndOccluder(frame, second, secondInward, probeLength, probeRadius);
        }

        private static Collider FindEndOccluder(ProjectionFrame frame, Vector3 end, Vector3 inward,
            float probeLength, float probeRadius)
        {
            Collider blocker = FindOccluder(frame, end, probeRadius);
            if (blocker != null || !IsFinite(inward) || inward.sqrMagnitude < 0.000001f) return blocker;
            Vector3 probe = inward.normalized * Mathf.Max(0f, probeLength);
            blocker = FindOccluder(frame, end + probe * 0.5f, 0f);
            return blocker != null ? blocker : FindOccluder(frame, end + probe, 0f);
        }

        private static Collider FindRayOccluder(ProjectionFrame frame, Vector3 point)
        {
            int mask = 1 << ProjectionPrototype.World3DLayer;
            // Diagnostics run while authoring, so retaining every overlap is preferable
            // to a fixed-size buffer that could omit the platform requiring repair.
            Collider[] overlaps = Physics.OverlapSphere(point, SurfaceClearance, mask, QueryTriggerInteraction.Ignore);
            if (overlaps.Length > 0) return overlaps[0];
            return Physics.Raycast(point - frame.Forward * SurfaceClearance, -frame.Forward,
                out RaycastHit hit, float.PositiveInfinity, mask, QueryTriggerInteraction.Ignore)
                ? hit.collider : null;
        }

        private static bool ClearRay(ProjectionFrame frame, Vector3 point)
        {
            int mask = 1 << ProjectionPrototype.World3DLayer;
            // Raycasts starting inside a collider can miss it, so reject embedded ends first.
            if (Physics.CheckSphere(point, SurfaceClearance, mask, QueryTriggerInteraction.Ignore)) return false;

            // The shared orthographic camera is placed in front of the complete terrain bounds.
            // Walking toward its side of the view catches every intervening original block and
            // avoids Camera.main (the 3D camera is disabled while playing in 2D).
            return !Physics.Raycast(point - frame.Forward * SurfaceClearance, -frame.Forward,
                float.PositiveInfinity, mask, QueryTriggerInteraction.Ignore);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
