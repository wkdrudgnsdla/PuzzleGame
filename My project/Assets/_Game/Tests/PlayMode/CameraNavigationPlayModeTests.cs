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
            yield return ProjectionTestWorld.AssignPlayerPrefabs(prototype);
            ProjectionTestWorld.SetInitialFrontView(prototype);
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
        public IEnumerator CubeMascotUsesModelStatesAndMirrorsItsFlatWalkSprites()
        {
            var model = root.GetComponentInChildren<CubeMascotModel>();
            Assert.That(model, Is.Not.Null);
            var player3D = model.GetComponentInParent<PrototypePlayer3D>();
            var player2D = root.GetComponentInChildren<PrototypePlayer2D>(true);
            Assert.That(player3D.transform.parent, Is.SameAs(root.transform));
            Assert.That(player2D.transform.parent, Is.SameAs(root.transform));
            Assert.That(player3D.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(player2D.GetComponent<Rigidbody2D>().simulated, Is.False);
            Animator animator = model.GetComponent<Animator>();
            foreach (PlayerAnimationState state in System.Enum.GetValues(typeof(PlayerAnimationState)))
                Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), Is.True, state.ToString());

            yield return WaitForState(PlayerAnimationState.Idle);
            Quaternion spawnHeading = model.transform.rotation;
            Assert.That(prototype.SetViewYaw(95f), Is.True);
            yield return null;
            Assert.That(Quaternion.Angle(model.transform.rotation, spawnHeading), Is.LessThan(0.1f));
            Assert.That(prototype.SetViewYaw(0f), Is.True);

            prototype.SetMoveInputOverride(Vector2.right);
            yield return WaitForState(PlayerAnimationState.Walk);
            yield return new WaitForSeconds(0.14f);
            Assert.That(Vector3.Angle(model.transform.forward, Vector3.right), Is.LessThan(1f));
            Quaternion travelHeading = model.transform.rotation;
            Transform leftFoot = model.transform.Find("Left Foot");
            Transform rightFoot = model.transform.Find("Right Foot");
            Assert.That(leftFoot.localPosition.x - rightFoot.localPosition.x, Is.GreaterThan(0.25f));
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return WaitForState(PlayerAnimationState.Idle);
            prototype.SetJumpInputOverride(true);
            yield return WaitForState(PlayerAnimationState.Jump);
            yield return WaitForState(PlayerAnimationState.Fall);
            yield return WaitForState(PlayerAnimationState.Land);
            yield return WaitForState(PlayerAnimationState.Idle);

            Assert.That(prototype.TryToggleMode(), Is.True);
            yield return null;
            Assert.That(player3D.gameObject.activeInHierarchy, Is.False);
            Assert.That(player2D.GetComponent<Rigidbody2D>().simulated, Is.True);
            var sprite = root.GetComponentInChildren<SpriteRenderer>();
            Assert.That(sprite, Is.Not.Null);
            Sprite idle = sprite.sprite;
            Assert.That(idle, Is.Not.Null);

            prototype.SetMoveInputOverride(Vector2.left);
            yield return new WaitForSeconds(0.12f);
            Assert.That(sprite.flipX, Is.True);
            Assert.That(sprite.sprite.rect.y, Is.Not.EqualTo(idle.rect.y));
            Sprite step = sprite.sprite;
            yield return new WaitForSeconds(0.1f);
            Assert.That(sprite.sprite, Is.Not.SameAs(step));

            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSeconds(0.12f);
            Assert.That(sprite.flipX, Is.False);
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return new WaitForSeconds(0.1f);
            float profileIdleRow = sprite.sprite.rect.y;
            prototype.SetJumpInputOverride(true);
            yield return new WaitForSeconds(0.08f);
            Assert.That(prototype.IsGrounded, Is.False);
            Assert.That(sprite.sprite.rect.y, Is.Not.EqualTo(profileIdleRow));
            yield return new WaitForSeconds(0.75f);
            Assert.That(prototype.IsGrounded, Is.True);
            Assert.That(sprite.sprite.rect.y, Is.EqualTo(profileIdleRow));
            Assert.That(prototype.TryToggleMode(), Is.True);
            Assert.That(model.gameObject.activeInHierarchy, Is.True);
            Assert.That(sprite.gameObject.activeInHierarchy, Is.False);
            yield return null;
            Assert.That(Quaternion.Angle(model.transform.rotation, travelHeading), Is.LessThan(0.1f));

            IEnumerator WaitForState(PlayerAnimationState state)
            {
                float deadline = Time.realtimeSinceStartup + 2f;
                string stateName = "Base Layer." + state;
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(stateName), Is.True, stateName);
            }
        }

        [UnityTest]
        public IEnumerator WheelZoomKeepsPlayerFramingAndRoundTripsWithoutAFramingJump()
        {
            Vector3 target = MapOrigin + new Vector3(0f, -0.5f, -1.5f);
            Vector2 pointer = camera3D.WorldToScreenPoint(target);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null;
            Vector3 screenBefore = camera3D.WorldToViewportPoint(PlayerCenter);
            float initialZoom = prototype.ZoomSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0f, 120f) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                AssertScreenPoint(screenBefore, camera3D.WorldToViewportPoint(PlayerCenter));
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

            // 2D zoom uses the same size and preserves the rendered player's framing.
            Vector2 projectedPlayer = prototype.PlayerProjectedFoot + Vector2.up * (PrototypePlayer3D.Height * 0.5f);
            Vector3 before2DZoom = camera2D.WorldToViewportPoint(new Vector3(projectedPlayer.x, projectedPlayer.y, 0f));
            float zoomBefore2DWheel = prototype.ZoomSize;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0f, -120f) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            for (int i = 0; i < 25; i++)
            {
                yield return null;
                projectedPlayer = prototype.PlayerProjectedFoot + Vector2.up * (PrototypePlayer3D.Height * 0.5f);
                AssertScreenPoint(before2DZoom, camera2D.WorldToViewportPoint(new Vector3(projectedPlayer.x, projectedPlayer.y, 0f)));
                AssertModeFraming(target);
            }
            Assert.That(prototype.ZoomSize, Is.GreaterThan(zoomBefore2DWheel));
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
        public IEnumerator LeftDragPansAndFollowKeepsPlayerFramingDuringMovementAndOrbit()
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
                AssertModeFraming(MapOrigin);
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

            var sprite = root.GetComponentInChildren<CubeMascotSprite>().transform;
            Quaternion startHeading = sprite.localRotation;
            Quaternion rightHeading = Quaternion.Euler(0f, 0f, -90f);
            float previousCaptureDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 0.01f;
                prototype.SetMoveInputOverride(Vector2.right);
                bool sawIntermediateHeading = false;
                for (int i = 0; i < 16; i++)
                {
                    yield return null;
                    sawIntermediateHeading |= Quaternion.Angle(sprite.localRotation, startHeading) > 1f &&
                        Quaternion.Angle(sprite.localRotation, rightHeading) > 1f;
                }

                Assert.That(sawIntermediateHeading, Is.True, "A 90-degree turn should pass through intermediate angles.");
                Assert.That(Quaternion.Angle(sprite.localRotation, rightHeading), Is.LessThan(0.5f));
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDelta;
                prototype.SetMoveInputOverride(Vector2.zero);
            }
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
