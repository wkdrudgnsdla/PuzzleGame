using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    [ExecuteAlways]
    public sealed class ProjectionPrototypeHud : MonoBehaviour
    {
        private const string ControlsVisiblePreferenceKey = "PuzzleGame.UI.ControlsVisible";

        [Header("Scene bindings")]
        public ProjectionPrototype Prototype;
        public TMP_Text ModeText, YawText, PitchText, ModeHintText;
        public TMP_Text ControlsText, ClearText;
        public Image ModeBadge, AccentTop;
        public Color ObjectivePendingColor = new Color(0.88f, 0.93f, 0.97f);
        public Color ObjectiveCompleteColor = new Color(0.3f, 1f, 0.58f);
        [Header("Optional mode colors")]
        public bool AutoUpdateColors;
        public Color Mode3DColor = new Color(1f, 0.72f, 0.32f);
        public Color Mode2DColor = new Color(0.25f, 0.91f, 0.95f);
        [Header("Korean font fallback (editor and runtime)")]
        public bool UseKoreanFallbackFont = true;
        public TMP_FontAsset KoreanFont;
        [Header("Controls panel")]
        public GameObject ControlsRoot;
        [Tooltip("Used only when no controls visibility preference has been saved.")]
        public bool ControlsVisibleOnStart = true;

        private readonly List<TMP_Text> fontTargets = new List<TMP_Text>();
        private float nextFontCheck;
        private int cachedYaw = int.MinValue, cachedPitch = int.MinValue;
        private string yawLabel, pitchLabel;
        private bool controlsInitialized;
        public bool ControlsVisible => ControlsRoot != null && ControlsRoot.activeSelf;

        public void Initialize(ProjectionPrototype owner)
        {
            Prototype = owner;
            RepairFallbackFonts();
            ProjectionMinimap.AttachTo(owner);
            ApplyReboundControls();
            RefreshLabels();
        }

        private void OnEnable()
        {
            RepairCollapsedCanvas();
            RepairFallbackFonts();
            controlsInitialized = false;
            InitializeControls();
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
            if (Application.isPlaying)
            {
                InitializeControls();
                if (GameInput.Action("Controls").WasPressedThisFrame())
                    ToggleControls();
                RefreshLabels();
            }
        }

        private void InitializeControls()
        {
            if (controlsInitialized || !Application.IsPlaying(gameObject)) return;
            if (ControlsRoot == null)
            {
                Transform panel = transform.Find("Safe Area/Controls") ?? transform.Find("Controls");
                if (panel != null) ControlsRoot = panel.gameObject;
            }
            if (ControlsRoot == null) return;

            // Restoring a scene must not overwrite the player's saved preference.
            bool visible = PlayerPrefs.GetInt(ControlsVisiblePreferenceKey, ControlsVisibleOnStart ? 1 : 0) != 0;
            ControlsRoot.SetActive(visible);
            controlsInitialized = true;
        }

        private void ApplyReboundControls()
        {
            if (!GameInput.HasOverrides || ControlsRoot == null) return;
            string guide = $"이동  {GameInput.MoveKeys}\n점프  {GameInput.KeyName("Jump")}\n" +
                $"차원 전환  {GameInput.KeyName("SwitchDimension")}\n화면 이동  {GameInput.KeyName("Pan")} 드래그\n" +
                $"회전  {GameInput.KeyName("Orbit")} 드래그 · 휠 줌\n재시작  {GameInput.KeyName("Restart")}";
            Transform panel = ControlsRoot.transform;
            SetText(panel.Find("Controls Close Text")?.GetComponent<TMP_Text>(),
                $"{GameInput.KeyName("Controls")} / {GameInput.KeyName("Controls", 1)} 키로 조작 안내 표시 전환");
            if (ControlsText != null)
            {
                SetText(ControlsText, guide);
                return;
            }

            Transform icons = panel.Find("Key images");
            Transform labels = panel.Find("Controls Text");
            if (icons == null || labels == null) return;
            icons.gameObject.SetActive(false);
            labels.gameObject.SetActive(false);
            var keys = new GameObject("Current Bindings", typeof(RectTransform), typeof(TextMeshProUGUI));
            keys.transform.SetParent(panel, false);
            var rect = (RectTransform)keys.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(16f, -60f);
            rect.offsetMax = new Vector2(-16f, 60f);
            var text = keys.GetComponent<TextMeshProUGUI>();
            text.font = KoreanFont != null ? KoreanFont : TMP_Settings.defaultFontAsset;
            text.fontSize = 14f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 14f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.text = guide;
        }

        public void ToggleControls()
        {
            InitializeControls();
            SetControlsVisible(!ControlsVisible);
        }

        public void SetControlsVisible(bool visible)
        {
            InitializeControls();
            if (ControlsRoot == null) return;
            ControlsRoot.SetActive(visible);
            if (!Application.IsPlaying(gameObject)) return;

            int value = visible ? 1 : 0;
            if (PlayerPrefs.HasKey(ControlsVisiblePreferenceKey) &&
                PlayerPrefs.GetInt(ControlsVisiblePreferenceKey) == value) return;
            PlayerPrefs.SetInt(ControlsVisiblePreferenceKey, value);
            PlayerPrefs.Save();
        }

        public void RefreshLabels()
        {
            if (Prototype == null) return;
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
            ProjectionPuzzleStage stage = Prototype.PuzzleStage;
            int total = stage != null ? stage.RequiredObjectiveCount : 0;
            int completed = stage != null ? stage.CompletedObjectiveCount : 0;
            SetText(ModeHintText, "클리어 조건 (" + completed + "/" + total + ")");
            SetColor(ModeHintText, total > 0 && completed == total
                ? ObjectiveCompleteColor : ObjectivePendingColor);
            if (ClearText != null) ClearText.gameObject.SetActive(Prototype.HasWon);
            if (AutoUpdateColors)
            {
                Color color = Prototype.Is2D ? Mode2DColor : Mode3DColor;
                SetColor(ModeBadge, color);
                SetColor(AccentTop, color);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetColor(Graphic target, Color color)
        {
            if (target != null && target.color != color) target.color = color;
        }

        private void RepairFallbackFonts()
        {
            if (!UseKoreanFallbackFont || KoreanFont == null) return;
            fontTargets.Clear();
            GetComponentsInChildren(true, fontTargets);
            for (int i = 0; i < fontTargets.Count; i++)
            {
                TMP_Text text = fontTargets[i];
                TMP_FontAsset assigned = text.font;
                if (assigned == null || assigned.name.StartsWith("LiberationSans", System.StringComparison.Ordinal))
                    text.font = KoreanFont;
            }
        }

    }
}
