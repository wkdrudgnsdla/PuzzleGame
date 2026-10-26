using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame.Editor
{
    // Temporary read-only inspection of the open editor scene; never enters Play or runs tests.
    [InitializeOnLoad]
    internal static class HudVisibilitySnapshot
    {
        static HudVisibilitySnapshot() { EditorApplication.delayCall += Capture; }
        private static void Capture()
        {
            var report = new Report { Playing = EditorApplication.isPlaying,
                Screen = Screen.width + "x" + Screen.height,
                Pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                    ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "Built-in" };
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.Cameras.Add(camera.name + " active=" + camera.isActiveAndEnabled + " display=" + camera.targetDisplay
                    + " depth=" + camera.depth + " mask=" + camera.cullingMask + " rect=" + camera.pixelRect);
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = canvas.transform as RectTransform;
                report.Canvases.Add(canvas.name + " scene=" + canvas.gameObject.scene.path + " active=" + canvas.isActiveAndEnabled
                    + " mode=" + canvas.renderMode + " display=" + canvas.targetDisplay + " pixel=" + canvas.pixelRect
                    + " displaySize=" + canvas.renderingDisplaySize + " factor=" + canvas.scaleFactor
                    + " pos=" + root.position + " scale=" + root.lossyScale + " rect=" + root.rect);
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
                {
                    var rect = graphic.rectTransform;
                    report.Graphics.Add(graphic.name + " active=" + graphic.isActiveAndEnabled
                        + " canvas=" + (graphic.canvas != null ? graphic.canvas.name : "none")
                        + " rect=" + rect.rect + " screenCenter=" + RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                        + " scale=" + rect.lossyScale + " culled=" + graphic.canvasRenderer.cull
                        + " alpha=" + graphic.canvasRenderer.GetInheritedAlpha()
                        + " material=" + graphic.materialForRendering.name + " shader=" + graphic.materialForRendering.shader.name);
                }
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/HudVisibility.json"), JsonUtility.ToJson(report, true));
        }
        [System.Serializable] private sealed class Report
        {
            public bool Playing;
            public string Screen, Pipeline;
            public List<string> Cameras = new List<string>();
            public List<string> Canvases = new List<string>();
            public List<string> Graphics = new List<string>();
        }
    }
}
