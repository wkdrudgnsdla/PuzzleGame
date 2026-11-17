using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PuzzleGame.Tests
{
    public class ProjectionMathTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in objects)
                Object.DestroyImmediate(instance);
            objects.Clear();
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(270f)]
        public void FrameRoundTripPreservesWorldPoint(float yaw)
        {
            var frame = new ProjectionFrame(new Vector3(3f, -2f, 7f), yaw);
            var point = new Vector3(-4f, 5f, 12f);
            AssertVector(point, frame.Unproject(frame.Project(point), frame.Depth(point)));
            Assert.That(Vector3.Dot(frame.Right, frame.Forward), Is.EqualTo(0f).Within(0.00001f));
            AssertVector(Vector3.up, frame.Up);
        }

        [Test]
        public void FrontProjectionKeepsTheFourUnitGap()
        {
            BoxCollider first = CreatePlatform(Vector3.zero);
            BoxCollider second = CreatePlatform(new Vector3(8f, 0f, 4f));
            Vector2[] a = ProjectionMath.ProjectBox(first, new ProjectionFrame(Vector3.zero, 0f));
            Vector2[] b = ProjectionMath.ProjectBox(second, new ProjectionFrame(Vector3.zero, 0f));

            Assert.That(a.Length, Is.EqualTo(4));
            Assert.That(MinX(a), Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(MaxX(a), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(MinX(b) - MaxX(a), Is.EqualTo(4f).Within(0.0001f));
            Assert.That(SignedArea(a), Is.GreaterThan(0f));
        }

        [Test]
        public void DiagonalProjectionCreatesTheExpectedOverlap()
        {
            BoxCollider first = CreatePlatform(Vector3.zero);
            BoxCollider second = CreatePlatform(new Vector3(8f, 0f, 4f));
            var frame = new ProjectionFrame(Vector3.zero, 45f);
            Vector2[] a = ProjectionMath.ProjectBox(first, frame);
            Vector2[] b = ProjectionMath.ProjectBox(second, frame);

            Assert.That(MinX(a), Is.EqualTo(-2.474874f).Within(0.0001f));
            Assert.That(MaxX(a), Is.EqualTo(2.474874f).Within(0.0001f));
            Assert.That(MinX(b), Is.EqualTo(0.353553f).Within(0.0001f));
            Assert.That(MaxX(b), Is.EqualTo(5.303301f).Within(0.0001f));
            Assert.That(MaxX(a) - MinX(b), Is.EqualTo(2.12132f).Within(0.0001f));
        }

        [Test]
        public void MissingAndNumericallyDegenerateBoxesProduceSafePaths()
        {
            var frame = new ProjectionFrame(Vector3.zero, 0f);
            Assert.That(ProjectionMath.ProjectBox(null, frame), Is.Empty);
            BoxCollider box = CreatePlatform(Vector3.zero);
            box.size = new Vector3(0.000001f, 1f, 3f);
            Assert.That(ProjectionMath.ProjectBox(box, frame).Length, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void ProjectionUsesColliderCenterScaleAndRotation()
        {
            BoxCollider box = CreatePlatform(new Vector3(10f, 0f, -4f));
            box.center = new Vector3(1f, 0.5f, -1f);
            box.size = new Vector3(2f, 1f, 2f);
            box.transform.position = new Vector3(10f, 2f, -4f);
            box.transform.localScale = new Vector3(2f, 3f, 4f);
            Vector2[] points = ProjectionMath.ProjectBox(box, new ProjectionFrame(Vector3.zero, 0f));
            Assert.That(MinX(points), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(MaxX(points), Is.EqualTo(14f).Within(0.0001f));

            box.transform.rotation = Quaternion.Euler(20f, 35f, 0f);
            points = ProjectionMath.ProjectBox(box, new ProjectionFrame(Vector3.zero, 10f));
            Assert.That(points.Length, Is.InRange(4, 6));
            Assert.That(SignedArea(points), Is.GreaterThan(0f));
        }

        [Test]
        public void FrontReturnPreservesDepthWhenItIsInsideTheInsetFootprint()
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            var frame = new ProjectionFrame(Vector3.zero, 0f);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, new Vector2(1f, 0f), 0.4f,
                new Vector3(-1f, 0f, 0.5f), out Vector3 foot), Is.True);
            AssertVector(new Vector3(1f, 0f, 0.5f), foot);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void ParallelSlabsClampDepthInsteadOfDividingByZero(float yaw)
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            var frame = new ProjectionFrame(Vector3.zero, yaw);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, Vector2.zero, 0.4f,
                100f * frame.Forward, out Vector3 foot), Is.True);
            Assert.That(Mathf.Abs(foot.x), Is.LessThanOrEqualTo(1.6001f));
            Assert.That(Mathf.Abs(foot.z), Is.LessThanOrEqualTo(1.1001f));
            Assert.That(foot.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frame.Project(foot).sqrMagnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void DiagonalReturnOnSecondPlatformChangesRealWorldDepth()
        {
            BoxCollider box = CreatePlatform(new Vector3(8f, 0f, 4f));
            var frame = new ProjectionFrame(Vector3.zero, 45f);
            Vector2 projected = frame.Project(new Vector3(9f, 0f, 4f));
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, projected, 0.4f, Vector3.zero,
                out Vector3 foot), Is.True);
            AssertVector(new Vector3(7.9f, 0f, 2.9f), foot);
            Assert.That(Vector2.Distance(projected, frame.Project(foot)), Is.LessThan(0.0001f));
        }

        [Test]
        public void ReturnUsesTransformedCenterAndNonUniformScale()
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            box.transform.position = new Vector3(10f, 2f, -4f);
            box.transform.localScale = new Vector3(2f, 3f, 4f);
            box.center = new Vector3(1f, 0.5f, -1f);
            box.size = new Vector3(2f, 1f, 2f);
            var frame = new ProjectionFrame(new Vector3(1f, 2f, 0f), 90f);
            var expected = new Vector3(11f, 5f, -8f);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, frame.Project(expected), 0.4f,
                new Vector3(11f, 5f, -9f), out Vector3 foot), Is.True);
            AssertVector(expected, foot);
        }

        [Test]
        public void SilhouetteEdgeWithoutBodyRoomCannotBeReturnedTo()
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, new ProjectionFrame(Vector3.zero, 45f),
                new Vector2(2.3f, 0f), 0.4f, Vector3.zero, out _), Is.False);
        }

        [Test]
        public void WrongTopHeightAndEmptyFootprintAreRejected()
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            var frame = new ProjectionFrame(Vector3.zero, 0f);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, new Vector2(0f, 0.2f),
                0.4f, Vector3.zero, out _), Is.False);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, Vector2.zero,
                2f, Vector3.zero, out _), Is.False);
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, Vector2.zero,
                -0.1f, Vector3.zero, out _), Is.False);
        }

        [Test]
        public void TiltedReturnSurfaceIsRejectedWhileProjectionStillExists()
        {
            BoxCollider box = CreatePlatform(Vector3.zero);
            box.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            var frame = new ProjectionFrame(Vector3.zero, 45f);
            Assert.That(ProjectionMath.ProjectBox(box, frame).Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(ProjectionMath.TryResolveBoxTop(box, frame, Vector2.zero, 0.4f,
                Vector3.zero, out _), Is.False);
        }

        private BoxCollider CreatePlatform(Vector3 topCenter)
        {
            var instance = new GameObject("Projection Math Test Platform");
            objects.Add(instance);
            instance.transform.position = topCenter - Vector3.up * 0.5f;
            var box = instance.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 1f, 3f);
            return box;
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.0002f), $"Expected {expected}, got {actual}.");
        }

        private static float MinX(Vector2[] points)
        {
            float result = float.PositiveInfinity;
            foreach (Vector2 point in points)
                result = Mathf.Min(result, point.x);
            return result;
        }

        private static float MaxX(Vector2[] points)
        {
            float result = float.NegativeInfinity;
            foreach (Vector2 point in points)
                result = Mathf.Max(result, point.x);
            return result;
        }

        private static float SignedArea(Vector2[] points)
        {
            float twiceArea = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Length];
                twiceArea += current.x * next.y - current.y * next.x;
            }
            return twiceArea * 0.5f;
        }
    }
}
