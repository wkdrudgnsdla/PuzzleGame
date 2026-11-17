using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace PuzzleGame
{
    internal sealed class DimensionFailureToasts
    {
        private const float Height = 56f;
        private const float Spacing = 62f;
        private const float HoldSeconds = 1.15f;
        private const float FadeSeconds = 1.65f;

        private readonly RectTransform stack;
        private readonly TMP_FontAsset font;
        private readonly ObjectPool<Toast> pool;
        private readonly List<Toast> active = new List<Toast>();

        internal DimensionFailureToasts(ProjectionPrototypeHud hud)
        {
            var layout = hud.GetComponent<ProjectionHudLayout>();
            Transform parent = layout != null && layout.SafeArea != null ? layout.SafeArea : hud.transform;
            stack = new GameObject("Dimension Failure Toasts", typeof(RectTransform)).GetComponent<RectTransform>();
            stack.gameObject.layer = parent.gameObject.layer;
            stack.SetParent(parent, false);
            OverlayUi.Stretch(stack);
            font = hud.KoreanFont;
            pool = new ObjectPool<Toast>(Create, actionOnRelease: toast => toast.Rect.gameObject.SetActive(false),
                actionOnDestroy: toast => Object.Destroy(toast.Rect.gameObject), defaultCapacity: 8, maxSize: 32);
        }

        internal void Show(string reason)
        {
            Toast toast = pool.Get();
            toast.Label.text = reason;
            toast.Age = 0f;
            toast.Canvas.alpha = 1f;
            toast.Rect.sizeDelta = new Vector2(AvailableWidth, Height);
            toast.Rect.anchoredPosition = new Vector2(0f, -14f);
            toast.Rect.SetAsLastSibling();
            toast.Rect.gameObject.SetActive(true);
            active.Add(toast);
        }

        internal void Tick(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Toast toast = active[i];
                toast.Age += deltaTime;
                if (toast.Age < HoldSeconds + FadeSeconds) continue;
                active.RemoveAt(i);
                pool.Release(toast);
            }

            if (active.Count == 0) return;
            float availableHeight = Mathf.Max(0f, stack.rect.height * 0.57f - Height);
            float step = Mathf.Min(Spacing, availableHeight / Mathf.Max(1, active.Count - 1));
            float blend = 1f - Mathf.Exp(-14f * deltaTime);
            float width = AvailableWidth;
            for (int i = 0; i < active.Count; i++)
            {
                Toast toast = active[i];
                float fade = (toast.Age - HoldSeconds) / FadeSeconds;
                toast.Canvas.alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);
                float target = (active.Count - 1 - i) * step;
                float y = Mathf.Lerp(toast.Rect.anchoredPosition.y, target, blend);
                toast.Rect.anchoredPosition = new Vector2(0f, y);
                if (!Mathf.Approximately(toast.Rect.sizeDelta.x, width))
                    toast.Rect.sizeDelta = new Vector2(width, Height);
            }
        }

        private float AvailableWidth => Mathf.Clamp(stack.rect.width - 32f, 0f, 520f);

        private Toast Create()
        {
            var background = OverlayUi.CreateImage("Dimension Switch Failure", stack, new Color32(48, 31, 36, 245));
            background.raycastTarget = false;
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.23f);

            var canvas = rect.gameObject.AddComponent<CanvasGroup>();
            canvas.interactable = false;
            canvas.blocksRaycasts = false;

            TMP_Text label = OverlayUi.CreateLabel("Reason", rect, font, string.Empty, 20f, OverlayUi.Text);
            OverlayUi.Stretch(label.rectTransform);
            label.margin = new Vector4(16f, 4f, 16f, 4f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            return new Toast(rect, canvas, label);
        }

        private sealed class Toast
        {
            internal readonly RectTransform Rect;
            internal readonly CanvasGroup Canvas;
            internal readonly TMP_Text Label;
            internal float Age;

            internal Toast(RectTransform rect, CanvasGroup canvas, TMP_Text label)
            {
                Rect = rect;
                Canvas = canvas;
                Label = label;
            }
        }
    }
}
