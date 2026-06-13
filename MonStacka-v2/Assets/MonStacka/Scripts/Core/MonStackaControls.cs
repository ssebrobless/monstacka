using UnityEngine;

namespace MonStacka.Core
{
    public enum MonStackaControlAction
    {
        Left,
        Right,
        Soft,
        Hard,
        RotateCcw,
        RotateCw,
        RotateFlip,
        Hold,
        Retry,
        Pause,
        RestartPaused,
    }

    public static class MonStackaControls
    {
        public static readonly MonStackaControlAction[] OrderedActions =
        {
            MonStackaControlAction.Left,
            MonStackaControlAction.Right,
            MonStackaControlAction.Soft,
            MonStackaControlAction.Hard,
            MonStackaControlAction.RotateCcw,
            MonStackaControlAction.RotateCw,
            MonStackaControlAction.RotateFlip,
            MonStackaControlAction.Hold,
            MonStackaControlAction.Retry,
            MonStackaControlAction.Pause,
            MonStackaControlAction.RestartPaused,
        };

        private readonly struct AxisBinding
        {
            public AxisBinding(string axisName, int direction, string label)
            {
                AxisName = axisName;
                Direction = direction;
                Label = label;
            }

            public string AxisName { get; }
            public int Direction { get; }
            public string Label { get; }
        }

        private const float AxisThreshold = 0.5f;

        private static readonly System.Collections.Generic.Dictionary<MonStackaControlAction, string> ActionLabels = new()
        {
            [MonStackaControlAction.Left] = "Move Left",
            [MonStackaControlAction.Right] = "Move Right",
            [MonStackaControlAction.Soft] = "Soft Drop",
            [MonStackaControlAction.Hard] = "Hard Drop",
            [MonStackaControlAction.RotateCcw] = "Rotate CCW",
            [MonStackaControlAction.RotateCw] = "Rotate CW",
            [MonStackaControlAction.RotateFlip] = "Rotate 180",
            [MonStackaControlAction.Hold] = "Hold",
            [MonStackaControlAction.Retry] = "Retry",
            [MonStackaControlAction.Pause] = "Pause / Resume",
            [MonStackaControlAction.RestartPaused] = "Restart Paused",
        };

        private static readonly System.Collections.Generic.Dictionary<MonStackaControlAction, KeyCode[]> KeyboardBindings = new()
        {
            [MonStackaControlAction.Left] = new[] { KeyCode.LeftArrow },
            [MonStackaControlAction.Right] = new[] { KeyCode.RightArrow },
            [MonStackaControlAction.Soft] = new[] { KeyCode.DownArrow },
            [MonStackaControlAction.Hard] = new[] { KeyCode.Space },
            [MonStackaControlAction.RotateCcw] = new[] { KeyCode.Z },
            [MonStackaControlAction.RotateCw] = new[] { KeyCode.X },
            [MonStackaControlAction.RotateFlip] = new[] { KeyCode.A },
            [MonStackaControlAction.Hold] = new[] { KeyCode.C },
            [MonStackaControlAction.Retry] = new[] { KeyCode.R },
            [MonStackaControlAction.Pause] = new[] { KeyCode.P, KeyCode.Escape },
            [MonStackaControlAction.RestartPaused] = new[] { KeyCode.O },
        };

        private static readonly System.Collections.Generic.Dictionary<MonStackaControlAction, KeyCode[]> DefaultKeyboardBindings = new()
        {
            [MonStackaControlAction.Left] = new[] { KeyCode.LeftArrow },
            [MonStackaControlAction.Right] = new[] { KeyCode.RightArrow },
            [MonStackaControlAction.Soft] = new[] { KeyCode.DownArrow },
            [MonStackaControlAction.Hard] = new[] { KeyCode.Space },
            [MonStackaControlAction.RotateCcw] = new[] { KeyCode.Z },
            [MonStackaControlAction.RotateCw] = new[] { KeyCode.X },
            [MonStackaControlAction.RotateFlip] = new[] { KeyCode.A },
            [MonStackaControlAction.Hold] = new[] { KeyCode.C },
            [MonStackaControlAction.Retry] = new[] { KeyCode.R },
            [MonStackaControlAction.Pause] = new[] { KeyCode.P, KeyCode.Escape },
            [MonStackaControlAction.RestartPaused] = new[] { KeyCode.O },
        };

        private static readonly System.Collections.Generic.Dictionary<MonStackaControlAction, KeyCode[]> GamepadButtonBindings = new()
        {
            [MonStackaControlAction.Left] = new[] { KeyCode.JoystickButton14 },
            [MonStackaControlAction.Right] = new[] { KeyCode.JoystickButton15 },
            [MonStackaControlAction.Soft] = new[] { KeyCode.JoystickButton13 },
            [MonStackaControlAction.Hard] = new[] { KeyCode.JoystickButton12 },
            [MonStackaControlAction.RotateCcw] = new[] { KeyCode.JoystickButton0 },
            [MonStackaControlAction.RotateCw] = new[] { KeyCode.JoystickButton1 },
            [MonStackaControlAction.RotateFlip] = new[] { KeyCode.JoystickButton3 },
            [MonStackaControlAction.Hold] = new[] { KeyCode.JoystickButton4 },
            [MonStackaControlAction.Retry] = new[] { KeyCode.JoystickButton9 },
            [MonStackaControlAction.Pause] = new[] { KeyCode.JoystickButton8 },
            [MonStackaControlAction.RestartPaused] = new[] { KeyCode.JoystickButton10 },
        };

        private static readonly System.Collections.Generic.Dictionary<MonStackaControlAction, AxisBinding[]> GamepadAxisBindings = new()
        {
            [MonStackaControlAction.Left] = new[] { new AxisBinding("Horizontal", -1, "Left Stick Left") },
            [MonStackaControlAction.Right] = new[] { new AxisBinding("Horizontal", 1, "Left Stick Right") },
            [MonStackaControlAction.Soft] = new[] { new AxisBinding("Vertical", -1, "Left Stick Down") },
            [MonStackaControlAction.Hard] = new[] { new AxisBinding("Vertical", 1, "Left Stick Up") },
        };

        public static float ReadHorizontalAxis() => Input.GetAxisRaw("Horizontal");

        public static float ReadVerticalAxis() => Input.GetAxisRaw("Vertical");

        public static bool IsActionHeld(MonStackaControlAction action)
        {
            if (KeyboardBindings.TryGetValue(action, out var keys))
            {
                foreach (var key in keys)
                {
                    if (Input.GetKey(key))
                    {
                        return true;
                    }
                }
            }

            if (GamepadButtonBindings.TryGetValue(action, out var buttons))
            {
                foreach (var button in buttons)
                {
                    if (Input.GetKey(button))
                    {
                        return true;
                    }
                }
            }

            if (GamepadAxisBindings.TryGetValue(action, out var axes))
            {
                foreach (var axis in axes)
                {
                    var value = Input.GetAxisRaw(axis.AxisName);
                    if ((axis.Direction < 0 && value <= -AxisThreshold) ||
                        (axis.Direction > 0 && value >= AxisThreshold))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsGameplayLeftHeld() => IsActionHeld(MonStackaControlAction.Left);

        public static bool IsGameplayRightHeld() => IsActionHeld(MonStackaControlAction.Right);

        public static bool IsGameplaySoftDropHeld() => IsActionHeld(MonStackaControlAction.Soft);

        public static bool IsGameplayHardDropHeld() => IsActionHeld(MonStackaControlAction.Hard);

        public static bool IsGameplayRotateCcwHeld() => IsActionHeld(MonStackaControlAction.RotateCcw);

        public static bool IsGameplayRotateCwHeld() => IsActionHeld(MonStackaControlAction.RotateCw);

        public static bool IsGameplayRotateFlipHeld() => IsActionHeld(MonStackaControlAction.RotateFlip);

        public static bool IsGameplayHoldHeld() => IsActionHeld(MonStackaControlAction.Hold);

        public static bool IsPauseHeld() => IsActionHeld(MonStackaControlAction.Pause);

        public static bool IsRetryHeld() => IsActionHeld(MonStackaControlAction.Retry);

        public static bool IsRestartPausedHeld() => IsActionHeld(MonStackaControlAction.RestartPaused);

        public static bool IsMenuUpHeld() =>
            Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.JoystickButton12) ||
            ReadVerticalAxis() >= 0.5f;

        public static bool IsMenuDownHeld() =>
            Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.JoystickButton13) ||
            ReadVerticalAxis() <= -0.5f;

        public static bool IsMenuCycleLeftHeld() =>
            Input.GetKey(KeyCode.LeftArrow) ||
            Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.JoystickButton14) ||
            Input.GetKey(KeyCode.JoystickButton4) ||
            ReadHorizontalAxis() <= -0.5f;

        public static bool IsMenuCycleRightHeld() =>
            Input.GetKey(KeyCode.RightArrow) ||
            Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.JoystickButton15) ||
            Input.GetKey(KeyCode.JoystickButton5) ||
            ReadHorizontalAxis() >= 0.5f;

        public static bool IsMenuSubmitHeld() =>
            Input.GetKey(KeyCode.Return) ||
            Input.GetKey(KeyCode.Space) ||
            Input.GetKey(KeyCode.JoystickButton0);

        public static bool IsMenuCancelHeld() =>
            Input.GetKey(KeyCode.Escape) ||
            Input.GetKey(KeyCode.Backspace) ||
            Input.GetKey(KeyCode.JoystickButton1);

        public static bool IsVoiceHeld() =>
            Input.GetKey(KeyCode.V) ||
            Input.GetKey(KeyCode.JoystickButton6);

        public static bool IsLoreHeld() =>
            Input.GetKey(KeyCode.L) ||
            Input.GetKey(KeyCode.JoystickButton7);

        public static string BuildControlsSummaryText()
        {
            return
                "Keyboard / Mouse\n" +
                $"{ActionLabels[MonStackaControlAction.Left]}: {FormatKeyboardBindings(MonStackaControlAction.Left)}\n" +
                $"{ActionLabels[MonStackaControlAction.Right]}: {FormatKeyboardBindings(MonStackaControlAction.Right)}\n" +
                $"{ActionLabels[MonStackaControlAction.Soft]}: {FormatKeyboardBindings(MonStackaControlAction.Soft)}\n" +
                $"{ActionLabels[MonStackaControlAction.Hard]}: {FormatKeyboardBindings(MonStackaControlAction.Hard)}\n" +
                $"{ActionLabels[MonStackaControlAction.RotateCcw]}: {FormatKeyboardBindings(MonStackaControlAction.RotateCcw)}    {ActionLabels[MonStackaControlAction.RotateCw]}: {FormatKeyboardBindings(MonStackaControlAction.RotateCw)}\n" +
                $"{ActionLabels[MonStackaControlAction.RotateFlip]}: {FormatKeyboardBindings(MonStackaControlAction.RotateFlip)}    {ActionLabels[MonStackaControlAction.Hold]}: {FormatKeyboardBindings(MonStackaControlAction.Hold)}\n" +
                $"{ActionLabels[MonStackaControlAction.Pause]}: {FormatKeyboardBindings(MonStackaControlAction.Pause)}\n" +
                $"{ActionLabels[MonStackaControlAction.Retry]}: {FormatKeyboardBindings(MonStackaControlAction.Retry)}    {ActionLabels[MonStackaControlAction.RestartPaused]}: {FormatKeyboardBindings(MonStackaControlAction.RestartPaused)}\n" +
                "Mouse: Click menu / settings buttons\n\n" +
                "Xbox\n" +
                $"{ActionLabels[MonStackaControlAction.Left]}: {FormatGamepadBindings(MonStackaControlAction.Left)}\n" +
                $"{ActionLabels[MonStackaControlAction.Right]}: {FormatGamepadBindings(MonStackaControlAction.Right)}\n" +
                $"{ActionLabels[MonStackaControlAction.Soft]}: {FormatGamepadBindings(MonStackaControlAction.Soft)}    {ActionLabels[MonStackaControlAction.Hard]}: {FormatGamepadBindings(MonStackaControlAction.Hard)}\n" +
                $"{ActionLabels[MonStackaControlAction.RotateCcw]}: {FormatGamepadBindings(MonStackaControlAction.RotateCcw)}    {ActionLabels[MonStackaControlAction.RotateCw]}: {FormatGamepadBindings(MonStackaControlAction.RotateCw)}\n" +
                $"{ActionLabels[MonStackaControlAction.RotateFlip]}: {FormatGamepadBindings(MonStackaControlAction.RotateFlip)}    {ActionLabels[MonStackaControlAction.Hold]}: {FormatGamepadBindings(MonStackaControlAction.Hold)}\n" +
                $"{ActionLabels[MonStackaControlAction.Pause]}: {FormatGamepadBindings(MonStackaControlAction.Pause)}\n" +
                $"{ActionLabels[MonStackaControlAction.Retry]}: {FormatGamepadBindings(MonStackaControlAction.Retry)}    {ActionLabels[MonStackaControlAction.RestartPaused]}: {FormatGamepadBindings(MonStackaControlAction.RestartPaused)}\n\n" +
                $"Current timing\nDAS {Mathf.RoundToInt(MonStackaAppState.DasSeconds * 1000f)}ms   ARR {Mathf.RoundToInt(MonStackaAppState.ArrSeconds * 1000f)}ms   Lock {Mathf.RoundToInt(MonStackaAppState.LockDelaySeconds * 1000f)}ms";
        }

        public static string GetActionLabel(MonStackaControlAction action) =>
            ActionLabels.TryGetValue(action, out var label) ? label : action.ToString();

        public static string FormatKeyboardBinding(MonStackaControlAction action) => FormatKeyboardBindings(action);

        public static void SetPrimaryKeyboardBinding(MonStackaControlAction action, KeyCode keyCode)
        {
            foreach (var pair in KeyboardBindings)
            {
                if (pair.Key == action || pair.Value == null)
                {
                    continue;
                }

                KeyboardBindings[pair.Key] = System.Array.FindAll(pair.Value, key => key != keyCode);
            }

            KeyboardBindings[action] = new[] { keyCode };
        }

        public static void ResetKeyboardBindings()
        {
            foreach (var pair in DefaultKeyboardBindings)
            {
                KeyboardBindings[pair.Key] = (KeyCode[])pair.Value.Clone();
            }
        }

        public static KeyCode? ReadPressedKeyboardBindingKey()
        {
            foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (keyCode == KeyCode.None ||
                    keyCode.ToString().StartsWith("JoystickButton"))
                {
                    continue;
                }

                if (Input.GetKeyDown(keyCode))
                {
                    return keyCode;
                }
            }

            return null;
        }

        private static string FormatKeyboardBindings(MonStackaControlAction action)
        {
            return KeyboardBindings.TryGetValue(action, out var keys)
                ? string.Join(" / ", System.Array.ConvertAll(keys, FormatKeyCode))
                : "Unbound";
        }

        private static string FormatGamepadBindings(MonStackaControlAction action)
        {
            var labels = new System.Collections.Generic.List<string>();
            if (GamepadButtonBindings.TryGetValue(action, out var buttons))
            {
                foreach (var button in buttons)
                {
                    labels.Add(FormatKeyCode(button));
                }
            }

            if (GamepadAxisBindings.TryGetValue(action, out var axes))
            {
                foreach (var axis in axes)
                {
                    labels.Add(axis.Label);
                }
            }

            return labels.Count > 0 ? string.Join(" / ", labels) : "Unbound";
        }

        private static string FormatKeyCode(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.LeftArrow => "Left Arrow",
                KeyCode.RightArrow => "Right Arrow",
                KeyCode.UpArrow => "Up Arrow",
                KeyCode.DownArrow => "Down Arrow",
                KeyCode.Space => "Space",
                KeyCode.Escape => "Esc",
                KeyCode.JoystickButton0 => "A",
                KeyCode.JoystickButton1 => "B",
                KeyCode.JoystickButton3 => "Y",
                KeyCode.JoystickButton4 => "LB",
                KeyCode.JoystickButton8 => "Back",
                KeyCode.JoystickButton9 => "Start",
                KeyCode.JoystickButton10 => "L3",
                KeyCode.JoystickButton12 => "D-pad Up",
                KeyCode.JoystickButton13 => "D-pad Down",
                KeyCode.JoystickButton14 => "D-pad Left",
                KeyCode.JoystickButton15 => "D-pad Right",
                _ => keyCode.ToString().StartsWith("Alpha") ? keyCode.ToString().Substring("Alpha".Length) : keyCode.ToString(),
            };
        }
    }
}
