using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PuzzleGame.Tests
{
    public sealed class ProjectionPrototypePlayModeTests
    {
        private ProjectionPrototype prototype;
        private readonly WaitForFixedUpdate fixedUpdate = new WaitForFixedUpdate();
        private float previousFixedDeltaTime;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousTimeScale = Time.timeScale;
            Time.fixedDeltaTime = 0.02f;
            Time.timeScale = 1f;

            // A saved prototype scene can already be open when the Test Runner starts.
            // Remove that instance first so two generated physics worlds cannot overlap.
            foreach (var existing in Object.FindObjectsByType<ProjectionPrototype>(FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;

            var root = new GameObject("Projection Prototype Test");
            root.SetActive(false);
            prototype = root.AddComponent<ProjectionPrototype>();
            yield return ProjectionTestWorld.AssignPlayerPrefabs(prototype);
            ProjectionTestWorld.SetInitialFrontView(prototype);
            prototype.MapRoot = new GameObject("Map").transform;
            prototype.MapRoot.SetParent(root.transform, false);
            CreatePlatform(1, new Vector3(0f, -0.5f, 0f));
            CreatePlatform(2, new Vector3(8f, -0.5f, 4f));
            root.SetActive(true);
            yield return null;
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;

            Check(prototype.IsGrounded, Is.True, "The player must settle on platform A before each test.");
        }

        private void CreatePlatform(int id, Vector3 center)
        {
            var platform = new GameObject("Platform " + id);
            platform.transform.SetParent(prototype.MapRoot, false);
            platform.transform.localPosition = center;
            platform.transform.localScale = new Vector3(4f, 1f, 3f);
            platform.AddComponent<BoxCollider>();
            platform.AddComponent<ProjectableBox>().Initialize(id, Color.white);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (prototype != null)
                Object.Destroy(prototype.gameObject);
            yield return null;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator StartsIn3DAndRoundTripsOnTheSamePlatform()
        {
            Check(prototype.Is2D, Is.False);
            Check(prototype.YawDegrees, Is.EqualTo(0f).Within(0.01f));
            Check(prototype.CurrentSupportId, Is.EqualTo(1));
            Check(Vector3.Distance(prototype.PlayerWorldFoot, Vector3.zero), Is.LessThan(0.08f));

            var originalFoot = prototype.PlayerWorldFoot;
            CaptureScreenshot("prototype-3d.png");
            yield return null;
            yield return null;
            yield return null;

            Check(prototype.TryToggleMode(), Is.True);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null; // Let the rendered-frame support resolver observe the physics result.
            Check(prototype.Is2D, Is.True);
            Check(prototype.CanReturnTo3D, Is.True);

            yield return WaitForDimensionSwitchCooldown();
            Check(prototype.TryToggleMode(), Is.True);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;
            Check(prototype.Is2D, Is.False);
            Check(prototype.CurrentSupportId, Is.EqualTo(1));
            Check(Vector3.Distance(prototype.PlayerWorldFoot, originalFoot), Is.LessThan(0.08f));
        }

        [UnityTest]
        public IEnumerator SwitchDimensionKeySwitchesInto2DAndBackInto3D()
        {
            var settings = InputSystem.settings;
            var previousBackgroundBehavior = settings.backgroundBehavior;
            var previousEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
            var previousKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            try
            {
                // Batch Test Runner has no focused Game View. Only this input-path test
                // bypasses focus routing; the project's normal input settings are preserved.
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>("Projection Test Keyboard");
                InputSystem.EnableDevice(keyboard);
                keyboard.MakeCurrent();
                yield return null;
                yield return null;
                Check(prototype.Is2D, Is.False);

                KeyControl switchKey = null;
                foreach (var control in GameInput.Action("SwitchDimension").controls)
                {
                    if (control is KeyControl key && key.device == keyboard)
                    {
                        switchKey = key;
                        break;
                    }
                }
                Check(switchKey, Is.Not.Null, "The switch action must have a binding on the test keyboard.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(switchKey.keyCode));
                yield return null;
                yield return null;
                Check(prototype.Is2D, Is.True,
                    "Pressing the switch key through the Input System must activate 2D mode.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForDimensionSwitchCooldown();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(switchKey.keyCode));
                yield return null;
                yield return null;
                Check(prototype.Is2D, Is.False,
                    "A second press after release and cooldown must return to 3D mode.");
                Check(prototype.CurrentSupportId, Is.EqualTo(1));
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                    InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added)
                    previousKeyboard.MakeCurrent();
                settings.backgroundBehavior = previousBackgroundBehavior;
                settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            }
        }

        [UnityTest]
        public IEnumerator DimensionSwitchCooldownTakesPriorityOverAirborneRejection()
        {
            Check(prototype.TryToggleMode(), Is.True);
            Check(prototype.DimensionSwitchCooldownRemaining, Is.EqualTo(0.7f).Within(0.02f));
            Check(prototype.TryToggleMode(), Is.False);
            Check(prototype.Is2D, Is.True);
            Check(prototype.StatusMessage, Does.StartWith("2D → 3D 전환 쿨타임이 "));

            yield return fixedUpdate;
            yield return fixedUpdate;
            prototype.SetJumpInputOverride(true);
            yield return null;
            yield return fixedUpdate;
            yield return null;

            Check(prototype.IsGrounded, Is.False);
            float remainingBeforeReject = prototype.DimensionSwitchCooldownRemaining;
            Check(remainingBeforeReject, Is.GreaterThan(0f));
            Check(prototype.TryToggleMode(), Is.False);
            Check(prototype.StatusMessage, Does.Match(@"^2D → 3D 전환 쿨타임이 \d+[.,]\d초 남았습니다\.$"));
            Check(prototype.DimensionSwitchCooldownRemaining, Is.EqualTo(remainingBeforeReject));

            yield return WaitForDimensionSwitchCooldown();
            float landingDeadline = Time.realtimeSinceStartup + 0.8f;
            while (!prototype.IsGrounded && Time.realtimeSinceStartup < landingDeadline) yield return null;
            Check(prototype.IsGrounded, Is.True);
            Check(prototype.TryToggleMode(), Is.True);
            Check(prototype.TryToggleMode(), Is.False);
            Check(prototype.StatusMessage, Does.StartWith("3D → 2D 전환 쿨타임이 "));
        }

        [UnityTest]
        public IEnumerator TenRepeatedRoundTripsDoNotAccumulateCoordinateDrift()
        {
            var originalFoot = prototype.PlayerWorldFoot;
            var originalProjectedFoot = prototype.PlayerProjectedFoot;

            for (int cycle = 0; cycle < 10; cycle++)
            {
                yield return WaitForDimensionSwitchCooldown();
                Check(prototype.TryToggleMode(), Is.True, "Could not enter 2D on cycle " + cycle);
                yield return fixedUpdate;
                yield return fixedUpdate;
                yield return null;
                Check(prototype.Is2D, Is.True);
                Check(prototype.CurrentSupportId, Is.EqualTo(1));
                Check(Vector2.Distance(prototype.PlayerProjectedFoot, originalProjectedFoot),
                    Is.LessThan(0.08f), "Projected coordinates drifted on cycle " + cycle);

                yield return WaitForDimensionSwitchCooldown();
                Check(prototype.TryToggleMode(), Is.True, "Could not return to 3D on cycle " + cycle);
                yield return fixedUpdate;
                yield return fixedUpdate;
                yield return null;
                Check(prototype.Is2D, Is.False);
                Check(prototype.CurrentSupportId, Is.EqualTo(1));
                Check(Vector3.Distance(prototype.PlayerWorldFoot, originalFoot),
                    Is.LessThan(0.08f), "World coordinates drifted on cycle " + cycle);
            }
        }

        [UnityTest]
        public IEnumerator CameraViewCannotChangeWhilePlayingIn2D()
        {
            Check(prototype.SetViewYaw(45f), Is.True);
            yield return new WaitForSeconds(0.15f);
            Check(prototype.TryToggleMode(), Is.True);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;

            var footBefore = prototype.PlayerProjectedFoot;
            Check(prototype.SetViewYaw(0f), Is.False);
            Check(prototype.Is2D, Is.True);
            Check(prototype.YawDegrees, Is.EqualTo(45f).Within(0.01f));
            Check(Vector2.Distance(prototype.PlayerProjectedFoot, footBefore), Is.LessThan(0.01f));

            CaptureScreenshot("prototype-2d.png");
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrontViewLeavesAGapAndRejectsAnAirborneReturn()
        {
            Check(prototype.TryToggleMode(), Is.True);
            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSeconds(0.95f);
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return fixedUpdate;
            yield return null;

            Check(prototype.Is2D, Is.True);
            Check(prototype.IsGrounded, Is.False);
            Check(prototype.PlayerProjectedFoot.y, Is.LessThan(-0.1f));
            Check(prototype.CanReturnTo3D, Is.False);

            var footBefore = prototype.PlayerProjectedFoot;
            Check(prototype.TryToggleMode(), Is.False);
            Check(prototype.Is2D, Is.True);
            Check(Vector2.Distance(prototype.PlayerProjectedFoot, footBefore), Is.LessThan(0.001f),
                "A failed transition must not alter player coordinates.");
        }

        [UnityTest]
        public IEnumerator AngledProjectionConnectsThePlatformsAndReturnsOnPlatformB()
        {
            Check(prototype.SetViewYaw(45f), Is.True);
            yield return new WaitForSeconds(0.15f);
            Check(prototype.TryToggleMode(), Is.True);
            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSeconds(1.2f);
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;

            Check(prototype.Is2D, Is.True);
            Check(prototype.IsGrounded, Is.True);
            Check(prototype.CurrentSupportId, Is.EqualTo(2),
                "Walking beyond A's silhouette must transfer support to B.");
            Check(prototype.CanReturnTo3D, Is.True);
            var footIn2D = prototype.PlayerProjectedFoot;

            Check(prototype.TryToggleMode(), Is.True);
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;

            Check(prototype.Is2D, Is.False);
            Check(prototype.CurrentSupportId, Is.EqualTo(2));
            var returnedFoot = prototype.PlayerWorldFoot;
            Check(returnedFoot.x, Is.InRange(6.29f, 9.71f));
            Check(returnedFoot.z, Is.InRange(2.79f, 5.21f));
            Check(returnedFoot.y, Is.EqualTo(0f).Within(0.08f));

            var projectedReturn = new Vector2(
                returnedFoot.x * Mathf.Cos(Mathf.PI / 4f) - returnedFoot.z * Mathf.Sin(Mathf.PI / 4f),
                returnedFoot.y);
            Check(Vector2.Distance(projectedReturn, footIn2D), Is.LessThan(0.08f),
                "Returning to 3D must preserve the visible projected position.");
        }

        [UnityTest]
        public IEnumerator ResetRestoresTheStartingWorldAndView()
        {
            Check(prototype.SetViewYaw(45f), Is.True);
            yield return new WaitForSeconds(0.15f);
            Check(prototype.TryToggleMode(), Is.True);
            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSeconds(0.4f);
            prototype.SetMoveInputOverride(Vector2.zero);

            prototype.ResetPrototype();
            yield return fixedUpdate;
            yield return fixedUpdate;
            yield return null;

            Check(prototype.Is2D, Is.False);
            Check(prototype.YawDegrees, Is.EqualTo(0f).Within(0.01f));
            Check(prototype.CurrentSupportId, Is.EqualTo(1));
            Check(prototype.HasWon, Is.False);
            Check(Vector3.Distance(prototype.PlayerWorldFoot, Vector3.zero), Is.LessThan(0.08f));
        }

        [UnityTest]
        public IEnumerator JumpIn3DRejectsConversionUntilLanding()
        {
            prototype.SetJumpInputOverride(true);
            yield return new WaitForSeconds(0.12f);
            Check(prototype.IsGrounded, Is.False);
            Check(prototype.PlayerWorldFoot.y, Is.GreaterThan(0.1f));

            var footBefore = prototype.PlayerWorldFoot;
            Check(prototype.TryToggleMode(), Is.False);
            Check(prototype.Is2D, Is.False);
            Check(Vector3.Distance(prototype.PlayerWorldFoot, footBefore), Is.LessThan(0.001f));

            yield return new WaitForSeconds(0.65f);
            Check(prototype.IsGrounded, Is.True);
            Check(prototype.TryToggleMode(), Is.True);
            Check(prototype.Is2D, Is.True);
        }

        private WaitUntil WaitForDimensionSwitchCooldown()
            => new WaitUntil(() => prototype.DimensionSwitchCooldownRemaining <= 0f);

        private void Check<T>(T actual, NUnit.Framework.Constraints.IResolveConstraint constraint, string context = null)
        {
            string diagnostics = prototype == null ? "Prototype instance is missing." :
                "mode=" + (prototype.Is2D ? "2D" : "3D") +
                ", grounded=" + prototype.IsGrounded +
                ", support=" + prototype.CurrentSupportId +
                ", worldFoot=" + prototype.PlayerWorldFoot.ToString("F4") +
                ", projectedFoot=" + prototype.PlayerProjectedFoot.ToString("F4") +
                ", yaw=" + prototype.YawDegrees +
                ", canReturn=" + prototype.CanReturnTo3D +
                ", switchCooldown=" + prototype.DimensionSwitchCooldownRemaining +
                ", status=" + prototype.StatusMessage;
            var keyboard = Keyboard.current;
            diagnostics += "; keyboard=" + (keyboard != null ? keyboard.name : "none") +
                ", enabled=" + (keyboard != null && keyboard.enabled) +
                ", focused=" + Application.isFocused;
            Assert.That(actual, constraint, (context ?? "Prototype state assertion failed.") + "\n" + diagnostics);
        }

        private static void CaptureScreenshot(string fileName)
        {
            // Do not wait for EndOfFrame: batch-mode Test Runner does not guarantee it.
            // Screenshots aid visual review; inability to capture is not a gameplay failure.
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var directory = Path.Combine(projectRoot, "Builds", "Validation");
                Directory.CreateDirectory(directory);
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, fileName));
            }
            catch (System.Exception exception)
            {
                Debug.Log("Optional prototype screenshot skipped: " + exception.Message);
            }
        }
    }
}
