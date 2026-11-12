using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal static class PauseSettingsView
    {
        internal static GameSettingsSession Create(Transform parent, TMP_FontAsset font, InputSystemUIInputModule input,
            UnityAction back, Action bindingsChanged, out GameObject panel)
        {
            Image card = OverlayUi.CreateImage("Settings Panel", parent, OverlayUi.Card);
            card.rectTransform.sizeDelta = new Vector2(704f, 556f);
            panel = card.gameObject;
            Transform root = card.transform;
            Label("Title", root, font, "설정", 29f, 30f, 20f, 644f, 46f);
            Label("Volume Label", root, font, "전체 볼륨", 19f, 30f, 82f, 125f, 40f);
            Slider volume = CreateSlider(root);
            TMP_Text volumeValue = Label("Volume Value", root, font, "", 19f, 580f, 82f, 94f, 40f);
            volumeValue.alignment = TextAlignmentOptions.MidlineRight;
            Label("Bindings Title", root, font, "키 바인딩", 22f, 30f, 135f, 644f, 38f);

            var bindings = new SettingsBindingView[GameInput.RebindableKeys.Count];
            int rows = (bindings.Length + 1) / 2;
            for (int i = 0; i < bindings.Length; i++)
            {
                float x = 30f + (i / rows) * 330f;
                float y = 185f + (i % rows) * 40f;
                Label("Action " + i, root, font, GameInput.RebindableKeys[i].Label, 16f, x, y, 194f, 34f);
                Button button = Button("Binding " + i, root, font, "", x + 198f, y, 116f, 34f, out TMP_Text value);
                value.enableAutoSizing = true;
                value.fontSizeMin = 11f;
                value.fontSizeMax = 17f;
                bindings[i] = new SettingsBindingView { Button = button, Value = value };
            }

            TMP_Text status = Label("Binding Status", root, font, "", 17f, 30f, 471f, 644f, 30f);
            status.color = OverlayUi.Accent;
            Button reset = Button("Reset Bindings", root, font, "기본 키로 복원", 30f, 508f, 178f, 34f, out _);
            Button backButton = Button("Back", root, font, "돌아가기", 496f, 508f, 178f, 34f, out _);
            backButton.onClick.AddListener(back);
            LayoutNavigation(volume, bindings, rows, reset, backButton);
            return new GameSettingsSession(volume, volumeValue, status, reset, bindings, input, bindingsChanged);
        }

        private static Slider CreateSlider(Transform root)
        {
            Image image = OverlayUi.CreateImage("Volume Slider", root, new Color(0f, 0f, 0f, 0f));
            OverlayUi.PlaceFromTopLeft(image.rectTransform, 172f, 82f, 395f, 40f);
            Slider slider = image.gameObject.AddComponent<Slider>();
            Image track = OverlayUi.CreateImage("Track", image.transform, new Color32(91, 80, 79, 255));
            track.raycastTarget = false;
            track.rectTransform.anchorMin = new Vector2(0f, 0.44f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.56f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
            Image fill = OverlayUi.CreateImage("Fill", track.transform, OverlayUi.Accent);
            fill.raycastTarget = false;
            OverlayUi.Stretch(fill.rectTransform);
            Image handle = OverlayUi.CreateImage("Handle", image.transform, OverlayUi.Accent);
            // Slider stretches the handle vertically across its 40-unit parent.
            handle.rectTransform.sizeDelta = new Vector2(16f, -16f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static TMP_Text Label(string name, Transform root, TMP_FontAsset font, string value, float size,
            float x, float y, float width, float height)
        {
            TMP_Text label = OverlayUi.CreateLabel(name, root, font, value, size, OverlayUi.Text);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OverlayUi.PlaceFromTopLeft(label.rectTransform, x, y, width, height);
            return label;
        }

        private static Button Button(string name, Transform root, TMP_FontAsset font, string caption,
            float x, float y, float width, float height, out TMP_Text label)
        {
            Button button = OverlayUi.CreateButton(name, root, font, caption, OverlayUi.Accent,
                OverlayUi.Card, 17f, out label);
            OverlayUi.PlaceFromTopLeft((RectTransform)button.transform, x, y, width, height);
            return button;
        }

        private static void LayoutNavigation(Slider volume, SettingsBindingView[] bindings, int rows,
            Button reset, Button back)
        {
            OverlayUi.Link(volume, null, bindings[0].Button);
            for (int i = 0; i < bindings.Length; i++)
            {
                bool left = i < rows;
                int columnEnd = left ? rows : bindings.Length;
                Selectable up = i % rows == 0 ? volume : bindings[i - 1].Button;
                Selectable down = i + 1 < columnEnd ? bindings[i + 1].Button : left ? reset : back;
                Button previous = left ? null : bindings[i - rows].Button;
                Button next = left && i + rows < bindings.Length ? bindings[i + rows].Button : null;
                OverlayUi.Link(bindings[i].Button, up, down, previous, next);
            }
            OverlayUi.Link(reset, bindings[rows - 1].Button, null, right: back);
            OverlayUi.Link(back, bindings[bindings.Length - 1].Button, null, left: reset);
        }
    }
}
