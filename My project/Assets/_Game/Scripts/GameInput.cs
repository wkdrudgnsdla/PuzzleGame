using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleGame
{
    public static class GameInput
    {
        private const string BindingsPreference = "Foldway.Input.Bindings";
        private static InputActionAsset asset;

        public readonly struct KeyBinding
        {
            public readonly string Label;
            public readonly string ActionName;
            public readonly int Index;
            public readonly bool AllowsMouse;

            public KeyBinding(string label, string actionName, int index = 0, bool allowsMouse = false)
            {
                Label = label;
                ActionName = actionName;
                Index = index;
                AllowsMouse = allowsMouse;
            }
        }

        public static IReadOnlyList<KeyBinding> RebindableKeys { get; } = new KeyBinding[]
        {
            new KeyBinding("위 / 앞으로", "Move", 1),
            new KeyBinding("아래 / 뒤로", "Move", 2),
            new KeyBinding("왼쪽", "Move", 3),
            new KeyBinding("오른쪽", "Move", 4),
            new KeyBinding("점프", "Jump"),
            new KeyBinding("차원 전환", "SwitchDimension"),
            new KeyBinding("스테이지 재시작", "Restart"),
            new KeyBinding("화면 이동 (드래그)", "Pan", allowsMouse: true),
            new KeyBinding("시점 회전 (드래그)", "Orbit", allowsMouse: true),
            new KeyBinding("조작 안내", "Controls"),
            new KeyBinding("조작 안내 (보조)", "Controls", 1),
            new KeyBinding("다음 스테이지", "Continue"),
            new KeyBinding("다음 스테이지 (보조)", "Continue", 1)
        };

        public static InputActionMap Gameplay
        {
            get
            {
                if (asset == null)
                {
                    asset = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("FoldwayGameplay"));
                    string saved = PlayerPrefs.GetString(BindingsPreference);
                    if (!string.IsNullOrEmpty(saved))
                    {
                        try { asset.LoadBindingOverridesFromJson(saved); }
                        catch (ArgumentException) { asset.RemoveAllBindingOverrides(); }
                    }
                }
                return asset.FindActionMap("Gameplay", throwIfNotFound: true);
            }
        }

        public static InputAction Action(string name) => Gameplay.FindAction(name, throwIfNotFound: true);

        public static string KeyName(string action, int index = 0)
        {
            InputAction input = Action(action);
            return input.bindings[index].effectivePath switch
            {
                "<Mouse>/leftButton" => "좌클릭",
                "<Mouse>/rightButton" => "우클릭",
                "<Mouse>/middleButton" => "휠 클릭",
                _ => input.GetBindingDisplayString(index)
            };
        }

        public static string MoveKeys => $"{KeyName("Move", 1)} / {KeyName("Move", 3)} / {KeyName("Move", 2)} / {KeyName("Move", 4)}";

        public static bool HasOverrides
        {
            get
            {
                foreach (InputBinding binding in Gameplay.bindings)
                    if (binding.overridePath != null) return true;
                return false;
            }
        }

        public static bool IsInUse(InputAction target, int index, InputControl control)
        {
            foreach (InputAction action in Gameplay.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if ((action != target || i != index) && !binding.isComposite &&
                        !string.IsNullOrEmpty(binding.effectivePath) &&
                        InputControlPath.Matches(binding.effectivePath, control)) return true;
                }
            return false;
        }

        public static void SaveOverrides()
        {
            PlayerPrefs.SetString(BindingsPreference, Gameplay.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetOverrides()
        {
            Gameplay.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingsPreference);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRuntimeAsset()
        {
            if (asset == null) return;
            asset.Disable();
            UnityEngine.Object.Destroy(asset);
            asset = null;
        }
    }
}
