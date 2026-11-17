using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal sealed class CampaignClearDialog
    {
        private readonly EventSystem events;
        private readonly InputSystemUIInputModule uiInput;
        private readonly Button initialSelection;

        internal CampaignClearDialog(ProjectionPrototypeHud hud, UnityAction nextStage, UnityAction replay,
            UnityAction selectStage)
        {
            Image backdrop = OverlayUi.CreateBackdrop(hud, "Stage Clear");
            Image card = OverlayUi.CreateImage("Dialog", backdrop.transform, OverlayUi.Card);
            bool hasNextStage = nextStage != null;
            card.rectTransform.sizeDelta = new Vector2(416f, hasNextStage ? 304f : 240f);

            TMP_Text title = OverlayUi.CreateLabel("Title", card.transform, hud.KoreanFont,
                "스테이지 클리어", 30f, OverlayUi.Text);
            OverlayUi.PlaceFromTopLeft(title.rectTransform, 20f, 20f, 376f, 50f);

            Button next = hasNextStage
                ? AddButton(card.transform, hud.KoreanFont, "Next Stage", "다음 스테이지", 0, nextStage)
                : null;
            int replayRow = hasNextStage ? 1 : 0;
            Button retry = AddButton(card.transform, hud.KoreanFont, "Replay Stage", "다시하기", replayRow, replay);
            Button stages = AddButton(card.transform, hud.KoreanFont, "Stage Select", "스테이지 선택", replayRow + 1, selectStage);
            if (next != null) OverlayUi.Link(next, null, retry);
            OverlayUi.Link(retry, next, stages);
            OverlayUi.Link(stages, retry, null);

            events = OverlayUi.GetOrCreateEventSystem(hud.transform);
            events.TryGetComponent(out uiInput);
            initialSelection = next ?? stages;
            events.SetSelectedGameObject(initialSelection.gameObject);
        }

        internal void HandleContinue(bool pressed)
        {
            GameObject selection = events.currentSelectedGameObject;
            if (selection == null || !selection.activeInHierarchy)
            {
                selection = initialSelection.gameObject;
                events.SetSelectedGameObject(selection);
            }
            if (pressed && !(uiInput?.submit?.action?.WasPressedThisFrame() ?? false))
                ExecuteEvents.Execute(selection, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private static Button AddButton(Transform parent, TMP_FontAsset font, string name, string caption,
            int row, UnityAction onClick)
        {
            Button button = OverlayUi.CreateButton(name, parent, font, caption, OverlayUi.Accent, OverlayUi.Card, 23f, out _);
            OverlayUi.PlaceFromTopLeft((RectTransform)button.transform, 62f, 86f + row * 64f, 292f, 52f);
            MenuSelectionAudio.Attach(button);
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
