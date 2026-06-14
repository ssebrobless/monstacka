using MonStacka.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace MonStacka.UI
{
    public sealed class GameSceneShellController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Button musicDownButton;
        [SerializeField] private Button musicUpButton;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Button sfxDownButton;
        [SerializeField] private Button sfxUpButton;
        [SerializeField] private Button ditherToggleButton;
        [SerializeField] private Button controlPreviousButton;
        [SerializeField] private Button controlBindButton;
        [SerializeField] private Button controlNextButton;
        [SerializeField] private Button controlResetButton;
        [SerializeField] private Button closeSettingsButton;

        private readonly List<Button> pauseButtons = new();
        private readonly List<Button> settingsButtons = new();
        private Button zanyToggleButton;
        private Text zanyToggleLabel;
        private bool lastZanyToggleVisible;
        private bool lastZanyToggleState;
        private bool previousNavigateUp;
        private bool previousNavigateDown;
        private bool previousSubmit;
        private bool previousCancel;
        private bool lastPausedState;
        private int selectedControlIndex;
        private MonStackaControlAction? awaitingBindingAction;
        private int bindingCaptureFrame;

        private void Awake()
        {
            settingsButton?.onClick.AddListener(OpenSettings);
            quitButton?.onClick.AddListener(() => gameManager?.QuitGame());
            homeButton?.onClick.AddListener(() => gameManager?.ReturnHome());
            musicToggleButton?.onClick.AddListener(ToggleMusic);
            musicDownButton?.onClick.AddListener(() => StepMusicVolume(-5));
            musicUpButton?.onClick.AddListener(() => StepMusicVolume(5));
            sfxToggleButton?.onClick.AddListener(ToggleSfx);
            sfxDownButton?.onClick.AddListener(() => StepSfxVolume(-5));
            sfxUpButton?.onClick.AddListener(() => StepSfxVolume(5));
            ditherToggleButton?.onClick.AddListener(ToggleDither);
            controlPreviousButton?.onClick.AddListener(() => StepSelectedControl(-1));
            controlBindButton?.onClick.AddListener(BeginControlBinding);
            controlNextButton?.onClick.AddListener(() => StepSelectedControl(1));
            controlResetButton?.onClick.AddListener(ResetControls);
            closeSettingsButton?.onClick.AddListener(CloseSettings);
            EnsureZanyToggleButton();
            pauseButtons.Clear();
            if (settingsButton) pauseButtons.Add(settingsButton);
            if (quitButton) pauseButtons.Add(quitButton);
            if (homeButton) pauseButtons.Add(homeButton);
            if (zanyToggleButton) pauseButtons.Add(zanyToggleButton);
            settingsButtons.Clear();
            if (musicToggleButton) settingsButtons.Add(musicToggleButton);
            if (musicDownButton) settingsButtons.Add(musicDownButton);
            if (musicUpButton) settingsButtons.Add(musicUpButton);
            if (sfxToggleButton) settingsButtons.Add(sfxToggleButton);
            if (sfxDownButton) settingsButtons.Add(sfxDownButton);
            if (sfxUpButton) settingsButtons.Add(sfxUpButton);
            if (ditherToggleButton) settingsButtons.Add(ditherToggleButton);
            if (controlPreviousButton) settingsButtons.Add(controlPreviousButton);
            if (controlBindButton) settingsButtons.Add(controlBindButton);
            if (controlNextButton) settingsButtons.Add(controlNextButton);
            if (controlResetButton) settingsButtons.Add(controlResetButton);
            if (closeSettingsButton) settingsButtons.Add(closeSettingsButton);
            if (settingsPanel)
            {
                settingsPanel.SetActive(false);
            }
            RefreshSettingsText();
            RefreshZanyToggleButton();
        }

        private void Update()
        {
            RefreshZanyToggleButton();

            if (gameManager != null && gameManager.IsDialogueInputBlocking)
            {
                ClearShellSelection();
                lastPausedState = false;
                return;
            }

            if (gameManager != null && gameManager.IsRestartConfirmActive)
            {
                lastPausedState = true;
                return;
            }

            if (gameManager != null && gameManager.IsEndRunPanelActive)
            {
                lastPausedState = false;
                return;
            }

            if (settingsPanel && settingsPanel.activeSelf)
            {
                HandleSettingsInput();
                return;
            }

            if (gameManager != null && gameManager.IsPaused)
            {
                EnsurePauseSelection();
                HandlePauseInput();
                lastPausedState = true;
            }
            else
            {
                ClearShellSelection();
                lastPausedState = false;
            }
        }

        private void OpenSettings()
        {
            if (settingsPanel)
            {
                settingsPanel.SetActive(true);
            }

            RefreshSettingsText();
            gameManager?.PauseIfRunning();

            if (EventSystem.current)
            {
                var selected = settingsButtons.Count > 0 ? settingsButtons[0] : closeSettingsButton;
                if (selected)
                {
                    EventSystem.current.SetSelectedGameObject(selected.gameObject);
                }
            }
        }

        private void CloseSettings()
        {
            if (settingsPanel)
            {
                settingsPanel.SetActive(false);
            }

            awaitingBindingAction = null;
            gameManager?.PauseIfRunning();
            if (settingsButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            }
        }

        private void RefreshSettingsText()
        {
            if (!settingsPanel)
            {
                return;
            }

            var settingsBody = settingsPanel.transform.Find("SettingsBody")?.GetComponent<Text>();
            if (!settingsBody)
            {
                return;
            }

            settingsBody.text =
                BuildSettingsSummaryText();
            RefreshSettingsButtonLabels();
        }

        private void ToggleDither()
        {
            MonStackaAppState.DitherEnabled = !MonStackaAppState.DitherEnabled;
            RefreshSettingsText();
        }

        private void ToggleMusic()
        {
            MonStackaAppState.MusicEnabled = !MonStackaAppState.MusicEnabled;
            RefreshSettingsText();
        }

        private void ToggleSfx()
        {
            MonStackaAppState.SfxEnabled = !MonStackaAppState.SfxEnabled;
            RefreshSettingsText();
        }

        private void StepMusicVolume(int delta)
        {
            MonStackaAppState.MusicVolume = Mathf.Clamp(MonStackaAppState.MusicVolume + delta, 0, 100);
            RefreshSettingsText();
        }

        private void StepSfxVolume(int delta)
        {
            MonStackaAppState.SfxVolume = Mathf.Clamp(MonStackaAppState.SfxVolume + delta, 0, 100);
            RefreshSettingsText();
        }

        private void StepSelectedControl(int delta)
        {
            var actions = MonStackaControls.OrderedActions;
            selectedControlIndex = (selectedControlIndex + delta + actions.Length) % actions.Length;
            awaitingBindingAction = null;
            RefreshSettingsText();
        }

        private void BeginControlBinding()
        {
            awaitingBindingAction = MonStackaControls.OrderedActions[selectedControlIndex];
            bindingCaptureFrame = Time.frameCount + 1;
            RefreshSettingsText();
        }

        private void ResetControls()
        {
            awaitingBindingAction = null;
            MonStackaControls.ResetKeyboardBindings();
            RefreshSettingsText();
        }

        private string BuildSettingsSummaryText()
        {
            var selectedAction = MonStackaControls.OrderedActions[selectedControlIndex];
            var bindingLine = awaitingBindingAction.HasValue
                ? $"Listening for {MonStackaControls.GetActionLabel(awaitingBindingAction.Value)}. Press a key, Esc to cancel."
                : $"{MonStackaControls.GetActionLabel(selectedAction)}: {MonStackaControls.FormatKeyboardBinding(selectedAction)}";
            return
                "Audio\n" +
                $"Music: {(MonStackaAppState.MusicEnabled ? "ON" : "OFF")}   Volume {MonStackaAppState.MusicVolume}\n" +
                $"SFX: {(MonStackaAppState.SfxEnabled ? "ON" : "OFF")}   Volume {MonStackaAppState.SfxVolume}\n\n" +
                "Visual\n" +
                $"Dither overlay: {(MonStackaAppState.DitherEnabled ? "ON" : "OFF")}\n\n" +
                "Controls\n" +
                bindingLine + "\n\n" +
                MonStackaControls.BuildControlsSummaryText();
        }

        private void RefreshSettingsButtonLabels()
        {
            SetButtonLabel(musicToggleButton, $"Music {(MonStackaAppState.MusicEnabled ? "ON" : "OFF")}");
            SetButtonLabel(musicDownButton, "Music -");
            SetButtonLabel(musicUpButton, "Music +");
            SetButtonLabel(sfxToggleButton, $"SFX {(MonStackaAppState.SfxEnabled ? "ON" : "OFF")}");
            SetButtonLabel(sfxDownButton, "SFX -");
            SetButtonLabel(sfxUpButton, "SFX +");
            SetButtonLabel(ditherToggleButton, $"Dither {(MonStackaAppState.DitherEnabled ? "ON" : "OFF")}");
            SetButtonLabel(controlPreviousButton, "< Control");
            SetButtonLabel(controlBindButton, awaitingBindingAction.HasValue ? "Listening..." : "Bind Key");
            SetButtonLabel(controlNextButton, "Control >");
            SetButtonLabel(controlResetButton, "Defaults");
        }

        private void EnsureZanyToggleButton()
        {
            if (zanyToggleButton || !settingsButton)
            {
                return;
            }

            var template = settingsButton.GetComponent<RectTransform>();
            var parent = template ? template.parent : settingsButton.transform.parent;
            if (!parent)
            {
                return;
            }

            var go = new GameObject("TrainingZanyToggleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (template)
            {
                rect.anchorMin = template.anchorMin;
                rect.anchorMax = template.anchorMax;
                rect.pivot = template.pivot;
                rect.anchoredPosition = template.anchoredPosition + new Vector2(0f, -78f);
                rect.sizeDelta = template.sizeDelta;
            }
            else
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-26f, -260f);
                rect.sizeDelta = new Vector2(88f, 58f);
            }

            var image = go.GetComponent<Image>();
            image.color = new Color(0.14f, 0.34f, 0.18f, 0.82f);
            zanyToggleButton = go.GetComponent<Button>();
            zanyToggleButton.targetGraphic = image;
            zanyToggleButton.onClick.AddListener(() =>
            {
                gameManager?.ToggleFriendlyAbilitiesAndRestart();
                RefreshZanyToggleButton(true);
            });

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            zanyToggleLabel = labelGo.GetComponent<Text>();
            zanyToggleLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            zanyToggleLabel.fontSize = 15;
            zanyToggleLabel.fontStyle = FontStyle.Bold;
            zanyToggleLabel.alignment = TextAnchor.MiddleCenter;
            zanyToggleLabel.color = Color.white;
            zanyToggleLabel.raycastTarget = false;
            go.SetActive(false);
        }

        private void RefreshZanyToggleButton(bool force = false)
        {
            if (!zanyToggleButton || gameManager == null)
            {
                return;
            }

            var visible = gameManager.CanToggleFriendlyAbilities;
            var enabled = gameManager.FriendlyAbilitiesEnabled;
            if (!force && visible == lastZanyToggleVisible && enabled == lastZanyToggleState)
            {
                return;
            }

            lastZanyToggleVisible = visible;
            lastZanyToggleState = enabled;
            zanyToggleButton.gameObject.SetActive(visible);
            if (zanyToggleLabel)
            {
                zanyToggleLabel.text = enabled ? "ZANY ON" : "ZANY OFF";
            }

            var image = zanyToggleButton.GetComponent<Image>();
            if (image)
            {
                image.color = enabled
                    ? new Color(0.12f, 0.58f, 0.22f, 0.9f)
                    : new Color(0.20f, 0.23f, 0.36f, 0.9f);
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button ? button.GetComponentInChildren<Text>() : null;
            if (label) label.text = value;
        }

        private void HandlePauseInput()
        {
            var navigateUp = IsNavigateUpHeld();
            var navigateDown = IsNavigateDownHeld();
            var submit = IsSubmitHeld();

            if (WasPressed(navigateUp, ref previousNavigateUp))
            {
                MoveSelection(-1);
            }

            if (WasPressed(navigateDown, ref previousNavigateDown))
            {
                MoveSelection(1);
            }

            if (WasPressed(submit, ref previousSubmit))
            {
                ActivateSelectedButton();
            }

            previousCancel = false;
        }

        private void EnsurePauseSelection()
        {
            if (!EventSystem.current)
            {
                return;
            }

            if (!lastPausedState || !EventSystem.current.currentSelectedGameObject)
            {
                if (settingsButton)
                {
                    EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
                }
            }
        }

        private void HandleSettingsInput()
        {
            var navigateUp = IsNavigateUpHeld();
            var navigateDown = IsNavigateDownHeld();
            var submit = IsSubmitHeld();
            var cancel = IsCancelHeld();

            if (HandleBindingCapture())
            {
                previousNavigateUp = navigateUp;
                previousNavigateDown = navigateDown;
                previousSubmit = submit;
                previousCancel = cancel;
                return;
            }

            if (WasPressed(navigateUp, ref previousNavigateUp))
            {
                MoveSettingsSelection(-1);
            }

            if (WasPressed(navigateDown, ref previousNavigateDown))
            {
                MoveSettingsSelection(1);
            }

            if (WasPressed(cancel, ref previousCancel))
            {
                CloseSettings();
            }
            else if (WasPressed(submit, ref previousSubmit))
            {
                ActivateSelectedButton();
            }

        }

        private void MoveSelection(int direction)
        {
            if (pauseButtons.Count == 0 || !EventSystem.current)
            {
                return;
            }

            var current = EventSystem.current.currentSelectedGameObject;
            var currentIndex = pauseButtons.FindIndex(button => button && button.gameObject == current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = Mathf.Clamp(currentIndex + direction, 0, pauseButtons.Count - 1);
            var nextButton = pauseButtons[nextIndex];
            if (nextButton)
            {
                EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }
        }

        private void ClearShellSelection()
        {
            if (!EventSystem.current)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected && IsShellButtonSelected(selected))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private bool IsShellButtonSelected(GameObject selected)
        {
            foreach (var button in pauseButtons)
            {
                if (button && button.gameObject == selected)
                {
                    return true;
                }
            }

            foreach (var button in settingsButtons)
            {
                if (button && button.gameObject == selected)
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveSettingsSelection(int direction)
        {
            if (settingsButtons.Count == 0 || !EventSystem.current)
            {
                return;
            }

            var current = EventSystem.current.currentSelectedGameObject;
            var currentIndex = settingsButtons.FindIndex(button => button && button.gameObject == current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = Mathf.Clamp(currentIndex + direction, 0, settingsButtons.Count - 1);
            var nextButton = settingsButtons[nextIndex];
            if (nextButton)
            {
                EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }
        }

        private bool HandleBindingCapture()
        {
            if (!awaitingBindingAction.HasValue)
            {
                return false;
            }

            if (Time.frameCount <= bindingCaptureFrame)
            {
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                awaitingBindingAction = null;
                RefreshSettingsText();
                return true;
            }

            var keyCode = MonStackaControls.ReadPressedKeyboardBindingKey();
            if (keyCode.HasValue)
            {
                MonStackaControls.SetPrimaryKeyboardBinding(awaitingBindingAction.Value, keyCode.Value);
                awaitingBindingAction = null;
                RefreshSettingsText();
            }

            return true;
        }

        private static void ActivateSelectedButton()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (!selected)
            {
                return;
            }

            var button = selected.GetComponent<Button>();
            button?.onClick.Invoke();
        }

        private static bool WasPressed(bool current, ref bool previous)
        {
            var pressed = current && !previous;
            previous = current;
            return pressed;
        }

        private static bool IsNavigateUpHeld() => MonStackaControls.IsMenuUpHeld();

        private static bool IsNavigateDownHeld() => MonStackaControls.IsMenuDownHeld();

        private static bool IsSubmitHeld() => MonStackaControls.IsMenuSubmitHeld();

        private static bool IsCancelHeld() => MonStackaControls.IsMenuCancelHeld();
    }
}
