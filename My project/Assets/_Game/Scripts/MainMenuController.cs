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
        public struct BindingView
        {
            public Button Button;
            public TMP_Text Value;
        }

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
        public Button StartButton, SettingsButton, QuitButton, BackButton, ResetBindingsButton;
        public Button StageSelectButton, StageSelectBackButton;
        public StageView[] Stages;
        public Slider VolumeSlider;
        public TMP_Text VolumeValue, BindingStatus;
        public InputSystemUIInputModule MenuInput;
        public BindingView[] Bindings;

        private readonly InputAction keypadSubmit = new InputAction("Menu Keypad Submit", InputActionType.Button,
            "<Keyboard>/numpadEnter");
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private int resumeMenuInputFrame = -1;
        private Button reboundButton;
        private string rebindResult;
        private bool loading;
        private bool volumeDirty;
        private int focusedStage = -1;
        private static readonly Color StageFill = new Color32(54, 50, 54, 255);
        private static readonly Color StageLabel = new Color32(222, 214, 208, 255);
        private static readonly Color FocusedStageFill = new Color32(73, 59, 56, 255);
        private static readonly Color FocusedStageLabel = new Color32(255, 195, 138, 255);

        private void OnEnable() => keypadSubmit.Enable();
        private void OnDisable() => keypadSubmit.Disable();

        private void Start()
        {
            StartButton.onClick.AddListener(StartGame);
            StageSelectButton.onClick.AddListener(OpenStageSelect);
            StageSelectBackButton.onClick.AddListener(CloseStageSelect);
            foreach (StageView stage in Stages)
            {
                string sceneName = stage.SceneName;
                stage.Button.onClick.AddListener(() => LoadStage(sceneName));
            }
            SettingsButton.onClick.AddListener(OpenSettings);
            QuitButton.onClick.AddListener(Quit);
            BackButton.onClick.AddListener(CloseSettings);
            ResetBindingsButton.onClick.AddListener(ResetBindings);
            VolumeSlider.SetValueWithoutNotify(GamePreferences.MasterVolume);
            ShowVolume(VolumeSlider.value);
            VolumeSlider.onValueChanged.AddListener(ChangeVolume);
            for (int i = 0; i < Bindings.Length; i++)
            {
                int row = i;
                Bindings[row].Button.onClick.AddListener(() => BeginRebind(row));
            }
            HomePanel.SetActive(true);
            SettingsPanel.SetActive(false);
            StageSelectPanel.SetActive(false);
            Select(StartButton.gameObject);
        }

        private void Update()
        {
            if (loading) return;
            if (resumeMenuInputFrame >= 0 && Time.frameCount >= resumeMenuInputFrame)
            {
                resumeMenuInputFrame = -1;
                MenuInput.enabled = true;
                Select(reboundButton.gameObject);
                reboundButton = null;
                return;
            }
            if (rebind != null || !MenuInput.enabled) return;
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
                    StageSelectPanel.activeSelf ? Stages[0].Button.gameObject : StartButton.gameObject);
                selection = events.currentSelectedGameObject;
            }

            if (StageSelectPanel.activeSelf) UpdateStageHighlight(selection);

            if (keypadSubmit.WasPressedThisFrame() && !(MenuInput.submit?.action.WasPressedThisFrame() ?? false))
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private void StartGame() => LoadStage(GamePreferences.StartScene);

        private void LoadStage(string scene)
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("The campaign scene is not in Build Settings: " + scene, this);
                return;
            }
            loading = true;
            HomePanel.SetActive(false);
            StageSelectPanel.SetActive(false);
            SaveVolume();
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        private void OpenStageSelect()
        {
            HomePanel.SetActive(false);
            StageSelectPanel.SetActive(true);
            GameObject firstStage = Stages[0].Button.gameObject;
            Select(firstStage);
            UpdateStageHighlight(firstStage);
        }

        private void CloseStageSelect()
        {
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
            HomePanel.SetActive(false);
            SettingsPanel.SetActive(true);
            BindingStatus.text = "변경할 항목을 선택하세요.";
            RefreshBindings();
            Select(VolumeSlider.gameObject);
        }

        private void CloseSettings()
        {
            SaveVolume();
            SettingsPanel.SetActive(false);
            HomePanel.SetActive(true);
            Select(SettingsButton.gameObject);
        }

        private void ChangeVolume(float value)
        {
            GamePreferences.MasterVolume = value;
            volumeDirty = true;
            ShowVolume(value);
        }

        private void ShowVolume(float value) => VolumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";

        private void SaveVolume()
        {
            if (!volumeDirty) return;
            PlayerPrefs.Save();
            volumeDirty = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveVolume();
        }

        private void RefreshBindings()
        {
            for (int i = 0; i < Bindings.Length; i++)
            {
                var key = GameInput.RebindableKeys[i];
                Bindings[i].Value.text = GameInput.KeyName(key.ActionName, key.Index);
            }
        }

        private void BeginRebind(int row)
        {
            if (rebind != null || resumeMenuInputFrame >= 0) return;
            var key = GameInput.RebindableKeys[row];
            InputAction action = GameInput.Action(key.ActionName);
            MenuInput.enabled = false;
            reboundButton = Bindings[row].Button;
            BindingStatus.text = key.AllowsMouse ? "키나 마우스 버튼을 누르세요. Esc로 취소" : "새 키를 누르세요. Esc로 취소";
            rebind = action.PerformInteractiveRebinding(key.Index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithExpectedControlType("Button")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnApplyBinding((operation, path) =>
                {
                    if (GameInput.IsInUse(action, key.Index, operation.selectedControl))
                    {
                        rebindResult = "이미 사용 중인 키입니다.";
                        return;
                    }
                    action.ApplyBindingOverride(key.Index, path);
                    GameInput.SaveOverrides();
                    rebindResult = "키 바인딩을 저장했습니다.";
                })
                .OnComplete(operation => FinishRebind(operation, rebindResult))
                .OnCancel(operation => FinishRebind(operation, "키 바인딩 변경을 취소했습니다."));
            if (key.AllowsMouse)
                rebind.WithControlsHavingToMatchPath("<Mouse>").WithControlsExcluding("<Mouse>/scroll");
            rebind.Start();
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, string message)
        {
            operation.Dispose();
            rebind = null;
            resumeMenuInputFrame = Time.frameCount + 1;
            RefreshBindings();
            BindingStatus.text = message;
        }

        private void ResetBindings()
        {
            GameInput.ResetOverrides();
            RefreshBindings();
            BindingStatus.text = "기본 키로 복원했습니다.";
        }

        private void Quit()
        {
            SaveVolume();
            Application.Quit();
        }

        private void OnDestroy()
        {
            rebind?.Dispose();
            keypadSubmit.Dispose();
            SaveVolume();
        }

        private static void Select(GameObject target) => EventSystem.current?.SetSelectedGameObject(target);
    }
}
