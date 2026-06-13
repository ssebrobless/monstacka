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
            pauseButtons.Clear();
            if (settingsButton) pauseButtons.Add(settingsButton);
            if (quitButton) pauseButtons.Add(quitButton);
            if (homeButton) pauseButtons.Add(homeButton);
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
            CloseSettings();
            RefreshSettingsText();
            if (settingsButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
            }
        }

        private void Update()
        {
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
            gameManager?.ResumeGame();
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
