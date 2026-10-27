using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PuzzleGame.Tests
{
    public sealed class CameraNavigationPlayModeTests
    {
        private static readonly Vector3 MapOrigin = new Vector3(1000f, 0f, 0f);
        private GameObject root;
        private ProjectionPrototype prototype;
        private Camera camera3D, camera2D;
        private CameraFollowSamples followSamples;
        private Mouse mouse, previousMouse;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var settings = InputSystem.settings;
            previousBackgroundBehavior = settings.backgroundBehavior;
            previousEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousMouse = Mouse.current;
            mouse = InputSystem.AddDevice<Mouse>("Camera navigation test mouse");
            InputSystem.EnableDevice(mouse);
            mouse.MakeCurrent();

            root = new GameObject("Camera navigation test world");
            root.SetActive(false);
            prototype = root.AddComponent<ProjectionPrototype>();
            prototype.MapRoot = Child("Map", Vector3.zero);
            CreatePlatform(1, MapOrigin + new Vector3(0f, -1f, 0f));
            CreatePlatform(2, MapOrigin + new Vector3(8f, -1f, 4f));
            prototype.SpawnPoint = Child("Spawn", MapOrigin);
            prototype.CameraFocusPoint = Child("Focus", MapOrigin + new Vector3(4f, 1.1f, 2f));
            root.SetActive(true);
            yield return null;
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            camera3D = root.transform.Find("3D camera").GetComponent<Camera>();
            camera2D = root.transform.Find("2D projection camera").GetComponent<Camera>();
            Assert.That(prototype.IsGrounded, Is.True);
            followSamples = root.AddComponent<CameraFollowSamples>();
            followSamples.Owner = prototype;
            followSamples.Camera3D = camera3D;
            followSamples.Camera2D = camera2D;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WheelZoomStaysUnderPointerAndRoundTripsWithoutAFramingJump()
        {
            Vector3 target = MapOrigin + new Vector3(0f, -0.5f, -1.5f);
            Vector2 pointer = camera3D.WorldToScreenPoint(target);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null;
            Vector3 screenBefore = camera3D.WorldToViewportPoint(target);
            float initialZoom = prototype.ZoomSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0f, 120f) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                AssertScreenPoint(screenBefore, camera3D.WorldToViewportPoint(target));
            }
            Assert.That(prototype.ZoomSize, Is.LessThan(initialZoom));
            Assert.That(camera3D.orthographic, Is.True);

            Assert.That(prototype.TryToggleMode(), Is.True, prototype.StatusMessage);
            Assert.That(camera3D.enabled, Is.False);
            Assert.That(camera2D.enabled, Is.True);
            AssertModeFraming(target);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            // The same wheel path must work in 2D and retain the projected target.
            Vector3 before2DZoom = camera2D.WorldToViewportPoint(new Vector3(target.x, target.y, 0f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0f, -120f) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                AssertScreenPoint(before2DZoom, camera2D.WorldToViewportPoint(new Vector3(target.x, target.y, 0f)));
                AssertModeFraming(target);
            }
            Assert.That(prototype.TryToggleMode(), Is.True, prototype.StatusMessage);
            Assert.That(prototype.Is2D, Is.False);
            Assert.That(camera3D.enabled, Is.True);
            AssertModeFraming(target);
        }

        [UnityTest]
        public IEnumerator RightDragPreservesPlayerFramingAndResetClearsNavigationState()
        {
            Vector3 target = MapOrigin + new Vector3(0f, -0.5f, -1.5f);
            Vector2 pointer = camera3D.WorldToScreenPoint(target);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Vector3 screenBefore = camera3D.WorldToViewportPoint(PlayerCenter);
            pointer += new Vector2(24f, -8f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                AssertScreenPoint(screenBefore, camera3D.WorldToViewportPoint(PlayerCenter));
                AssertModeFraming(target);
            }
            Assert.That(prototype.YawDegrees, Is.GreaterThan(0.1f));
            Assert.That(prototype.PitchDegrees, Is.GreaterThan(0.1f));

            prototype.ResetPrototype();
            Vector3 resetFocus = camera3D.WorldToViewportPoint(prototype.CameraFocusPoint.position);
            Assert.That(resetFocus.x, Is.EqualTo(0.5f).Within(0.002f));
            Assert.That(resetFocus.y, Is.EqualTo(0.5f).Within(0.002f));
            Assert.That(prototype.YawDegrees, Is.EqualTo(0f));
            Assert.That(prototype.PitchDegrees, Is.EqualTo(0f));
            Assert.That(prototype.ZoomSize, Is.EqualTo(4.4f));
            for (int i = 0; i < 10; i++) yield return null;
            AssertScreenPoint(resetFocus, camera3D.WorldToViewportPoint(prototype.CameraFocusPoint.position));
        }

        [UnityTest]
        public IEnumerator PanStillMovesTheCameraAndKeepsPlayerFramingDuringMovementAndOrbit()
        {
            Vector3 initialCameraPosition = camera3D.transform.position;
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
            yield return null;
            pointer += new Vector2(64f, 24f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 25; i++) yield return null;
            Assert.That(Vector3.Distance(initialCameraPosition, camera3D.transform.position), Is.GreaterThan(0.05f));
            yield return new WaitForSeconds(0.5f); // Let the deliberate pan smoothing settle before testing follow.

            float focusDepth = camera3D.WorldToViewportPoint(prototype.CameraFocusPoint.position).z;
            Vector3 movedFocus = camera3D.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, focusDepth));
            Assert.That(Vector3.Distance(movedFocus, prototype.CameraFocusPoint.position), Is.GreaterThan(0.05f));
            Vector3 playerScreen = followSamples.ScreenCenters[followSamples.ScreenCenters.Count - 1];
            followSamples.Clear();
            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSeconds(0.15f);
            prototype.SetMoveInputOverride(Vector2.zero);
            foreach (Vector3 sample in followSamples.ScreenCenters) AssertScreenPoint(playerScreen, sample);
            pointer = camera3D.ViewportToScreenPoint(new Vector3(0.2f, 0.8f, 0f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            pointer += new Vector2(20f, -5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                AssertScreenPoint(playerScreen, camera3D.WorldToViewportPoint(PlayerCenter));
                AssertModeFraming(movedFocus);
            }
        }

        [UnityTest]
        public IEnumerator FollowTracksWalkingAndJumpingInTheSameRenderedFrame()
        {
            Vector3 playerScreen = followSamples.ScreenCenters[followSamples.ScreenCenters.Count - 1];
            followSamples.Clear();
            prototype.SetMoveInputOverride(Vector2.right);
            float deadline = Time.time + 2f;
            while (prototype.PlayerWorldFoot.x < MapOrigin.x + 0.8f && Time.time < deadline) yield return null;
            prototype.SetMoveInputOverride(Vector2.zero);
            Assert.That(prototype.PlayerWorldFoot.x, Is.GreaterThan(MapOrigin.x + 0.6f));
            prototype.SetJumpInputOverride(true);
            yield return new WaitForSeconds(0.7f);
            Assert.That(followSamples.WorldFeet.Exists(point => point.y > 0.25f), Is.True);
            Assert.That(followSamples.ScreenCenters.Count, Is.GreaterThan(10));
            foreach (Vector3 sample in followSamples.ScreenCenters) AssertScreenPoint(playerScreen, sample);
        }

        [UnityTest]
        public IEnumerator CrossingIn2DAndReturningAtANewDepthKeepsPlayerFramingDuringOrbit()
        {
            Assert.That(prototype.SetViewYaw(45f), Is.True);
            Assert.That(prototype.TryToggleMode(), Is.True, prototype.StatusMessage);
            yield return new WaitForSeconds(0.15f);
            Vector3 playerScreen = followSamples.ScreenCenters[followSamples.ScreenCenters.Count - 1];
            followSamples.Clear();
            float initialProjectedX = prototype.PlayerProjectedFoot.x;
            float deadline = Time.time + 3f;
            prototype.SetMoveInputOverride(Vector2.right);
            while (prototype.PlayerProjectedFoot.x < initialProjectedX + 3.5f && Time.time < deadline)
                yield return null;
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(prototype.CurrentSupportId, Is.EqualTo(2));
            Assert.That(prototype.CanReturnTo3D, Is.True, prototype.StatusMessage);
            foreach (Vector3 sample in followSamples.ScreenCenters) AssertScreenPoint(playerScreen, sample);

            Vector3 worldMarker = MapOrigin + new Vector3(8f, 0f, 4f);
            Vector3 markerScreen = camera3D.WorldToViewportPoint(worldMarker);
            Assert.That(prototype.TryToggleMode(), Is.True, prototype.StatusMessage);
            AssertScreenPoint(markerScreen, camera3D.WorldToViewportPoint(worldMarker));
            var frame = new ProjectionFrame(Vector3.zero, prototype.YawDegrees, prototype.PitchDegrees);
            Assert.That(Vector3.Dot(prototype.PlayerWorldFoot - MapOrigin, frame.Forward), Is.GreaterThan(2f));
            yield return new WaitForSeconds(0.1f);

            playerScreen = followSamples.ScreenCenters[followSamples.ScreenCenters.Count - 1];
            followSamples.Clear();
            Vector2 pointer = camera3D.ViewportToScreenPoint(new Vector3(0.8f, 0.2f, 0f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            pointer += new Vector2(60f, 20f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Right));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 30; i++) yield return null;
            foreach (Vector3 sample in followSamples.ScreenCenters) AssertScreenPoint(playerScreen, sample);
            AssertModeFraming(worldMarker);
        }

        [UnityTest]
        public IEnumerator TopDownMovementFollowsTheRendered2DPlayer()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ProjectionPrototype).GetField("currentViewPitch", flags).SetValue(prototype, 75f);
            typeof(ProjectionPrototype).GetField("targetViewPitch", flags).SetValue(prototype, 75f);
            Assert.That(prototype.SetViewYaw(30f), Is.True);
            Assert.That(prototype.TryToggleMode(), Is.True, prototype.StatusMessage);
            Assert.That(prototype.IsTopDown2D, Is.True);
            yield return new WaitForSeconds(0.1f);
            Vector3 playerScreen = followSamples.ScreenCenters[followSamples.ScreenCenters.Count - 1];
            Vector2 start = prototype.PlayerProjectedFoot;
            followSamples.Clear();
            prototype.SetMoveInputOverride(Vector2.up);
            yield return new WaitForSeconds(0.2f);
            prototype.SetMoveInputOverride(Vector2.zero);
            Assert.That(Vector2.Distance(start, prototype.PlayerProjectedFoot), Is.GreaterThan(0.2f));
            foreach (Vector3 sample in followSamples.ScreenCenters) AssertScreenPoint(playerScreen, sample);
        }

        private Vector3 PlayerCenter => prototype.PlayerWorldFoot + Vector3.up * (PrototypePlayer3D.Height * 0.5f);

        private void AssertModeFraming(Vector3 point)
        {
            var frame = new ProjectionFrame(Vector3.zero, prototype.YawDegrees, prototype.PitchDegrees);
            Vector2 projected = frame.Project(point);
            Assert.That(camera2D.orthographicSize, Is.EqualTo(camera3D.orthographicSize));
            AssertScreenPoint(camera3D.WorldToViewportPoint(point),
                camera2D.WorldToViewportPoint(new Vector3(projected.x, projected.y, 0f)));
        }

        private static void AssertScreenPoint(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.002f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.002f));
        }

        private Transform Child(string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform);
            obj.transform.position = position;
            return obj.transform;
        }

        private void CreatePlatform(int id, Vector3 center)
        {
            Transform platform = Child("Platform " + id, center);
            platform.SetParent(prototype.MapRoot);
            platform.localScale = new Vector3(4f, 2f, 3f);
            platform.gameObject.AddComponent<BoxCollider>();
            platform.gameObject.AddComponent<ProjectableBox>().Initialize(id, Color.white);
        }
    }

    // Observe the camera after its LateUpdate, without waiting for EndOfFrame in batch mode.
    [DefaultExecutionOrder(10000)]
    public sealed class CameraFollowSamples : MonoBehaviour
    {
        public ProjectionPrototype Owner;
        public Camera Camera3D, Camera2D;
        public readonly List<Vector3> ScreenCenters = new List<Vector3>();
        public readonly List<Vector3> WorldFeet = new List<Vector3>();

        public void Clear()
        {
            ScreenCenters.Clear();
            WorldFeet.Clear();
        }

        private void LateUpdate()
        {
            if (Owner == null || Camera3D == null || Camera2D == null) return;
            if (Owner.Is2D)
            {
                Vector2 center = Owner.PlayerProjectedFoot + (Owner.IsTopDown2D ? Vector2.zero
                    : Vector2.up * (PrototypePlayer3D.Height * 0.5f));
                ScreenCenters.Add(Camera2D.WorldToViewportPoint(new Vector3(center.x, center.y, 0f)));
            }
            else ScreenCenters.Add(Camera3D.WorldToViewportPoint(Owner.PlayerWorldFoot
                + Vector3.up * (PrototypePlayer3D.Height * 0.5f)));
            WorldFeet.Add(Owner.PlayerWorldFoot);
        }
    }
}
