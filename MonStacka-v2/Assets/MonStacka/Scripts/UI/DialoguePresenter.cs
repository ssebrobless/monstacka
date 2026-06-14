using System;
using MonStacka.Core;
using MonStacka.Story;
using UnityEngine;
using UnityEngine.UI;

namespace MonStacka.UI
{
    /// <summary>
    /// Story dialogue overlay. Shows one line at a time (speaker name + text);
    /// advance with the menu submit key, hard drop key, or click. Fires onFinished
    /// when the last line is dismissed. GameManager blocks gameplay while visible.
    /// </summary>
    public sealed class DialoguePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text lineText;
        [SerializeField] private Text continueHintText;

        private DialogueLine[] lines = Array.Empty<DialogueLine>();
        private int index;
        private Action onFinished;
        private bool previousAdvanceHeld = true;
        private bool waitForAdvanceRelease;

        public bool IsActive => root && root.activeSelf;
        public bool IsWaitingForAdvanceRelease => waitForAdvanceRelease;

        public void Play(DialogueLine[] dialogue, Action finished)
        {
            if (dialogue == null || dialogue.Length == 0)
            {
                finished?.Invoke();
                return;
            }

            lines = dialogue;
            index = 0;
            onFinished = finished;
            previousAdvanceHeld = true;
            if (root)
            {
                root.SetActive(true);
            }

            ShowCurrentLine();
        }

        public void Skip()
        {
            Finish();
        }

        private void Update()
        {
            var advanceHeld = IsAdvanceHeld();
            if (waitForAdvanceRelease)
            {
                waitForAdvanceRelease = advanceHeld;
            }

            if (!IsActive)
            {
                return;
            }

            if (advanceHeld && !previousAdvanceHeld)
            {
                Advance();
            }

            previousAdvanceHeld = advanceHeld;
        }

        private void Advance()
        {
            index += 1;
            if (index >= lines.Length)
            {
                Finish();
                return;
            }

            ShowCurrentLine();
        }

        private void Finish()
        {
            if (root)
            {
                root.SetActive(false);
            }

            var callback = onFinished;
            onFinished = null;
            waitForAdvanceRelease = IsAdvanceHeld();
            callback?.Invoke();
        }

        private static bool IsAdvanceHeld() =>
            MonStackaControls.IsMenuSubmitHeld() ||
            MonStackaControls.IsGameplayHardDropHeld() ||
            Input.GetMouseButton(0);

        private void ShowCurrentLine()
        {
            var line = lines[index];
            if (speakerText)
            {
                speakerText.text = line.Speaker switch
                {
                    DialogueSpeaker.Player => line.IsThought ? "You (thinking)" : "You",
                    DialogueSpeaker.Narrator => string.Empty,
                    DialogueSpeaker.PaSystem => "PA System",
                    _ => string.Empty,
                };
            }

            if (lineText)
            {
                lineText.text = line.IsThought ? $"*{line.Text}*" : line.Text;
                lineText.fontStyle = line.Speaker == DialogueSpeaker.Narrator || line.IsThought
                    ? FontStyle.Italic
                    : FontStyle.Normal;
            }

            if (continueHintText)
            {
                continueHintText.text = index + 1 >= lines.Length
                    ? "Space / Enter / Click - continue"
                    : $"Space / Enter / Click - next  ({index + 1}/{lines.Length})";
            }
        }
    }
}
