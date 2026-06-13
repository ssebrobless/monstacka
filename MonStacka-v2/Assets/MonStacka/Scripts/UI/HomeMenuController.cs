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
                monstosLoreText.resizeTextForBestFit = true;
                monstosLoreText.resizeTextMinSize = 16;
                monstosLoreText.resizeTextMaxSize = 26;
                monstosLoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
                monstosLoreText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            prevButton?.onClick.AddListener(() => Cycle(-1));
            nextButton?.onClick.AddListener(() => Cycle(1));
            monstosVoiceButton?.onClick.AddListener(PlayVoicePreview);
            monstosLoreButton?.onClick.AddListener(ToggleLore);
            startOgbmButton?.onClick.AddListener(() => StartMode(MonStackaMode.Ogbm));
            startSprintButton?.onClick.AddListener(() => StartMode(MonStackaMode.Sprint40));
            startTrainingButton?.onClick.AddListener(() => StartMode(MonStackaMode.Training));
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
            SetLoreVisible(false);
            RebuildPreview();
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
                monstosLoreText.text = center.Lore;
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
            loreOpen = !loreOpen;
            SetLoreVisible(loreOpen);
        }

        private void SetLoreVisible(bool visible)
        {
            if (monstosLorePanel)
            {
                monstosLorePanel.SetActive(visible);
            }
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

            loreOpen = false;
            SetLoreVisible(false);
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
            if (startOgbmButton && EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(startOgbmButton.gameObject);
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
                    loreOpen = false;
                    SetLoreVisible(false);
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
