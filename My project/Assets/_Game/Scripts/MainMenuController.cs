using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleGame
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Game/Scenes/MainMenu.unity";

        [Serializable]
        public struct StageView
        {
            public string SceneName;
            public Button Button;
            public Image Fill;
            public TMP_Text Label;
        }

        public GameObject HomePanel;
        public GameObject SettingsPanel;
        public GameObject StageSelectPanel;
        public Button StartButton, ContinueButton, SettingsButton, QuitButton, BackButton, ResetBindingsButton;
        public Button StageSelectButton, StageSelectBackButton;
        public StageView[] Stages;
        public Slider VolumeSlider, CameraSensitivitySlider;
        public TMP_Text ContinueStageLabel, VolumeValue, CameraSensitivityValue, BindingStatus;
        public InputSystemUIInputModule MenuInput;
        public SettingsBindingView[] Bindings;

        private readonly InputAction keypadSubmit = new InputAction("Menu Keypad Submit", InputActionType.Button,
            "<Keyboard>/numpadEnter");
        private GameSettingsSession settings;
        private bool loading;
        private int focusedStage = -1;
        private static int? stageToSelectOnOpen;
        private static readonly Color StageFill = new Color32(54, 50, 54, 255);
        private static readonly Color StageLabel = new Color32(222, 214, 208, 255);
        private static readonly Color FocusedStageFill = new Color32(73, 59, 56, 255);
        private static readonly Color FocusedStageLabel = new Color32(255, 195, 138, 255);

        private void OnEnable() => keypadSubmit.Enable();
        private void OnDisable() => keypadSubmit.Disable();

        internal static void SelectStageOnNextMenuOpen(int stageNumber) => stageToSelectOnOpen = stageNumber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStageSelectRequest() => stageToSelectOnOpen = null;

        private void Start()
        {
            MenuSelectionAudio.Attach(StartButton);
            MenuSelectionAudio.Attach(ContinueButton);
            MenuSelectionAudio.Attach(StageSelectButton);
            MenuSelectionAudio.Attach(StageSelectBackButton);
            MenuSelectionAudio.Attach(SettingsButton);
            MenuSelectionAudio.Attach(QuitButton);
            MenuSelectionAudio.Attach(BackButton);
            StartButton.onClick.AddListener(StartGame);
            SetupContinue();
            StageSelectButton.onClick.AddListener(OpenStageSelect);
            StageSelectBackButton.onClick.AddListener(CloseStageSelect);
            foreach (StageView stage in Stages)
            {
                MenuSelectionAudio.Attach(stage.Button);
                string sceneName = stage.SceneName;
                stage.Button.onClick.AddListener(() => LoadStage(sceneName));
            }
            SettingsButton.onClick.AddListener(OpenSettings);
            QuitButton.onClick.AddListener(Quit);
            BackButton.onClick.AddListener(CloseSettings);
            settings = new GameSettingsSession(VolumeSlider, VolumeValue, CameraSensitivitySlider,
                CameraSensitivityValue, BindingStatus, ResetBindingsButton, Bindings, MenuInput);
            SettingsPanel.SetActive(false);
            int? stageNumber = stageToSelectOnOpen;
            stageToSelectOnOpen = null;
            if (stageNumber.HasValue)
            {
                ShowStageSelect(stageNumber.Value);
                return;
            }

            HomePanel.SetActive(true);
            StageSelectPanel.SetActive(false);
            Select(DefaultHomeButton.gameObject);
        }

        private void Update()
        {
            if (loading || settings.ConsumesMenuInput() || !MenuInput.enabled) return;
            if (MenuInput.cancel != null && MenuInput.cancel.action.WasPressedThisFrame())
            {
                if (SettingsPanel.activeSelf) CloseSettings();
                else if (StageSelectPanel.activeSelf) CloseStageSelect();
                return;
            }

            EventSystem events = EventSystem.current;
            if (events == null) return;
            GameObject selection = events.currentSelectedGameObject;
            if (selection == null || !selection.activeInHierarchy)
            {
                Select(SettingsPanel.activeSelf ? VolumeSlider.gameObject :
                    StageSelectPanel.activeSelf ? Stages[0].Button.gameObject : DefaultHomeButton.gameObject);
                selection = events.currentSelectedGameObject;
            }

            if (StageSelectPanel.activeSelf) UpdateStageHighlight(selection);

            if (keypadSubmit.WasPressedThisFrame() && !(MenuInput.submit?.action.WasPressedThisFrame() ?? false))
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private void StartGame() => LoadStage(GamePreferences.StartScene);

        private Button DefaultHomeButton => ContinueButton.interactable ? ContinueButton : StartButton;

        private void SetupContinue()
        {
            string resumeScene = GamePreferences.CampaignResumeScene;
            int stage = Array.FindIndex(Stages, item => item.SceneName == resumeScene);
            bool canContinue = stage >= 0 && Application.CanStreamedLevelBeLoaded(resumeScene);
            ContinueButton.interactable = canContinue;
            ContinueButton.GetComponentInChildren<TMP_Text>().alpha = canContinue ? 1f : 0.45f;
            ContinueStageLabel.text = canContinue ? $"스테이지 {stage}" : string.Empty;
            if (canContinue) ContinueButton.onClick.AddListener(() => LoadStage(resumeScene));

            OverlayUi.Link(StartButton, null, canContinue ? ContinueButton : StageSelectButton);
            OverlayUi.Link(ContinueButton, StartButton, StageSelectButton);
            OverlayUi.Link(StageSelectButton, canContinue ? ContinueButton : StartButton, SettingsButton);
            OverlayUi.Link(SettingsButton, StageSelectButton, QuitButton);
            OverlayUi.Link(QuitButton, SettingsButton, null);
        }

        private void LoadStage(string scene)
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("The campaign scene is not in Build Settings: " + scene, this);
                return;
            }
            loading = true;
            GameAudioEvents.Play(SoundCue.UiConfirm);
            HomePanel.SetActive(false);
            StageSelectPanel.SetActive(false);
            settings.SavePreferences();
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        private void OpenStageSelect()
        {
            GameAudioEvents.Play(SoundCue.UiConfirm);
            ShowStageSelect(0);
        }

        private void ShowStageSelect(int stageNumber)
        {
            HomePanel.SetActive(false);
            StageSelectPanel.SetActive(true);
            GameObject initialStage = Stages[Mathf.Clamp(stageNumber, 0, Stages.Length - 1)].Button.gameObject;
            Select(initialStage);
            UpdateStageHighlight(initialStage);
        }

        private void CloseStageSelect()
        {
            GameAudioEvents.Play(SoundCue.UiBack);
            UpdateStageHighlight(null);
            StageSelectPanel.SetActive(false);
            HomePanel.SetActive(true);
            Select(StageSelectButton.gameObject);
        }

        private void UpdateStageHighlight(GameObject selection)
        {
            if (focusedStage >= 0 && Stages[focusedStage].Button.gameObject == selection) return;
            if (focusedStage >= 0)
            {
                Stages[focusedStage].Fill.color = StageFill;
                Stages[focusedStage].Label.color = StageLabel;
                focusedStage = -1;
            }
            for (int i = 0; i < Stages.Length; i++)
            {
                if (Stages[i].Button.gameObject != selection) continue;
                Stages[i].Fill.color = FocusedStageFill;
                Stages[i].Label.color = FocusedStageLabel;
                focusedStage = i;
                break;
            }
        }

        private void OpenSettings()
        {
            GameAudioEvents.Play(SoundCue.UiConfirm);
            HomePanel.SetActive(false);
            SettingsPanel.SetActive(true);
            settings.Open();
        }

        private void CloseSettings()
        {
            GameAudioEvents.Play(SoundCue.UiBack);
            settings.Close();
            SettingsPanel.SetActive(false);
            HomePanel.SetActive(true);
            Select(SettingsButton.gameObject);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) settings?.SavePreferences();
        }

        private void Quit()
        {
            GameAudioEvents.Play(SoundCue.UiConfirm);
            settings.SavePreferences();
            Application.Quit();
        }

        private void OnDestroy()
        {
            keypadSubmit.Dispose();
            settings?.Dispose();
        }

        private static void Select(GameObject target) => EventSystem.current?.SetSelectedGameObject(target);
    }
}
