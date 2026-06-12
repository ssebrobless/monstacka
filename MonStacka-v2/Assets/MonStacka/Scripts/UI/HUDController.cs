using MonStacka.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MonStacka.UI
{
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private Text modeDescriptionText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text goalValueText;
        [SerializeField] private Text linesText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text leaderboardTitleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text assistText;
        [SerializeField] private Text[] leaderboardValueTexts;

        private MonStackaMode currentMode;
        private int lastScore = int.MinValue;
        private int lastLines = int.MinValue;
        private int lastWholeMilliseconds = -1;
        private bool lastGameOver;
        private bool lastPaused;
        private bool lastCountdownActive;

        public void Configure(MonStackaMode mode, MonStacka.Story.StoryChapterSpec storyChapter = null)
        {
            currentMode = mode;

            if (modeDescriptionText)
            {
                modeDescriptionText.text = storyChapter != null
                    ? $"{storyChapter.Id} \"{storyChapter.Title}\""
                    : mode switch
                    {
                        MonStackaMode.Ogbm => "Play until you top out. Chase the highest score you can post.",
                        MonStackaMode.Sprint40 => "Clear 40 lines as fast as you can.",
                        MonStackaMode.Training => "Practice clean stacking and rotations without sprint pressure.",
                        _ => "MonStacka mode.",
                    };
            }

            if (goalValueText)
            {
                goalValueText.text = storyChapter != null
                    ? DescribeObjective(storyChapter.Objective)
                    : mode switch
                    {
                        MonStackaMode.Sprint40 => "40 LINES",
                        MonStackaMode.Training => "PRACTICE",
                        _ => "ENDLESS",
                    };
            }

            if (leaderboardTitleText)
            {
                leaderboardTitleText.text = mode switch
                {
                    MonStackaMode.Sprint40 => "Top 3 Times",
                    MonStackaMode.Story => "Mission",
                    _ => "Top 3 Scores",
                };
            }
        }

        private static string DescribeObjective(MonStacka.Story.StoryObjective objective)
        {
            return objective.Kind switch
            {
                MonStacka.Story.StoryObjectiveKind.ClearLines => $"{objective.TargetLines} LINES",
                MonStacka.Story.StoryObjectiveKind.ClearLinesTimed =>
                    $"{objective.TargetLines} IN {Mathf.RoundToInt(objective.TimeLimitSeconds)}s",
                MonStacka.Story.StoryObjectiveKind.ReachScore => $"{objective.TargetScore} PTS",
                MonStacka.Story.StoryObjectiveKind.SurviveSeconds =>
                    $"SURVIVE {Mathf.RoundToInt(objective.TimeLimitSeconds)}s",
                _ => "MISSION",
            };
        }

        public void Render(MonStackaMode mode, int score, int lines, float elapsedSeconds, bool gameOver, bool paused, float countdownRemaining = 0f)
        {
            if (mode != currentMode)
            {
                Configure(mode);
            }

            var countdownActive = countdownRemaining > 0f;

            if (scoreText && score != lastScore)
            {
                scoreText.text = $"{score}";
                lastScore = score;
            }

            if (linesText && lines != lastLines)
            {
                linesText.text = $"{lines}";
                lastLines = lines;
            }

            var wholeMilliseconds = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds * 1000f));
            if (timeText && wholeMilliseconds != lastWholeMilliseconds)
            {
                timeText.text = FormatTime(elapsedSeconds);
                lastWholeMilliseconds = wholeMilliseconds;
            }

            if (statusText && !storyStatusActive && (paused != lastPaused || gameOver != lastGameOver || countdownActive != lastCountdownActive))
            {
                statusText.text = paused
                    ? "Run paused."
                    : gameOver
                        ? "Run complete."
                        : countdownActive
                            ? "Get ready..."
                            : mode switch
                            {
                                MonStackaMode.Ogbm => "O.G.B.M. active.",
                                MonStackaMode.Sprint40 => "X(4)-LINES active.",
                                MonStackaMode.Training => "Training active.",
                                _ => "MonStacka active.",
                            };
                lastPaused = paused;
                lastGameOver = gameOver;
                lastCountdownActive = countdownActive;
            }
        }

        private bool storyStatusActive;
        private string lastStoryStatus;

        /// <summary>Story modifier chips/failure text. Takes over the status line while non-empty.</summary>
        public void RenderStoryStatus(string chips, bool hintsMuted)
        {
            if (!statusText)
            {
                return;
            }

            var display = hintsMuted && string.IsNullOrEmpty(chips) ? " " : chips;
            storyStatusActive = !string.IsNullOrEmpty(display);
            if (storyStatusActive && display != lastStoryStatus)
            {
                statusText.text = display;
                lastStoryStatus = display;
            }

            assistMuted = hintsMuted;
            if (assistText && hintsMuted && assistText.text.Length > 0)
            {
                assistText.text = string.Empty;
                lastAssistDisplay = string.Empty;
            }
        }

        /// <summary>Fills the top-3 leaderboard rows ("---" for empty slots).</summary>
        public void RenderLeaderboard(System.Collections.Generic.IReadOnlyList<string> rows)
        {
            if (leaderboardValueTexts == null)
            {
                return;
            }

            for (var index = 0; index < leaderboardValueTexts.Length; index += 1)
            {
                if (leaderboardValueTexts[index])
                {
                    leaderboardValueTexts[index].text = rows != null && index < rows.Count ? rows[index] : "---";
                }
            }
        }

        private string lastAssistDisplay;

        private bool assistMuted;
        private int lastAssistProgress = -1;
        private int lastAssistWindowSeconds = -1;
        private AssistType? lastAssistWindowType;

        /// <summary>Held-assist progress and active effect (handoff UI requirement).</summary>
        public void RenderAssist(AssistEffectSystem assist)
        {
            if (!assistText || assistMuted)
            {
                return;
            }

            if (assist == null)
            {
                if (lastAssistDisplay != string.Empty)
                {
                    assistText.text = string.Empty;
                    lastAssistDisplay = string.Empty;
                }
                return;
            }

            // Only rebuild the string when the visible state actually changes.
            var windowType = assist.ActiveWindow;
            var windowSeconds = windowType.HasValue ? Mathf.CeilToInt(assist.WindowRemaining) : -1;
            var progress = windowType.HasValue ? -1 : assist.HeldProgress;
            if (windowType == lastAssistWindowType && windowSeconds == lastAssistWindowSeconds && progress == lastAssistProgress)
            {
                return;
            }

            lastAssistWindowType = windowType;
            lastAssistWindowSeconds = windowSeconds;
            lastAssistProgress = progress;
            lastAssistDisplay = windowType.HasValue
                ? $"{AssistEffectSystem.LabelFor(windowType.Value)} {windowSeconds}s"
                : $"ASSIST {progress}/{AssistEffectSystem.TriggerEvery}";
            assistText.text = lastAssistDisplay;
        }

        private static string FormatTime(float elapsedSeconds)
        {
            var totalMs = Mathf.Max(0, Mathf.RoundToInt(elapsedSeconds * 1000f));
            var minutes = totalMs / 60000;
            var seconds = (totalMs / 1000) % 60;
            var milliseconds = totalMs % 1000;
            return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
        }
    }
}
