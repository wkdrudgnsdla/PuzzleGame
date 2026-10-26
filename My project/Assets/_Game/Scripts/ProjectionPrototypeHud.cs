using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    [ExecuteAlways]
    public sealed class ProjectionPrototypeHud : MonoBehaviour
    {
        [Header("Scene bindings")]
        public ProjectionPrototype Prototype;
        public Text ModeText, YawText, PitchText, SupportText, ModeHintText;
        public Image ModeBadge, AccentTop;
        [Header("Optional mode colors")]
        public bool AutoUpdateColors;
        public Color Mode3DColor = new Color(1f, 0.72f, 0.32f);
        public Color Mode2DColor = new Color(0.25f, 0.91f, 0.95f);
        [Header("Font preview")]
        public bool UseKoreanFallbackFont = true;

        private readonly List<Text> fontTargets = new List<Text>();
        private Font fallbackFont;
        private float nextFontCheck;
        private int cachedYaw = int.MinValue, cachedPitch = int.MinValue;
        private string yawLabel, pitchLabel;

        public void Initialize(ProjectionPrototype owner)
        {
            Prototype = owner;
            RepairFallbackFonts();
            if (Application.isPlaying) UpdateLabels();
        }

        private void OnEnable()
        {
            RepairCollapsedCanvas();
            RepairFallbackFonts();
        }

        private void RepairCollapsedCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            RectTransform rect = transform as RectTransform;
            if (canvas == null || rect == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
            // Recover invalid root geometry without changing any child UI layout.
            Vector3 scale = rect.localScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) ||
                Mathf.Approximately(scale.z, 0f)) rect.localScale = Vector3.one;
            if (rect.rect.width <= 0f || rect.rect.height <= 0f)
            {
                CanvasScaler scaler = GetComponent<CanvasScaler>();
                Vector2 size = scaler != null ? scaler.referenceResolution : new Vector2(1280f, 720f);
                size.x = Mathf.Max(1f, size.x);
                size.y = Mathf.Max(1f, size.y);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = size * 0.5f;
            }
        }

        private void OnValidate()
        {
            nextFontCheck = 0f;
            cachedYaw = cachedPitch = int.MinValue;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup >= nextFontCheck)
            {
                nextFontCheck = Time.realtimeSinceStartup + 1f;
                RepairFallbackFonts();
            }
            if (Application.isPlaying && Prototype != null) UpdateLabels();
        }

        private void UpdateLabels()
        {
            SetText(ModeText, Prototype.Is2D ? "2D" : "3D");
            int yaw = Mathf.RoundToInt(Prototype.YawDegrees * 10f);
            if (yaw != cachedYaw)
            {
                cachedYaw = yaw;
                yawLabel = "좌우 " + (yaw * 0.1f).ToString("F1") + "°";
            }
            int pitch = Mathf.RoundToInt(Prototype.PitchDegrees * 10f);
            if (pitch != cachedPitch)
            {
                cachedPitch = pitch;
                pitchLabel = "상하 " + (pitch * 0.1f).ToString("F1") + "°";
            }
            SetText(YawText, yawLabel);
            SetText(PitchText, pitchLabel);
            SetText(SupportText, SupportName());
            SetText(ModeHintText, Prototype.Is2D
                ? (Prototype.IsTopDown2D ? "탑뷰 · " : "횡스크롤 · ")
                    + (Prototype.CanReturnTo3D ? "3D 복귀 가능" : "3D 복귀 위치를 찾으세요")
                : (Prototype.IsTopDownView ? "R → 탑뷰 2D (WASD)" : "R → 횡스크롤 2D (A/D)"));
            if (ModeHintText != null && Prototype.Route != null)
                SetText(ModeHintText, ModeHintText.text + " · " + Prototype.Route.ProgressHint);
            if (AutoUpdateColors)
            {
                Color color = Prototype.Is2D ? Mode2DColor : Mode3DColor;
                SetColor(ModeBadge, color);
                SetColor(AccentTop, color);
            }
        }

        private string SupportName()
        {
            var terrain = Prototype.Terrain;
            if (terrain != null)
                for (int i = 0; i < terrain.Count; i++)
                    if (terrain[i] != null && terrain[i].Id == Prototype.CurrentSupportId)
                        return terrain[i].gameObject.name;
            return "없음";
        }

        private static void SetText(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetColor(Image target, Color color)
        {
            if (target != null && target.color != color) target.color = color;
        }

        private void RepairFallbackFonts()
        {
            if (!UseKoreanFallbackFont) return;
            fontTargets.Clear();
            GetComponentsInChildren(true, fontTargets);
            for (int i = 0; i < fontTargets.Count; i++)
            {
                Text text = fontTargets[i];
                Font assigned = text.font;
                if (assigned != null && assigned != fallbackFont && !IsDefaultFont(assigned)) continue;
                if (fallbackFont == null)
                {
                    fallbackFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial" }, 20);
                    if (fallbackFont == null) return;
                    fallbackFont.hideFlags = HideFlags.HideAndDontSave;
                }
                if (text.font != fallbackFont) text.font = fallbackFont;
            }
        }

        private static bool IsDefaultFont(Font value)
        {
            return value == Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnDestroy()
        {
            if (fallbackFont == null) return;
            if (Application.isPlaying) Destroy(fallbackFont);
            else DestroyImmediate(fallbackFont);
        }
    }
}
