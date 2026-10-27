using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PuzzleGame.Tests
{
    public sealed class CameraZoomTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<RenderTexture> textures = new List<RenderTexture>();
        private GameObject root;
        private ProjectionPrototype prototype;
        private Camera camera3D, camera2D;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Camera navigation test");
            prototype = root.AddComponent<ProjectionPrototype>();
            camera3D = (Camera)Invoke("CreateCamera", "3D test", 0);
            camera2D = (Camera)Invoke("CreateCamera", "2D test", 0);
            SetField("camera3D", camera3D);
            SetField("camera2D", camera2D);
            prototype.CameraFocusPoint = new GameObject("Focus").transform;
            prototype.CameraFocusPoint.SetParent(root.transform);
            prototype.CameraFocusPoint.position = new Vector3(3f, 2f, -1f);
            SetField("cameraPlayerAnchor", new Vector3(0f, 0.9f, 0f));
            SetField("cameraFramingOffset", prototype.CameraFocusPoint.position - new Vector3(0f, 0.9f, 0f));
            SetField("currentPanOffset", new Vector3(2f, -1f, 3f));
            SetField("targetPanOffset", new Vector3(2f, -1f, 3f));
            SetViewport(540);
            SetView(0f, 0f);
            ApplyZoom(4.4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (RenderTexture texture in textures) Object.DestroyImmediate(texture);
            textures.Clear();
        }

        [TestCase(0f, 0f, 1.5f)]
        [TestCase(45f, 30f, 1.5f)]
        [TestCase(270f, -90f, 1.5f)]
        [TestCase(0f, 0f, 50f)]
        [TestCase(45f, 30f, 50f)]
        [TestCase(270f, 90f, 50f)]
        public void ZoomChangesVisibleSizeAndMatches2DAtEveryDepth(float yaw, float pitch, float zoom)
        {
            SetField("maxZoomSize", 50f);
            SetView(yaw, pitch);
            ApplyZoom(4.4f);
            Quaternion rotation = camera3D.transform.rotation;
            var frame = GetField<ProjectionFrame>("frame");
            Vector3 focus = Focus;
            float before = VisibleHeight(focus, frame.Up);
            ApplyZoom(zoom);
            Assert.That(camera3D.orthographic, Is.True);
            Assert.That(camera2D.orthographic, Is.True);
            Assert.That(camera3D.orthographicSize, Is.EqualTo(zoom));
            Assert.That(camera2D.orthographicSize, Is.EqualTo(zoom));
            Assert.That(prototype.ZoomSize, Is.EqualTo(zoom));
            Assert.That(Quaternion.Angle(rotation, camera3D.transform.rotation), Is.LessThan(0.001f));
            Assert.That(VisibleHeight(focus, frame.Up) / before, Is.EqualTo(4.4f / zoom).Within(0.0001f));
            AssertMatchingProjection();
        }

        [TestCase(false, 30)]
        [TestCase(false, 120)]
        [TestCase(true, 30)]
        [TestCase(true, 120)]
        public void CursorAnchorStaysStillDuringSmoothZoomAndReversal(bool is2D, int fps)
        {
            typeof(ProjectionPrototype).GetProperty("Is2D").GetSetMethod(true).Invoke(prototype, new object[] { is2D });
            SetView(35f, 25f);
            Invoke("UpdateCameraPose");
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.72f, 0.64f, 0f));
            Vector3 anchor = (Vector3)Invoke("PickCameraAnchor", pointer);
            Vector3 before = camera3D.WorldToViewportPoint(anchor);
            Invoke("ZoomAtPointer", 3f, pointer);
            for (int i = 0; i < fps; i++)
            {
                Invoke("UpdateCameraMotion", 1f / fps);
                AssertSameScreenPoint(before, camera3D.WorldToViewportPoint(anchor));
                AssertMatchingProjection();
            }
            Assert.That(prototype.ZoomSize, Is.LessThan(4.4f));
            pointer = camera3D.ViewportToScreenPoint(new Vector3(0.25f, 0.33f, 0f));
            anchor = (Vector3)Invoke("PickCameraAnchor", pointer);
            before = camera3D.WorldToViewportPoint(anchor);
            Invoke("ZoomAtPointer", -6f, pointer);
            for (int i = 0; i < fps; i++)
            {
                Invoke("UpdateCameraMotion", 1f / fps);
                AssertSameScreenPoint(before, camera3D.WorldToViewportPoint(anchor));
            }
            Assert.That(prototype.ZoomSize, Is.GreaterThan(4.4f));
            Vector3 settledPan = GetField<Vector3>("currentPanOffset");
            Invoke("UpdateCameraMotion", 1f);
            Assert.That(Vector3.Distance(settledPan, GetField<Vector3>("currentPanOffset")), Is.LessThan(0.0001f));
        }

        [Test]
        public void OrbitKeepsThePlayerInTheSameScreenPositionRegardlessOfWhereTheDragStarts()
        {
            Vector3 focus = Focus;
            Vector3 pan = GetField<Vector3>("currentPanOffset");
            Vector3? finalCameraPosition = null;
            // Distinct surfaces at different depths used to select different orbit pivots.
            CreateBox(focus + new Vector3(-2f, -1f, -3f), Vector3.one * 2f);
            CreateBox(focus + new Vector3(2f, 1f, 5f), Vector3.one * 2f);
            Physics.SyncTransforms();
            foreach (Vector2 viewport in new[] { new Vector2(0.2f, 0.25f),
                new Vector2(0.8f, 0.7f), new Vector2(0.5f, 0.5f) })
            {
                SetView(30f, 20f);
                Invoke("UpdateCameraPose");
                Vector3 playerScreen = camera3D.WorldToViewportPoint(GetField<Vector3>("cameraPlayerAnchor"));
                Vector2 pointer = camera3D.ViewportToScreenPoint(viewport);
                Invoke("BeginViewDrag", pointer);
                Invoke("DragView", new Vector2(85f, 45f));
                for (int i = 0; i < 80; i++)
                {
                    Invoke("UpdateCameraMotion", 1f / 60f);
                    AssertSameScreenPoint(playerScreen, camera3D.WorldToViewportPoint(GetField<Vector3>("cameraPlayerAnchor")));
                    Assert.That(Vector3.Distance(GetField<Vector3>("currentPanOffset"), pan), Is.LessThan(0.0001f));
                    AssertMatchingProjection();
                }
                if (finalCameraPosition.HasValue)
                    Assert.That(Vector3.Distance(camera3D.transform.position, finalCameraPosition.Value), Is.LessThan(0.0001f));
                finalCameraPosition = camera3D.transform.position;
                Invoke("StopMouseOrbit");
                SetField("projectionDirty", false);
            }
            Quaternion stopped = camera3D.transform.rotation;
            Vector3 stoppedPlayerScreen = camera3D.WorldToViewportPoint(GetField<Vector3>("cameraPlayerAnchor"));
            Invoke("UpdateCameraMotion", 1f);
            Assert.That(Quaternion.Angle(stopped, camera3D.transform.rotation), Is.LessThan(0.001f));
            AssertSameScreenPoint(stoppedPlayerScreen, camera3D.WorldToViewportPoint(GetField<Vector3>("cameraPlayerAnchor")));
        }

        [Test]
        public void ZoomedInOrbitHasSimilarScreenMotionToNormalZoom()
        {
            float normal = OrbitScreenMotion(4.4f);
            Invoke("StopMouseOrbit");
            SetField("projectionDirty", false);
            float close = OrbitScreenMotion(1.5f);
            Assert.That(close / normal, Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void OrbitMatchesViewportResolutionAndPreservesVerticalDirectionAndLimits()
        {
            Invoke("DragView", new Vector2(54f, 27f));
            float yaw = GetField<float>("targetViewYaw");
            float pitch = GetField<float>("targetViewPitch");
            Assert.That(yaw, Is.GreaterThan(0f));
            Assert.That(pitch, Is.LessThan(0f));
            SetView(0f, 0f);
            SetViewport(1080);
            Invoke("DragView", new Vector2(108f, 54f));
            Assert.That(GetField<float>("targetViewYaw"), Is.EqualTo(yaw).Within(0.0001f));
            Assert.That(GetField<float>("targetViewPitch"), Is.EqualTo(pitch).Within(0.0001f));
            Invoke("DragView", new Vector2(0f, 100000f));
            Assert.That(GetField<float>("targetViewPitch"), Is.EqualTo(-90f));
        }

        [Test]
        public void ZoomLimitsDoNotPanWhenWheelContinuesPastTheLimit()
        {
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.8f, 0.7f, 0f));
            Invoke("ZoomAtPointer", 1000f, pointer);
            Invoke("UpdateCameraMotion", 1f);
            Assert.That(prototype.ZoomSize, Is.EqualTo(1.5f));
            Vector3 minimumFocus = Focus;
            Invoke("ZoomAtPointer", 1f, pointer);
            Invoke("UpdateCameraMotion", 1f);
            Assert.That(Vector3.Distance(Focus, minimumFocus), Is.LessThan(0.0001f));
            Invoke("ZoomAtPointer", -1000f, pointer);
            Invoke("UpdateCameraMotion", 1f);
            Assert.That(prototype.ZoomSize, Is.EqualTo(20f));
            AssertMatchingProjection();
        }

        [Test]
        public void StartingOrbitDuringZoomCancelsTheCompetingZoomAnchor()
        {
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.75f, 0.6f, 0f));
            Invoke("ZoomAtPointer", 3f, pointer);
            Invoke("UpdateCameraMotion", 0.02f);
            Invoke("BeginViewDrag", pointer);
            Assert.That(GetField<bool>("zoomAroundPointer"), Is.False);
            Invoke("DragView", new Vector2(20f, 8f));
            for (int i = 0; i < 80; i++) Invoke("UpdateCameraMotion", 1f / 60f);
            AssertMatchingProjection();
            Assert.That(float.IsNaN(camera3D.transform.position.x), Is.False);
        }

        [Test]
        public void DeepTerrainStaysInsideTheClipPlanes()
        {
            SetView(45f, 30f);
            var frame = GetField<ProjectionFrame>("frame");
            var near = CreateBox(Focus - frame.Forward * 80f, Vector3.one * 4f);
            var far = CreateBox(Focus + frame.Forward * 120f, Vector3.one * 4f);
            Physics.SyncTransforms();
            Invoke("UpdateCameraPose");
            foreach (ProjectableBox box in new[] { near, far })
            {
                Bounds bounds = box.SourceCollider.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                            float depth = camera3D.WorldToViewportPoint(corner).z;
                            Assert.That(depth, Is.GreaterThan(camera3D.nearClipPlane));
                            Assert.That(depth, Is.LessThan(camera3D.farClipPlane));
                        }
            }
            AssertMatchingProjection();
        }

        private Vector3 Focus => (Vector3)typeof(ProjectionPrototype).GetProperty("CameraFocus", PrivateInstance).GetValue(prototype);

        private void AssertMatchingProjection()
        {
            var frame = GetField<ProjectionFrame>("frame");
            foreach (float depth in new[] { -8f, 0f, 8f })
            {
                Vector3 point = Focus + frame.Right * 0.75f + frame.Up * 0.5f + frame.Forward * depth;
                Vector2 projected = frame.Project(point);
                AssertSameScreenPoint(camera3D.WorldToViewportPoint(point),
                    camera2D.WorldToViewportPoint(new Vector3(projected.x, projected.y, 0f)));
            }
        }

        private static void AssertSameScreenPoint(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        }

        private float OrbitScreenMotion(float zoom)
        {
            SetView(0f, 0f);
            SetField("currentPanOffset", Vector3.zero);
            SetField("targetPanOffset", Vector3.zero);
            ApplyZoom(zoom);
            Vector3 point = Focus + Vector3.forward * 4f + Vector3.right * 0.5f;
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            Invoke("BeginViewDrag", pointer);
            Vector3 before = camera3D.WorldToScreenPoint(point);
            Invoke("DragView", new Vector2(6f, 0f));
            Invoke("UpdateCameraMotion", 1f);
            return Vector2.Distance(before, camera3D.WorldToScreenPoint(point));
        }

        private ProjectableBox CreateBox(Vector3 center, Vector3 size)
        {
            var obj = new GameObject("Navigation target", typeof(BoxCollider), typeof(ProjectableBox));
            obj.transform.SetParent(root.transform);
            obj.transform.position = center;
            obj.transform.localScale = size;
            obj.layer = ProjectionPrototype.World3DLayer;
            ProjectableBox box = obj.GetComponent<ProjectableBox>();
            GetField<List<ProjectableBox>>("terrain").Add(box);
            return box;
        }

        private float VisibleHeight(Vector3 center, Vector3 up)
        {
            return (camera3D.WorldToViewportPoint(center + up) - camera3D.WorldToViewportPoint(center)).magnitude;
        }

        private void SetViewport(int height)
        {
            var texture = new RenderTexture(height * 16 / 9, height, 0);
            textures.Add(texture);
            camera3D.targetTexture = camera2D.targetTexture = texture;
        }

        private void SetView(float yaw, float pitch)
        {
            SetField("currentViewYaw", yaw);
            SetField("targetViewYaw", yaw);
            SetField("currentViewPitch", pitch);
            SetField("targetViewPitch", pitch);
            SetField("frame", new ProjectionFrame(Vector3.zero, yaw, pitch));
        }

        private void ApplyZoom(float zoom)
        {
            SetField("currentZoomSize", zoom);
            SetField("targetZoomSize", zoom);
            Invoke("UpdateCameraPose");
        }

        private object Invoke(string name, params object[] values)
        {
            return typeof(ProjectionPrototype).GetMethod(name, PrivateInstance).Invoke(prototype, values);
        }

        private T GetField<T>(string name)
        {
            return (T)typeof(ProjectionPrototype).GetField(name, PrivateInstance).GetValue(prototype);
        }

        private void SetField(string name, object value)
        {
            typeof(ProjectionPrototype).GetField(name, PrivateInstance).SetValue(prototype, value);
        }
    }
}
