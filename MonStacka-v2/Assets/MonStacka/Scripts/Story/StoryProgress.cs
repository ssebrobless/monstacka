using System.Linq;
using UnityEngine;

namespace MonStacka.Story
{
    /// <summary>
    /// PlayerPrefs-backed campaign progress. Chapters unlock strictly in catalog
    /// order; completing a chapter unlocks the next and records best score/time.
    /// </summary>
    public static class StoryProgress
    {
        private const string CompletedKeyPrefix = "monstacka.story.completed.";
        private const string BestScoreKeyPrefix = "monstacka.story.bestScore.";
        private const string BestTimeKeyPrefix = "monstacka.story.bestTime.";
        private const string CurrentChapterKey = "monstacka.story.current";

        public static bool IsCompleted(string chapterId) =>
            PlayerPrefs.GetInt(CompletedKeyPrefix + chapterId, 0) == 1;

        public static bool IsUnlocked(string chapterId)
        {
            var chapters = StoryCatalog.Chapters;
            for (var index = 0; index < chapters.Count; index += 1)
            {
                if (chapters[index].Id != chapterId)
                {
                    continue;
                }
                return index == 0 || IsCompleted(chapters[index - 1].Id);
            }
            return false;
        }

        public static int BestScore(string chapterId) =>
            PlayerPrefs.GetInt(BestScoreKeyPrefix + chapterId, 0);

        /// <summary>Best clear time in seconds, or 0 when never completed.</summary>
        public static float BestTime(string chapterId) =>
            PlayerPrefs.GetFloat(BestTimeKeyPrefix + chapterId, 0f);

        /// <summary>The first unlocked-but-incomplete chapter (continue point).</summary>
        public static StoryChapterSpec CurrentChapter()
        {
            var saved = PlayerPrefs.GetString(CurrentChapterKey, null);
            if (!string.IsNullOrEmpty(saved))
            {
                var savedChapter = StoryCatalog.GetChapter(saved);
                if (savedChapter != null && IsUnlocked(saved))
                {
                    return savedChapter;
                }
            }

            return StoryCatalog.Chapters.FirstOrDefault(chapter => !IsCompleted(chapter.Id))
                ?? StoryCatalog.Chapters[StoryCatalog.Chapters.Count - 1];
        }

        public static void SetCurrentChapter(string chapterId)
        {
            PlayerPrefs.SetString(CurrentChapterKey, chapterId);
            PlayerPrefs.Save();
        }

        public static void RecordCompletion(string chapterId, int score, float clearSeconds)
        {
            PlayerPrefs.SetInt(CompletedKeyPrefix + chapterId, 1);

            if (score > BestScore(chapterId))
            {
                PlayerPrefs.SetInt(BestScoreKeyPrefix + chapterId, score);
            }

            var bestTime = BestTime(chapterId);
            if (clearSeconds > 0f && (bestTime <= 0f || clearSeconds < bestTime))
            {
                PlayerPrefs.SetFloat(BestTimeKeyPrefix + chapterId, clearSeconds);
            }

            var chapter = StoryCatalog.GetChapter(chapterId);
            if (chapter?.UnlocksNext != null)
            {
                PlayerPrefs.SetString(CurrentChapterKey, chapter.UnlocksNext);
            }

            PlayerPrefs.Save();
        }

        /// <summary>Wipes all campaign progress (debug/new game).</summary>
        public static void ResetAll()
        {
            foreach (var chapter in StoryCatalog.Chapters)
            {
                PlayerPrefs.DeleteKey(CompletedKeyPrefix + chapter.Id);
                PlayerPrefs.DeleteKey(BestScoreKeyPrefix + chapter.Id);
                PlayerPrefs.DeleteKey(BestTimeKeyPrefix + chapter.Id);
            }
            PlayerPrefs.DeleteKey(CurrentChapterKey);
            PlayerPrefs.Save();
        }
    }
}
