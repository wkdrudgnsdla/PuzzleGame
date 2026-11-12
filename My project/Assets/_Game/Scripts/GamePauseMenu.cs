using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleGame
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class GamePauseMenu : MonoBehaviour
    {
        private readonly InputAction escape = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        private readonly InputAction keypadSubmit = new InputAction("Pause Keypad Submit", InputActionType.Button,
            "<Keyboard>/numpadEnter");
        private ProjectionPrototype prototype;
        private ProjectionPrototypeHud hud;
        private Image backdrop;
        private GameObject choices, settingsPanel, previousSelection;
        private Button resumeButton, settingsButton;
        private EventSystem events;
        private InputSystemUIInputModule uiInput;
        private GameSettingsSession settings;
        private float previousTimeScale;
        private bool paused, leaving;

        internal void Initialize(ProjectionPrototype owner, ProjectionPrototypeHud ownerHud)
        {
            prototype = owner;
            hud = ownerHud;
        }

        private void OnEnable()
        {
            escape.Enable();
            keypadSubmit.Enable();
        }

        private void Update()
        {
            if (prototype == null || leaving) return;
            if (!paused)
            {
                if (prototype.isActiveAndEnabled && escape.WasPressedThisFrame()) OpenPause();
                return;
            }

            if (settings != null && settings.ConsumesMenuInput()) return;
            if (escape.WasPressedThisFrame())
            {
                if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
                else EndPause(true);
                return;
            }
            if (keypadSubmit.WasPressedThisFrame() && !(uiInput.submit?.action.WasPressedThisFrame() ?? false))
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private void OpenPause()
        {
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (backdrop == null) BuildMenu();
            previousTimeScale = Time.timeScale;
            paused = true;
            prototype.SuspendGameplay();
            Time.timeScale = 0f;
            backdrop.gameObject.SetActive(true);
            choices.SetActive(true);
            events.SetSelectedGameObject(resumeButton.gameObject);
        }

        private void BuildMenu()
        {
            backdrop = OverlayUi.CreateBackdrop(hud, "Pause Menu");
            Image card = OverlayUi.CreateImage("Options", backdrop.transform, OverlayUi.Card);
            card.rectTransform.sizeDelta = new Vector2(400f, 354f);
            choices = card.gameObject;
            var title = OverlayUi.CreateLabel("Title", card.transform, hud.KoreanFont, "일시정지", 32f, OverlayUi.Text);
            OverlayUi.PlaceFromTopLeft(title.rectTransform, 20f, 20f, 360f, 52f);

            resumeButton = AddOption("Resume", "게임 재개", 0);
            settingsButton = AddOption("Settings", "설정", 1);
            Button mainMenu = AddOption("Main Menu", "메인메뉴로", 2);
            Button quit = AddOption("Quit", "게임종료", 3);
            resumeButton.onClick.AddListener(() => EndPause(true));
            settingsButton.onClick.AddListener(OpenSettings);
            mainMenu.onClick.AddListener(ReturnToMainMenu);
            quit.onClick.AddListener(Quit);
            OverlayUi.Link(resumeButton, null, settingsButton);
            OverlayUi.Link(settingsButton, resumeButton, mainMenu);
            OverlayUi.Link(mainMenu, settingsButton, quit);
            OverlayUi.Link(quit, mainMenu, null);

            events = OverlayUi.GetOrCreateEventSystem(backdrop.transform);
            if (!events.TryGetComponent(out uiInput))
                uiInput = events.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private Button AddOption(string name, string caption, int row)
        {
            Button button = OverlayUi.CreateButton(name, choices.transform, hud.KoreanFont,
                caption, OverlayUi.Accent, OverlayUi.Card, 23f, out _);
            OverlayUi.PlaceFromTopLeft((RectTransform)button.transform, 52f, 82f + row * 62f, 296f, 52f);
            return button;
        }

        private void OpenSettings()
        {
            if (leaving) return;
            if (settings == null)
                settings = PauseSettingsView.Create(backdrop.transform, hud.KoreanFont, uiInput,
                    CloseSettings, RefreshInputLabels, out settingsPanel);
            choices.SetActive(false);
            settingsPanel.SetActive(true);
            settings.Open();
        }

        private void CloseSettings()
        {
            settings.Close();
            settingsPanel.SetActive(false);
            choices.SetActive(true);
            events.SetSelectedGameObject(settingsButton.gameObject);
        }

        private void RefreshInputLabels()
        {
            hud.RefreshControlBindings();
            prototype.GetComponent<CampaignStageFlow>()?.RefreshInputLabels();
        }

        private void ReturnToMainMenu()
        {
            if (leaving || !Application.CanStreamedLevelBeLoaded(MainMenuController.ScenePath)) return;
            leaving = true;
            settings?.SaveVolume();
            SceneManager.LoadSceneAsync(MainMenuController.ScenePath, LoadSceneMode.Single);
        }

        private void Quit()
        {
            if (leaving) return;
            settings?.SaveVolume();
            Application.Quit();
        }

        private void EndPause(bool resumeGameplay)
        {
            if (!paused || (resumeGameplay && leaving)) return;
            paused = false;
            Time.timeScale = previousTimeScale;
            settings?.Close();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (events != null && events.isActiveAndEnabled) events.SetSelectedGameObject(previousSelection);
            if (resumeGameplay && prototype != null && prototype.gameObject.activeInHierarchy)
                prototype.enabled = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) settings?.SaveVolume();
        }

        private void OnDisable()
        {
            escape.Disable();
            keypadSubmit.Disable();
            EndPause(!leaving);
        }

        private void OnDestroy()
        {
            settings?.Dispose();
            escape.Dispose();
            keypadSubmit.Dispose();
        }
    }
}
