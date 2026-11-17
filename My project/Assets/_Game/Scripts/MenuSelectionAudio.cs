using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleGame
{
    [DisallowMultipleComponent]
    internal sealed class MenuSelectionAudio : MonoBehaviour, ISelectHandler
    {
        internal static void Attach(Selectable item) => item.gameObject.AddComponent<MenuSelectionAudio>();

        public void OnSelect(BaseEventData eventData)
        {
            // Directional navigation passes AxisEventData. Dialogs assign their initial focus separately.
            if (eventData is AxisEventData) GameAudioEvents.Play(SoundCue.UiMove);
        }
    }
}
