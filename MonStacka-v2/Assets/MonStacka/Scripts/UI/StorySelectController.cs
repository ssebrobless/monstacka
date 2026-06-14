using System.Collections.Generic;
using MonStacka.Core;
using MonStacka.Story;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MonStacka.UI
{
    /// <summary>
    /// Story chapter select. Builds one row per catalog chapter at runtime:
    /// unlocked chapters are clickable, locked ones are dimmed. Completing a
    /// chapter unlocks the next (StoryProgress). Back returns to Home.
    /// </summary>
    public sealed class StorySelectController : MonoBehaviour
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private Button backButton;
        [SerializeField] private Text headerText;

        private readonly List<Button> chapterButtons = new();

        private void Start()
        {
            if (backButton)
            {
                backButton.onClick.AddListener(() => SceneManager.LoadScene("Home"));
            }

            if (headerText)
            {
                var current = StoryProgress.CurrentChapter();
                headerText.text = $"PORTENTUM BIOSCIENCE - INTERACTIVE TOUR\nNext stop: {current.Id} \"{current.Title}\"";
            }

            BuildChapterList();
        }

        private void BuildChapterList()
        {
            if (!listRoot)
            {
                return;
            }

            chapterButtons.Clear();
            var index = 0;
            foreach (var chapter in StoryCatalog.Chapters)
            {
                var unlocked = StoryProgress.IsUnlocked(chapter.Id);
                var completed = StoryProgress.IsCompleted(chapter.Id);
                var row = CreateChapterRow(chapter, unlocked, completed, index);
                if (row)
                {
                    chapterButtons.Add(row);
                }
                index += 1;
            }

            for (var i = 0; i < chapterButtons.Count; i += 1)
            {
                var navigation = chapterButtons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = i > 0 ? chapterButtons[i - 1] : backButton;
                navigation.selectOnDown = i + 1 < chapterButtons.Count ? chapterButtons[i + 1] : backButton;
                chapterButtons[i].navigation = navigation;
            }

            if (chapterButtons.Count > 0)
            {
                chapterButtons[0].Select();
            }
        }

        private Button CreateChapterRow(StoryChapterSpec chapter, bool unlocked, bool completed, int index)
        {
            const float rowHeight = 46f;
            const float columnWidth = 560f;
            var column = index / 10;
            var rowInColumn = index % 10;

            var go = new GameObject($"Chapter_{chapter.Id}");
            go.transform.SetParent(listRoot, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f + (column * (columnWidth + 40f)), -(rowInColumn * (rowHeight + 10f)));
            rect.sizeDelta = new Vector2(columnWidth, rowHeight);

            var image = go.AddComponent<Image>();
            image.color = unlocked
                ? new Color(0.16f, 0.2f, 0.38f, 0.95f)
                : new Color(0.1f, 0.11f, 0.16f, 0.85f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.color = unlocked ? new Color(0.94f, 0.95f, 0.98f, 1f) : new Color(0.45f, 0.46f, 0.52f, 1f);
            var status = completed ? " - CLEARED" : unlocked ? string.Empty : " - LOCKED";
            var draft = chapter.DialogueIsGeneratedDraft ? " *" : string.Empty;
            label.text = $"  {chapter.Id}  \"{chapter.Title}\"{status}{draft}";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            if (!unlocked)
            {
                return null;
            }

            var button = go.AddComponent<Button>();
            var chapterId = chapter.Id;
            button.onClick.AddListener(() => StartChapter(chapterId));
            return button;
        }

        private static void StartChapter(string chapterId)
        {
            var chapter = StoryCatalog.GetChapter(chapterId);
            if (chapter == null)
            {
                return;
            }

            MonStackaAppState.SelectedMode = MonStackaMode.Story;
            MonStackaAppState.FriendlyAbilitiesEnabled = true;
            MonStackaAppState.SelectedStoryChapterId = chapterId;
            MonStackaAppState.GravitySeconds = chapter.GravitySeconds;
            MonStackaAppState.LockDelaySeconds = chapter.LockDelaySeconds;
            StoryProgress.SetCurrentChapter(chapterId);
            SceneManager.LoadScene("Game");
        }
    }
}
