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
        [SerializeField] private Button closeSettingsButton;

        private readonly List<Button> pauseButtons = new();
        private bool previousNavigateUp;
        private bool previousNavigateDown;
        private bool previousSubmit;
        private bool previousCancel;
        private bool lastPausedState;

        private void Awake()
        {
            settingsButton?.onClick.AddListener(OpenSettings);
            quitButton?.onClick.AddListener(() => gameManager?.QuitGame());
            homeButton?.onClick.AddListener(() => gameManager?.ReturnHome());
            closeSettingsButton?.onClick.AddListener(CloseSettings);
            pauseButtons.Clear();
            if (settingsButton) pauseButtons.Add(settingsButton);
            if (quitButton) pauseButtons.Add(quitButton);
            if (homeButton) pauseButtons.Add(homeButton);
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

            if (closeSettingsButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(closeSettingsButton.gameObject);
            }
        }

        private void CloseSettings()
        {
            if (settingsPanel)
            {
                settingsPanel.SetActive(false);
            }

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
                MonStackaControls.BuildControlsSummaryText();
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
            var submit = IsSubmitHeld();
            var cancel = IsCancelHeld();

            if (WasPressed(cancel, ref previousCancel))
            {
                CloseSettings();
            }
            else if (WasPressed(submit, ref previousSubmit))
            {
                ActivateSelectedButton();
            }

            previousNavigateUp = false;
            previousNavigateDown = false;
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
