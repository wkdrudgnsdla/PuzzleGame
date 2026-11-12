using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class ProjectionHudLayout : MonoBehaviour
    {
        public RectTransform SafeArea;
        public bool RespectSafeArea = true;

        private CanvasScaler scaler;
        private ProjectionPrototypeHud hud;
        private RectTransform header, minimap, tutorialHint;
        private Vector2 designResolution, headerSize, clearPosition, clearSize, stagePosition, mapPosition;
        private Vector2 lastDisplay;
        private Rect lastSafeArea;

        private void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            hud = GetComponent<ProjectionPrototypeHud>();
            designResolution = scaler.referenceResolution;
            header = SafeArea != null ? SafeArea.Find("Header") as RectTransform : null;
            minimap = SafeArea != null ? SafeArea.Find("Tactical Minimap") as RectTransform : null;
            if (header != null) headerSize = header.sizeDelta;
            if (minimap != null) mapPosition = minimap.anchoredPosition;
            if (hud != null && hud.StageText != null) stagePosition = hud.StageText.rectTransform.anchoredPosition;
            if (hud != null && hud.ClearText != null)
            {
                clearPosition = hud.ClearText.rectTransform.anchoredPosition;
                clearSize = hud.ClearText.rectTransform.sizeDelta;
            }
            lastDisplay = Vector2.zero;
            RefreshLayout();
        }

        private void LateUpdate() => RefreshLayout();

        internal void SetTutorialHint(TMP_Text hint)
        {
            if (SafeArea == null || hint == null) return;
            tutorialHint = hint.rectTransform;
            tutorialHint.SetParent(SafeArea, false);
            lastDisplay = Vector2.zero;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (SafeArea == null || scaler == null || !Application.IsPlaying(gameObject)) return;
            Vector2 display = new Vector2(Screen.width, Screen.height);
            if (display.x <= 0f || display.y <= 0f) return;
            Rect safe = ResponsiveCanvas.GetSafeArea(display, RespectSafeArea);
            if (display == lastDisplay && safe == lastSafeArea) return;
            lastDisplay = display;
            lastSafeArea = safe;

            bool compact = safe.width / safe.height < 1.35f;
            Vector2 design = compact ? new Vector2(780f, designResolution.y) : designResolution;
            ResponsiveCanvas.Fit(scaler, SafeArea, display, safe, design);
            ApplyPanels(compact);
        }

        private void ApplyPanels(bool compact)
        {
            if (header != null) header.sizeDelta = compact ? new Vector2(-48f, 122f) : headerSize;
            if (hud != null && hud.ClearText != null)
            {
                RectTransform clear = hud.ClearText.rectTransform;
                clear.anchoredPosition = compact ? new Vector2(18f, -74f) : clearPosition;
                clear.sizeDelta = compact ? new Vector2(-36f, 34f) : clearSize;
            }
            if (hud != null && hud.StageText != null)
                hud.StageText.rectTransform.anchoredPosition = stagePosition + (compact ? Vector2.down * 38f : Vector2.zero);
            if (minimap != null)
                minimap.anchoredPosition = compact ? new Vector2(mapPosition.x, -164f) : mapPosition;
            if (tutorialHint != null)
            {
                float top = compact ? -164f : -124f;
                tutorialHint.offsetMin = new Vector2(32f, top - 102f);
                tutorialHint.offsetMax = new Vector2(-284f, top);
            }
        }

        private void OnDisable()
        {
            if (!Application.IsPlaying(gameObject) || scaler == null) return;
            scaler.referenceResolution = designResolution;
            ApplyPanels(false);
        }
    }
}
