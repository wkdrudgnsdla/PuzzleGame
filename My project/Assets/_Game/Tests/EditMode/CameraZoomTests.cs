using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PuzzleGame.Tests
{
    public sealed class CameraZoomTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private ProjectionPrototype prototype;
        private Camera camera3D;
        private Camera camera2D;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Camera zoom test");
            prototype = root.AddComponent<ProjectionPrototype>();
            var createCamera = typeof(ProjectionPrototype).GetMethod("CreateCamera", PrivateInstance);
            camera3D = (Camera)createCamera.Invoke(prototype, new object[] { "3D test", 0 });
            camera2D = (Camera)createCamera.Invoke(prototype, new object[] { "2D test", 0 });
            SetField("camera3D", camera3D);
            SetField("camera2D", camera2D);
            prototype.CameraFocusPoint = new GameObject("Focus").transform;
            prototype.CameraFocusPoint.SetParent(root.transform);
            prototype.CameraFocusPoint.position = new Vector3(3f, 2f, -1f);
            SetField("currentPanOffset", new Vector3(2f, -1f, 3f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [TestCase(0f, 0f, 1.5f)]
        [TestCase(45f, 30f, 1.5f)]
        [TestCase(270f, -90f, 1.5f)]
        [TestCase(0f, 0f, 50f)]
        [TestCase(45f, 30f, 50f)]
        [TestCase(270f, 90f, 50f)]
        public void ZoomMovesAlongViewAxisAndPreserves2DFramingAtEveryDepth(float yaw, float pitch, float zoom)
        {
            var frame = new ProjectionFrame(Vector3.zero, yaw, pitch);
            SetField("frame", frame);
            ApplyZoom(4.4f);
            Vector3 originalPosition = camera3D.transform.position;
            Quaternion originalRotation = camera3D.transform.rotation;
            ApplyZoom(zoom);

            Assert.That(camera3D.orthographic, Is.True);
            Assert.That(camera3D.orthographicSize, Is.EqualTo(camera2D.orthographicSize));
            Assert.That(Quaternion.Angle(originalRotation, camera3D.transform.rotation), Is.LessThan(0.001f));
            Vector3 movement = camera3D.transform.position - originalPosition;
            Assert.That(Vector3.Cross(movement, frame.Forward).magnitude, Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(movement, frame.Forward) * (4.4f - zoom), Is.GreaterThan(0f),
                "Zooming in must move forward; zooming out must move backward.");

            Vector3 focus = prototype.CameraFocusPoint.position + new Vector3(2f, -1f, 3f);
            Vector3 viewportFocus = camera3D.WorldToViewportPoint(focus);
            Assert.That(viewportFocus.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(viewportFocus.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(viewportFocus.z, Is.GreaterThan(camera3D.nearClipPlane));
            Assert.That(viewportFocus.z, Is.LessThan(camera3D.farClipPlane));

            Assert.That(camera2D.orthographic, Is.True);
            // Near and far geometry must retain its screen position during 2D conversion.
            foreach (float depth in new[] { -8f, 0f, 8f })
            {
                Vector3 worldPoint = focus + frame.Right * 0.75f + frame.Up * 0.5f + frame.Forward * depth;
                Vector2 projectedPoint = frame.Project(worldPoint);
                Vector3 viewport3D = camera3D.WorldToViewportPoint(worldPoint);
                Vector3 viewport2D = camera2D.WorldToViewportPoint(new Vector3(projectedPoint.x, projectedPoint.y, 0f));
                Assert.That(viewport3D.x, Is.EqualTo(viewport2D.x).Within(0.0001f), "Depth: " + depth);
                Assert.That(viewport3D.y, Is.EqualTo(viewport2D.y).Within(0.0001f), "Depth: " + depth);
            }
        }

        private void ApplyZoom(float zoom)
        {
            SetField("currentZoomSize", zoom);
            typeof(ProjectionPrototype).GetMethod("UpdateCameraPose", PrivateInstance).Invoke(prototype, null);
        }

        private void SetField(string name, object value)
        {
            typeof(ProjectionPrototype).GetField(name, PrivateInstance).SetValue(prototype, value);
        }
    }
}
