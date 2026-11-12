using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal static class CampaignClearDialog
    {
        internal static void Show(ProjectionPrototypeHud hud, UnityAction returnToMenu)
        {
            Image backdrop = OverlayUi.CreateBackdrop(hud, "Final Stage Clear");
            Image card = OverlayUi.CreateImage("Dialog", backdrop.transform, OverlayUi.Card);
            card.rectTransform.sizeDelta = new Vector2(416f, 188f);

            TMP_Text title = OverlayUi.CreateLabel("Title", card.transform, hud.KoreanFont,
                "마지막 스테이지 클리어!", 28f, OverlayUi.Text);
            title.rectTransform.sizeDelta = new Vector2(376f, 44f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 42f);

            Button button = OverlayUi.CreateButton("Return to Main Menu", card.transform, hud.KoreanFont,
                "메인메뉴로 나가기", OverlayUi.Accent, OverlayUi.Card, 22f, out _);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(260f, 52f);
            rect.anchoredPosition = new Vector2(0f, -34f);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(returnToMenu);

            EventSystem events = OverlayUi.GetOrCreateEventSystem(backdrop.transform);
            events.SetSelectedGameObject(button.gameObject);
        }
    }
}
