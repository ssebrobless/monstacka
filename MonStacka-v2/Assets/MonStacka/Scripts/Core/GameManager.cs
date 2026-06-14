using System.Collections.Generic;
using System.Linq;
using MonStacka.Story;
using MonStacka.UI;
using MonStacka.Visual;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonStacka.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        private readonly struct PieceRenderData
        {
            public PieceRenderData(List<Vector2Int> localCells, Vector2Int origin, bool useFullBoxSprite)
            {
                LocalCells = localCells;
                Origin = origin;
                UseFullBoxSprite = useFullBoxSprite;
            }

            public List<Vector2Int> LocalCells { get; }
            public Vector2Int Origin { get; }
            public bool UseFullBoxSprite { get; }
        }

        [Header("Gameplay")]
        [SerializeField] private float gravitySeconds = 0.65f;
        [SerializeField] private float lockDelaySeconds = 0.25f;
        [SerializeField] private float dasSeconds = 0.095f;
        [SerializeField] private float arrSeconds = 0.012f;
        [SerializeField] private float cellWorldSize = 1f;
        [SerializeField] private bool useThreePieceVerticalSlice = false;
        [SerializeField] private bool enableVisualExtras = true;
        [SerializeField] private string homeSceneName = "Home";

        [Header("Scene References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform stackRoot;
        [SerializeField] private Transform activeRoot;
        [SerializeField] private HoldBoxView holdBoxView;
        [SerializeField] private NextQueueView nextQueueView;
        [SerializeField] private HUDController hudController;
        [SerializeField] private PauseOverlay pauseOverlay;
        [SerializeField] private DialoguePresenter dialoguePresenter;
        [SerializeField] private MonStackaAudioController audioController;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private BorderDeformTuningProfile deformTuning;
        [SerializeField] private PieceSkinData[] pieceSkins;

        /// <summary>v1 parity: lock delay can be reset at most this many times per piece.</summary>
        private const int MaxLockResets = 15;
        /// <summary>v1 parity: pre-game countdown duration (COUNTDOWN_MS).</summary>
        private const float CountdownSeconds = 1f;

        private readonly Dictionary<int, PieceSkin> stackViews = new();
        private readonly Dictionary<int, string> stackViewSignatures = new();
        private readonly Dictionary<(PieceType, int), PieceSkin> activeViewPool = new();
        private readonly HashSet<int> syncSeenIds = new();
        private readonly Dictionary<PieceType, PieceSkinData> skinLookup = new();
        private (PieceType, int) activeViewKey = ((PieceType)0, -1);
        private NeighborMap cachedNeighborMap;
        private bool neighborMapDirty = true;
        private BoardState boardState;
        private AssistEffectSystem assistSystem;
        private MonStacka.Visual.GarbageCellView garbageCellView;
        private PieceSkin activePieceView;
        private float gravityTimer;
        private float lockTimer;
        private int lockResets;
        private float countdownRemaining;
        private float pausedAccumSeconds;
        private float pauseStartedTime;
        private float completedElapsedSeconds = -1f;
        private bool recordsSubmitted;
        private bool paused;
        private float startTime;
        private StoryChapterSpec storyChapter;
        private StoryModifierSystem storyModifiers;
        private SpriteRenderer storyDimOverlay;
        private bool storyMissionComplete;
        private bool storyMissionFailed;
        private bool storyOutroStarted;
        private bool dialogueGate;
        private MonStackaMode mode;
        private int heldHorizontalDirection;
        private float horizontalHeldTime;
        private float horizontalRepeatTime;
        private bool previousHardDropHeld;
        private bool previousRotateCcwHeld;
        private bool previousRotateCwHeld;
        private bool previousRotateFlipHeld;
        private bool previousHoldHeld;
        private bool previousHoldSwapOneHeld;
        private bool previousHoldSwapTwoHeld;
        private bool previousHoldSwapThreeHeld;
        private bool previousRetryHeld;
        private bool previousPauseHeld;
        private bool previousRestartPausedHeld;
        private bool previousRestartConfirmAcceptHeld = true;
        private bool previousRestartConfirmCancelHeld = true;
        private bool previousLeftHeld;
        private bool previousRightHeld;
        private bool hasAnyGameplayPulse;
        private bool gameplayPieceVisualsVisible = true;
        private bool restartConfirmActive;
        private GameObject restartConfirmRoot;
        private Button restartConfirmAcceptButton;
        private Button restartConfirmCancelButton;
        private bool endRunPanelShown;
        private GameObject endRunRoot;
        private Text endRunTitleText;
        private Text endRunScoreText;
        private Button endRunHomeButton;

        private IReadOnlyList<PieceType> SupportedPieces =>
            useThreePieceVerticalSlice
                ? new[] { PieceType.I, PieceType.T, PieceType.S }
                : new[] { PieceType.I, PieceType.O, PieceType.T, PieceType.S, PieceType.Z, PieceType.J, PieceType.L };

        private void Awake()
        {
            EnsureSceneReferences();
            RebuildSkinLookup();
            mode = MonStackaAppState.SelectedMode;
            gravitySeconds = MonStackaAppState.GravitySeconds;
            lockDelaySeconds = MonStackaAppState.LockDelaySeconds;
            dasSeconds = MonStackaAppState.DasSeconds;
            arrSeconds = MonStackaAppState.ArrSeconds;

            int? targetLines = mode == MonStackaMode.Sprint40 ? PieceDefinitions.TargetLines : null;
            IReadOnlyDictionary<PieceType, float> spawnWeights = null;
            if (mode == MonStackaMode.Story)
            {
                storyChapter = StoryCatalog.GetChapter(MonStackaAppState.SelectedStoryChapterId)
                    ?? StoryProgress.CurrentChapter();
                gravitySeconds = storyChapter.GravitySeconds;
                lockDelaySeconds = storyChapter.LockDelaySeconds;
                if (!storyChapter.Objective.HasBossHealth &&
                    storyChapter.Objective.Kind is StoryObjectiveKind.ClearLines or StoryObjectiveKind.ClearLinesTimed)
                {
                    targetLines = storyChapter.Objective.TargetLines;
                }
                if (storyChapter.SpawnBias is { Count: > 0 })
                {
                    spawnWeights = storyChapter.SpawnBias;
                }
            }

            boardState = new BoardState(
                SupportedPieces,
                targetLines: targetLines,
                selectedMode: mode,
                trainingFeedback: MonStackaAppState.TrainingFeedbackMode,
                spawnWeights: spawnWeights
            );

            if (storyChapter != null)
            {
                storyModifiers = new StoryModifierSystem(storyChapter, boardState);
                storyModifiers.OnMatchStart();
                CreateStoryDimOverlay();
            }
            if (AssistEffectSystem.IsEnabledFor(mode, MonStackaAppState.FriendlyAbilitiesEnabled))
            {
                assistSystem = new AssistEffectSystem();
            }
            boardState.OnPieceLocked += lockEvent =>
            {
                assistSystem?.OnPieceLocked(lockEvent, boardState, points => boardState.AddScore(points, lockEvent.PieceType));
            };
            boardState.OnLinesCleared += lines =>
                assistSystem?.OnLinesCleared(lines, points => boardState.AddScore(points, assistSystem.ActiveWindowPiece));
            boardState.OnPointsGained += (points, sourcePiece) =>
                hudController?.ShowPointGain(points, sourcePiece);
            boardState.OnPieceLocked += HandlePieceLocked;
            // Line clears update only the views that actually changed (no full stack rebuild).
            boardState.OnLinesCleared += _ => { SyncLockedPieceViews(); neighborMapDirty = true; audioController?.PlayUiClick(); };
            boardState.OnGarbageChanged += () => neighborMapDirty = true;
            if (stackRoot)
            {
                garbageCellView = gameObject.AddComponent<MonStacka.Visual.GarbageCellView>();
                garbageCellView.Initialize(boardState, stackRoot, cellWorldSize);
            }
            countdownRemaining = CountdownSeconds;
            startTime = Time.time + CountdownSeconds;
            hudController?.Configure(mode, storyChapter);
            hudController?.RenderLeaderboard(MonStackaRecords.GetDisplayRows(mode, MonStackaAppState.FriendlyAbilitiesEnabled));
            RebuildBoardViews();

            var skipDialogue = System.Array.Exists(
                System.Environment.GetCommandLineArgs(),
                arg => string.Equals(arg, "-monstacka-skip-dialogue", System.StringComparison.OrdinalIgnoreCase));
            skipDialogue |= MonStackaAppState.SkipDialogueForHarness;
            if (storyChapter != null && dialoguePresenter && !skipDialogue)
            {
                // Intro + pre-match dialogue plays before the countdown starts.
                dialogueGate = true;
                var opening = ConcatDialogue(
                    StoryProgress.IsCompleted(storyChapter.Id) ? System.Array.Empty<DialogueLine>() : storyChapter.IntroDialogue,
                    storyChapter.PreMatchDialogue
                );
                dialoguePresenter.Play(opening, () =>
                {
                    dialogueGate = false;
                    SetGameplayPieceVisualsVisible(true);
                    countdownRemaining = CountdownSeconds;
                    startTime = Time.time + CountdownSeconds;
                });
                SetGameplayPieceVisualsVisible(false);
            }
        }

        private static DialogueLine[] ConcatDialogue(DialogueLine[] first, DialogueLine[] second)
        {
            first ??= System.Array.Empty<DialogueLine>();
            second ??= System.Array.Empty<DialogueLine>();
            var combined = new DialogueLine[first.Length + second.Length];
            first.CopyTo(combined, 0);
            second.CopyTo(combined, first.Length);
            return combined;
        }

        private void CreateStoryDimOverlay()
        {
            if (!boardRoot)
            {
                return;
            }

            var go = new GameObject("StoryDimOverlay");
            go.transform.SetParent(boardRoot, false);
            var columns = PieceDefinitions.Columns;
            var rows = PieceDefinitions.VisibleRows;
            go.transform.localPosition = new Vector3(columns * cellWorldSize * 0.5f, -rows * cellWorldSize * 0.5f, -1f);
            go.transform.localScale = new Vector3(columns * cellWorldSize, rows * cellWorldSize, 1f);
            storyDimOverlay = go.AddComponent<SpriteRenderer>();
            storyDimOverlay.sprite = CreateWhiteSprite();
            storyDimOverlay.color = new Color(0f, 0f, 0f, 0f);
            storyDimOverlay.sortingOrder = 150;
        }

        private static Sprite whiteOverlaySprite;

        private static Sprite CreateWhiteSprite()
        {
            if (whiteOverlaySprite)
            {
                return whiteOverlaySprite;
            }

            var texture = Texture2D.whiteTexture;
            whiteOverlaySprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            return whiteOverlaySprite;
        }

        private void Update()
        {
            if (dialogueGate || (dialoguePresenter && dialoguePresenter.IsActive))
            {
                SetGameplayPieceVisualsVisible(false);
                UpdateVisuals();
                RememberGamepadButtonState();
                return;
            }

            SetGameplayPieceVisualsVisible(true);

            if (dialoguePresenter && dialoguePresenter.IsWaitingForAdvanceRelease)
            {
                UpdateVisuals();
                RememberGamepadButtonState();
                return;
            }

            if (restartConfirmActive)
            {
                HandleRestartConfirmInput();
                UpdateVisuals();
                RememberGamepadButtonState();
                return;
            }

            if (endRunPanelShown)
            {
                UpdateVisuals();
                RememberGamepadButtonState();
                return;
            }

            var pausePressed = IsPausePressed();
            if (pausePressed)
            {
                TogglePause();
            }

            if (IsRetryPressed())
            {
                RequestRestart();
            }

            if (paused && IsRestartPausedPressed())
            {
                RequestRestart();
            }

            if (!paused && countdownRemaining > 0f)
            {
                countdownRemaining -= Time.deltaTime;
                if (countdownRemaining <= 0f)
                {
                    startTime = Time.time;
                }
            }
            else if (!paused && !boardState.IsGameOver() && !storyMissionComplete && !storyMissionFailed)
            {
                assistSystem?.Tick(Time.deltaTime);
                storyModifiers?.Tick(Time.deltaTime);
                HandleGameplayInput();
                TickGravity();
                CheckStoryObjective();
            }

            HandleRunCompletion();
            UpdateStoryVisuals();
            UpdateVisuals();
            RememberGamepadButtonState();
        }

        private void CheckStoryObjective()
        {
            if (storyChapter == null || storyMissionComplete || storyMissionFailed)
            {
                return;
            }

            var objective = storyChapter.Objective;
            var complete = objective.HasBossHealth
                ? boardState.Score >= objective.BossHealthPoints
                : objective.Kind switch
            {
                StoryObjectiveKind.ClearLines => boardState.SprintComplete,
                StoryObjectiveKind.ClearLinesTimed => boardState.SprintComplete,
                StoryObjectiveKind.ReachScore => boardState.Score >= objective.TargetScore,
                StoryObjectiveKind.SurviveSeconds => ElapsedSeconds >= objective.TimeLimitSeconds,
                _ => false,
            };

            if (complete)
            {
                CompleteStoryMission();
                return;
            }

            if (objective.HasTimeLimit &&
                !complete &&
                ElapsedSeconds > objective.TimeLimitSeconds)
            {
                storyMissionFailed = true;
            }
        }

        private void CompleteStoryMission()
        {
            storyMissionComplete = true;
            completedElapsedSeconds = ElapsedSeconds;
            StoryProgress.RecordCompletion(storyChapter.Id, boardState.Score, completedElapsedSeconds);

            if (dialoguePresenter && !storyOutroStarted)
            {
                storyOutroStarted = true;
                dialoguePresenter.Play(storyChapter.PostMatchDialogue, ReturnToStorySelect);
            }
            else
            {
                ReturnToStorySelect();
            }
        }

        private void ReturnToStorySelect()
        {
            SceneManager.LoadScene("StorySelect");
        }

        private void SetGameplayPieceVisualsVisible(bool visible)
        {
            if (gameplayPieceVisualsVisible == visible)
            {
                return;
            }

            gameplayPieceVisualsVisible = visible;
            if (activeRoot)
            {
                activeRoot.gameObject.SetActive(visible);
            }

            if (stackRoot)
            {
                stackRoot.gameObject.SetActive(visible);
            }

            holdBoxView?.SetPreviewVisible(visible);
            nextQueueView?.SetPreviewsVisible(visible);
        }

        private void UpdateStoryVisuals()
        {
            if (storyModifiers == null)
            {
                return;
            }

            if (storyDimOverlay)
            {
                var alpha = storyModifiers.BoardDimAlpha;
                if (!Mathf.Approximately(storyDimOverlay.color.a, alpha))
                {
                    storyDimOverlay.color = new Color(0f, 0f, 0f, alpha);
                }
            }

            if (activePieceView)
            {
                var visible = storyModifiers.ActivePieceVisible;
                if (activePieceView.gameObject.activeSelf != visible)
                {
                    activePieceView.gameObject.SetActive(visible);
                }
            }

            var storyStatus = storyMissionFailed
                ? "MISSION FAILED - press R to retry"
                : BuildStoryBossStatus();
            hudController?.RenderStoryStatus(
                storyStatus,
                storyModifiers.HintsMuted
            );
            hudController?.RenderStoryEnemyStatus(storyModifiers.BuildEnemyAbilityStatus());
        }

        private string BuildStoryBossStatus()
        {
            var chips = storyModifiers?.BuildStatusChips() ?? string.Empty;
            if (storyChapter == null || !storyChapter.Objective.HasBossHealth)
            {
                return chips;
            }

            var objective = storyChapter.Objective;
            var remainingHp = Mathf.Max(0, objective.BossHealthPoints - boardState.Score);
            var bossStatus = $"BOSS HP {remainingHp}/{objective.BossHealthPoints}";
            if (objective.HasTimeLimit)
            {
                var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(objective.TimeLimitSeconds - ElapsedSeconds));
                bossStatus += $"  |  TIME {remainingSeconds}s";
            }

            return string.IsNullOrEmpty(chips)
                ? bossStatus
                : $"{bossStatus}  |  {chips}";
        }

        /// <summary>Elapsed run time excluding countdown, pauses, and post-completion time.</summary>
        private float ElapsedSeconds
        {
            get
            {
                if (completedElapsedSeconds >= 0f)
                {
                    return completedElapsedSeconds;
                }

                if (countdownRemaining > 0f)
                {
                    return 0f;
                }

                var pausedSoFar = pausedAccumSeconds + (paused ? Time.time - pauseStartedTime : 0f);
                return Mathf.Max(0f, Time.time - startTime - pausedSoFar);
            }
        }

        private void HandleRunCompletion()
        {
            if (!boardState.IsGameOver() || recordsSubmitted)
            {
                return;
            }

            if (completedElapsedSeconds < 0f)
            {
                completedElapsedSeconds = ElapsedSeconds;
            }

            recordsSubmitted = true;
            if (mode == MonStackaMode.Ogbm)
            {
                MonStackaRecords.TryAddOgbmScore(boardState.Score, MonStackaAppState.FriendlyAbilitiesEnabled);
            }
            else if (mode == MonStackaMode.Sprint40 && boardState.SprintComplete)
            {
                MonStackaRecords.TryAddSprintTime(
                    Mathf.RoundToInt(completedElapsedSeconds * 1000f),
                    MonStackaAppState.FriendlyAbilitiesEnabled
                );
            }

            hudController?.RenderLeaderboard(MonStackaRecords.GetDisplayRows(mode, MonStackaAppState.FriendlyAbilitiesEnabled));
            ShowEndRunPanel();
        }

        private void HandleGameplayInput()
        {
            var moved = false;
            var rotated = false;
            var held = false;
            var leftHeld = MonStackaControls.IsGameplayLeftHeld();
            var rightHeld = MonStackaControls.IsGameplayRightHeld();
            var leftPressed = leftHeld && !previousLeftHeld;
            var rightPressed = rightHeld && !previousRightHeld;

            if (leftPressed && !rightHeld)
            {
                if (boardState.TryMove(-1, 0))
                {
                    moved = true;
                    boardState.RegisterTrainingInput();
                }
            }

            if (rightPressed && !leftHeld)
            {
                if (boardState.TryMove(1, 0))
                {
                    moved = true;
                    boardState.RegisterTrainingInput();
                }
            }

            var desiredHorizontalDirection =
                leftHeld == rightHeld
                    ? 0
                    : leftHeld
                        ? -1
                        : 1;

            if (desiredHorizontalDirection == 0)
            {
                heldHorizontalDirection = 0;
                horizontalHeldTime = 0f;
                horizontalRepeatTime = 0f;
            }
            else
            {
                var pressedThisFrame =
                    (desiredHorizontalDirection < 0 && leftPressed) ||
                    (desiredHorizontalDirection > 0 && rightPressed);

                if (heldHorizontalDirection != desiredHorizontalDirection)
                {
                    heldHorizontalDirection = desiredHorizontalDirection;
                    horizontalHeldTime = 0f;
                    horizontalRepeatTime = 0f;
                }
                else if (!pressedThisFrame)
                {
                    // Lysergicada sedation: DAS/ARR are sluggish during the window.
                    var sluggish = storyModifiers?.InputSluggishMultiplier ?? 1f;
                    var effectiveDas = dasSeconds * sluggish;
                    var effectiveArr = arrSeconds * sluggish;
                    horizontalHeldTime += Time.deltaTime;
                    if (horizontalHeldTime >= effectiveDas)
                    {
                        if (effectiveArr <= 0.0001f)
                        {
                            if (boardState.TryMove(desiredHorizontalDirection, 0))
                            {
                                moved = true;
                                boardState.RegisterTrainingInput();
                            }
                        }
                        else
                        {
                            horizontalRepeatTime += Time.deltaTime;
                            while (horizontalRepeatTime >= effectiveArr)
                            {
                                horizontalRepeatTime -= effectiveArr;
                                if (boardState.TryMove(desiredHorizontalDirection, 0))
                                {
                                    moved = true;
                                    boardState.RegisterTrainingInput();
                                }
                            }
                        }
                    }
                }
            }

            if (MonStackaControls.IsGameplaySoftDropHeld() && boardState.TrySoftDrop())
            {
                moved = true;
            }

            if (IsPressed(MonStackaControls.IsGameplayRotateCcwHeld(), ref previousRotateCcwHeld))
            {
                if (boardState.TryRotate(-1))
                {
                    rotated = true;
                    boardState.RegisterTrainingInput();
                }
            }

            if (IsPressed(MonStackaControls.IsGameplayRotateCwHeld(), ref previousRotateCwHeld))
            {
                if (boardState.TryRotate(1))
                {
                    rotated = true;
                    boardState.RegisterTrainingInput();
                }
            }

            if (IsPressed(MonStackaControls.IsGameplayRotateFlipHeld(), ref previousRotateFlipHeld))
            {
                if (boardState.TryRotate(2))
                {
                    rotated = true;
                    boardState.RegisterTrainingInput();
                }
            }

            var holdAllowed = (mode != MonStackaMode.Training || assistSystem != null) && (storyChapter == null || storyChapter.HoldEnabled);
            if (holdAllowed && IsHoldQueueSwapAllowed() && TryHandleHoldQueueSwap())
            {
                UpdatePreviewViews();
                neighborMapDirty = true;
                return;
            }

            if (holdAllowed && IsPressed(MonStackaControls.IsGameplayHoldHeld(), ref previousHoldHeld))
            {
                var hadHoldPiece = boardState.HasHoldPiece;
                if (boardState.TryHold())
                {
                    held = true;
                    if (!hadHoldPiece)
                    {
                        // First hold spawns a fresh piece from the queue (v1 spawn resets the cap).
                        lockResets = 0;
                    }
                }
            }

            if (IsPressed(MonStackaControls.IsGameplayHardDropHeld(), ref previousHardDropHeld))
            {
                HardDropAndSpawn();
                return;
            }

            if (held)
            {
                ApplyLockReset();
                // Hold only changes the active piece, hold box, and queue - the
                // locked stack is untouched, so never rebuild it here.
                RebuildActivePieceView();
                UpdatePreviewViews();
                neighborMapDirty = true;
                return;
            }

            if (rotated)
            {
                ApplyLockReset();
                RebuildActivePieceView();
                neighborMapDirty = true;
                return;
            }

            if (moved)
            {
                ApplyLockReset();
                UpdateActivePieceViewPosition();
                neighborMapDirty = true;
            }
        }

        private bool IsHoldQueueSwapAllowed() =>
            mode == MonStackaMode.Story || assistSystem != null;

        private bool TryHandleHoldQueueSwap()
        {
            if (IsPressed(IsHoldQueueSwapOneHeld(), ref previousHoldSwapOneHeld))
            {
                return boardState.TrySwapHoldWithUpcoming(0);
            }

            if (IsPressed(IsHoldQueueSwapTwoHeld(), ref previousHoldSwapTwoHeld))
            {
                return boardState.TrySwapHoldWithUpcoming(1);
            }

            if (IsPressed(IsHoldQueueSwapThreeHeld(), ref previousHoldSwapThreeHeld))
            {
                return boardState.TrySwapHoldWithUpcoming(2);
            }

            return false;
        }

        /// <summary>
        /// v1 parity: a successful move/rotate resets the lock delay only while the
        /// piece is grounded, capped at MaxLockResets per piece so a piece cannot be
        /// stalled forever. Movement never resets the gravity timer.
        /// </summary>
        private void ApplyLockReset()
        {
            if (!boardState.IsGrounded())
            {
                lockTimer = 0f;
                return;
            }

            if (lockResets < MaxLockResets)
            {
                lockTimer = 0f;
                lockResets += 1;
            }
        }

        private void TickGravity()
        {
            gravityTimer += Time.deltaTime;

            if (!boardState.HasActivePiece)
            {
                return;
            }

            var effectiveGravitySeconds =
                GravityCurve.SecondsFor(mode, boardState.Lines, gravitySeconds) *
                (assistSystem?.GravityMultiplier ?? 1f) *
                (storyModifiers?.GravityMultiplier ?? 1f);
            var effectiveLockDelaySeconds =
                (lockDelaySeconds * (storyModifiers?.LockDelayMultiplier ?? 1f)) +
                (assistSystem?.LockDelayBonusSeconds ?? 0f);

            if (boardState.IsGrounded())
            {
                lockTimer += Time.deltaTime;
                if (lockTimer >= effectiveLockDelaySeconds)
                {
                    LockAndSpawn();
                }
            }
            else
            {
                lockTimer = 0f;
            }

            if (gravityTimer >= effectiveGravitySeconds)
            {
                gravityTimer = 0f;
                if (boardState.TryMove(0, 1))
                {
                    UpdateActivePieceViewPosition();
                    neighborMapDirty = true;
                }
                else if (lockTimer <= 0f)
                {
                    lockTimer = Time.deltaTime;
                }
            }
        }

        private void LockAndSpawn()
        {
            if (!boardState.LockPiece())
            {
                return;
            }

            if (mode != MonStackaMode.Training && !boardState.SprintComplete)
            {
                boardState.SpawnNext();
            }

            lockTimer = 0f;
            gravityTimer = 0f;
            lockResets = 0;
            RebuildActivePieceView();
            UpdatePreviewViews();
        }

        private void HardDropAndSpawn()
        {
            if (!boardState.HardDrop())
            {
                return;
            }

            if (mode != MonStackaMode.Training && !boardState.SprintComplete)
            {
                boardState.SpawnNext();
            }

            lockTimer = 0f;
            gravityTimer = 0f;
            lockResets = 0;
            RebuildActivePieceView();
            UpdatePreviewViews();
        }

        private void HandlePieceLocked(PieceLockEvent lockEvent)
        {
            if (!stackViews.TryGetValue(lockEvent.PieceId, out var skin) || !skin)
            {
                CreateLockedPieceView(new LockedPieceRecord
                {
                    PieceId = lockEvent.PieceId,
                    PieceType = lockEvent.PieceType,
                    Rotation = lockEvent.Rotation,
                    Cells = lockEvent.Cells.ToList(),
                    BoxOrigin = lockEvent.BoxOrigin,
                });
                skin = stackViews[lockEvent.PieceId];
            }

            neighborMapDirty = true;
            skin.TriggerImpact(lockEvent.Cells, PieceDefinitions.HiddenRows);
            audioController?.PlayMonsterImpact(lockEvent.PieceType);
        }

        private void UpdateVisuals()
        {
            var now = Time.time;
            NeighborMap neighborMap = null;
            if (hasAnyGameplayPulse)
            {
                // The neighbor map only changes when the board does; rebuilding it
                // every frame was pure allocation churn.
                if (neighborMapDirty || cachedNeighborMap == null)
                {
                    cachedNeighborMap = boardState.GetNeighborMap(includeActive: true);
                    neighborMapDirty = false;
                }
                neighborMap = cachedNeighborMap;
            }

            foreach (var skin in stackViews.Values)
            {
                if (neighborMap != null && skin && skin.UsesBorderPulse)
                {
                    skin.SetNeighborMap(neighborMap);
                }

                if (skin && skin.RequiresManualUpdate)
                {
                    skin.ManualUpdate(now);
                }
            }

            if (activePieceView && neighborMap != null && activePieceView.UsesBorderPulse)
            {
                activePieceView.SetNeighborMap(neighborMap);
            }

            if (activePieceView && activePieceView.RequiresManualUpdate)
            {
                activePieceView.ManualUpdate(now);
            }

            if (holdBoxView)
            {
                holdBoxView.ManualUpdate(now);
            }

            if (nextQueueView)
            {
                nextQueueView.ManualUpdate(now);
            }

            if (hudController)
            {
                hudController.Render(
                    mode,
                    boardState.Score,
                    boardState.Lines,
                    ElapsedSeconds,
                    boardState.IsGameOver(),
                    paused,
                    countdownRemaining
                );
                hudController.RenderAssist(assistSystem);
            }
        }

        private void RebuildBoardViews()
        {
            if (!stackRoot || !activeRoot)
            {
                return;
            }

            RebuildLockedPieceViews();
            RebuildActivePieceView();
            UpdatePreviewViews();
        }

        private void RebuildLockedPieceViews()
        {
            foreach (var existing in stackViews.Values)
            {
                if (existing)
                {
                    existing.gameObject.SetActive(false);
                    Destroy(existing.gameObject);
                }
            }
            stackViews.Clear();
            stackViewSignatures.Clear();

            foreach (Transform child in activeRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            activePieceView = null;
            activeViewPool.Clear();
            activeViewKey = ((PieceType)0, -1);
            neighborMapDirty = true;

            foreach (var record in boardState.GetLockedPieceGroups())
            {
                CreateLockedPieceView(record);
            }
        }

        /// <summary>
        /// Incremental stack sync after line clears: pieces that only shifted down
        /// get a position update, split pieces are rebuilt, vanished pieces are
        /// destroyed. Replaces the old full destroy/recreate (confirmed lag source).
        /// </summary>
        private void SyncLockedPieceViews()
        {
            if (!stackRoot)
            {
                return;
            }

            syncSeenIds.Clear();
            foreach (var record in boardState.GetLockedPieceGroups())
            {
                syncSeenIds.Add(record.PieceId);
                var renderData = BuildLockedRenderData(record.PieceType, record.Rotation, record.Cells, record.BoxOrigin);
                var signature = BuildCellSignature(renderData.LocalCells);

                if (stackViews.TryGetValue(record.PieceId, out var view) && view)
                {
                    if (stackViewSignatures.TryGetValue(record.PieceId, out var oldSignature) && oldSignature == signature)
                    {
                        view.transform.localPosition = BoardToWorld(renderData.Origin.x, renderData.Origin.y);
                        continue;
                    }

                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                    stackViews.Remove(record.PieceId);
                    stackViewSignatures.Remove(record.PieceId);
                }

                CreateLockedPieceView(record, renderData, signature);
            }

            var removedIds = stackViews.Keys.Where(id => !syncSeenIds.Contains(id)).ToList();
            foreach (var id in removedIds)
            {
                var view = stackViews[id];
                if (view)
                {
                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
                stackViews.Remove(id);
                stackViewSignatures.Remove(id);
            }
        }

        private void CreateLockedPieceView(LockedPieceRecord record, PieceRenderData? prebuilt = null, string signature = null)
        {
            var renderData = prebuilt ?? BuildLockedRenderData(record.PieceType, record.Rotation, record.Cells, record.BoxOrigin);
            var pieceView = CreatePieceSkin(
                $"Piece_{record.PieceId}",
                stackRoot,
                record.PieceType,
                record.Rotation,
                renderData.LocalCells,
                false,
                record.PieceId,
                renderData.UseFullBoxSprite,
                GetLockedPulseScale()
            );
            pieceView.transform.localPosition = BoardToWorld(renderData.Origin.x, renderData.Origin.y);
            stackViews[record.PieceId] = pieceView;
            stackViewSignatures[record.PieceId] = signature ?? BuildCellSignature(renderData.LocalCells);
        }

        private static string BuildCellSignature(List<Vector2Int> cells)
        {
            var sorted = cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x);
            var builder = new System.Text.StringBuilder(cells.Count * 6);
            foreach (var cell in sorted)
            {
                builder.Append(cell.x).Append(',').Append(cell.y).Append(';');
            }
            return builder.ToString();
        }

        /// <summary>
        /// Active piece views are pooled per (type, rotation) - 28 worst case - so
        /// rotation/spawn never destroys or recreates GameObjects mid-play.
        /// </summary>
        private void RebuildActivePieceView()
        {
            if (!boardState.HasActivePiece)
            {
                if (activePieceView)
                {
                    activePieceView.gameObject.SetActive(false);
                    activePieceView = null;
                }
                return;
            }

            var piece = boardState.ActivePiece;
            var key = (piece.Type, piece.Rotation);
            if (activePieceView && activeViewKey == key)
            {
                UpdateActivePieceViewPosition();
                return;
            }

            if (activePieceView)
            {
                activePieceView.gameObject.SetActive(false);
            }

            if (!activeViewPool.TryGetValue(key, out var pooled) || !pooled)
            {
                var renderData = BuildActiveRenderData(piece);
                pooled = CreatePieceSkin(
                    $"ActivePiece_{piece.Type}_{piece.Rotation}",
                    activeRoot,
                    piece.Type,
                    piece.Rotation,
                    renderData.LocalCells,
                    false,
                    -1,
                    renderData.UseFullBoxSprite,
                    GetActivePulseScale()
                );
                activeViewPool[key] = pooled;
            }

            pooled.gameObject.SetActive(true);
            activePieceView = pooled;
            activeViewKey = key;
            UpdateActivePieceViewPosition();
        }

        private void UpdateActivePieceViewPosition()
        {
            if (!boardState.HasActivePiece || !activePieceView)
            {
                return;
            }

            var renderData = BuildActiveRenderData(boardState.ActivePiece);
            activePieceView.transform.localPosition = BoardToWorld(renderData.Origin.x, renderData.Origin.y);
        }

        private void UpdatePreviewViews()
        {
            if (holdBoxView)
            {
                var holdPiece = boardState.HasHoldPiece ? boardState.HoldPiece : (PieceType?)null;
                var holdAbilityArmed =
                    holdPiece.HasValue &&
                    assistSystem != null &&
                    assistSystem.HeldProgress >= AssistEffectSystem.TriggerEvery - 1;
                holdBoxView.Render(holdPiece, skinLookup, outlineMaterial, deformTuning, cellWorldSize * 0.72f, holdAbilityArmed);
            }

            if (nextQueueView)
            {
                var basePreview = storyChapter?.NextPreviewCount ?? 3;
                var previewCount = basePreview + (assistSystem?.ExtraPreviewCount ?? 0);
                nextQueueView.Render(boardState.NextQueue.Take(previewCount).ToList(), skinLookup, outlineMaterial, deformTuning, cellWorldSize * 0.55f);
            }
        }

        private PieceSkin CreatePieceSkin(
            string name,
            Transform parent,
            PieceType pieceType,
            int rotation,
            IReadOnlyCollection<Vector2Int> localCells,
            bool previewOnly,
            int pieceId,
            bool useFullBoxSprite = false,
            float pulseScale = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var pieceSkin = go.AddComponent<PieceSkin>();
            pieceSkin.PieceId = pieceId;
            if (skinLookup.TryGetValue(pieceType, out var data))
            {
                var animateBody = true;
                var visualExtrasEnabled = AreVisualExtrasEnabled();
                var effectivePulseScale = visualExtrasEnabled ? pulseScale : 0f;
                var enableFacialAnimation = visualExtrasEnabled && !previewOnly;
                pieceSkin.Initialize(
                    data,
                    pieceType,
                    rotation,
                    localCells,
                    cellWorldSize,
                    outlineMaterial,
                    deformTuning,
                    previewOnly,
                    effectivePulseScale,
                    animateBody,
                    enableFacialAnimation,
                    useFullBoxSprite
                );
                hasAnyGameplayPulse |= pieceSkin.UsesBorderPulse;
            }
            return pieceSkin;
        }

        private bool AreVisualExtrasEnabled()
        {
            return enableVisualExtras && MonStackaAppState.VisualExtrasEnabled;
        }

        private float GetRecoveryPulseScale(float requestedScale)
        {
            return AreVisualExtrasEnabled() ? requestedScale : 0f;
        }

        private float GetActivePulseScale()
        {
            return MonStackaAppState.RippleStage >= MonStackaRippleStage.ActiveGameplay
                ? GetRecoveryPulseScale(0.11f)
                : 0f;
        }

        private float GetLockedPulseScale()
        {
            return MonStackaAppState.RippleStage >= MonStackaRippleStage.LandedGameplay
                ? GetRecoveryPulseScale(0.035f)
                : 0f;
        }

        private Vector3 BoardToWorld(int boardX, int boardY)
        {
            var visibleY = boardY - PieceDefinitions.HiddenRows;
            return new Vector3(boardX * cellWorldSize, -visibleY * cellWorldSize, 0f);
        }

        private static PieceRenderData BuildLockedRenderData(
            PieceType pieceType,
            int rotation,
            IReadOnlyCollection<Vector2Int> absoluteCells,
            Vector2Int? boxOrigin)
        {
            if (boxOrigin.HasValue && MatchesAbsoluteDefinition(pieceType, rotation, boxOrigin.Value, absoluteCells))
            {
                return NormalizeRenderData(
                    PieceDefinitions.GetCells(pieceType, rotation),
                    boxOrigin.Value,
                    false
                );
            }

            var minX = absoluteCells.Min(cell => cell.x);
            var minY = absoluteCells.Min(cell => cell.y);
            var normalizedLockedCells = absoluteCells
                .Select(cell => new Vector2Int(cell.x - minX, cell.y - minY))
                .ToList();
            return new PieceRenderData(normalizedLockedCells, new Vector2Int(minX, minY), false);
        }

        private static PieceRenderData BuildActiveRenderData(PieceInstance piece)
        {
            return NormalizeRenderData(
                PieceDefinitions.GetCells(piece.Type, piece.Rotation),
                new Vector2Int(piece.X, piece.Y),
                false
            );
        }

        private static PieceRenderData NormalizeRenderData(
            IReadOnlyCollection<Vector2Int> sourceCells,
            Vector2Int boxOrigin,
            bool useFullBoxSprite = false)
        {
            if (useFullBoxSprite)
            {
                return new PieceRenderData(
                    sourceCells.ToList(),
                    boxOrigin,
                    true
                );
            }

            var minX = sourceCells.Min(cell => cell.x);
            var minY = sourceCells.Min(cell => cell.y);
            var normalizedCells = sourceCells
                .Select(cell => new Vector2Int(cell.x - minX, cell.y - minY))
                .ToList();

            return new PieceRenderData(
                normalizedCells,
                new Vector2Int(boxOrigin.x + minX, boxOrigin.y + minY),
                useFullBoxSprite
            );
        }

        private static bool MatchesAbsoluteDefinition(
            PieceType type,
            int rotation,
            Vector2Int boxOrigin,
            IReadOnlyCollection<Vector2Int> absoluteCells)
        {
            var expected = PieceDefinitions.GetCells(type, rotation)
                .Select(cell => new Vector2Int(boxOrigin.x + cell.x, boxOrigin.y + cell.y))
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();
            var actual = absoluteCells
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .ToArray();

            if (expected.Length != actual.Length)
            {
                return false;
            }

            for (var index = 0; index < expected.Length; index += 1)
            {
                if (expected[index] != actual[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureSceneReferences()
        {
            boardRoot ??= new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);
            stackRoot ??= new GameObject("StackView").transform;
            stackRoot.SetParent(boardRoot, false);
            activeRoot ??= new GameObject("ActivePieceView").transform;
            activeRoot.SetParent(boardRoot, false);
        }

        private void RebuildSkinLookup()
        {
            skinLookup.Clear();
            foreach (var skin in pieceSkins.Where(skin => skin))
            {
                skinLookup[skin.pieceType] = skin;
            }
        }

        private void LateUpdate()
        {
            if (!hasAnyGameplayPulse)
            {
                return;
            }

            hasAnyGameplayPulse =
                (activePieceView && activePieceView.UsesBorderPulse) ||
                stackViews.Values.Any(view => view && view.UsesBorderPulse);
        }

        public void TogglePause()
        {
            if (endRunPanelShown)
            {
                return;
            }

            if (restartConfirmActive)
            {
                CancelRestartConfirmation();
                return;
            }

            if (paused)
            {
                ResumeGame();
            }
            else
            {
                PauseIfRunning();
            }
        }

        public void ResumeGame()
        {
            if (endRunPanelShown)
            {
                return;
            }

            if (restartConfirmActive)
            {
                return;
            }

            if (paused)
            {
                pausedAccumSeconds += Time.time - pauseStartedTime;
            }

            paused = false;
            pauseOverlay?.SetVisible(false);
        }

        public void PauseIfRunning()
        {
            if (endRunPanelShown)
            {
                return;
            }

            if (paused)
            {
                return;
            }

            paused = true;
            pauseStartedTime = Time.time;
            pauseOverlay?.SetVisible(true);
        }

        public AssistEffectSystem AssistSystem => assistSystem;

        public BoardState Board => boardState;

        public MonStackaMode CurrentMode => mode;

        public bool FriendlyAbilitiesEnabled => assistSystem != null;

        public bool CanToggleFriendlyAbilities => mode == MonStackaMode.Training;

        public void ToggleFriendlyAbilitiesAndRestart()
        {
            if (!CanToggleFriendlyAbilities)
            {
                return;
            }

            MonStackaAppState.FriendlyAbilitiesEnabled = !MonStackaAppState.FriendlyAbilitiesEnabled;
            assistSystem = MonStackaAppState.FriendlyAbilitiesEnabled ? new AssistEffectSystem() : null;
            RestartMode();
        }

        public bool IsRestartConfirmActive => restartConfirmActive;

        public bool IsEndRunPanelActive => endRunPanelShown;

        public bool HasRestartConfirmUi => restartConfirmRoot != null;

        public bool HasEndRunUi => endRunRoot != null;

        public void RequestRestart()
        {
            if (mode == MonStackaMode.Training)
            {
                RestartMode();
                return;
            }

            PauseIfRunning();
            ShowRestartConfirmation();
        }

        private void ShowRestartConfirmation()
        {
            EnsureRestartConfirmationUi();
            restartConfirmActive = true;
            pauseOverlay?.SetVisible(true);

            if (restartConfirmRoot)
            {
                restartConfirmRoot.SetActive(true);
            }

            if (restartConfirmAcceptButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(restartConfirmAcceptButton.gameObject);
            }

            previousRestartConfirmAcceptHeld = IsRestartConfirmAcceptHeld();
            previousRestartConfirmCancelHeld = IsRestartConfirmCancelHeld();
        }

        private void HandleRestartConfirmInput()
        {
            if (WasRestartConfirmPressed(IsRestartConfirmAcceptHeld(), ref previousRestartConfirmAcceptHeld))
            {
                ConfirmRestart();
                return;
            }

            if (WasRestartConfirmPressed(IsRestartConfirmCancelHeld(), ref previousRestartConfirmCancelHeld))
            {
                CancelRestartConfirmation();
            }
        }

        private void ConfirmRestart()
        {
            HideRestartConfirmation();
            RestartMode();
        }

        private void CancelRestartConfirmation()
        {
            HideRestartConfirmation();
            PauseIfRunning();
            pauseOverlay?.SetVisible(true);
        }

        private void HideRestartConfirmation()
        {
            restartConfirmActive = false;
            if (restartConfirmRoot)
            {
                restartConfirmRoot.SetActive(false);
            }
        }

        private void ShowEndRunPanel()
        {
            if (endRunPanelShown || mode == MonStackaMode.Training)
            {
                return;
            }

            EnsureEndRunUi();
            endRunPanelShown = true;
            paused = false;
            pauseOverlay?.SetVisible(false);
            HideRestartConfirmation();

            if (endRunRoot)
            {
                endRunRoot.SetActive(true);
                endRunRoot.transform.SetAsLastSibling();
            }

            if (endRunTitleText)
            {
                endRunTitleText.text = boardState.SprintComplete ? "Run Complete" : "Game Over";
            }

            if (endRunScoreText)
            {
                endRunScoreText.text = mode == MonStackaMode.Sprint40 && boardState.SprintComplete
                    ? $"Time\n{MonStackaRecords.FormatMs(Mathf.RoundToInt(completedElapsedSeconds * 1000f))}"
                    : $"Score\n{boardState.Score}";
            }

            if (endRunHomeButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(endRunHomeButton.gameObject);
            }
        }

        private void EnsureEndRunUi()
        {
            if (endRunRoot)
            {
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (!canvas)
            {
                return;
            }

            endRunRoot = new GameObject("EndRunOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            endRunRoot.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)endRunRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var blocker = endRunRoot.GetComponent<Image>();
            blocker.color = new Color(0.02f, 0.02f, 0.08f, 0.68f);
            blocker.raycastTarget = true;

            var group = endRunRoot.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            var panel = CreateRestartUiObject("Panel", endRunRoot.transform);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 330f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.05f, 0.14f, 0.97f);

            endRunTitleText = CreateRestartText("Title", panel.transform, "Game Over", 38, TextAnchor.MiddleCenter);
            SetRestartRect(endRunTitleText.rectTransform, new Vector2(0f, 104f), new Vector2(500f, 58f));

            endRunScoreText = CreateRestartText("Score", panel.transform, "Score\n0", 34, TextAnchor.MiddleCenter);
            endRunScoreText.fontStyle = FontStyle.Bold;
            SetRestartRect(endRunScoreText.rectTransform, new Vector2(0f, 20f), new Vector2(480f, 100f));

            endRunHomeButton = CreateRestartButton(panel.transform, "Home", new Vector2(0f, -102f), ReturnHome);

            endRunRoot.SetActive(false);
        }

        private void EnsureRestartConfirmationUi()
        {
            if (restartConfirmRoot)
            {
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (!canvas)
            {
                return;
            }

            restartConfirmRoot = new GameObject("RestartConfirmOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            restartConfirmRoot.transform.SetParent(canvas.transform, false);
            restartConfirmRoot.transform.SetAsLastSibling();

            var rootRect = (RectTransform)restartConfirmRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var blocker = restartConfirmRoot.GetComponent<Image>();
            blocker.color = new Color(0.02f, 0.02f, 0.08f, 0.62f);
            blocker.raycastTarget = true;

            var group = restartConfirmRoot.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            var panel = CreateRestartUiObject("Panel", restartConfirmRoot.transform);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 290f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.05f, 0.05f, 0.14f, 0.96f);

            var title = CreateRestartText("Title", panel.transform, "Restart run?", 34, TextAnchor.MiddleCenter);
            SetRestartRect(title.rectTransform, new Vector2(0f, 82f), new Vector2(560f, 54f));

            var body = CreateRestartText(
                "Body",
                panel.transform,
                "This will reset the current attempt. The match is paused while you choose.",
                22,
                TextAnchor.MiddleCenter
            );
            SetRestartRect(body.rectTransform, new Vector2(0f, 26f), new Vector2(540f, 68f));

            restartConfirmAcceptButton = CreateRestartButton(panel.transform, "Restart", new Vector2(-132f, -76f), ConfirmRestart);
            restartConfirmCancelButton = CreateRestartButton(panel.transform, "Cancel", new Vector2(132f, -76f), CancelRestartConfirmation);

            restartConfirmRoot.SetActive(false);
        }

        private static GameObject CreateRestartUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateRestartText(string name, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var go = CreateRestartUiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.93f, 0.9f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateRestartButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateRestartUiObject($"{label}Button", parent);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(210f, 58f);
            rect.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            image.color = label == "Restart"
                ? new Color(0.42f, 0.18f, 0.50f, 0.98f)
                : new Color(0.20f, 0.23f, 0.36f, 0.98f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateRestartText("Label", go.transform, label, 24, TextAnchor.MiddleCenter);
            SetRestartRect(text.rectTransform, Vector2.zero, rect.sizeDelta);
            return button;
        }

        private static void SetRestartRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static bool IsRestartConfirmAcceptHeld() =>
            Input.GetKey(KeyCode.Return) ||
            Input.GetKey(KeyCode.KeypadEnter) ||
            Input.GetKey(KeyCode.JoystickButton0);

        private static bool IsRestartConfirmCancelHeld() =>
            Input.GetKey(KeyCode.Escape) ||
            Input.GetKey(KeyCode.JoystickButton1);

        private static bool IsHoldQueueSwapOneHeld() =>
            Input.GetKey(KeyCode.Alpha1) ||
            Input.GetKey(KeyCode.Keypad1);

        private static bool IsHoldQueueSwapTwoHeld() =>
            Input.GetKey(KeyCode.Alpha2) ||
            Input.GetKey(KeyCode.Keypad2);

        private static bool IsHoldQueueSwapThreeHeld() =>
            Input.GetKey(KeyCode.Alpha3) ||
            Input.GetKey(KeyCode.Keypad3);

        private static bool WasRestartConfirmPressed(bool current, ref bool previous)
        {
            var pressed = current && !previous;
            previous = current;
            return pressed;
        }

        public void RestartMode()
        {
            HideRestartConfirmation();
            endRunPanelShown = false;
            if (endRunRoot)
            {
                endRunRoot.SetActive(false);
            }

            if (AssistEffectSystem.IsEnabledFor(mode, MonStackaAppState.FriendlyAbilitiesEnabled))
            {
                assistSystem ??= new AssistEffectSystem();
            }
            else
            {
                assistSystem = null;
            }

            boardState.Reset();
            assistSystem?.Reset();
            storyMissionComplete = false;
            storyMissionFailed = false;
            storyOutroStarted = false;
            storyModifiers?.OnMatchStart();
            garbageCellView?.Refresh();
            gravityTimer = 0f;
            lockTimer = 0f;
            lockResets = 0;
            countdownRemaining = CountdownSeconds;
            pausedAccumSeconds = 0f;
            completedElapsedSeconds = -1f;
            recordsSubmitted = false;
            paused = false;
            pauseOverlay?.SetVisible(false);
            startTime = Time.time + CountdownSeconds;
            hudController?.RenderLeaderboard(MonStackaRecords.GetDisplayRows(mode, MonStackaAppState.FriendlyAbilitiesEnabled));
            RebuildBoardViews();
        }

        public void ReturnHome()
        {
            SceneManager.LoadScene(homeSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public bool IsPaused => paused;

        public bool IsDialogueInputBlocking =>
            dialogueGate ||
            (dialoguePresenter && (dialoguePresenter.IsActive || dialoguePresenter.IsWaitingForAdvanceRelease));

        public bool IsGameOver => boardState?.IsGameOver() ?? false;

        private bool IsGamepadLeftHeld()
        {
            return MonStackaControls.IsGameplayLeftHeld();
        }

        private bool IsGamepadRightHeld()
        {
            return MonStackaControls.IsGameplayRightHeld();
        }

        private bool IsGamepadLeftPressed()
        {
            var current = IsGamepadLeftHeld();
            return current && !previousLeftHeld;
        }

        private bool IsGamepadRightPressed()
        {
            var current = IsGamepadRightHeld();
            return current && !previousRightHeld;
        }

        private bool IsGamepadSoftDropHeld()
        {
            return MonStackaControls.IsGameplaySoftDropHeld();
        }

        private bool IsGamepadHardDropPressed()
        {
            var current = MonStackaControls.IsGameplayHardDropHeld();
            return current && !previousHardDropHeld;
        }

        private bool IsGamepadCcwPressed()
        {
            var current = MonStackaControls.IsGameplayRotateCcwHeld();
            return current && !previousRotateCcwHeld;
        }

        private bool IsGamepadCwPressed()
        {
            var current = MonStackaControls.IsGameplayRotateCwHeld();
            return current && !previousRotateCwHeld;
        }

        private bool IsGamepadFlipPressed()
        {
            var current = MonStackaControls.IsGameplayRotateFlipHeld();
            return current && !previousRotateFlipHeld;
        }

        private bool IsGamepadHoldPressed()
        {
            var current = MonStackaControls.IsGameplayHoldHeld();
            return current && !previousHoldHeld;
        }

        private bool IsRetryPressed()
        {
            return IsPressed(MonStackaControls.IsRetryHeld(), ref previousRetryHeld);
        }

        private bool IsPausePressed()
        {
            return IsPressed(MonStackaControls.IsPauseHeld(), ref previousPauseHeld);
        }

        private bool IsRestartPausedPressed()
        {
            return IsPressed(MonStackaControls.IsRestartPausedHeld(), ref previousRestartPausedHeld);
        }

        private void RememberGamepadButtonState()
        {
            previousLeftHeld = MonStackaControls.IsGameplayLeftHeld();
            previousRightHeld = MonStackaControls.IsGameplayRightHeld();
            previousHardDropHeld = MonStackaControls.IsGameplayHardDropHeld();
            previousRotateCcwHeld = MonStackaControls.IsGameplayRotateCcwHeld();
            previousRotateCwHeld = MonStackaControls.IsGameplayRotateCwHeld();
            previousRotateFlipHeld = MonStackaControls.IsGameplayRotateFlipHeld();
            previousHoldHeld = MonStackaControls.IsGameplayHoldHeld();
            previousHoldSwapOneHeld = IsHoldQueueSwapOneHeld();
            previousHoldSwapTwoHeld = IsHoldQueueSwapTwoHeld();
            previousHoldSwapThreeHeld = IsHoldQueueSwapThreeHeld();
            previousPauseHeld = MonStackaControls.IsPauseHeld();
            previousRetryHeld = MonStackaControls.IsRetryHeld();
            previousRestartPausedHeld = MonStackaControls.IsRestartPausedHeld();
        }

        private static bool IsPressed(bool current, ref bool previous)
        {
            var pressed = current && !previous;
            return pressed;
        }
    }
}
