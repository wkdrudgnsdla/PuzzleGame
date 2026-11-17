using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleGame
{
    [RequireComponent(typeof(MainMenuController))]
    public sealed class MainMenuLayout : MonoBehaviour
    {
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RawImage artwork;
        [SerializeField] private TMP_Text homeTitle, homeHint;
        [SerializeField] private RectTransform settingsCard, settingsTitle, volumeLabel, volumeTrack, divider, bindingsTitle;
        [SerializeField] private RectTransform sensitivityLabel, sensitivityTrack;
        [SerializeField] private RectTransform[] bindingLabels;
        [SerializeField] private RectTransform stageLayout, stageGrid;
        [SerializeField] private RectTransform[] stageFooter;

        private readonly Dictionary<RectTransform, RectPlacement> wideLayout = new();
        private readonly Dictionary<Selectable, Navigation> wideNavigation = new();
        private MainMenuController menu;
        private TextAlignmentOptions wideTitleAlignment, wideHintAlignment;
        private float wideTutorialFontSize;
        private bool compact;
        private Vector2 lastDisplay;
        private Rect lastSafeArea;

        private void Awake()
        {
            menu = GetComponent<MainMenuController>();
            wideTitleAlignment = homeTitle.alignment;
            wideHintAlignment = homeHint.alignment;
            wideTutorialFontSize = menu.Stages[0].Label.fontSize;
            OverlayUi.Link(menu.VolumeSlider, null, menu.CameraSensitivitySlider);
            OverlayUi.Link(menu.CameraSensitivitySlider, menu.VolumeSlider, menu.Bindings[0].Button);
            RefreshLayout();
        }

        private void LateUpdate() => RefreshLayout();

        private void RefreshLayout()
        {
            Vector2 display = new Vector2(Screen.width, Screen.height);
            if (display.x <= 0f || display.y <= 0f) return;
            Rect safe = ResponsiveCanvas.GetSafeArea(display, true);
            if (display == lastDisplay && safe == lastSafeArea) return;
            lastDisplay = display;
            lastSafeArea = safe;

            float aspect = safe.width / safe.height;
            bool useCompactLayout = aspect < 1.25f;
            float wideWidth = Mathf.Lerp(1560f, 1920f, Mathf.InverseLerp(1.25f, 16f / 9f, aspect));
            ResponsiveCanvas.Fit(scaler, safeArea, display, safe,
                new Vector2(useCompactLayout ? 760f : wideWidth, 1080f));
            LayoutArtwork(display, useCompactLayout);
            if (useCompactLayout == compact) return;

            compact = useCompactLayout;
            if (compact)
            {
                LayoutHome();
                LayoutSettings();
                LayoutStageGrid();
            }
            else
            {
                foreach (var entry in wideLayout) entry.Value.Apply(entry.Key);
                foreach (var entry in wideNavigation) entry.Key.navigation = entry.Value;
                homeTitle.alignment = wideTitleAlignment;
                homeHint.alignment = wideHintAlignment;
                menu.Stages[0].Label.fontSize = wideTutorialFontSize;
            }
        }

        private void LayoutArtwork(Vector2 display, bool narrow)
        {
            float bottom = narrow ? 0.42f : 0f;
            RectTransform rect = artwork.rectTransform;
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            Texture texture = artwork.texture;
            if (texture == null) return;
            float textureAspect = (float)texture.width / texture.height;
            float regionAspect = display.x / (display.y * (1f - bottom));
            float width = Mathf.Min(1f, regionAspect / textureAspect);
            float height = Mathf.Min(1f, textureAspect / regionAspect);
            float focus = narrow ? 0.68f : 0.5f;
            float x = Mathf.Clamp(focus - width * 0.5f, 0f, 1f - width);
            artwork.uvRect = new Rect(x, (1f - height) * 0.5f, width, height);
        }

        private void LayoutHome()
        {
            var anchor = new Vector2(0.5f, 0.565f);
            var topCenter = new Vector2(0.5f, 1f);
            Place(homeTitle.rectTransform, Vector2.zero, homeTitle.rectTransform.sizeDelta, anchor, anchor, topCenter);
            homeTitle.alignment = TextAlignmentOptions.Center;
            PlaceHomeButton(menu.StartButton, -136f, anchor, topCenter);
            PlaceHomeButton(menu.ContinueButton, -214f, anchor, topCenter);
            Place(menu.ContinueStageLabel.rectTransform, new Vector2(214f, -214f), new Vector2(144f, 74f),
                anchor, anchor, new Vector2(0f, 1f));
            PlaceHomeButton(menu.StageSelectButton, -292f, anchor, topCenter);
            PlaceHomeButton(menu.SettingsButton, -370f, anchor, topCenter);
            PlaceHomeButton(menu.QuitButton, -448f, anchor, topCenter);
            var bottomCenter = new Vector2(0.5f, 0f);
            Place(homeHint.rectTransform, new Vector2(0f, 45f), homeHint.rectTransform.sizeDelta,
                bottomCenter, bottomCenter, bottomCenter);
            homeHint.alignment = TextAlignmentOptions.Center;
        }

        private void PlaceHomeButton(Button button, float y, Vector2 anchor, Vector2 pivot)
            => Place((RectTransform)button.transform, new Vector2(0f, y), new Vector2(404f, 74f), anchor, anchor, pivot);

        private void LayoutSettings()
        {
            Resize(settingsCard, 680f, 1050f);
            Place(settingsTitle, 30f, -22f, 240f, 66f);
            Place(volumeLabel, 30f, -103f, 150f, 44f);
            Place((RectTransform)menu.VolumeSlider.transform, 190f, -103f, 350f, 44f);
            Resize(volumeTrack, 350f, 8f);
            Place(menu.VolumeValue.rectTransform, 548f, -103f, 102f, 44f);
            Place(sensitivityLabel, 30f, -147f, 150f, 44f);
            Place((RectTransform)menu.CameraSensitivitySlider.transform, 190f, -147f, 350f, 44f);
            Resize(sensitivityTrack, 350f, 8f);
            Place(menu.CameraSensitivityValue.rectTransform, 548f, -147f, 102f, 44f);
            Place(divider, 30f, -206f, 620f, 1f);
            Place(bindingsTitle, 30f, -218f, 300f, 48f);
            for (int i = 0; i < menu.Bindings.Length; i++)
            {
                SettingsBindingView row = menu.Bindings[i];
                float y = -274f - i * 49f;
                Place(bindingLabels[i], 30f, y, 400f, 42f);
                Place((RectTransform)row.Button.transform, 442f, y, 208f, 42f);
                Resize(row.Value.rectTransform, 208f, 42f);
                Selectable previous = i == 0 ? menu.CameraSensitivitySlider : menu.Bindings[i - 1].Button;
                Selectable next = i == menu.Bindings.Length - 1 ? menu.BackButton : menu.Bindings[i + 1].Button;
                SetNavigation(row.Button, previous, next);
            }
            Place(menu.BindingStatus.rectTransform, 30f, -914f, 620f, 42f);
            Place((RectTransform)menu.ResetBindingsButton.transform, 30f, -967f, 246f, 52f);
            Place((RectTransform)menu.BackButton.transform, 461f, -967f, 189f, 52f);
            Button last = menu.Bindings[menu.Bindings.Length - 1].Button;
            SetNavigation(menu.ResetBindingsButton, last, null, right: menu.BackButton);
            SetNavigation(menu.BackButton, last, null, left: menu.ResetBindingsButton);
        }

        private void LayoutStageGrid()
        {
            const int columns = 2;
            Resize(stageLayout, 700f, 990f);
            Place(stageGrid, 0f, -100f, 700f, 820f);
            Place((RectTransform)menu.StageSelectBackButton.transform, 561f, -11f, 139f, 44f);
            menu.Stages[0].Label.fontSize = 38f;
            for (int i = 0; i < menu.Stages.Length; i++)
            {
                int column = i % columns;
                MainMenuController.StageView stage = menu.Stages[i];
                Place((RectTransform)stage.Button.transform, column * 359f, -(i / columns) * 103f, 341f, 87f);
                Resize(stage.Label.rectTransform, 341f, 87f);
                SetNavigation(stage.Button, StageButton(i - columns), StageButton(i + columns),
                    column == 0 ? null : StageButton(i - 1), column == columns - 1 ? null : StageButton(i + 1));
            }
            foreach (RectTransform item in stageFooter)
            {
                // Keep the two key groups against the same sides as in the wide layout.
                float wideX = item.anchoredPosition.x;
                Place(item, wideX > 700f ? wideX - 740f : wideX, -938f, item.sizeDelta.x, item.sizeDelta.y);
            }
        }

        private Button StageButton(int index)
            => index >= 0 && index < menu.Stages.Length ? menu.Stages[index].Button : null;

        private void SetNavigation(Selectable item, Selectable up, Selectable down,
            Selectable left = null, Selectable right = null)
        {
            wideNavigation.TryAdd(item, item.navigation);
            OverlayUi.Link(item, up, down, left, right);
        }

        private void Place(RectTransform rect, float x, float y, float width, float height)
            => Place(rect, new Vector2(x, y), new Vector2(width, height), rect.anchorMin, rect.anchorMax, rect.pivot);

        private void Resize(RectTransform rect, float width, float height)
            => Place(rect, new Vector2(width, height));

        private void Place(RectTransform rect, Vector2 size)
            => Place(rect, rect.anchoredPosition, size, rect.anchorMin, rect.anchorMax, rect.pivot);

        private void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            wideLayout.TryAdd(rect, new RectPlacement(rect));
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private readonly struct RectPlacement
        {
            private readonly Vector2 anchorMin, anchorMax, pivot, position, size;

            internal RectPlacement(RectTransform rect)
            {
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
                pivot = rect.pivot;
                position = rect.anchoredPosition;
                size = rect.sizeDelta;
            }

            internal void Apply(RectTransform rect)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
        }
    }
}
