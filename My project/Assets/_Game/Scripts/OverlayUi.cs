using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal static class OverlayUi
    {
        internal static readonly Color Card = new Color32(28, 27, 32, 255);
        internal static readonly Color Text = new Color32(247, 239, 229, 255);
        internal static readonly Color Accent = new Color32(227, 154, 98, 255);

        internal static Image CreateBackdrop(ProjectionPrototypeHud hud, string name)
        {
            var layout = hud.GetComponent<ProjectionHudLayout>();
            Transform parent = layout != null && layout.SafeArea != null ? layout.SafeArea : hud.transform;
            Image backdrop = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.62f));
            Stretch(backdrop.rectTransform);
            return backdrop;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            var element = new GameObject(name, typeof(RectTransform), typeof(Image));
            element.layer = parent.gameObject.layer;
            element.transform.SetParent(parent, false);
            Image image = element.GetComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TMP_Text CreateLabel(string name, Transform parent, TMP_FontAsset font, string value,
            float size, Color color)
        {
            var element = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            element.layer = parent.gameObject.layer;
            element.transform.SetParent(parent, false);
            var text = element.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.color = color;
            return text;
        }

        internal static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string caption,
            Color fill, Color textColor, float fontSize, out TMP_Text label)
        {
            Image image = CreateImage(name, parent, fill);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            label = CreateLabel("Label", button.transform, font, caption, fontSize, textColor);
            Stretch(label.rectTransform);
            return button;
        }

        internal static EventSystem GetOrCreateEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return EventSystem.current;
            var events = new GameObject("Overlay EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
            events.GetComponent<InputSystemUIInputModule>().deselectOnBackgroundClick = false;
            return events.GetComponent<EventSystem>();
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void PlaceFromTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Link(Selectable item, Selectable up, Selectable down,
            Selectable left = null, Selectable right = null)
        {
            item.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
        }
    }
}
