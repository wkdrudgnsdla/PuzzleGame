using System;
using UnityEngine;

namespace PuzzleGame
{
    public static class ProjectionMath
    {
        private const float Epsilon = 0.00001f;
        private const float TopHeightTolerance = 0.08f;

        /// <summary>
        /// Projects the actual collider corners, including its center and transform.
        /// Returns a counter-clockwise convex hull; degenerate boxes can return fewer than three points.
        /// </summary>
        public static Vector2[] ProjectBox(BoxCollider box, ProjectionFrame frame)
        {
            if (box == null)
                return Array.Empty<Vector2>();

            Vector3[] corners = GetWorldCorners(box);
            var points = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                points[i] = frame.Project(corners[i]);

            Array.Sort(points, ComparePoints);
            int uniqueCount = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (uniqueCount == 0 || (points[i] - points[uniqueCount - 1]).sqrMagnitude > Epsilon * Epsilon)
                    points[uniqueCount++] = points[i];
            }

            if (uniqueCount <= 2)
            {
                Array.Resize(ref points, uniqueCount);
                return points;
            }

            var hull = new Vector2[uniqueCount * 2];
            int count = 0;
            for (int i = 0; i < uniqueCount; i++)
            {
                while (count >= 2 && Cross(hull[count - 1] - hull[count - 2], points[i] - hull[count - 1]) <= Epsilon)
                    count--;
                hull[count++] = points[i];
            }

            int upperStart = count + 1;
            for (int i = uniqueCount - 2; i >= 0; i--)
            {
                while (count >= upperStart && Cross(hull[count - 1] - hull[count - 2], points[i] - hull[count - 1]) <= Epsilon)
                    count--;
                hull[count++] = points[i];
            }

            // The last point repeats the first one; Collider2D closes the path itself.
            Array.Resize(ref hull, count - 1);
            return hull;
        }

        /// <summary>Projects only the real top face, returning a counter-clockwise floor polygon.</summary>
        public static Vector2[] ProjectBoxTop(BoxCollider box, ProjectionFrame frame)
        {
            if (box == null || !IsAxisAlignedUpright(box.transform))
                return Array.Empty<Vector2>();

            Vector3[] corners = GetWorldCorners(box);
            // A negative Y scale swaps which local face is the actual world top.
            int topBit = box.transform.TransformVector(Vector3.up).y >= 0f ? 2 : 0;
            var polygon = new[]
            {
                frame.Project(corners[topBit]),
                frame.Project(corners[topBit | 1]),
                frame.Project(corners[topBit | 5]),
                frame.Project(corners[topBit | 4])
            };

            float twiceArea = 0f;
            for (int i = 0; i < polygon.Length; i++)
                twiceArea += Cross(polygon[i], polygon[(i + 1) % polygon.Length]);
            if (Mathf.Abs(twiceArea) < Epsilon)
                return Array.Empty<Vector2>();
            if (twiceArea < 0f)
                Array.Reverse(polygon);
            return polygon;
        }

        /// <summary>
        /// Intersects a screen point with the actual top plane for top-down floor movement.
        /// Rejects points outside the inset footprint instead of clamping or moving them elsewhere.
        /// </summary>
        public static bool TryResolveBoxTopPoint(
            BoxCollider box,
            ProjectionFrame frame,
            Vector2 projectedPoint,
            float inset,
            out Vector3 worldFoot)
        {
            worldFoot = default;
            if (box == null || !IsFinite(inset) || inset < 0f ||
                !IsFinite(projectedPoint.x) || !IsFinite(projectedPoint.y) ||
                !IsAxisAlignedUpright(box.transform) || Mathf.Abs(frame.Forward.y) < Epsilon)
                return false;

            Vector3[] corners = GetWorldCorners(box);
            Vector3 minimum = corners[0];
            Vector3 maximum = corners[0];
            for (int i = 1; i < corners.Length; i++)
            {
                minimum = Vector3.Min(minimum, corners[i]);
                maximum = Vector3.Max(maximum, corners[i]);
            }

            minimum.x += inset;
            minimum.z += inset;
            maximum.x -= inset;
            maximum.z -= inset;
            if (minimum.x > maximum.x || minimum.z > maximum.z || maximum.y - minimum.y <= Epsilon)
                return false;

            Vector3 rayOrigin = frame.Unproject(projectedPoint, 0f);
            float depth = (maximum.y - rayOrigin.y) / frame.Forward.y;
            Vector3 candidate = rayOrigin + depth * frame.Forward;
            if (!IsFinite(depth) || !IsFinite(candidate.x) || !IsFinite(candidate.y) || !IsFinite(candidate.z) ||
                candidate.x < minimum.x - Epsilon || candidate.x > maximum.x + Epsilon ||
                candidate.z < minimum.z - Epsilon || candidate.z > maximum.z + Epsilon)
                return false;

            worldFoot = candidate;
            return true;
        }

        /// <summary>Finds the highest upper-hull edge at a screen horizontal coordinate.</summary>
        public static bool TryGetProjectedSurface(
            BoxCollider box,
            ProjectionFrame frame,
            float horizontal,
            out float height,
            out Vector2 normal)
        {
            height = 0f;
            normal = Vector2.up;
            if (!IsFinite(horizontal))
                return false;

            Vector2[] hull = ProjectBox(box, frame);
            bool found = false;
            for (int i = 0; i < hull.Length; i++)
            {
                Vector2 start = hull[i];
                Vector2 end = hull[(i + 1) % hull.Length];
                Vector2 edge = end - start;
                if (Mathf.Abs(edge.x) < Epsilon || edge.sqrMagnitude < Epsilon * Epsilon)
                    continue;

                // The hull is CCW, so the right-hand normal points out of the solid.
                Vector2 outward = new Vector2(edge.y, -edge.x).normalized;
                if (outward.y <= Epsilon || horizontal < Mathf.Min(start.x, end.x) - Epsilon ||
                    horizontal > Mathf.Max(start.x, end.x) + Epsilon)
                    continue;

                float fraction = Mathf.Clamp01((horizontal - start.x) / edge.x);
                float candidateHeight = Mathf.Lerp(start.y, end.y, fraction);
                if (!found || candidateHeight > height + Epsilon ||
                    (Mathf.Abs(candidateHeight - height) <= Epsilon && outward.y > normal.y))
                {
                    height = candidateHeight;
                    normal = outward;
                    found = true;
                }
            }
            return hull.Length >= 3 && found;
        }

        /// <summary>
        /// Resolves a grounded screen foot to an upright, world-axis-aligned box top.
        /// Arbitrary rotated or sheared boxes intentionally cannot be return surfaces in this prototype.
        /// At zero pitch the nearest previous depth is retained. At nonzero pitch the visible upper
        /// surface selects a depth, then the body inset can shift its projected height slightly inward.
        /// </summary>
        public static bool TryResolveBoxTop(
            BoxCollider box,
            ProjectionFrame frame,
            Vector2 projectedFoot,
            float inset,
            Vector3 previousAnchor,
            out Vector3 worldFoot)
        {
            worldFoot = default;
            if (box == null || !IsFinite(inset) || inset < 0f ||
                !IsFinite(projectedFoot.x) || !IsFinite(projectedFoot.y) || !IsAxisAlignedUpright(box.transform))
                return false;

            Vector3[] corners = GetWorldCorners(box);
            Vector3 minimum = corners[0];
            Vector3 maximum = corners[0];
            for (int i = 1; i < corners.Length; i++)
            {
                minimum = Vector3.Min(minimum, corners[i]);
                maximum = Vector3.Max(maximum, corners[i]);
            }

            minimum.x += inset;
            minimum.z += inset;
            maximum.x -= inset;
            maximum.z -= inset;
            if (minimum.x > maximum.x || minimum.z > maximum.z || maximum.y - minimum.y <= Epsilon)
                return false;

            Vector3 horizontalForward = Vector3.ProjectOnPlane(frame.Forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < Epsilon * Epsilon)
                return false;
            horizontalForward.Normalize();

            // Right stays horizontal without roll. This line is on the real top plane and
            // contains every world point with the requested screen horizontal coordinate.
            Vector3 lineOrigin = frame.Origin + projectedFoot.x * frame.Right +
                Vector3.up * (maximum.y - frame.Origin.y);
            float depthMin = float.NegativeInfinity;
            float depthMax = float.PositiveInfinity;
            if (!ClipSlab(lineOrigin.x, horizontalForward.x, minimum.x, maximum.x, ref depthMin, ref depthMax) ||
                !ClipSlab(lineOrigin.z, horizontalForward.z, minimum.z, maximum.z, ref depthMin, ref depthMax))
                return false;

            float baseHeight = Vector3.Dot(lineOrigin - frame.Origin, frame.Up);
            float heightPerDepth = Vector3.Dot(horizontalForward, frame.Up);
            float preferredDepth;
            if (Mathf.Abs(heightPerDepth) < Epsilon)
            {
                if (Mathf.Abs(projectedFoot.y - baseHeight) > TopHeightTolerance)
                    return false;
                preferredDepth = Vector3.Dot(previousAnchor - frame.Origin, horizontalForward);
            }
            else
            {
                if (!TryGetProjectedSurface(box, frame, projectedFoot.x, out float surfaceHeight, out _) ||
                    Mathf.Abs(projectedFoot.y - surfaceHeight) > 0.12f)
                    return false;
                preferredDepth = (projectedFoot.y - baseHeight) / heightPerDepth;
            }

            if (!IsFinite(preferredDepth) || !IsFinite(depthMin) || !IsFinite(depthMax))
                return false;

            float depth = Mathf.Clamp(preferredDepth, depthMin, depthMax);
            worldFoot = lineOrigin + depth * horizontalForward;
            worldFoot.y = maximum.y;
            return true;
        }

        private static Vector3[] GetWorldCorners(BoxCollider box)
        {
            var corners = new Vector3[8];
            Vector3 halfSize = box.size * 0.5f;
            for (int i = 0; i < corners.Length; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                corners[i] = box.transform.TransformPoint(box.center + Vector3.Scale(halfSize, sign));
            }
            return corners;
        }

        private static bool IsAxisAlignedUpright(Transform transform)
        {
            Matrix4x4 matrix = transform.localToWorldMatrix;
            Vector3 right = matrix.MultiplyVector(Vector3.right);
            Vector3 up = matrix.MultiplyVector(Vector3.up);
            Vector3 forward = matrix.MultiplyVector(Vector3.forward);
            if (right.sqrMagnitude < Epsilon * Epsilon || up.sqrMagnitude < Epsilon * Epsilon || forward.sqrMagnitude < Epsilon * Epsilon)
                return false;

            right.Normalize();
            up.Normalize();
            forward.Normalize();
            const float alignment = 0.99999f;
            return Mathf.Abs(up.y) >= alignment &&
                   (Mathf.Abs(right.x) >= alignment || Mathf.Abs(right.z) >= alignment) &&
                   (Mathf.Abs(forward.x) >= alignment || Mathf.Abs(forward.z) >= alignment) &&
                   Mathf.Abs(Vector3.Dot(right, forward)) < Epsilon;
        }

        private static bool ClipSlab(float origin, float direction, float minimum, float maximum, ref float depthMin, ref float depthMax)
        {
            if (Mathf.Abs(direction) < Epsilon)
                return origin >= minimum - Epsilon && origin <= maximum + Epsilon;

            float first = (minimum - origin) / direction;
            float second = (maximum - origin) / direction;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }
            depthMin = Mathf.Max(depthMin, first);
            depthMax = Mathf.Min(depthMax, second);
            return depthMin <= depthMax;
        }

        private static int ComparePoints(Vector2 left, Vector2 right)
        {
            int horizontal = left.x.CompareTo(right.x);
            return horizontal == 0 ? left.y.CompareTo(right.y) : horizontal;
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
