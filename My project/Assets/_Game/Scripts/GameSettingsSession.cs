using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleGame
{
    [Serializable]
    public struct SettingsBindingView
    {
        public Button Button;
        public TMP_Text Value;
    }

    internal sealed class GameSettingsSession : IDisposable
    {
        private readonly Slider volume, sensitivity;
        private readonly TMP_Text volumeValue, sensitivityValue, status;
        private readonly Button reset;
        private readonly SettingsBindingView[] bindings;
        private readonly InputSystemUIInputModule input;
        private readonly Action bindingsChanged;
        private readonly UnityAction[] bindingListeners;
        private InputActionRebindingExtensions.RebindingOperation rebind;
        private int resumeInputFrame = -1;
        private Button reboundButton;
        private string rebindResult;
        private bool preferencesDirty;
        private float nextSliderCueTime;

        internal GameSettingsSession(Slider volume, TMP_Text volumeValue, Slider sensitivity,
            TMP_Text sensitivityValue, TMP_Text status, Button reset, SettingsBindingView[] bindings,
            InputSystemUIInputModule input, Action bindingsChanged = null)
        {
            this.volume = volume;
            this.volumeValue = volumeValue;
            this.sensitivity = sensitivity;
            this.sensitivityValue = sensitivityValue;
            this.status = status;
            this.reset = reset;
            this.bindings = bindings;
            this.input = input;
            this.bindingsChanged = bindingsChanged;
            MenuSelectionAudio.Attach(volume);
            MenuSelectionAudio.Attach(sensitivity);
            MenuSelectionAudio.Attach(reset);
            volume.onValueChanged.AddListener(ChangeVolume);
            sensitivity.minValue = GamePreferences.MinCameraRotationSensitivity;
            sensitivity.maxValue = GamePreferences.MaxCameraRotationSensitivity;
            sensitivity.onValueChanged.AddListener(ChangeSensitivity);
            reset.onClick.AddListener(ResetBindings);
            bindingListeners = new UnityAction[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                MenuSelectionAudio.Attach(bindings[i].Button);
                int row = i;
                bindingListeners[i] = () => BeginRebind(row);
                bindings[i].Button.onClick.AddListener(bindingListeners[i]);
            }
        }

        internal void Open()
        {
            volume.SetValueWithoutNotify(GamePreferences.MasterVolume);
            volumeValue.text = FormatPercent(volume.value);
            sensitivity.SetValueWithoutNotify(GamePreferences.CameraRotationSensitivity);
            sensitivityValue.text = FormatPercent(sensitivity.value);
            RefreshBindings();
            status.text = "변경할 항목을 선택하세요.";
            EventSystem.current?.SetSelectedGameObject(volume.gameObject);
        }

        // The Escape that cancels a rebind must not also close the settings panel.
        internal bool ConsumesMenuInput()
        {
            if (resumeInputFrame < 0) return rebind != null;
            if (Time.frameCount >= resumeInputFrame)
            {
                resumeInputFrame = -1;
                input.enabled = true;
                EventSystem.current?.SetSelectedGameObject(reboundButton.gameObject);
                reboundButton = null;
            }
            return true;
        }

        internal void SavePreferences()
        {
            if (!preferencesDirty) return;
            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        internal void Close()
        {
            if (rebind != null || resumeInputFrame >= 0)
            {
                rebind?.Dispose();
                rebind = null;
                resumeInputFrame = -1;
                reboundButton = null;
                if (input != null) input.enabled = true;
            }
            SavePreferences();
        }

        private void ChangeVolume(float value)
        {
            GamePreferences.MasterVolume = value;
            preferencesDirty = true;
            volumeValue.text = FormatPercent(value);
            PlaySliderCue();
        }

        private void ChangeSensitivity(float value)
        {
            GamePreferences.CameraRotationSensitivity = value;
            preferencesDirty = true;
            sensitivityValue.text = FormatPercent(value);
            PlaySliderCue();
        }

        private void PlaySliderCue()
        {
            if (Time.unscaledTime < nextSliderCueTime) return;
            nextSliderCueTime = Time.unscaledTime + 0.12f;
            GameAudioEvents.Play(SoundCue.UiMove);
        }

        private static string FormatPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        private void RefreshBindings()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var key = GameInput.RebindableKeys[i];
                bindings[i].Value.text = GameInput.KeyName(key.ActionName, key.Index);
            }
        }

        private void BeginRebind(int row)
        {
            if (rebind != null || resumeInputFrame >= 0) return;
            GameAudioEvents.Play(SoundCue.UiConfirm);
            var key = GameInput.RebindableKeys[row];
            InputAction action = GameInput.Action(key.ActionName);
            input.enabled = false;
            reboundButton = bindings[row].Button;
            status.text = key.AllowsMouse ? "키나 마우스 버튼을 누르세요. Esc로 취소" : "새 키를 누르세요. Esc로 취소";
            rebind = action.PerformInteractiveRebinding(key.Index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithExpectedControlType("Button")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnApplyBinding((operation, path) =>
                {
                    if (GameInput.IsInUse(action, key.Index, operation.selectedControl))
                    {
                        rebindResult = "이미 사용 중인 키입니다.";
                        GameAudioEvents.Play(SoundCue.UiBack);
                        return;
                    }
                    action.ApplyBindingOverride(key.Index, path);
                    GameInput.SaveOverrides();
                    rebindResult = "키 바인딩을 저장했습니다.";
                    GameAudioEvents.Play(SoundCue.UiConfirm);
                    bindingsChanged?.Invoke();
                })
                .OnComplete(operation => FinishRebind(operation, rebindResult))
                .OnCancel(operation =>
                {
                    GameAudioEvents.Play(SoundCue.UiBack);
                    FinishRebind(operation, "키 바인딩 변경을 취소했습니다.");
                });
            if (key.AllowsMouse)
                rebind.WithControlsHavingToMatchPath("<Mouse>").WithControlsExcluding("<Mouse>/scroll");
            rebind.Start();
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, string message)
        {
            operation.Dispose();
            rebind = null;
            resumeInputFrame = Time.frameCount + 1;
            RefreshBindings();
            status.text = message;
        }

        private void ResetBindings()
        {
            GameInput.ResetOverrides();
            GameAudioEvents.Play(SoundCue.UiConfirm);
            RefreshBindings();
            bindingsChanged?.Invoke();
            status.text = "기본 키로 복원했습니다.";
        }

        public void Dispose()
        {
            Close();
            if (volume != null) volume.onValueChanged.RemoveListener(ChangeVolume);
            if (sensitivity != null) sensitivity.onValueChanged.RemoveListener(ChangeSensitivity);
            if (reset != null) reset.onClick.RemoveListener(ResetBindings);
            for (int i = 0; i < bindings.Length; i++)
                if (bindings[i].Button != null) bindings[i].Button.onClick.RemoveListener(bindingListeners[i]);
        }
    }
}
