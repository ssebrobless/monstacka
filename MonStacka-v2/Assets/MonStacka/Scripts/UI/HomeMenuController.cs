using System;
using System.Collections.Generic;
using System.Linq;
using MonStacka.Core;
using MonStacka.Visual;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonStacka.UI
{
    public sealed class HomeMenuController : MonoBehaviour
    {
        private readonly struct MonstosProfile
        {
            public readonly PieceType PieceType;
            public readonly string Name;
            public readonly string Lore;
            public readonly string VoiceHint;
            public readonly int PreviewRotation;

            public MonstosProfile(PieceType pieceType, string name, string lore, string voiceHint, int previewRotation)
            {
                PieceType = pieceType;
                Name = name;
                Lore = lore;
                VoiceHint = voiceHint;
                PreviewRotation = previewRotation;
            }
        }

        private enum MonstosInfoMode
        {
            None,
            Lore,
            Rules,
            FriendlyAbility,
            EnemyAbility,
        }

        private static readonly MonstosProfile[] Profiles =
        {
            new(PieceType.S, "SORRISOL", "Designed to clean any mess and have a constant insatiable hunger. In need of dental reconstruction, majority of mouth full of molars; too many waking up before it finishes cleaning.", "A scratchy zipper grin.", 0),
            new(PieceType.I, "BLYNDOOLIE", "It sees all. Good luck sneaking up. Wait... it doesn't even blink. Is it drooling on its own eyes?", "Wet little staring noises.", 1),
            new(PieceType.Z, "AGGRASO", "The first one that didn't melt into goop... ectodermal influx. A minor over correction on our part. Approach with caution.", "A mossy chomp.", 0),
            new(PieceType.O, "MUWERDE", "Unfortunately one of the smartest in the bunch, though measures of intelligence are inconsistent. Provides solid data until it begins refusing to cooperate. The screams were annoying.", "A round hungry gasp.", 0),
            new(PieceType.T, "LYSERGICADA", "Might as well be lobotomized. Successfully managed to have naturally occurring traces of lysergic acid diethylamide secreting from the saliva glands. Safe to say the host is no longer in control.", "A sticky gargle.", 2),
            new(PieceType.J, "DOUSEMA", "Surprisingly resilient. All teeth and four of its eyes were redistributed to more promising candidates. Had I realized the potential sooner... what a waste.", "A tiny nasal sniff.", 3),
            new(PieceType.L, "GALIFFAMBOS", "Thee who listens. Not a step is taken without being announced first. The oldest of the refined ones. The eye remained because the echolocation was too funny not to keep.", "A twitchy ear wiggle.", 1),
        };

        private static readonly Dictionary<PieceType, float> ActivePreviewFill = new()
        {
            [PieceType.I] = 0.60f,
            [PieceType.O] = 0.54f,
            [PieceType.T] = 0.54f,
            [PieceType.S] = 0.48f,
            [PieceType.Z] = 0.48f,
            [PieceType.J] = 0.50f,
            [PieceType.L] = 0.50f,
        };

        private static readonly Dictionary<PieceType, float> SidePreviewFill = new()
        {
            [PieceType.I] = 0.49f,
            [PieceType.O] = 0.45f,
            [PieceType.T] = 0.44f,
            [PieceType.S] = 0.40f,
            [PieceType.Z] = 0.40f,
            [PieceType.J] = 0.42f,
            [PieceType.L] = 0.42f,
        };

        private static readonly Vector2 LeftPreviewSlotWorld = new(3.15f, 2.57f);
        private static readonly Vector2 CenterPreviewSlotWorld = new(5.22f, 4.45f);
        private static readonly Vector2 RightPreviewSlotWorld = new(3.53f, 2.66f);
        private const float PixelsPerUnit = 100f;

        [SerializeField] private Transform previewLeftAnchor;
        [SerializeField] private Transform previewCenterAnchor;
        [SerializeField] private Transform previewRightAnchor;
        [SerializeField] private Text monstosNameText;
        [SerializeField] private Button monstosVoiceButton;
        [SerializeField] private Button monstosLoreButton;
        private Button monstosRulesButton;
        private Button monstosFriendlyAbilityButton;
        private Button monstosEnemyAbilityButton;
        private GameObject modeVariantPromptRoot;
        private Button zanyVariantButton;
        private Button classicVariantButton;
        private Button leaderboardStyleToggleButton;
        private Text leaderboardStyleToggleLabel;
        private Text[] homeOgbmLeaderboardTexts;
        private Text[] homeSprintLeaderboardTexts;
        [SerializeField] private GameObject monstosLorePanel;
        [SerializeField] private Text monstosLoreText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button startOgbmButton;
        [SerializeField] private Button startSprintButton;
        [SerializeField] private Button startTrainingButton;
        [SerializeField] private Button startStoryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
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
        [SerializeField] private bool enableVisualExtras = true;
        [SerializeField] private MonStackaAudioController audioController;
        [SerializeField] private PieceSkinData[] pieceSkins;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private BorderDeformTuningProfile deformTuning;

        private readonly Dictionary<PieceType, PieceSkinData> skinLookup = new();
        private readonly List<PieceSkin> previewSkins = new();
        private readonly Dictionary<PieceType, AudioClip> voicePreviewClips = new();
        private readonly List<Button> menuButtons = new();
        private readonly List<Button> settingsButtons = new();
        private int activeIndex = 1;
        private bool loreOpen;
        private MonstosInfoMode activeInfoMode;
        private bool leaderboardShowsZany;
        private AudioSource voicePreviewSource;
        private bool previousNavigateUp;
        private bool previousNavigateDown;
        private bool previousCycleLeft;
        private bool previousCycleRight;
        private bool previousSubmit;
        private bool previousCancel;
        private bool previousVoice;
        private bool previousLore;
        private MonStackaMode? commandLineLaunchMode;
        private MonStackaMode pendingVariantMode;
        private int selectedControlIndex;
        private MonStackaControlAction? awaitingBindingAction;
        private int bindingCaptureFrame;

        private void Awake()
        {
            foreach (var skin in pieceSkins.Where(skin => skin))
            {
                skinLookup[skin.pieceType] = skin;
            }

            voicePreviewSource = gameObject.AddComponent<AudioSource>();
            voicePreviewSource.playOnAwake = false;
            voicePreviewSource.loop = false;
            voicePreviewSource.spatialBlend = 0f;
            voicePreviewSource.volume = 0.34f;

            if (monstosNameText)
            {
                monstosNameText.resizeTextForBestFit = false;
                monstosNameText.fontSize = 28;
                monstosNameText.supportRichText = false;
                monstosNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                monstosNameText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (monstosLoreText)
            {
                monstosLoreText.supportRichText = true;
                monstosLoreText.resizeTextForBestFit = true;
                monstosLoreText.resizeTextMinSize = 12;
                monstosLoreText.resizeTextMaxSize = 22;
                monstosLoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
                monstosLoreText.verticalOverflow = VerticalWrapMode.Truncate;
                monstosLoreText.lineSpacing = 0.88f;
                monstosLoreText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                var outline = monstosLoreText.GetComponent<Outline>() ?? monstosLoreText.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.02f, 0.015f, 0.05f, 0.92f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);
            }

            prevButton?.onClick.AddListener(() => Cycle(-1));
            nextButton?.onClick.AddListener(() => Cycle(1));
            EnsureAbilityInfoButtons();
            monstosVoiceButton?.onClick.AddListener(PlayVoicePreview);
            monstosLoreButton?.onClick.AddListener(ToggleLore);
            monstosRulesButton?.onClick.AddListener(ToggleRules);
            monstosFriendlyAbilityButton?.onClick.AddListener(ToggleFriendlyAbility);
            monstosEnemyAbilityButton?.onClick.AddListener(ToggleEnemyAbility);
            startOgbmButton?.onClick.AddListener(() => ShowModeVariantPrompt(MonStackaMode.Ogbm));
            startSprintButton?.onClick.AddListener(() => ShowModeVariantPrompt(MonStackaMode.Sprint40));
            startTrainingButton?.onClick.AddListener(StartTrainingMode);
            startStoryButton?.onClick.AddListener(OpenStorySelect);
            settingsButton?.onClick.AddListener(OpenSettings);
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
            quitButton?.onClick.AddListener(QuitGame);
            menuButtons.Clear();
            if (settingsButton) menuButtons.Add(settingsButton);
            if (quitButton) menuButtons.Add(quitButton);
            if (startOgbmButton) menuButtons.Add(startOgbmButton);
            if (startSprintButton) menuButtons.Add(startSprintButton);
            if (startTrainingButton) menuButtons.Add(startTrainingButton);
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
            EnsureModeVariantPrompt();
            EnsureHomeLeaderboard();
            SetInfoVisible(MonstosInfoMode.None);
            RebuildPreview();
            RefreshHomeLeaderboard();
            RefreshSettingsText();

            if (startOgbmButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(startOgbmButton.gameObject);
            }

            commandLineLaunchMode = GetCommandLineLaunchMode();
        }

        private void Start()
        {
            if (commandLineLaunchMode.HasValue)
            {
                var mode = commandLineLaunchMode.Value;
                commandLineLaunchMode = null;
                StartMode(mode);
            }
        }

        private void Update()
        {
            var now = Time.time;
            foreach (var preview in previewSkins)
            {
                if (preview && preview.RequiresManualUpdate)
                {
                    preview.ManualUpdate(now);
                }
            }

            HandleMenuInput();
        }

        private void StartMode(MonStackaMode mode)
        {
            StopVoicePreview();
            MonStackaAppState.SelectedMode = mode;
            SceneManager.LoadScene("Game");
        }

        private void StartTrainingMode()
        {
            MonStackaAppState.FriendlyAbilitiesEnabled = true;
            StartMode(MonStackaMode.Training);
        }

        private void StartVariantMode(bool zany)
        {
            MonStackaAppState.FriendlyAbilitiesEnabled = zany;
            SetModeVariantPromptVisible(false);
            StartMode(pendingVariantMode);
        }

        private void ShowModeVariantPrompt(MonStackaMode mode)
        {
            pendingVariantMode = mode;
            EnsureModeVariantPrompt();
            SetModeVariantPromptVisible(true);
        }

        private void OpenStorySelect()
        {
            StopVoicePreview();
            SceneManager.LoadScene("StorySelect");
        }

        private void Cycle(int direction)
        {
            StopVoicePreview();
            activeIndex = (activeIndex + direction + Profiles.Length) % Profiles.Length;
            RebuildPreview();
            RefreshInfoPanelText();
        }

        private void RebuildPreview()
        {
            foreach (var preview in previewSkins)
            {
                if (!preview)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    preview.gameObject.SetActive(false);
                    Destroy(preview.gameObject);
                }
                else
                {
                    DestroyImmediate(preview.gameObject);
                }
            }

            previewSkins.Clear();

            var left = Profiles[(activeIndex - 1 + Profiles.Length) % Profiles.Length];
            var center = Profiles[activeIndex];
            var right = Profiles[(activeIndex + 1) % Profiles.Length];

            if (monstosNameText)
            {
                monstosNameText.text = center.Name;
            }

            if (monstosLoreText)
            {
                monstosLoreText.text = BuildInfoPanelText(center);
            }

            previewSkins.Add(CreatePreview("PreviewLeft", previewLeftAnchor, left, LeftPreviewSlotWorld, SidePreviewFill[left.PieceType], 0f));
            previewSkins.Add(CreatePreview("PreviewCenter", previewCenterAnchor, center, CenterPreviewSlotWorld, ActivePreviewFill[center.PieceType], GetRecoveryPulseScale(0.12f)));
            previewSkins.Add(CreatePreview("PreviewRight", previewRightAnchor, right, RightPreviewSlotWorld, SidePreviewFill[right.PieceType], 0f));
        }

        private PieceSkin CreatePreview(string objectName, Transform anchor, MonstosProfile profile, Vector2 slotWorldSize, float fillRatio, float pulseScale)
        {
            if (!anchor || !skinLookup.TryGetValue(profile.PieceType, out var skinData))
            {
                return null;
            }

            var definition = PieceDefinitions.GetCells(profile.PieceType, profile.PreviewRotation).ToArray();
            var minX = definition.Min(cell => cell.x);
            var minY = definition.Min(cell => cell.y);
            var maxX = definition.Max(cell => cell.x);
            var maxY = definition.Max(cell => cell.y);
            var widthCells = maxX - minX + 1;
            var heightCells = maxY - minY + 1;
            var normalized = definition.Select(cell => new Vector2Int(cell.x - minX, cell.y - minY)).ToArray();
            var cellWorldSize = Mathf.Min(slotWorldSize.x / widthCells, slotWorldSize.y / heightCells) * fillRatio;

            var go = new GameObject(objectName);
            go.transform.SetParent(anchor, false);
            var skin = go.AddComponent<PieceSkin>();
            skin.Initialize(
                skinData,
                profile.PieceType,
                profile.PreviewRotation,
                normalized,
                cellWorldSize,
                outlineMaterial,
                deformTuning,
                true,
                pulseScale,
                true,
                true
            );
            go.transform.localPosition = new Vector3(
                -(widthCells * cellWorldSize) * 0.5f,
                (heightCells * cellWorldSize) * 0.5f,
                0f
            );
            return skin;
        }

        private bool AreVisualExtrasEnabled()
        {
            return enableVisualExtras && MonStackaAppState.VisualExtrasEnabled;
        }

        private float GetRecoveryPulseScale(float requestedScale)
        {
            return AreVisualExtrasEnabled() && MonStackaAppState.RippleStage >= MonStackaRippleStage.HomePreview ? requestedScale : 0f;
        }

        private void ToggleLore()
        {
            SetInfoVisible(activeInfoMode == MonstosInfoMode.Lore ? MonstosInfoMode.None : MonstosInfoMode.Lore);
        }

        private void ToggleRules()
        {
            SetInfoVisible(activeInfoMode == MonstosInfoMode.Rules ? MonstosInfoMode.None : MonstosInfoMode.Rules);
        }

        private void ToggleFriendlyAbility()
        {
            SetInfoVisible(activeInfoMode == MonstosInfoMode.FriendlyAbility ? MonstosInfoMode.None : MonstosInfoMode.FriendlyAbility);
        }

        private void ToggleEnemyAbility()
        {
            SetInfoVisible(activeInfoMode == MonstosInfoMode.EnemyAbility ? MonstosInfoMode.None : MonstosInfoMode.EnemyAbility);
        }

        private void SetInfoVisible(MonstosInfoMode mode)
        {
            activeInfoMode = mode;
            loreOpen = mode != MonstosInfoMode.None;
            ConfigureInfoPanelLayout();
            RefreshInfoPanelText();
            if (monstosLorePanel)
            {
                monstosLorePanel.SetActive(loreOpen);
            }
        }

        private void ConfigureInfoPanelLayout()
        {
            if (!monstosLorePanel || !monstosLoreText)
            {
                return;
            }

            var largeReference = activeInfoMode is MonstosInfoMode.Rules or MonstosInfoMode.FriendlyAbility or MonstosInfoMode.EnemyAbility;
            var panelRect = monstosLorePanel.GetComponent<RectTransform>();
            var surfaceRect = monstosLoreText.transform.parent ? monstosLoreText.transform.parent.GetComponent<RectTransform>() : null;
            var tailRect = monstosLorePanel.transform.Find("LoreTail")?.GetComponent<RectTransform>();

            if (panelRect)
            {
                panelRect.anchoredPosition = largeReference ? new Vector2(560f, -116f) : new Vector2(564f, -322f);
                panelRect.sizeDelta = largeReference ? new Vector2(720f, 360f) : new Vector2(612f, 214f);
            }

            if (surfaceRect)
            {
                surfaceRect.anchoredPosition = largeReference ? new Vector2(0f, 0f) : new Vector2(56f, 0f);
                surfaceRect.sizeDelta = largeReference ? new Vector2(720f, 360f) : new Vector2(486f, 152f);
                var surfaceImage = surfaceRect.GetComponent<Image>();
                if (surfaceImage)
                {
                    surfaceImage.color = largeReference
                        ? new Color(0.045f, 0.055f, 0.105f, 0.98f)
                        : new Color(1f, 0.996f, 0.988f, 1f);
                }
            }

            if (tailRect)
            {
                tailRect.gameObject.SetActive(!largeReference);
            }

            var textRect = monstosLoreText.rectTransform;
            textRect.anchoredPosition = largeReference ? new Vector2(28f, -24f) : new Vector2(22f, -18f);
            textRect.sizeDelta = largeReference ? new Vector2(664f, 312f) : new Vector2(442f, 116f);

            monstosLoreText.alignment = largeReference ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            monstosLoreText.resizeTextForBestFit = !largeReference;
            monstosLoreText.fontSize = largeReference ? 20 : 22;
            monstosLoreText.resizeTextMinSize = largeReference ? 18 : 16;
            monstosLoreText.resizeTextMaxSize = largeReference ? 20 : 24;
            monstosLoreText.lineSpacing = largeReference ? 1.02f : 0.92f;
            monstosLoreText.color = largeReference
                ? new Color(0.96f, 0.96f, 0.99f, 1f)
                : new Color(0.06f, 0.05f, 0.1f, 1f);
            var textOutline = monstosLoreText.GetComponent<Outline>();
            if (textOutline)
            {
                textOutline.enabled = !largeReference;
            }
        }

        private void RefreshInfoPanelText()
        {
            if (!monstosLoreText)
            {
                return;
            }

            monstosLoreText.text = BuildInfoPanelText(Profiles[activeIndex]);
        }

        private string BuildInfoPanelText(MonstosProfile profile)
        {
            return activeInfoMode switch
            {
                MonstosInfoMode.Rules => BuildSharedFriendlyRulesText(),
                MonstosInfoMode.FriendlyAbility => BuildFriendlyAbilityText(profile.PieceType),
                MonstosInfoMode.EnemyAbility => BuildEnemyAbilityText(profile.PieceType),
                _ => profile.Lore,
            };
        }

        private void EnsureAbilityInfoButtons()
        {
            if (!monstosLoreButton)
            {
                return;
            }

            var loreRect = monstosLoreButton.GetComponent<RectTransform>();
            var parent = loreRect ? loreRect.parent : monstosLoreButton.transform.parent;
            if (!parent)
            {
                return;
            }

            monstosRulesButton ??= CreateInfoButton(
                "RulesAbilityButton",
                parent,
                loreRect,
                new Vector2(139f, -74f),
                "RULES",
                new Color(0.18f, 0.21f, 0.54f, 0.94f),
                new Vector2(164f, 42f)
            );
            monstosFriendlyAbilityButton ??= CreateInfoButton(
                "FriendlyAbilityButton",
                parent,
                loreRect,
                new Vector2(96f, -126f),
                "ALLY",
                new Color(0.14f, 0.68f, 0.28f, 0.92f)
            );
            monstosEnemyAbilityButton ??= CreateInfoButton(
                "EnemyAbilityButton",
                parent,
                loreRect,
                new Vector2(182f, -126f),
                "ENEMY",
                new Color(0.78f, 0.12f, 0.12f, 0.92f)
            );
        }

        private void EnsureModeVariantPrompt()
        {
            if (modeVariantPromptRoot)
            {
                return;
            }

            var parent = settingsPanel ? settingsPanel.transform.parent : transform;
            modeVariantPromptRoot = new GameObject("ModeVariantPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            modeVariantPromptRoot.transform.SetParent(parent, false);
            modeVariantPromptRoot.transform.SetAsLastSibling();
            var rootRect = modeVariantPromptRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var blocker = modeVariantPromptRoot.GetComponent<Image>();
            blocker.color = new Color(0.02f, 0.02f, 0.08f, 0.52f);
            blocker.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(modeVariantPromptRoot.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(520f, 230f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.16f, 0.96f);

            var title = CreatePromptText(panel.transform, "Choose Style", 31, new Vector2(0f, 70f), new Vector2(460f, 44f));
            title.fontStyle = FontStyle.Bold;
            CreatePromptText(panel.transform, "Zany uses friendly held-block abilities. Classic has zero abilities.", 20, new Vector2(0f, 26f), new Vector2(440f, 58f));

            zanyVariantButton = CreatePromptButton(panel.transform, "Zany", new Vector2(-120f, -66f), new Color(0.14f, 0.68f, 0.28f, 0.96f), () => StartVariantMode(true));
            classicVariantButton = CreatePromptButton(panel.transform, "Classic", new Vector2(120f, -66f), new Color(0.18f, 0.23f, 0.36f, 0.96f), () => StartVariantMode(false));

            SetModeVariantPromptVisible(false);
        }

        private void EnsureHomeLeaderboard()
        {
            if (leaderboardStyleToggleButton)
            {
                return;
            }

            var parent = settingsPanel ? settingsPanel.transform.parent : transform;

            leaderboardStyleToggleButton = CreateLeaderboardStyleButton(parent);
            leaderboardStyleToggleButton.onClick.AddListener(() =>
            {
                leaderboardShowsZany = !leaderboardShowsZany;
                RefreshHomeLeaderboard();
            });

            homeSprintLeaderboardTexts = CreateLeaderboardColumn(parent, "Sprint", new Vector2(1050f, -826f));
            homeOgbmLeaderboardTexts = CreateLeaderboardColumn(parent, "Ogbm", new Vector2(1190f, -826f));
        }

        private Button CreateLeaderboardStyleButton(Transform parent)
        {
            var go = new GameObject("LeaderboardStyleToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(1132f, -572f);
            rect.sizeDelta = new Vector2(170f, 32f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.32f, 0.92f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            leaderboardStyleToggleLabel = labelGo.GetComponent<Text>();
            leaderboardStyleToggleLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            leaderboardStyleToggleLabel.fontSize = 18;
            leaderboardStyleToggleLabel.fontStyle = FontStyle.Bold;
            leaderboardStyleToggleLabel.alignment = TextAnchor.MiddleCenter;
            leaderboardStyleToggleLabel.color = Color.white;
            leaderboardStyleToggleLabel.raycastTarget = false;
            return button;
        }

        private static Text[] CreateLeaderboardColumn(Transform parent, string name, Vector2 startPosition)
        {
            var rows = new Text[MonStackaRecords.MaxEntries];
            for (var index = 0; index < rows.Length; index += 1)
            {
                var go = new GameObject($"HomeLeaderboard_{name}_{index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(parent, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = startPosition + new Vector2(0f, -(index * 58f));
                rect.sizeDelta = new Vector2(112f, 34f);

                var text = go.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 20;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.05f, 0.04f, 0.10f, 1f);
                text.raycastTarget = false;
                rows[index] = text;
            }

            return rows;
        }

        private void RefreshHomeLeaderboard()
        {
            if (!leaderboardStyleToggleLabel)
            {
                return;
            }

            leaderboardStyleToggleLabel.text = leaderboardShowsZany ? "ZANY SCORES" : "CLASSIC SCORES";
            var buttonImage = leaderboardStyleToggleButton ? leaderboardStyleToggleButton.GetComponent<Image>() : null;
            if (buttonImage)
            {
                buttonImage.color = leaderboardShowsZany
                    ? new Color(0.12f, 0.50f, 0.22f, 0.92f)
                    : new Color(0.12f, 0.16f, 0.32f, 0.92f);
            }

            SetLeaderboardRows(
                homeOgbmLeaderboardTexts,
                MonStackaRecords.GetOgbmScores(leaderboardShowsZany).Select(score => score.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList()
            );
            SetLeaderboardRows(
                homeSprintLeaderboardTexts,
                MonStackaRecords.GetSprintTimes(leaderboardShowsZany).Select(MonStackaRecords.FormatMs).ToList()
            );
        }

        private static void SetLeaderboardRows(Text[] textRows, IReadOnlyList<string> values)
        {
            if (textRows == null)
            {
                return;
            }

            for (var index = 0; index < textRows.Length; index += 1)
            {
                if (textRows[index])
                {
                    textRows[index].text = values != null && index < values.Count ? values[index] : "---";
                }
            }
        }

        private static Text CreatePromptText(Transform parent, string value, int fontSize, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.95f, 0.94f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreatePromptButton(Transform parent, string label, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"{label}Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 58f);

            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreatePromptText(go.transform, label, 24, Vector2.zero, rect.sizeDelta).fontStyle = FontStyle.Bold;
            return button;
        }

        private void SetModeVariantPromptVisible(bool visible)
        {
            if (modeVariantPromptRoot)
            {
                modeVariantPromptRoot.SetActive(visible);
            }

            if (visible)
            {
                SetAbilityInfoButtonsVisible(false);
                SetHomeLeaderboardVisible(false);
                SetInfoVisible(MonstosInfoMode.None);
                if (zanyVariantButton && EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(zanyVariantButton.gameObject);
                }
            }
            else if (!(settingsPanel && settingsPanel.activeSelf))
            {
                SetAbilityInfoButtonsVisible(true);
                SetHomeLeaderboardVisible(true);
            }
        }

        private static Button CreateInfoButton(string objectName, Transform parent, RectTransform templateRect, Vector2 offset, string label, Color color, Vector2? size = null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (templateRect)
            {
                rect.anchorMin = templateRect.anchorMin;
                rect.anchorMax = templateRect.anchorMax;
                rect.pivot = templateRect.pivot;
                rect.anchoredPosition = templateRect.anchoredPosition + offset;
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = offset;
            }

            rect.sizeDelta = size ?? new Vector2(78f, 48f);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 17;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }

        private static string BuildSharedFriendlyRulesText()
        {
            return
                "<color=#8EE8FF><b>SHARED ALLY RULES</b></color>\n" +
                "<color=#C8FF9A><b>Where enabled</b></color>\n" +
                "- Story Mode: always on.\n" +
                "- O.G.B.M. and X(4)-LINES: Zany on, Classic off.\n" +
                "- Training: toggle on/off in match; toggling resets the board.\n\n" +
                "<color=#C8FF9A><b>How to trigger</b></color>\n" +
                "Put a block in Hold, bring that held block back out, then place it. Do that 3 total times. When the Hold box glows, the next held block placed fires its ally ability.\n\n" +
                "<color=#FFD86B><b>Shared points</b></color>\n" +
                "+150 base trigger points.\n" +
                "+100 per combo step after the first trigger.\n" +
                "+300 danger-save bonus when applicable.\n" +
                "+200 per line during active 8-second timed windows.\n\n" +
                "<color=#FF9A9A><b>Combo + danger</b></color>\n" +
                "Trigger again within 6 total placements to keep the combo chain. Danger means blocks have reached the upper danger zone, about 14 visible rows high.";
        }

        private static string BuildFriendlyAbilityText(PieceType pieceType)
        {
            var assist = AssistEffectSystem.AssistForPiece(pieceType);
            var label = AssistEffectSystem.LabelFor(assist);
            var titleColor = PieceDefinitions.PieceColors.TryGetValue(pieceType, out var pieceColor)
                ? ColorUtility.ToHtmlStringRGB(pieceColor)
                : "FFFFFF";
            var mechanics = assist switch
            {
                AssistType.GuardBreak =>
                    "Removes up to 6 enemy junk/garbage cells. Cleanup checks low rows first, then the left and right edges.",
                AssistType.Calculation =>
                    "Starts an 8-second window where the Next queue shows 2 extra upcoming blocks.",
                AssistType.EchoGuide =>
                    "Starts an 8-second Echo Guide window for enhanced placement guidance / safer landing reads.",
                AssistType.Stitch =>
                    "Searches for the deepest covered hole, then fills that empty cell with a repair cell.",
                AssistType.Digest =>
                    "Removes up to 8 enemy junk/garbage cells and pays extra for each cell eaten.",
                AssistType.Sedate =>
                    "Starts an 8-second Sedate window. Falling slows and lock delay gets longer.",
                AssistType.Alert =>
                    "Starts an 8-second Alert window. If the stack is dangerous, clears can get extra value.",
                _ => "Triggers this block's held-piece assist.",
            };
            var pointLine = assist switch
            {
                AssistType.GuardBreak => "+20 per junk cell removed. +300 danger-save if dangerous and at least one junk cell was removed.",
                AssistType.Digest => "+40 per junk cell removed. +300 danger-save if dangerous and at least one junk cell was removed.",
                AssistType.Stitch => "+120 if a covered hole was successfully repaired.",
                AssistType.Calculation or AssistType.EchoGuide or AssistType.Sedate or AssistType.Alert =>
                    "+200 per line cleared before the 8-second window ends.",
                _ => "Uses shared trigger and combo scoring.",
            };
            var tuningLine = assist switch
            {
                AssistType.Calculation =>
                    "This ability rewards planning and line clears during its window. It does not give an instant danger-save payout by itself.",
                AssistType.EchoGuide =>
                    "This ability rewards line clears while the guide window is active. It does not give an instant danger-save payout by itself.",
                AssistType.Sedate =>
                    "Gravity multiplier becomes 1.6, meaning slower falling. Lock delay gets +0.15 seconds. It does not give an instant danger-save payout by itself.",
                AssistType.Alert =>
                    "If already dangerous when triggered, awards +300. During the window, dangerous clears can receive a 1.5x Alert score multiplier.",
                _ => string.Empty,
            };
            return
                $"<color=#{titleColor}><b>{label}</b></color>\n" +
                "<color=#C8FF9A><b>What it does</b></color>\n" +
                $"{mechanics}\n\n" +
                "<color=#FFD86B><b>Ability points</b></color>\n" +
                $"{pointLine}\n\n" +
                (string.IsNullOrEmpty(tuningLine)
                    ? "<color=#A8E6FF><b>Shared rules</b></color>\nUse the Rules button for trigger, combo, and shared scoring."
                    : $"<color=#A8E6FF><b>Runtime note</b></color>\n{tuningLine}\n\n<color=#A8E6FF><b>Shared rules</b></color>\nUse the Rules button for trigger, combo, and shared scoring.");
        }

        private static string BuildEnemyAbilityText(PieceType pieceType)
        {
            var titleColor = PieceDefinitions.PieceColors.TryGetValue(pieceType, out var pieceColor)
                ? ColorUtility.ToHtmlStringRGB(pieceColor)
                : "FFFFFF";
            return pieceType switch
            {
                PieceType.Z =>
                    $"<color=#{titleColor}><b>AGGRASO PRESSURE</b></color>\n" +
                    "<color=#FFB0B0><b>Modifiers</b></color>\nGuardPressure, TerritoryCells\n\n" +
                    "<color=#FFD86B><b>Triggers</b></color>\nGuardPressure is active for the whole mission. It applies whenever a falling piece touches down and enters lock-delay behavior.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nLock delay is multiplied by 0.6, so pieces stick faster after contact. TerritoryCells seeds 4 + difficulty tier enemy cells at match start. Signal Relay can temporarily reactivate either effect.",
                PieceType.O =>
                    $"<color=#{titleColor}><b>CALCULATED</b></color>\n" +
                    "<color=#FFB0B0><b>Modifiers</b></color>\nCalculatedPlanning, PrecisionPressure\n\n" +
                    "<color=#FFD86B><b>Triggers</b></color>\nCalculatedPlanning watches every piece you lock. PrecisionPressure watches each locked piece for unsupported overhang cells.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nCalculatedPlanning gives safer missions a longer Next preview, but each piece has only 2 safe successful rotations. Extra rotations seed 1 enemy cell each, up to 3 per piece. PrecisionPressure seeds enemy cells when a locked block leaves unsupported cells hanging over empty space, up to 3 cells per piece.",
                PieceType.L =>
                    $"<color=#{titleColor}><b>BLINDED</b></color>\n" +
                    "<color=#FFB0B0><b>Modifiers</b></color>\nGhostFlicker, EcholocationDim\n\n" +
                    "<color=#FFD86B><b>Triggers</b></color>\nBoth effects are active for the whole mission when included.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nGhostFlicker runs on a 2.6s cycle: active piece hidden for 0.35s, then visible. EcholocationDim runs on a 3.5s cycle: board clear for 0.5s, then dimmed at 0.42 alpha. Signal Relay can temporarily reactivate GhostFlicker.",
                PieceType.J =>
                    $"<color=#{titleColor}><b>MUTE</b></color>\n" +
                    "<color=#FFB0B0><b>Modifiers</b></color>\nResilientCells, MutedHints, NoHold\n\n" +
                    "<color=#FFD86B><b>Triggers</b></color>\nMutedHints and NoHold are whole-mission rules when included. ResilientCells triggers after line clears.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nHints can be hidden, Hold can be disabled, and line clears can regrow one enemy territory cell. Regrow chance is 30% + 3% per difficulty tier.",
                PieceType.S =>
                    $"<color=#{titleColor}><b>SORRISOL HUNGER</b></color>\n" +
                    "<color=#FFB0B0><b>Modifier</b></color>\nHungerMeter\n\n" +
                    "<color=#FFD86B><b>Trigger</b></color>\nActive for the whole mission. Timer starts at 0 and resets whenever you clear any number of lines.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nThe hunger window is 22s minus difficulty tier, minimum 10s. If it fills, one garbage row rises from the bottom with one random open column.",
                PieceType.T =>
                    $"<color=#{titleColor}><b>LYSERGICADA SEDATION</b></color>\n" +
                    "<color=#FFB0B0><b>Modifier</b></color>\nSedationWindows\n\n" +
                    "<color=#FFD86B><b>Trigger</b></color>\nActive for the whole mission on an 18s cycle. Warning starts 7s into the cycle and lasts 3s. Active sedation starts at 14s and lasts 4s.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nDuring active sedation, DAS/ARR input timing is multiplied by 2.4, making movement sluggish. At 18s the cycle resets.",
                PieceType.I =>
                    $"<color=#{titleColor}><b>BLYNDOOLIE ADRENALINE</b></color>\n" +
                    "<color=#FFB0B0><b>Modifiers</b></color>\nAdrenalineMonitor, SignalRelay\n\n" +
                    "<color=#FFD86B><b>Triggers</b></color>\nAdrenaline checks stack height for the whole mission. Signal Relay waits 25s, activates for 6s, then repeats.\n\n" +
                    "<color=#C8FF9A><b>Effect</b></color>\nIf the stack reaches about 13 visible rows high, gravity multiplier becomes 0.7, making pieces fall faster. Signal Relay randomly activates GuardPressure, TerritoryCells, GhostFlicker, or AdrenalineMonitor. Territory relay immediately seeds 3 enemy cells.",
                _ =>
                    $"<color=#{titleColor}><b>ENEMY ABILITY</b></color>\n" +
                    "Story chapters can turn this monster's traits into stage pressure.",
            };
        }

        private void PlayVoicePreview()
        {
            var active = Profiles[activeIndex];
            if (!voicePreviewSource)
            {
                return;
            }

            voicePreviewSource.Stop();
            if (audioController)
            {
                audioController.PlayMonsterPreview(active.PieceType);
            }
            else
            {
                voicePreviewSource.clip = GetOrCreateVoiceClip(active.PieceType);
                voicePreviewSource.Play();
            }
        }

        private void StopVoicePreview()
        {
            voicePreviewSource?.Stop();
            audioController?.StopMonsterPreview();
        }

        private AudioClip GetOrCreateVoiceClip(PieceType pieceType)
        {
            if (voicePreviewClips.TryGetValue(pieceType, out var clip) && clip)
            {
                return clip;
            }

            clip = BuildVoiceClip(pieceType);
            voicePreviewClips[pieceType] = clip;
            return clip;
        }

        private static AudioClip BuildVoiceClip(PieceType pieceType)
        {
            const int sampleRate = 44100;
            const float durationSeconds = 0.34f;
            var samples = Mathf.CeilToInt(sampleRate * durationSeconds);
            var buffer = new float[samples];
            var notes = GetVoicePattern(pieceType);

            foreach (var note in notes)
            {
                var startSample = Mathf.Clamp(Mathf.FloorToInt(note.delay * sampleRate), 0, samples - 1);
                var endSample = Mathf.Clamp(Mathf.CeilToInt((note.delay + note.duration) * sampleRate), startSample + 1, samples);
                var noteLength = Mathf.Max(1, endSample - startSample);
                for (var index = startSample; index < endSample; index += 1)
                {
                    var localTime = (index - startSample) / (float)sampleRate;
                    var t = (index - startSample) / (float)noteLength;
                    var env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
                    buffer[index] += SampleWave(note.waveform, note.frequency, localTime) * note.amplitude * env;
                }
            }

            for (var index = 0; index < buffer.Length; index += 1)
            {
                buffer[index] = Mathf.Clamp(buffer[index], -0.95f, 0.95f);
            }

            var clip = AudioClip.Create($"VoicePreview_{pieceType}", samples, 1, sampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private static (Waveform waveform, float frequency, float delay, float duration, float amplitude)[] GetVoicePattern(PieceType pieceType)
        {
            return pieceType switch
            {
                PieceType.I => new[]
                {
                    (Waveform.Sine, 260f, 0f, 0.08f, 0.28f),
                    (Waveform.Triangle, 340f, 0.06f, 0.08f, 0.24f),
                    (Waveform.Sine, 220f, 0.12f, 0.12f, 0.2f),
                },
                PieceType.O => new[]
                {
                    (Waveform.Triangle, 280f, 0f, 0.06f, 0.28f),
                    (Waveform.Triangle, 280f, 0.05f, 0.06f, 0.28f),
                },
                PieceType.T => new[]
                {
                    (Waveform.Saw, 210f, 0f, 0.12f, 0.22f),
                    (Waveform.Sine, 160f, 0.08f, 0.18f, 0.2f),
                },
                PieceType.S => new[]
                {
                    (Waveform.Square, 200f, 0f, 0.07f, 0.24f),
                    (Waveform.Square, 250f, 0.04f, 0.07f, 0.24f),
                    (Waveform.Saw, 180f, 0.1f, 0.1f, 0.18f),
                },
                PieceType.Z => new[]
                {
                    (Waveform.Triangle, 180f, 0f, 0.08f, 0.22f),
                    (Waveform.Triangle, 150f, 0.05f, 0.08f, 0.22f),
                },
                PieceType.J => new[]
                {
                    (Waveform.Sine, 300f, 0f, 0.05f, 0.22f),
                    (Waveform.Sine, 360f, 0.045f, 0.05f, 0.22f),
                    (Waveform.Triangle, 220f, 0.1f, 0.08f, 0.2f),
                },
                _ => new[]
                {
                    (Waveform.Saw, 230f, 0f, 0.06f, 0.22f),
                    (Waveform.Triangle, 190f, 0.06f, 0.08f, 0.2f),
                    (Waveform.Saw, 260f, 0.12f, 0.07f, 0.22f),
                },
            };
        }

        private static float SampleWave(Waveform waveform, float frequency, float time)
        {
            var phase = time * frequency;
            var cycle = phase - Mathf.Floor(phase);
            return waveform switch
            {
                Waveform.Sine => Mathf.Sin(Mathf.PI * 2f * phase),
                Waveform.Triangle => 1f - (4f * Mathf.Abs(cycle - 0.5f)),
                Waveform.Square => cycle < 0.5f ? 1f : -1f,
                Waveform.Saw => (2f * cycle) - 1f,
                _ => 0f,
            };
        }

        private enum Waveform
        {
            Sine,
            Triangle,
            Square,
            Saw,
        }

        private void OpenSettings()
        {
            if (settingsPanel)
            {
                settingsPanel.SetActive(true);
            }

            SetModeVariantPromptVisible(false);
            SetAbilityInfoButtonsVisible(false);
            SetHomeLeaderboardVisible(false);
            SetInfoVisible(MonstosInfoMode.None);
            RefreshSettingsText();

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
            SetAbilityInfoButtonsVisible(true);
            SetHomeLeaderboardVisible(true);
            if (startOgbmButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(startOgbmButton.gameObject);
            }
        }

        private void SetAbilityInfoButtonsVisible(bool visible)
        {
            if (monstosRulesButton)
            {
                monstosRulesButton.gameObject.SetActive(visible);
            }

            if (monstosFriendlyAbilityButton)
            {
                monstosFriendlyAbilityButton.gameObject.SetActive(visible);
            }

            if (monstosEnemyAbilityButton)
            {
                monstosEnemyAbilityButton.gameObject.SetActive(visible);
            }
        }

        private void SetHomeLeaderboardVisible(bool visible)
        {
            if (leaderboardStyleToggleButton)
            {
                leaderboardStyleToggleButton.gameObject.SetActive(visible);
            }

            SetTextRowsVisible(homeOgbmLeaderboardTexts, visible);
            SetTextRowsVisible(homeSprintLeaderboardTexts, visible);
        }

        private static void SetTextRowsVisible(Text[] rows, bool visible)
        {
            if (rows == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                if (row)
                {
                    row.gameObject.SetActive(visible);
                }
            }
        }

        private static void QuitGame()
        {
            Application.Quit();
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

        private void HandleMenuInput()
        {
            var submit = IsSubmitHeld();
            var cancel = IsCancelHeld();
            var navigateUp = IsNavigateUpHeld();
            var navigateDown = IsNavigateDownHeld();
            var cycleLeft = IsCycleLeftHeld();
            var cycleRight = IsCycleRightHeld();
            var voice = IsVoiceHeld();
            var lore = IsLoreHeld();

            if (modeVariantPromptRoot && modeVariantPromptRoot.activeSelf)
            {
                if (WasPressed(cancel, ref previousCancel))
                {
                    SetModeVariantPromptVisible(false);
                }
                else if (WasPressed(submit, ref previousSubmit))
                {
                    ActivateSelectedButton();
                }

                previousNavigateUp = navigateUp;
                previousNavigateDown = navigateDown;
                previousCycleLeft = cycleLeft;
                previousCycleRight = cycleRight;
                previousVoice = voice;
                previousLore = lore;
                return;
            }

            if (settingsPanel && settingsPanel.activeSelf)
            {
                if (HandleBindingCapture())
                {
                    previousNavigateUp = navigateUp;
                    previousNavigateDown = navigateDown;
                    previousCycleLeft = cycleLeft;
                    previousCycleRight = cycleRight;
                    previousSubmit = submit;
                    previousCancel = cancel;
                    previousVoice = voice;
                    previousLore = lore;
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

                previousCycleLeft = cycleLeft;
                previousCycleRight = cycleRight;
                previousVoice = voice;
                previousLore = lore;
                return;
            }

            if (WasPressed(cycleLeft, ref previousCycleLeft))
            {
                Cycle(-1);
            }

            if (WasPressed(cycleRight, ref previousCycleRight))
            {
                Cycle(1);
            }

            if (WasPressed(voice, ref previousVoice))
            {
                PlayVoicePreview();
            }

            if (WasPressed(lore, ref previousLore))
            {
                ToggleLore();
            }

            if (WasPressed(cancel, ref previousCancel))
            {
                if (loreOpen)
                {
                    SetInfoVisible(MonstosInfoMode.None);
                }
                else if (settingsPanel && settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
            }

            if (WasPressed(navigateUp, ref previousNavigateUp))
            {
                MoveMenuSelection(-1);
            }

            if (WasPressed(navigateDown, ref previousNavigateDown))
            {
                MoveMenuSelection(1);
            }

            if (WasPressed(submit, ref previousSubmit))
            {
                ActivateSelectedButton();
            }
        }

        private void MoveMenuSelection(int direction)
        {
            if (menuButtons.Count == 0 || !EventSystem.current)
            {
                return;
            }

            var current = EventSystem.current.currentSelectedGameObject;
            var currentIndex = menuButtons.FindIndex(button => button && button.gameObject == current);
            if (currentIndex < 0)
            {
                currentIndex = menuButtons.FindIndex(button => button == startOgbmButton);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }
            }

            var nextIndex = Mathf.Clamp(currentIndex + direction, 0, menuButtons.Count - 1);
            var nextButton = menuButtons[nextIndex];
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

        private static bool IsCycleLeftHeld() => MonStackaControls.IsMenuCycleLeftHeld();

        private static bool IsCycleRightHeld() => MonStackaControls.IsMenuCycleRightHeld();

        private static bool IsSubmitHeld() => MonStackaControls.IsMenuSubmitHeld();

        private static bool IsCancelHeld() => MonStackaControls.IsMenuCancelHeld();

        private static bool IsVoiceHeld() => MonStackaControls.IsVoiceHeld();

        private static bool IsLoreHeld() => MonStackaControls.IsLoreHeld();

        private static MonStackaMode? GetCommandLineLaunchMode()
        {
            var envMode = Environment.GetEnvironmentVariable("MONSTACKA_MODE");
            if (!string.IsNullOrWhiteSpace(envMode))
            {
                return ParseLaunchMode(envMode);
            }

            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index += 1)
            {
                if (!string.Equals(args[index], "-monstacka-mode", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length)
                {
                    return MonStackaMode.Ogbm;
                }

                return ParseLaunchMode(args[index + 1]);
            }

            return null;
        }

        private static MonStackaMode ParseLaunchMode(string value)
        {
            if (string.Equals(value, "ogbm", StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaMode.Ogbm;
            }

            if (string.Equals(value, "sprint", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "x4lines", StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaMode.Sprint40;
            }

            if (string.Equals(value, "training", StringComparison.OrdinalIgnoreCase))
            {
                return MonStackaMode.Training;
            }

            return MonStackaMode.Ogbm;
        }
    }
}
