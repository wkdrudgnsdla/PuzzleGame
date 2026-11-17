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
        [Tooltip("Optional TMP label in the HUD prefab for the current campaign stage number.")]
        public TMP_Text StageText;
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
        private bool useCampaignClearDialog;
        private DimensionFailureToasts dimensionFailureToasts;
        private TMP_Text reboundGuide, controlsHint;
        private Transform defaultControlIcons, defaultControlLabels;
        private string defaultGuideText, defaultHintText;
        private bool capturedControlDefaults, showDefaultIcons, showDefaultLabels;
        public bool ControlsVisible => ControlsRoot != null && ControlsRoot.activeSelf;

        public void Initialize(ProjectionPrototype owner)
        {
            Prototype = owner;
            RepairFallbackFonts();
            ProjectionMinimap.AttachTo(owner);
            RefreshControlBindings();
            RefreshLabels();
        }

        internal void ConfigureCampaignStage(int stageNumber)
        {
            useCampaignClearDialog = true;
            if (StageText != null)
                SetText(StageText, stageNumber > 0 ? $"스테이지 {stageNumber}" : string.Empty);
        }

        internal void ShowDimensionFailure(string reason)
        {
            if (dimensionFailureToasts == null) dimensionFailureToasts = new DimensionFailureToasts(this);
            dimensionFailureToasts.Show(reason);
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
                dimensionFailureToasts?.Tick(Time.unscaledDeltaTime);
                InitializeControls();
                if (GameInput.Action("Controls").WasPressedThisFrame())
                {
                    ToggleControls();
                    GameAudioEvents.Play(ControlsVisible ? SoundCue.UiConfirm : SoundCue.UiBack);
                }
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

        internal void RefreshControlBindings()
        {
            if (ControlsRoot == null) return;
            Transform panel = ControlsRoot.transform;
            if (!capturedControlDefaults)
            {
                controlsHint = panel.Find("Controls Close Text")?.GetComponent<TMP_Text>();
                defaultControlIcons = panel.Find("Key images");
                defaultControlLabels = panel.Find("Controls Text");
                defaultGuideText = ControlsText != null ? ControlsText.text : null;
                defaultHintText = controlsHint != null ? controlsHint.text : null;
                showDefaultIcons = defaultControlIcons != null && defaultControlIcons.gameObject.activeSelf;
                showDefaultLabels = defaultControlLabels != null && defaultControlLabels.gameObject.activeSelf;
                capturedControlDefaults = true;
            }

            if (!GameInput.HasOverrides)
            {
                SetText(controlsHint, defaultHintText);
                SetText(ControlsText, defaultGuideText);
                if (reboundGuide != null) reboundGuide.gameObject.SetActive(false);
                if (defaultControlIcons != null) defaultControlIcons.gameObject.SetActive(showDefaultIcons);
                if (defaultControlLabels != null) defaultControlLabels.gameObject.SetActive(showDefaultLabels);
                return;
            }

            string guide = $"이동  {GameInput.MoveKeys}\n점프  {GameInput.KeyName("Jump")}\n" +
                $"차원 전환  {GameInput.KeyName("SwitchDimension")}\n화면 이동  {GameInput.KeyName("Pan")} 드래그\n" +
                $"회전  {GameInput.KeyName("Orbit")} 드래그 · 휠 줌\n재시작  {GameInput.KeyName("Restart")}";
            SetText(controlsHint,
                $"{GameInput.KeyName("Controls")} / {GameInput.KeyName("Controls", 1)} 키로 조작 안내 표시 전환");
            if (ControlsText != null)
            {
                SetText(ControlsText, guide);
                return;
            }

            if (defaultControlIcons == null || defaultControlLabels == null) return;
            defaultControlIcons.gameObject.SetActive(false);
            defaultControlLabels.gameObject.SetActive(false);
            if (reboundGuide == null)
            {
                reboundGuide = OverlayUi.CreateLabel("Current Bindings", panel, KoreanFont, "", 14f, Color.white);
                RectTransform rect = reboundGuide.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.offsetMin = new Vector2(16f, -60f);
                rect.offsetMax = new Vector2(-16f, 60f);
                reboundGuide.enableAutoSizing = true;
                reboundGuide.fontSizeMin = 9f;
                reboundGuide.fontSizeMax = 14f;
                reboundGuide.alignment = TextAlignmentOptions.MidlineLeft;
            }
            reboundGuide.gameObject.SetActive(true);
            SetText(reboundGuide, guide);
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
            if (ClearText != null) ClearText.gameObject.SetActive(Prototype.HasWon && !useCampaignClearDialog);
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
