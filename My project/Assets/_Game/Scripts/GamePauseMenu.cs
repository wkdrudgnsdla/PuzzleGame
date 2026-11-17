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
                else Resume();
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
            backdrop.transform.SetAsLastSibling();
            backdrop.gameObject.SetActive(true);
            choices.SetActive(true);
            events.SetSelectedGameObject(resumeButton.gameObject);
            GameAudioEvents.Play(SoundCue.UiPause);
        }

        private void BuildMenu()
        {
            backdrop = OverlayUi.CreateBackdrop(hud, "Pause Menu");
            Image card = OverlayUi.CreateImage("Options", backdrop.transform, OverlayUi.Card);
            card.rectTransform.sizeDelta = new Vector2(400f, 416f);
            choices = card.gameObject;
            var title = OverlayUi.CreateLabel("Title", card.transform, hud.KoreanFont, "일시정지", 32f, OverlayUi.Text);
            OverlayUi.PlaceFromTopLeft(title.rectTransform, 20f, 20f, 360f, 52f);

            resumeButton = AddOption("Resume", "게임 재개", 0);
            Button restart = AddOption("Restart", "다시 시작", 1);
            settingsButton = AddOption("Settings", "설정", 2);
            Button mainMenu = AddOption("Main Menu", "메인메뉴로", 3);
            Button quit = AddOption("Quit", "게임종료", 4);
            resumeButton.onClick.AddListener(Resume);
            restart.onClick.AddListener(Restart);
            settingsButton.onClick.AddListener(OpenSettings);
            mainMenu.onClick.AddListener(ReturnToMainMenu);
            quit.onClick.AddListener(Quit);
            OverlayUi.Link(resumeButton, null, restart);
            OverlayUi.Link(restart, resumeButton, settingsButton);
            OverlayUi.Link(settingsButton, restart, mainMenu);
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
            MenuSelectionAudio.Attach(button);
            OverlayUi.PlaceFromTopLeft((RectTransform)button.transform, 52f, 82f + row * 62f, 296f, 52f);
            return button;
        }

        private void OpenSettings()
        {
            if (leaving) return;
            GameAudioEvents.Play(SoundCue.UiConfirm);
            if (settings == null)
                settings = PauseSettingsView.Create(backdrop.transform, hud.KoreanFont, uiInput,
                    CloseSettings, RefreshInputLabels, out settingsPanel);
            choices.SetActive(false);
            settingsPanel.SetActive(true);
            settings.Open();
        }

        private void CloseSettings()
        {
            GameAudioEvents.Play(SoundCue.UiBack);
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

        private void Restart()
        {
            if (leaving) return;
            GameAudioEvents.Play(SoundCue.PlayerRestart);
            if (TryLeaveForScene(prototype.gameObject.scene.path)) return;
            EndPause(true);
            prototype.ResetPrototype();
        }

        private void ReturnToMainMenu()
        {
            if (TryLeaveForScene(MainMenuController.ScenePath)) GameAudioEvents.Play(SoundCue.UiBack);
        }

        private bool TryLeaveForScene(string scene)
        {
            if (leaving || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            leaving = true;
            settings?.SavePreferences();
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            return true;
        }

        private void Quit()
        {
            if (leaving) return;
            GameAudioEvents.Play(SoundCue.UiConfirm);
            settings?.SavePreferences();
            Application.Quit();
        }

        private void Resume()
        {
            if (!paused || leaving) return;
            EndPause(true);
            GameAudioEvents.Play(SoundCue.UiResume);
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
            if (!focused) settings?.SavePreferences();
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
