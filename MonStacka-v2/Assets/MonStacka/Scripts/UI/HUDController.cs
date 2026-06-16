using MonStacka.Core;
using System.Collections.Generic;
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
        private MonStacka.Story.StoryObjective currentStoryObjective;
        private RectTransform bossHealthFillRect;
        private GameObject bossHealthBarRoot;
        private int lastBossScore = int.MinValue;
        private Text storyScoreLabelText;
        private Text storyScoreValueText;
        private Text storyBossLabelText;
        private Text storyEnemyStatusText;
        private Text storyEnemyTriggerText;
        private float storyEnemyTriggerUntil;
        private Color storyEnemyTriggerBaseColor = Color.white;
        private Text[] storyRankTexts;
        private string lastStoryEnemyStatus;
        private readonly List<PointPopup> pointPopups = new();

        private sealed class PointPopup
        {
            public Text Text;
            public RectTransform Rect;
            public Vector2 StartPosition;
            public float Age;
        }

        public void Configure(MonStackaMode mode, MonStacka.Story.StoryChapterSpec storyChapter = null)
        {
            currentMode = mode;
            currentStoryObjective = storyChapter?.Objective ?? default;
            lastScore = int.MinValue;
            lastLines = int.MinValue;
            lastWholeMilliseconds = -1;

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
                    ? string.Empty
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
                    MonStackaMode.Story => string.Empty,
                    _ => "Top 3 Scores",
                };
            }

            EnsureStoryRightHud();
            SetStoryRightHudVisible(mode == MonStackaMode.Story && currentStoryObjective.HasBossHealth);
            SetStoryLeaderboardVisible(mode != MonStackaMode.Story);
            EnsureBossHealthBar();
            SetBossHealthBarVisible(mode == MonStackaMode.Story && currentStoryObjective.HasBossHealth);
            lastBossScore = int.MinValue;
            lastStoryEnemyStatus = null;
        }

        private static string DescribeObjective(MonStacka.Story.StoryObjective objective)
        {
            if (objective.HasBossHealth)
            {
                return $"HP {objective.BossHealthPoints}";
            }

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
                scoreText.text = mode == MonStackaMode.Story ? string.Empty : $"{score}";
                if (storyScoreValueText)
                {
                    storyScoreValueText.text = mode == MonStackaMode.Story ? $"{score}" : string.Empty;
                }
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

            if (statusText && mode == MonStackaMode.Story)
            {
                statusText.text = string.Empty;
            }
            else if (statusText && !storyStatusActive && (paused != lastPaused || gameOver != lastGameOver || countdownActive != lastCountdownActive))
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

            if (mode == MonStackaMode.Story)
            {
                RenderBossHealth(score);
            }

            UpdatePointPopups();
            UpdateEnemyTriggerCue();
        }

        private void EnsureBossHealthBar()
        {
            if (bossHealthBarRoot)
            {
                return;
            }

            var anchorRect = currentMode == MonStackaMode.Story && leaderboardTitleText
                ? leaderboardTitleText.GetComponent<RectTransform>()
                : goalValueText ? goalValueText.GetComponent<RectTransform>() : null;
            if (!anchorRect || !anchorRect.parent)
            {
                return;
            }

            bossHealthBarRoot = new GameObject("BossHealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bossHealthBarRoot.transform.SetParent(anchorRect.parent, false);
            var rootRect = bossHealthBarRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = anchorRect.anchorMin;
            rootRect.anchorMax = anchorRect.anchorMax;
            rootRect.pivot = anchorRect.pivot;
            rootRect.anchoredPosition = currentMode == MonStackaMode.Story
                ? new Vector2(1340f, -262f)
                : anchorRect.anchoredPosition + new Vector2(0f, -42f);
            rootRect.sizeDelta = currentMode == MonStackaMode.Story
                ? new Vector2(330f, 24f)
                : new Vector2(190f, 14f);

            var back = bossHealthBarRoot.GetComponent<Image>();
            back.color = new Color(0.05f, 0.04f, 0.08f, 0.88f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(bossHealthBarRoot.transform, false);
            bossHealthFillRect = fillGo.GetComponent<RectTransform>();
            bossHealthFillRect.anchorMin = new Vector2(0f, 0f);
            bossHealthFillRect.anchorMax = new Vector2(0f, 1f);
            bossHealthFillRect.pivot = new Vector2(0f, 0.5f);
            bossHealthFillRect.offsetMin = new Vector2(2f, 2f);
            bossHealthFillRect.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.9f, 0.16f, 0.28f, 0.95f);
        }

        private void EnsureStoryRightHud()
        {
            if (storyScoreValueText || !leaderboardTitleText)
            {
                return;
            }

            var anchorRect = leaderboardTitleText.GetComponent<RectTransform>();
            if (!anchorRect || !anchorRect.parent)
            {
                return;
            }

            storyBossLabelText = CreateStoryHudText(anchorRect.parent, "StoryBossLabel", "MISSION HP", 22, FontStyle.Bold, new Vector2(1340f, -224f), new Vector2(360f, 30f), TextAnchor.MiddleLeft);
            storyScoreLabelText = CreateStoryHudText(anchorRect.parent, "StoryScoreLabel", "SCORE", 20, FontStyle.Normal, new Vector2(1340f, -306f), new Vector2(360f, 28f), TextAnchor.MiddleLeft);
            storyScoreValueText = CreateStoryHudText(anchorRect.parent, "StoryScoreValue", "0", 36, FontStyle.Bold, new Vector2(1340f, -338f), new Vector2(360f, 44f), TextAnchor.MiddleLeft);
            storyEnemyTriggerText = CreateStoryHudText(anchorRect.parent, "StoryEnemyTriggerCue", string.Empty, 18, FontStyle.Bold, new Vector2(1340f, -386f), new Vector2(430f, 34f), TextAnchor.MiddleLeft);
            storyEnemyTriggerText.supportRichText = true;
            storyEnemyTriggerText.color = new Color(1f, 0.78f, 0.38f, 1f);
            storyEnemyTriggerText.gameObject.SetActive(false);
            storyEnemyStatusText = CreateStoryHudText(anchorRect.parent, "StoryEnemyStatus", string.Empty, 17, FontStyle.Normal, new Vector2(1340f, -430f), new Vector2(420f, 430f), TextAnchor.UpperLeft);
            storyEnemyStatusText.supportRichText = true;
            storyEnemyStatusText.lineSpacing = 0.92f;
            storyEnemyStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private static Text CreateStoryHudText(Transform parent, string name, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void SetStoryRightHudVisible(bool visible)
        {
            if (storyBossLabelText)
            {
                storyBossLabelText.gameObject.SetActive(visible);
            }

            if (storyScoreLabelText)
            {
                storyScoreLabelText.gameObject.SetActive(visible);
            }

            if (storyScoreValueText)
            {
                storyScoreValueText.gameObject.SetActive(visible);
            }

            if (storyEnemyTriggerText)
            {
                storyEnemyTriggerText.gameObject.SetActive(visible && Time.time < storyEnemyTriggerUntil);
            }

            if (storyEnemyStatusText)
            {
                storyEnemyStatusText.gameObject.SetActive(visible);
            }
        }

        private void SetStoryLeaderboardVisible(bool visible)
        {
            if (leaderboardValueTexts != null)
            {
                foreach (var valueText in leaderboardValueTexts)
                {
                    if (valueText)
                    {
                        valueText.gameObject.SetActive(visible);
                    }
                }
            }

            storyRankTexts ??= new[]
            {
                GameObject.Find("Rank1")?.GetComponent<Text>(),
                GameObject.Find("Rank2")?.GetComponent<Text>(),
                GameObject.Find("Rank3")?.GetComponent<Text>(),
            };
            foreach (var rankText in storyRankTexts)
            {
                if (rankText)
                {
                    rankText.gameObject.SetActive(visible);
                }
            }
        }

        private void SetBossHealthBarVisible(bool visible)
        {
            if (bossHealthBarRoot)
            {
                bossHealthBarRoot.SetActive(visible);
            }
        }

        private void RenderBossHealth(int score)
        {
            if (!currentStoryObjective.HasBossHealth)
            {
                SetBossHealthBarVisible(false);
                return;
            }

            SetBossHealthBarVisible(true);
            if (score == lastBossScore || !bossHealthFillRect)
            {
                return;
            }

            lastBossScore = score;
            var healthPercent = Mathf.Clamp01((currentStoryObjective.BossHealthPoints - score) / (float)currentStoryObjective.BossHealthPoints);
            bossHealthFillRect.anchorMax = new Vector2(healthPercent, 1f);
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
                statusText.text = currentMode == MonStackaMode.Story ? string.Empty : display;
                lastStoryStatus = display;
            }

            assistMuted = hintsMuted;
            if (assistText && hintsMuted && assistText.text.Length > 0)
            {
                assistText.text = string.Empty;
                lastAssistDisplay = string.Empty;
            }
        }

        public void RenderStoryEnemyStatus(string status)
        {
            if (!storyEnemyStatusText)
            {
                return;
            }

            var display = currentMode == MonStackaMode.Story ? status ?? string.Empty : string.Empty;
            if (display == lastStoryEnemyStatus)
            {
                return;
            }

            storyEnemyStatusText.text = display;
            lastStoryEnemyStatus = display;
        }

        public void ShowEnemyModifierTrigger(string name, string state, string detail)
        {
            if (!storyEnemyTriggerText || currentMode != MonStackaMode.Story)
            {
                return;
            }

            var safeName = string.IsNullOrWhiteSpace(name) ? "Enemy Ability" : name;
            var safeState = string.IsNullOrWhiteSpace(state) ? "TRIGGER" : state;
            storyEnemyTriggerText.text = $"<color=#ffcf74>{safeName}</color> [{safeState}] {detail}";
            storyEnemyTriggerBaseColor = new Color(1f, 0.78f, 0.38f, 1f);
            storyEnemyTriggerText.color = storyEnemyTriggerBaseColor;
            storyEnemyTriggerText.fontStyle = FontStyle.Bold;
            storyEnemyTriggerUntil = Time.time + 1.8f;
            storyEnemyTriggerText.gameObject.SetActive(true);
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
                    leaderboardValueTexts[index].text = currentMode == MonStackaMode.Story
                        ? string.Empty
                        : rows != null && index < rows.Count ? rows[index] : "---";
                }
            }
        }

        public void ShowPointGain(int points, PieceType? sourcePiece)
        {
            if (points <= 0 || currentMode != MonStackaMode.Story || !storyScoreValueText)
            {
                return;
            }

            var rect = storyScoreValueText.GetComponent<RectTransform>();
            if (!rect || !rect.parent)
            {
                return;
            }

            var popupText = CreateStoryHudText(
                rect.parent,
                "PointGainPopup",
                $"+{points}",
                30,
                FontStyle.Bold,
                rect.anchoredPosition + new Vector2(0f, -46f),
                new Vector2(220f, 40f),
                TextAnchor.MiddleLeft
            );
            if (sourcePiece.HasValue && PieceDefinitions.PieceColors.TryGetValue(sourcePiece.Value, out var pieceColor))
            {
                popupText.color = pieceColor;
            }
            else
            {
                popupText.color = new Color(0.96f, 0.92f, 0.72f, 1f);
            }

            pointPopups.Add(new PointPopup
            {
                Text = popupText,
                Rect = popupText.GetComponent<RectTransform>(),
                StartPosition = popupText.GetComponent<RectTransform>().anchoredPosition,
                Age = 0f,
            });
        }

        private void UpdatePointPopups()
        {
            for (var index = pointPopups.Count - 1; index >= 0; index -= 1)
            {
                var popup = pointPopups[index];
                if (popup.Text == null || popup.Rect == null)
                {
                    pointPopups.RemoveAt(index);
                    continue;
                }

                popup.Age += Time.deltaTime;
                var t = Mathf.Clamp01(popup.Age / 0.95f);
                popup.Rect.anchoredPosition = popup.StartPosition + new Vector2(0f, t * 64f);
                var color = popup.Text.color;
                color.a = 1f - Mathf.SmoothStep(0f, 1f, t);
                popup.Text.color = color;

                if (t >= 1f)
                {
                    Destroy(popup.Text.gameObject);
                    pointPopups.RemoveAt(index);
                }
            }
        }

        private void UpdateEnemyTriggerCue()
        {
            if (!storyEnemyTriggerText || !storyEnemyTriggerText.gameObject.activeSelf)
            {
                return;
            }

            var remaining = storyEnemyTriggerUntil - Time.time;
            if (remaining <= 0f || currentMode != MonStackaMode.Story)
            {
                storyEnemyTriggerText.gameObject.SetActive(false);
                return;
            }

            var t = Mathf.Clamp01(remaining / 1.8f);
            var color = storyEnemyTriggerBaseColor;
            color.a = Mathf.SmoothStep(0f, 1f, t);
            storyEnemyTriggerText.color = color;
        }

        private string lastAssistDisplay;

        private bool assistMuted;
        private float assistFlashUntil;
        private Color assistBaseColor;
        private bool hasAssistBaseColor;
        private int lastAssistProgress = -1;
        private int lastAssistUntilTrigger = -1;
        private int lastAssistWindowSeconds = -1;
        private AssistType? lastAssistWindowType;

        public void ShowAssistTrigger(AssistTrigger trigger)
        {
            if (!assistText || assistMuted)
            {
                return;
            }

            EnsureAssistBaseColor();
            assistFlashUntil = Time.time + 1.45f;
            lastAssistDisplay = $"{trigger.Label} +{trigger.ScoreAwarded}";
            assistText.text = lastAssistDisplay;
            assistText.fontStyle = FontStyle.Bold;
            assistText.color = PieceDefinitions.PieceColors.TryGetValue(trigger.Piece, out var pieceColor)
                ? pieceColor
                : new Color(0.96f, 0.92f, 0.72f, 1f);
            lastAssistWindowType = null;
            lastAssistWindowSeconds = int.MinValue;
            lastAssistProgress = int.MinValue;
            lastAssistUntilTrigger = int.MinValue;
        }

        /// <summary>Held-assist progress and active effect (handoff UI requirement).</summary>
        public void RenderAssist(AssistEffectSystem assist)
        {
            if (!assistText || assistMuted)
            {
                return;
            }

            EnsureAssistBaseColor();
            if (Time.time < assistFlashUntil)
            {
                return;
            }

            if (assistText.fontStyle != FontStyle.Normal)
            {
                assistText.fontStyle = FontStyle.Normal;
            }

            if (assistText.color != assistBaseColor)
            {
                assistText.color = assistBaseColor;
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
            var untilTrigger = windowType.HasValue ? -1 : assist.HeldPlacementsUntilTrigger;
            if (windowType == lastAssistWindowType &&
                windowSeconds == lastAssistWindowSeconds &&
                progress == lastAssistProgress &&
                untilTrigger == lastAssistUntilTrigger)
            {
                return;
            }

            lastAssistWindowType = windowType;
            lastAssistWindowSeconds = windowSeconds;
            lastAssistProgress = progress;
            lastAssistUntilTrigger = untilTrigger;
            lastAssistDisplay = windowType.HasValue
                ? $"{AssistEffectSystem.LabelFor(windowType.Value)} {windowSeconds}s"
                : assist.NextHeldPlacementWillTrigger
                    ? "ASSIST READY"
                    : $"ASSIST {progress}/{AssistEffectSystem.TriggerEvery}";
            assistText.text = lastAssistDisplay;
        }

        private void EnsureAssistBaseColor()
        {
            if (!hasAssistBaseColor && assistText)
            {
                assistBaseColor = assistText.color;
                hasAssistBaseColor = true;
            }
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
