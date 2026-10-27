#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Opt-in standalone rendering check; never starts during ordinary Play.</summary>
    public sealed class PrototypeCaptureRunner : MonoBehaviour
    {
        private const string CaptureArgument = "-prototypeCapture";
        private const string OutputArgument = "-prototypeCapturePath";
        private static readonly string[] ScreenshotNames =
        {
            "prototype-3d.png",
            "prototype-2d.png",
            "prototype-bridge.png",
            "prototype-return.png"
        };

        private ProjectionPrototype prototype;
        private string outputDirectory;
        private DateTime captureStartedUtc;
        private readonly CaptureReport report = new CaptureReport();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartOnlyWhenRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            bool requested = false;
            string output = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], CaptureArgument, StringComparison.Ordinal))
                    requested = true;
                else if (string.Equals(arguments[i], OutputArgument, StringComparison.Ordinal)
                    && i + 1 < arguments.Length && !arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    output = arguments[++i];
            }

            if (!requested)
                return;

            ProjectionPrototype owner = FindFirstObjectByType<ProjectionPrototype>();
            if (owner == null)
            {
                Debug.LogError("PROTOTYPE CAPTURE FAILED: no ProjectionPrototype exists in the loaded scene.");
                Application.Quit(1);
                return;
            }

            if (owner.GetComponent<PrototypeCaptureRunner>() != null)
                return;

            var runner = owner.gameObject.AddComponent<PrototypeCaptureRunner>();
            runner.prototype = owner;
            runner.outputDirectory = string.IsNullOrWhiteSpace(output)
                ? Path.Combine(Application.persistentDataPath, "PrototypeCapture")
                : output;
        }

        private IEnumerator Start()
        {
            IEnumerator capture = Capture();
            while (true)
            {
                bool hasNext = false;
                object current = null;
                Exception failure = null;
                try
                {
                    hasNext = capture.MoveNext();
                    if (hasNext)
                        current = capture.Current;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                if (failure != null)
                {
                    Fail(failure);
                    yield break;
                }
                if (!hasNext)
                    yield break;

                yield return current;
            }
        }

        private IEnumerator Capture()
        {
            yield return null;
            yield return null;
            Application.runInBackground = true;
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            captureStartedUtc = DateTime.UtcNow;
            report.outputDirectory = outputDirectory;
            report.screenshots = ScreenshotNames;
            prototype.SetMoveInputOverride(Vector2.zero);

            Debug.Log("PROTOTYPE CAPTURE: starting explicit render validation in " + outputDirectory);
            report.viewChanged = prototype.SetViewYaw(45f);
            yield return new WaitForSecondsRealtime(0.3f);
            Screenshot(0);
            yield return new WaitForSecondsRealtime(0.2f);

            report.entered2D = prototype.TryToggleMode();
            yield return new WaitForSecondsRealtime(0.3f);
            report.is2DAfterEntry = prototype.Is2D;
            Screenshot(1);
            yield return new WaitForSecondsRealtime(0.2f);

            prototype.SetMoveInputOverride(Vector2.right);
            yield return new WaitForSecondsRealtime(1.2f);
            prototype.SetMoveInputOverride(Vector2.zero);
            yield return new WaitForSecondsRealtime(0.15f);
            report.bridgeSupportId = prototype.CurrentSupportId;
            report.bridgeProjectedFoot = prototype.PlayerProjectedFoot;
            report.bridgeWorldFoot = prototype.PlayerWorldFoot;
            report.canReturnAtBridge = prototype.CanReturnTo3D;
            Screenshot(2);
            yield return new WaitForSecondsRealtime(0.2f);

            report.returnedTo3D = prototype.TryToggleMode();
            yield return new WaitForSecondsRealtime(0.2f);
            report.finalSupportId = prototype.CurrentSupportId;
            report.finalIs2D = prototype.Is2D;
            report.finalWorldFoot = prototype.PlayerWorldFoot;
            report.finalProjectedFoot = prototype.PlayerProjectedFoot;
            Screenshot(3);
            yield return new WaitForSecondsRealtime(0.3f);

            // CaptureScreenshot writes at the end of the rendered frame. Give slower disks a bounded wait.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!ScreenshotsWritten() && Time.realtimeSinceStartup < deadline)
                yield return null;

            report.screenshotsWritten = ScreenshotsWritten();
            report.success = report.viewChanged && report.entered2D && report.is2DAfterEntry
                && report.returnedTo3D && report.finalSupportId == 2 && !report.finalIs2D
                && report.screenshotsWritten;
            WriteReport();

            if (report.success)
                Debug.Log("PROTOTYPE CAPTURE PASSED: entered 2D, crossed to support B, returned to 3D; PNGs and validation.json saved in " + outputDirectory);
            else
                Debug.LogError("PROTOTYPE CAPTURE FAILED: inspect validation.json in " + outputDirectory);

            prototype.SetMoveInputOverride(null);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit(report.success ? 0 : 1);
        }

        private void Screenshot(int index)
        {
            string path = Path.Combine(outputDirectory, ScreenshotNames[index]);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("PROTOTYPE CAPTURE: requested " + path);
        }

        private bool ScreenshotsWritten()
        {
            for (int i = 0; i < ScreenshotNames.Length; i++)
            {
                string path = Path.Combine(outputDirectory, ScreenshotNames[i]);
                if (!File.Exists(path) || new FileInfo(path).Length == 0
                    || File.GetLastWriteTimeUtc(path) < captureStartedUtc)
                    return false;
            }
            return true;
        }

        private void WriteReport()
        {
            File.WriteAllText(Path.Combine(outputDirectory, "validation.json"), JsonUtility.ToJson(report, true));
        }

        private void Fail(Exception exception)
        {
            report.success = false;
            report.exception = exception.ToString();
            if (prototype != null)
                prototype.SetMoveInputOverride(null);
            try
            {
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                    WriteReport();
                }
            }
            catch (Exception reportException)
            {
                Debug.LogError("PROTOTYPE CAPTURE: could not write the failure report: " + reportException);
            }

            Debug.LogError("PROTOTYPE CAPTURE FAILED: " + exception);
            Application.Quit(1);
        }

        private void OnDestroy()
        {
            if (prototype != null)
                prototype.SetMoveInputOverride(null);
        }

        [Serializable]
        private sealed class CaptureReport
        {
            public bool success;
            public bool viewChanged;
            public bool entered2D;
            public bool is2DAfterEntry;
            public int bridgeSupportId;
            public Vector2 bridgeProjectedFoot;
            public Vector3 bridgeWorldFoot;
            public bool canReturnAtBridge;
            public bool returnedTo3D;
            public int finalSupportId;
            public bool finalIs2D;
            public Vector3 finalWorldFoot;
            public Vector2 finalProjectedFoot;
            public bool screenshotsWritten;
            public string outputDirectory;
            public string[] screenshots;
            public string exception;
        }
    }
}
#endif
