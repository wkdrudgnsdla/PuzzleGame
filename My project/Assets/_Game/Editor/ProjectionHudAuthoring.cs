using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleGame.Editor
{
    /// <summary>Authors the retained header UI as ordinary scene objects.</summary>
    public static class ProjectionHudAuthoring
    {
        private static readonly Color Panel = new Color(0.035f, 0.055f, 0.085f, 0.94f);
        private static readonly Color Muted = new Color(0.64f, 0.72f, 0.80f);
        private static readonly Color Amber = new Color(1f, 0.72f, 0.32f);

        [MenuItem("Puzzle Game/Add or Select Scene HUD")]
        public static void AddOrSelectSceneHud()
        {
            ProjectionPrototype owner = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<ProjectionPrototype>() : null;
            if (owner == null)
                owner = UnityEngine.Object.FindFirstObjectByType<ProjectionPrototype>(FindObjectsInactive.Include);
            if (owner == null)
            {
                Debug.LogWarning("Projection Prototype 오브젝트를 먼저 선택하거나 프로토타입 씬을 열어주세요.");
                return;
            }

            CreateHud(owner);
        }

        public static ProjectionPrototypeHud CreateHud(ProjectionPrototype owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (owner.Hud != null)
            {
                Selection.activeGameObject = owner.Hud.gameObject;
                return owner.Hud;
            }

            foreach (Canvas existing in UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing.gameObject.scene != owner.gameObject.scene) continue;
                // Existing scene UI belongs to the designer; select it without changing it.
                Selection.activeGameObject = existing.gameObject;
                return existing.GetComponent<ProjectionPrototypeHud>();
            }

            var canvasObject = new GameObject("Prototype HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ProjectionPrototypeHud));
            canvasObject.layer = 5;
            SceneManager.MoveGameObjectToScene(canvasObject, owner.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Projection HUD");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            ProjectionPrototypeHud hud = canvasObject.GetComponent<ProjectionPrototypeHud>();
            hud.Prototype = owner;
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            Image header = Image("Header", canvasRect, new Vector2(24f, -24f), new Vector2(-48f, 88f), Panel);
            StretchTop(header.rectTransform);
            hud.AccentTop = Image("Accent Top", header.rectTransform, Vector2.zero, new Vector2(3f, 0f), Amber);
            hud.AccentTop.rectTransform.anchorMin = Vector2.zero;
            hud.AccentTop.rectTransform.anchorMax = Vector2.up;
            hud.ModeBadge = Image("Mode Badge", header.rectTransform, new Vector2(18f, -20f),
                new Vector2(80f, 48f), new Color(Amber.r, Amber.g, Amber.b, 0.14f));
            hud.ModeText = Text("Mode Text", hud.ModeBadge.rectTransform, Vector2.zero, Vector2.zero,
                "3D", 30, Amber, FontStyle.Bold, TextAnchor.MiddleCenter);
            FillParent(hud.ModeText.rectTransform);
            hud.YawText = Text("Yaw Value", header.rectTransform, new Vector2(120f, -27f), new Vector2(128f, 34f),
                "좌우 0.0°", 22, Amber, FontStyle.Bold);
            hud.PitchText = Text("Pitch Value", header.rectTransform, new Vector2(264f, -27f), new Vector2(128f, 34f),
                "상하 0.0°", 22, Amber, FontStyle.Bold);
            hud.SupportText = Text("Support Value", header.rectTransform, new Vector2(408f, -27f), new Vector2(160f, 34f),
                "Platform A", 20, Color.white, FontStyle.Bold);
            hud.ModeHintText = Text("Mode Hint", header.rectTransform, new Vector2(600f, -28f), new Vector2(-614f, 32f),
                "R로 공간 접기", 16, Muted);
            StretchTop(hud.ModeHintText.rectTransform);

            Undo.RecordObject(owner, "Bind Projection HUD");
            owner.Hud = hud;
            EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            Selection.activeGameObject = canvasObject;
            return hud;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.layer = 5;
            RectTransform rect = (RectTransform)result.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Image result = rect.gameObject.AddComponent<Image>();
            result.color = color;
            result.raycastTarget = false;
            return result;
        }

        private static Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value,
            int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Text result = rect.gameObject.AddComponent<Text>();
            result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            result.text = value;
            result.fontSize = fontSize;
            result.fontStyle = fontStyle;
            result.color = color;
            result.alignment = alignment;
            result.supportRichText = false;
            result.horizontalOverflow = HorizontalWrapMode.Wrap;
            result.verticalOverflow = VerticalWrapMode.Overflow;
            result.raycastTarget = false;
            return result;
        }

        private static void FillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchTop(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
        }
    }
}
