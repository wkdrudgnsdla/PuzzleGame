using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal static class ResponsiveCanvas
    {
        internal static Rect GetSafeArea(Vector2 display, bool respectSafeArea)
        {
            Rect full = new Rect(Vector2.zero, display);
            if (!respectSafeArea) return full;
            Rect safe = Screen.safeArea;
            Vector2 minimum = Vector2.Max(full.min, safe.min);
            Vector2 maximum = Vector2.Min(full.max, safe.max);
            return maximum.x > minimum.x && maximum.y > minimum.y
                ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y)
                : full;
        }

        internal static void Fit(CanvasScaler scaler, RectTransform root, Vector2 display, Rect safe, Vector2 design)
        {
            Vector2 fraction = new Vector2(safe.width / display.x, safe.height / display.y);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(design.x / fraction.x, design.y / fraction.y);
            scaler.GetComponent<Canvas>().scaleFactor = Mathf.Min(safe.width / design.x, safe.height / design.y);
            root.anchorMin = new Vector2(safe.xMin / display.x, safe.yMin / display.y);
            root.anchorMax = new Vector2(safe.xMax / display.x, safe.yMax / display.y);
            root.offsetMin = root.offsetMax = Vector2.zero;
        }
    }
}
