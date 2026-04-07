import './styles.css';
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';
import {
  SETTINGS_DEFAULTS,
  DEFAULT_CONTROLS,
  DEFAULT_GAMEPAD_CONTROLS,
  CONTROL_LABELS,
  CONTROL_ORDER,
  DEFAULT_MODE,
  MAX_NICKNAME_LENGTH,
  MIN_NICKNAME_LENGTH,
  MAX_LEADERBOARD_ENTRIES,
  MODE_LABELS,
} from './constants';
import { AudioManager, type SoundCue } from './audio';
import {
  loadStorage,
  saveStorage,
  normalizeNickname,
  clearLeaderboards,
  clearSavedRun,
  getSavedRun,
  qualifiesScoreRecord,
  qualifiesSprintRecord,
  saveScoreRecord,
  saveSprintRecord,
  setSavedRun,
} from './storage';
import {
  captureSavedRun, createGameState, reset, restoreSavedRun, dropOnce, lockPiece, elapsed,
} from './engine/state';
import { getGravityMs } from './engine/gravity';
import {
  setupKeyboard,
  setupGamepad,
  createInputState,
  clearHorizontalRepeat,
  type BindingCaptureTarget,
} from './input/keyboard';
import { assignBinding, formatBindingLabel, keyboardToken, mouseToken } from './input/bindings';
import { getDomRefs, render, resetRenderCache } from './ui/render';
import {
  createHomeMenuState,
  cycleHomeMonstos,
  getActiveMonstos,
  getHomeMenuRefs,
  renderActiveHomeMonstosPreview,
  renderHomeMenu,
} from './ui/homeMenu';
import { applyRegionMap, GAME_REGIONS, HOME_REGIONS } from './ui/regionMap';
import { prepareMonsterSkin } from './monsterSkin';
import { computeArtboardScale, computeBoardFit } from './layout';
import { applyDitherOverlay } from './ui/dither';
import type { AppPhase, ControlAction, ControlBindingSource, GameMode, PieceType } from './types';

interface PendingRecord {
  mode: GameMode;
  summary: string;
  score?: number;
  timeMs?: number;
  lines: number;
  pieces: number;
}

type HomeControllerFocus =
  | 'arcade'
  | 'sprint'
  | 'training'
  | 'quit'
  | 'settings'
  | 'leaderboardArcade'
  | 'leaderboardSprint'
  | 'preview';

type GameControllerFocus = 'settings' | 'quit' | 'home';

type SettingsFieldKey =
  | 'das'
  | 'arr'
  | 'lockDelay'
  | 'trainingFeedback'
  | 'sfxEnabled'
  | 'sfxVolume'
  | 'musicEnabled'
  | 'musicVolume'
  | 'ditherEnabled'
  | 'cleanLabels';

type ControllerUiSurface =
  | 'none'
  | 'home'
  | 'paused'
  | 'settings-main'
  | 'settings-controls'
  | 'resume'
  | 'record';

interface UiGamepadSnapshot {
  up: boolean;
  down: boolean;
  left: boolean;
  right: boolean;
  a: boolean;
  b: boolean;
  x: boolean;
  y: boolean;
  lb: boolean;
  rb: boolean;
  lt: boolean;
  rt: boolean;
  view: boolean;
  menu: boolean;
  l3: boolean;
}

declare global {
    interface Window {
      monstackaDebug?: {
        forceArcadeTopOut(score?: number, lines?: number, elapsedMs?: number): void;
        forceSprintClear(timeMs?: number, pieces?: number): void;
        snapshot(): {
          appPhase: AppPhase;
          mode: GameMode;
          score: number;
          lines: number;
          pieces: number;
          activeType: string | null;
          activeX: number | null;
          activeY: number | null;
          hold: string;
        };
      };
    }
  }

async function init() {
  const storage = loadStorage();
  const settings = storage.settings;
  const state = createGameState(DEFAULT_MODE);
  const debugMode = new URLSearchParams(window.location.search).has('debug');
  state.trainingFeedback = settings.trainingFeedback;
  const input = createInputState();
  const refs = getDomRefs();
  const homeRefs = getHomeMenuRefs();
  const homeState = createHomeMenuState();
  const audio = new AudioManager();
  audio.boot(settings);
  applyDitherOverlay(refs.ditherOverlay, settings.ditherEnabled);

  function applyCleanLabels(enabled: boolean) {
    const arcadeBtn = document.getElementById('startArcadeButton') as HTMLButtonElement;
    const arcadeScoreBtn = homeRefs.leaderboardArcadeButton;
    if (enabled) {
      arcadeBtn.textContent = 'Standard';
      arcadeBtn.classList.add('clean-label-overlay');
      arcadeBtn.setAttribute('aria-label', 'Start Standard mode');
      arcadeScoreBtn.textContent = 'Standard';
      arcadeScoreBtn.classList.add('clean-label-overlay', 'clean-label-score');
      arcadeScoreBtn.setAttribute('aria-label', 'Show Standard leaderboard');
    } else {
      arcadeBtn.textContent = '';
      arcadeBtn.classList.remove('clean-label-overlay');
      arcadeBtn.setAttribute('aria-label', 'Start OG Bubba Mode');
      arcadeScoreBtn.textContent = '';
      arcadeScoreBtn.classList.remove('clean-label-overlay', 'clean-label-score');
      arcadeScoreBtn.setAttribute('aria-label', 'Show OGBM leaderboard');
    }
  }

  const homeScreen = document.getElementById('homeScreen')!;
  const gameShell = document.getElementById('gameShell')!;
  const homeArtboard = document.getElementById('homeArtboard')!;
  const gameArtboard = document.getElementById('gameArtboard')!;
  const gameBoardZone = document.getElementById('gameBoardZone')!;
  const recordModal = document.getElementById('recordModal')!;
  const recordSummary = document.getElementById('recordSummary')!;
  const recordTitle = recordModal.querySelector('h2')!;
  const resumeModal = document.getElementById('resumeModal')!;
  const resumeSummary = document.getElementById('resumeSummary')!;
  const resumeTitle = document.getElementById('resumeTitle')!;
  const continueSavedButton = document.getElementById('continueSavedButton') as HTMLButtonElement;
  const startFreshButton = document.getElementById('startFreshButton') as HTMLButtonElement;
  const cancelResumeButton = document.getElementById('cancelResumeButton') as HTMLButtonElement;
  const nicknameForm = document.getElementById('nicknameForm') as HTMLFormElement;
  const nicknameInput = document.getElementById('nicknameInput') as HTMLInputElement;
  const saveRecordButton = nicknameForm.querySelector('button[type="submit"]') as HTMLButtonElement;
  const skipRecordButton = document.getElementById('skipRecordButton') as HTMLButtonElement;
  const settingsModal = document.getElementById('settingsModal')!;
  const settingsMainView = document.getElementById('settingsMainView')!;
  const controlsView = document.getElementById('controlsView')!;
  const openControlsButton = document.getElementById('openControlsButton') as HTMLButtonElement;
  const controlsBackButton = document.getElementById('controlsBackButton') as HTMLButtonElement;
  const controlsDefaultsButton = document.getElementById('controlsDefaultsButton') as HTMLButtonElement;
  const controlsList = document.getElementById('controlsList')!;
  const controlsCaptureHint = document.getElementById('controlsCaptureHint')!;
  const openSettingsButtonHome = document.getElementById('openSettingsButtonHome') as HTMLButtonElement;
  const openSettingsButtonGame = document.getElementById('openSettingsButtonGame') as HTMLButtonElement;
  const closeSettingsButton = document.getElementById('closeSettingsButton') as HTMLButtonElement;
  const quitGameButtonHome = document.getElementById('quitGameButtonHome') as HTMLButtonElement;
  const quitGameButtonGame = document.getElementById('quitGameButtonGame') as HTMLButtonElement;
  const monstosPrevButton = document.getElementById('monstosPrevButton') as HTMLButtonElement;
  const monstosNextButton = document.getElementById('monstosNextButton') as HTMLButtonElement;
  const startArcadeButton = document.getElementById('startArcadeButton') as HTMLButtonElement;
  const startSprintButton = document.getElementById('startSprintButton') as HTMLButtonElement;
  const startTrainingButton = document.getElementById('startTrainingButton') as HTMLButtonElement;
  const homeButtonGame = document.getElementById('homeButtonGame') as HTMLButtonElement;
  const debugLabel = debugMode ? document.createElement('div') : null;

  if (debugMode) {
    document.body.classList.add('debug-mode');
    debugLabel!.id = 'debugLabel';
    document.body.appendChild(debugLabel!);
  }

  let appPhase: AppPhase = 'menu';
  let handledRunKey = '';
  let pendingRecord: PendingRecord | null = null;
  let pendingResumeMode: GameMode | null = null;
  let lastLockSoundAt = 0;
  let lastLineClearSoundAt = 0;
  let gameOverSounded = false;
  let lastActiveType: string = '';
  let lastCountdownMarker = -1;
  let monsterSkinReady = false;
  let lastMenuPreviewFrameAt = 0;
  let pausedAt = 0;
  let awaitingBindingTarget: BindingCaptureTarget | null = null;
  let gamepadCaptureReadyAt = 0;
  let homeControllerFocus: HomeControllerFocus = 'arcade';
  let pausedControllerFocus: GameControllerFocus = 'settings';
  let settingsFieldFocusIndex = 0;
  let settingsEditing = false;
  let controlsFocusIndex = 0;
  let controlsFocusSource: ControlBindingSource = 'gamepadControls';
  let resumeFocusIndex = 0;
  let recordFocusIndex = 0;
  let recordTextEditing = false;
  let recordNicknameCommitted = '';
  let recordNicknamePreview = 'A';
  let previousUiGamepad: UiGamepadSnapshot = {
    up: false,
    down: false,
    left: false,
    right: false,
    a: false,
    b: false,
    x: false,
    y: false,
    lb: false,
    rb: false,
    lt: false,
    rt: false,
    view: false,
    menu: false,
    l3: false,
  };

  function seedLeaderboardTestData() {
    const arcadeSeeds = [
      { nickname: 'SLM3R', score: 11850, lines: 29, timeMs: 120500, timestamp: 'test-arcade-mid-1' },
      { nickname: 'GNASH', score: 8350, lines: 20, timeMs: 99500, timestamp: 'test-arcade-mid-2' },
    ];
    const sprintSeeds = [
      { nickname: 'RUSH1', timeMs: 59050, lines: 40, pieces: 100, timestamp: 'test-sprint-mid-1' },
      { nickname: 'DASH2', timeMs: 68840, lines: 40, pieces: 117, timestamp: 'test-sprint-mid-2' },
    ];

    let changed = false;

    for (const entry of arcadeSeeds) {
      if (!storage.score.some((record) => record.timestamp === entry.timestamp)) {
        storage.score.push(entry);
        changed = true;
      }
    }

    if (changed) {
      storage.score.sort((a, b) => b.score - a.score || a.timestamp.localeCompare(b.timestamp));
      storage.score = storage.score.slice(0, MAX_LEADERBOARD_ENTRIES);
    }

    let sprintChanged = false;
    for (const entry of sprintSeeds) {
      if (!storage.sprint.some((record) => record.timestamp === entry.timestamp)) {
        storage.sprint.push(entry);
        sprintChanged = true;
      }
    }

    if (sprintChanged) {
      storage.sprint.sort((a, b) => a.timeMs - b.timeMs || a.timestamp.localeCompare(b.timestamp));
      storage.sprint = storage.sprint.slice(0, MAX_LEADERBOARD_ENTRIES);
    }

    if (changed || sprintChanged) {
      saveStorage(storage);
    }
  }

  seedLeaderboardTestData();

  function showHomeScreen() {
    gameShell.classList.add('hidden');
    homeScreen.classList.remove('hidden');
  }

  function showGameScreen() {
    gameShell.classList.remove('hidden');
    homeScreen.classList.add('hidden');
  }

  function renderCurrentView(now = performance.now()) {
    if (appPhase === 'menu') {
      renderHomeMenu(homeRefs, storage, homeState, now);
      homeRefs.monstosCenter.classList.toggle('preview-loading', !monsterSkinReady);
      if (!monsterSkinReady) {
        homeRefs.monstosCenter.textContent = 'Loading...';
      }
      syncControllerFocusVisuals();
      return;
    }

    render(refs, state, settings, storage, appPhase, now);
    syncControllerFocusVisuals();
  }

  function fitBoardToZone() {
    const fit = computeBoardFit(gameBoardZone.clientWidth, gameBoardZone.clientHeight);
    if (!fit) {
      return;
    }

    refs.boardWrap.style.width = `${fit.width.toFixed(2)}px`;
    refs.boardWrap.style.height = `${fit.height.toFixed(2)}px`;
  }

  function scheduleBoardFit() {
    window.requestAnimationFrame(() => {
      fitBoardToZone();
    });
  }

  function updateArtboardScale() {
    const scale = computeArtboardScale(window.innerWidth, window.innerHeight);
    document.documentElement.style.setProperty('--artboard-scale', scale.toFixed(6));
  }

  function applyFixedArtboardLayout() {
    // The internal UI stays in fixed 1920x1080 artboard pixels.
    // Window resize must not mutate these coordinates.
    applyRegionMap(homeArtboard, HOME_REGIONS);
    applyRegionMap(gameArtboard, GAME_REGIONS);
  }

  function handleWindowResize() {
    // Resize is only allowed to change the global stage scale and board fit.
    updateArtboardScale();
    fitBoardToZone();
  }

  const settingsFieldOrder: SettingsFieldKey[] = [
    'das',
    'arr',
    'lockDelay',
    'trainingFeedback',
    'sfxEnabled',
    'sfxVolume',
    'musicEnabled',
    'musicVolume',
    'ditherEnabled',
    'cleanLabels',
  ];

  function getConnectedGamepad(): Gamepad | null {
    const pads = navigator.getGamepads?.() ?? [];
    for (const pad of pads) {
      if (pad?.connected) {
        return pad;
      }
    }
    return null;
  }

  function readUiGamepadSnapshot(gamepad: Gamepad | null): UiGamepadSnapshot {
    const axisX = gamepad?.axes[0] ?? 0;
    const axisY = gamepad?.axes[1] ?? 0;
    const button = (index: number) => Boolean(gamepad?.buttons[index]?.pressed || (gamepad?.buttons[index]?.value ?? 0) >= 0.5);
    return {
      up: button(12) || axisY <= -0.55,
      down: button(13) || axisY >= 0.55,
      left: button(14) || axisX <= -0.55,
      right: button(15) || axisX >= 0.55,
      a: button(0),
      b: button(1),
      x: button(2),
      y: button(3),
      lb: button(4),
      rb: button(5),
      lt: button(6),
      rt: button(7),
      view: button(8),
      menu: button(9),
      l3: button(10),
    };
  }

  function wasPressed(current: UiGamepadSnapshot, key: keyof UiGamepadSnapshot): boolean {
    return current[key] && !previousUiGamepad[key];
  }

  function populateSettingsInputs(source: typeof settings) {
    refs.dasInput.value = String(source.dasMs);
    refs.arrInput.value = String(source.arrMs);
    refs.lockDelayInput.value = String(source.lockDelayMs);
    refs.trainingFeedbackInput.value = source.trainingFeedback;
    refs.sfxEnabledInput.checked = source.sfxEnabled;
    refs.sfxVolumeInput.value = String(source.sfxVolume);
    refs.musicEnabledInput.checked = source.musicEnabled;
    refs.musicVolumeInput.value = String(source.musicVolume);
    refs.ditherEnabledInput.checked = source.ditherEnabled;
    refs.cleanLabelsInput.checked = source.cleanLabels;
  }

  function resetSettingsDraftToDefaults() {
    populateSettingsInputs(SETTINGS_DEFAULTS);
  }

  function getSettingsFieldElement(field: SettingsFieldKey): HTMLElement | null {
    switch (field) {
      case 'das':
        return refs.dasInput.closest('label');
      case 'arr':
        return refs.arrInput.closest('label');
      case 'lockDelay':
        return refs.lockDelayInput.closest('label');
      case 'trainingFeedback':
        return refs.trainingFeedbackInput.closest('label');
      case 'sfxEnabled':
        return refs.sfxEnabledInput.closest('label');
      case 'sfxVolume':
        return refs.sfxVolumeInput.closest('label');
      case 'musicEnabled':
        return refs.musicEnabledInput.closest('label');
      case 'musicVolume':
        return refs.musicVolumeInput.closest('label');
      case 'ditherEnabled':
        return refs.ditherEnabledInput.closest('label');
      case 'cleanLabels':
        return refs.cleanLabelsInput.closest('label');
      default:
        return null;
    }
  }

  function getSettingsFieldStep(field: SettingsFieldKey): number {
    switch (field) {
      case 'lockDelay':
        return 10;
      default:
        return 1;
    }
  }

  function adjustSettingsField(field: SettingsFieldKey, direction: 1 | -1) {
    const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));
    switch (field) {
      case 'das':
        refs.dasInput.value = String(clamp(Number(refs.dasInput.value || settings.dasMs) + direction * getSettingsFieldStep(field), 0, 300));
        break;
      case 'arr':
        refs.arrInput.value = String(clamp(Number(refs.arrInput.value || settings.arrMs) + direction * getSettingsFieldStep(field), 0, 120));
        break;
      case 'lockDelay':
        refs.lockDelayInput.value = String(clamp(Number(refs.lockDelayInput.value || settings.lockDelayMs) + direction * getSettingsFieldStep(field), 0, 1000));
        break;
      case 'trainingFeedback': {
        const options: Array<typeof settings.trainingFeedback> = ['show', 'redo', 'off'];
        const currentIndex = Math.max(0, options.indexOf(refs.trainingFeedbackInput.value as typeof settings.trainingFeedback));
        const nextIndex = (currentIndex + direction + options.length) % options.length;
        refs.trainingFeedbackInput.value = options[nextIndex];
        break;
      }
      case 'sfxVolume':
        refs.sfxVolumeInput.value = String(clamp(Number(refs.sfxVolumeInput.value || settings.sfxVolume) + direction, 0, 100));
        break;
      case 'musicVolume':
        refs.musicVolumeInput.value = String(clamp(Number(refs.musicVolumeInput.value || settings.musicVolume) + direction, 0, 100));
        break;
      default:
        break;
    }
  }

  function toggleSettingsField(field: SettingsFieldKey) {
    if (field === 'sfxEnabled') {
      refs.sfxEnabledInput.checked = !refs.sfxEnabledInput.checked;
    } else if (field === 'musicEnabled') {
      refs.musicEnabledInput.checked = !refs.musicEnabledInput.checked;
    } else if (field === 'ditherEnabled') {
      refs.ditherEnabledInput.checked = !refs.ditherEnabledInput.checked;
    } else if (field === 'cleanLabels') {
      refs.cleanLabelsInput.checked = !refs.cleanLabelsInput.checked;
    }
  }

  function getControllerUiSurface(): ControllerUiSurface {
    if (!recordModal.classList.contains('hidden')) {
      return 'record';
    }
    if (!resumeModal.classList.contains('hidden')) {
      return 'resume';
    }
    if (!settingsModal.classList.contains('hidden')) {
      return controlsView.classList.contains('hidden') ? 'settings-main' : 'settings-controls';
    }
    if (appPhase === 'menu') {
      return 'home';
    }
    if (appPhase === 'paused') {
      return 'paused';
    }
    return 'none';
  }

  function clearControllerFocusVisuals() {
    document.querySelectorAll('.controller-focused').forEach((node) => node.classList.remove('controller-focused'));
    document.querySelectorAll('.controller-editing').forEach((node) => node.classList.remove('controller-editing'));
  }

  function getHomeFocusElement(target: HomeControllerFocus): HTMLElement | null {
    switch (target) {
      case 'arcade':
        return startArcadeButton;
      case 'sprint':
        return startSprintButton;
      case 'training':
        return startTrainingButton;
      case 'quit':
        return quitGameButtonHome;
      case 'settings':
        return openSettingsButtonHome;
      case 'leaderboardArcade':
        return homeRefs.leaderboardArcadeButton;
      case 'leaderboardSprint':
        return homeRefs.leaderboardSprintButton;
      case 'preview':
        return homeRefs.monstosCenter.closest('.preview-slot') as HTMLElement | null;
      default:
        return null;
    }
  }

  function getPausedFocusElement(target: GameControllerFocus): HTMLElement {
    switch (target) {
      case 'settings':
        return openSettingsButtonGame;
      case 'quit':
        return quitGameButtonGame;
      case 'home':
      default:
        return homeButtonGame;
    }
  }

  function getControlsFocusElement(): HTMLButtonElement | null {
    const action = CONTROL_ORDER[controlsFocusIndex] ?? CONTROL_ORDER[0];
    return controlsList.querySelector(
      `.controls-binding[data-action="${action}"][data-source="${controlsFocusSource}"]`,
    ) as HTMLButtonElement | null;
  }

  function syncControllerFocusVisuals() {
    clearControllerFocusVisuals();
    if (!getConnectedGamepad()) {
      return;
    }

    const surface = getControllerUiSurface();
    if (surface === 'home') {
      getHomeFocusElement(homeControllerFocus)?.classList.add('controller-focused');
    } else if (surface === 'paused') {
      getPausedFocusElement(pausedControllerFocus).classList.add('controller-focused');
    } else if (surface === 'settings-main') {
      const fieldElement = getSettingsFieldElement(settingsFieldOrder[settingsFieldFocusIndex] ?? settingsFieldOrder[0]);
      fieldElement?.classList.add('controller-focused');
      if (settingsEditing) {
        fieldElement?.classList.add('controller-editing');
      }
    } else if (surface === 'settings-controls') {
      const controlsElement = getControlsFocusElement();
      controlsElement?.classList.add('controller-focused');
      if (awaitingBindingTarget) {
        controlsElement?.classList.add('controller-editing');
      }
    } else if (surface === 'resume') {
      [continueSavedButton, startFreshButton, cancelResumeButton][resumeFocusIndex]?.classList.add('controller-focused');
    } else if (surface === 'record') {
      const recordElement = getRecordFocusElement();
      recordElement.classList.add('controller-focused');
      if (recordTextEditing && recordElement === nicknameInput) {
        recordElement.classList.add('controller-editing');
      }
    }
  }

  function moveHomeControllerFocus(direction: 'up' | 'down' | 'left' | 'right') {
    if (direction === 'up') {
      if (homeControllerFocus === 'arcade') homeControllerFocus = 'quit';
      else if (homeControllerFocus === 'quit') homeControllerFocus = 'settings';
      else if (homeControllerFocus === 'sprint') homeControllerFocus = 'arcade';
      else if (homeControllerFocus === 'training') homeControllerFocus = 'sprint';
    } else if (direction === 'down') {
      if (homeControllerFocus === 'settings') homeControllerFocus = 'quit';
      else if (homeControllerFocus === 'quit') homeControllerFocus = 'arcade';
      else if (homeControllerFocus === 'arcade') homeControllerFocus = 'sprint';
      else if (homeControllerFocus === 'sprint') homeControllerFocus = 'training';
    } else if (direction === 'left') {
      if (homeControllerFocus === 'arcade' || homeControllerFocus === 'sprint' || homeControllerFocus === 'training') {
        homeControllerFocus = 'leaderboardArcade';
      } else if (homeControllerFocus === 'leaderboardArcade') {
        homeControllerFocus = 'leaderboardSprint';
      } else if (homeControllerFocus === 'leaderboardSprint') {
        homeControllerFocus = 'preview';
      }
    } else if (direction === 'right') {
      if (homeControllerFocus === 'preview') {
        homeControllerFocus = 'leaderboardSprint';
      } else if (homeControllerFocus === 'leaderboardSprint') {
        homeControllerFocus = 'leaderboardArcade';
      } else if (homeControllerFocus === 'leaderboardArcade') {
        homeControllerFocus = 'arcade';
      }
    }
  }

  function movePausedControllerFocus(direction: 'up' | 'down') {
    if (direction === 'up') {
      if (pausedControllerFocus === 'home') pausedControllerFocus = 'quit';
      else if (pausedControllerFocus === 'quit') pausedControllerFocus = 'settings';
    } else {
      if (pausedControllerFocus === 'settings') pausedControllerFocus = 'quit';
      else if (pausedControllerFocus === 'quit') pausedControllerFocus = 'home';
    }
  }

  function moveSettingsFocus(direction: 'up' | 'down') {
    if (direction === 'up') {
      settingsFieldFocusIndex = (settingsFieldFocusIndex - 1 + settingsFieldOrder.length) % settingsFieldOrder.length;
    } else {
      settingsFieldFocusIndex = (settingsFieldFocusIndex + 1) % settingsFieldOrder.length;
    }
  }

  function moveControlsFocus(direction: 'up' | 'down' | 'left' | 'right') {
    if (direction === 'up') {
      controlsFocusIndex = (controlsFocusIndex - 1 + CONTROL_ORDER.length) % CONTROL_ORDER.length;
    } else if (direction === 'down') {
      controlsFocusIndex = (controlsFocusIndex + 1) % CONTROL_ORDER.length;
    } else if (direction === 'left') {
      controlsFocusSource = 'controls';
    } else if (direction === 'right') {
      controlsFocusSource = 'gamepadControls';
    }
  }

  function moveResumeFocus(direction: 'left' | 'right') {
    if (direction === 'left') {
      resumeFocusIndex = (resumeFocusIndex + 2) % 3;
    } else {
      resumeFocusIndex = (resumeFocusIndex + 1) % 3;
    }
  }

  function updateRecordInputDisplay() {
    const displayValue = recordTextEditing
      ? `${recordNicknameCommitted}${recordNicknamePreview}`
      : recordNicknameCommitted;
    nicknameInput.value = displayValue;
    saveRecordButton.disabled = recordNicknameCommitted.length < MIN_NICKNAME_LENGTH;
    if (recordTextEditing) {
      nicknameInput.focus();
      const previewIndex = recordNicknameCommitted.length;
      nicknameInput.setSelectionRange(previewIndex, previewIndex + 1);
    }
  }

  function resetRecordControllerState() {
    recordFocusIndex = 0;
    recordTextEditing = false;
    recordNicknameCommitted = '';
    recordNicknamePreview = 'A';
    updateRecordInputDisplay();
  }

  function getRecordFocusElement(): HTMLElement {
    return [nicknameInput, saveRecordButton, skipRecordButton][recordFocusIndex] ?? nicknameInput;
  }

  function moveRecordFocus(direction: 'up' | 'down' | 'left' | 'right') {
    if (direction === 'up') {
      if (recordFocusIndex !== 0) {
        recordFocusIndex = 0;
      }
    } else if (direction === 'down') {
      if (recordFocusIndex === 0) {
        recordFocusIndex = 1;
      }
    } else if (direction === 'left') {
      if (recordFocusIndex === 2) {
        recordFocusIndex = 1;
      }
    } else if (direction === 'right') {
      if (recordFocusIndex === 1) {
        recordFocusIndex = 2;
      }
    }
  }

  function updateRecordPreviewFromDirection(direction: 'up' | 'down' | 'left' | 'right') {
    const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const digits = '1234567890';
    const letterIndex = letters.indexOf(recordNicknamePreview);
    const digitIndex = digits.indexOf(recordNicknamePreview);

    if (direction === 'up') {
      if (letterIndex >= 0) {
        recordNicknamePreview = letters[(letterIndex + 1) % letters.length];
      } else {
        recordNicknamePreview = 'A';
      }
    } else if (direction === 'down') {
      if (letterIndex >= 0) {
        recordNicknamePreview = letters[(letterIndex - 1 + letters.length) % letters.length];
      } else {
        recordNicknamePreview = 'Z';
      }
    } else if (direction === 'right') {
      if (digitIndex >= 0) {
        recordNicknamePreview = digits[(digitIndex + 1) % digits.length];
      } else {
        recordNicknamePreview = '1';
      }
    } else if (direction === 'left') {
      if (digitIndex >= 0) {
        recordNicknamePreview = digits[(digitIndex - 1 + digits.length) % digits.length];
      } else {
        recordNicknamePreview = '0';
      }
    }

    updateRecordInputDisplay();
  }

  function commitRecordPreview() {
    if (recordNicknameCommitted.length >= MAX_NICKNAME_LENGTH) {
      return;
    }

    recordNicknameCommitted = `${recordNicknameCommitted}${recordNicknamePreview}`;
    recordNicknamePreview = 'A';
    updateRecordInputDisplay();
  }

  function showSettingsMainView() {
    settingsMainView.classList.remove('hidden');
    controlsView.classList.add('hidden');
    settingsEditing = false;
    syncControllerFocusVisuals();
  }

  function showControlsView() {
    settingsMainView.classList.add('hidden');
    controlsView.classList.remove('hidden');
    controlsFocusSource = 'gamepadControls';
    controlsFocusIndex = Math.max(0, Math.min(controlsFocusIndex, CONTROL_ORDER.length - 1));
    syncControllerFocusVisuals();
  }

  function getBindingSet(source: ControlBindingSource) {
    return source === 'gamepadControls' ? settings.gamepadControls : settings.controls;
  }

  function setBindingSet(source: ControlBindingSource, bindings: typeof settings.controls) {
    if (source === 'gamepadControls') {
      settings.gamepadControls = bindings;
      return;
    }
    settings.controls = bindings;
  }

  function getBindingCaptureLabel(source: ControlBindingSource): string {
    return source === 'gamepadControls' ? 'controller input' : 'key or mouse button';
  }

  function stopBindingCapture(message = 'Click a keyboard, mouse, or controller binding. Changes save automatically.') {
    awaitingBindingTarget = null;
    gamepadCaptureReadyAt = 0;
    controlsCaptureHint.textContent = message;
  }

  function renderControlsList() {
    controlsList.innerHTML = '';

    const header = document.createElement('div');
    header.className = 'controls-row controls-row-header';

    const actionHeader = document.createElement('div');
    actionHeader.className = 'controls-heading';
    actionHeader.textContent = 'Action';

    const keyboardHeader = document.createElement('div');
    keyboardHeader.className = 'controls-heading';
    keyboardHeader.textContent = 'Keyboard / Mouse';

    const gamepadHeader = document.createElement('div');
    gamepadHeader.className = 'controls-heading';
    gamepadHeader.textContent = 'Controller';

    header.appendChild(actionHeader);
    header.appendChild(keyboardHeader);
    header.appendChild(gamepadHeader);
    controlsList.appendChild(header);

    for (const { action, label, keyboardBinding, gamepadBinding } of CONTROL_ORDER.map((controlAction) => ({
      action: controlAction,
      label: CONTROL_LABELS[controlAction],
      keyboardBinding: settings.controls[controlAction],
      gamepadBinding: settings.gamepadControls[controlAction],
    }))) {
      const row = document.createElement('div');
      row.className = 'controls-row';
      row.dataset.action = action;

      const labelEl = document.createElement('div');
      labelEl.className = 'controls-label';
      labelEl.textContent = label;

      const keyboardButton = document.createElement('button');
      keyboardButton.type = 'button';
      keyboardButton.className = 'secondary controls-binding';
      keyboardButton.dataset.action = action;
      keyboardButton.dataset.source = 'controls';
      if (awaitingBindingTarget?.action === action && awaitingBindingTarget.source === 'controls') {
        keyboardButton.classList.add('is-listening');
        keyboardButton.textContent = 'Press input...';
      } else {
        keyboardButton.textContent = formatBindingLabel(keyboardBinding);
      }
      keyboardButton.addEventListener('click', () => {
        awaitingBindingTarget = { action, source: 'controls' };
        controlsCaptureHint.textContent = `${label}: press a key or mouse button. Press Escape to cancel or Backspace to clear.`;
        renderControlsList();
      });

      const gamepadButton = document.createElement('button');
      gamepadButton.type = 'button';
      gamepadButton.className = 'secondary controls-binding';
      gamepadButton.dataset.action = action;
      gamepadButton.dataset.source = 'gamepadControls';
      if (awaitingBindingTarget?.action === action && awaitingBindingTarget.source === 'gamepadControls') {
        gamepadButton.classList.add('is-listening');
        gamepadButton.textContent = 'Press input...';
      } else {
        gamepadButton.textContent = formatBindingLabel(gamepadBinding);
      }
      gamepadButton.addEventListener('click', () => {
        awaitingBindingTarget = { action, source: 'gamepadControls' };
        gamepadCaptureReadyAt = performance.now() + 260;
        controlsCaptureHint.textContent = `${label}: press a controller button or stick direction. Press Escape to cancel or Backspace to clear.`;
        renderControlsList();
      });

      row.appendChild(labelEl);
      row.appendChild(keyboardButton);
      row.appendChild(gamepadButton);
      controlsList.appendChild(row);
    }

    syncControllerFocusVisuals();
  }

  function closeSettingsModal() {
    stopBindingCapture();
    populateSettingsInputs(settings);
    showSettingsMainView();
    settingsFieldFocusIndex = 0;
    settingsEditing = false;
    controlsFocusIndex = 0;
    controlsFocusSource = 'gamepadControls';
    settingsModal.classList.add('hidden');
    syncControllerFocusVisuals();
  }

  function openSettingsModal() {
    if (appPhase === 'playing') {
      pauseRun();
    }
    stopBindingCapture();
    populateSettingsInputs(settings);
    showSettingsMainView();
    settingsFieldFocusIndex = 0;
    settingsEditing = false;
    controlsFocusIndex = 0;
    controlsFocusSource = 'gamepadControls';
    renderControlsList();
    settingsModal.classList.remove('hidden');
    syncControllerFocusVisuals();
  }

  function applyDraftSettings() {
    settings.dasMs = Math.max(0, Number(refs.dasInput.value || SETTINGS_DEFAULTS.dasMs));
    settings.arrMs = Math.max(0, Number(refs.arrInput.value || SETTINGS_DEFAULTS.arrMs));
    settings.lockDelayMs = Math.max(0, Number(refs.lockDelayInput.value || SETTINGS_DEFAULTS.lockDelayMs));
    settings.trainingFeedback = refs.trainingFeedbackInput.value as typeof settings.trainingFeedback;
    settings.sfxEnabled = refs.sfxEnabledInput.checked;
    settings.sfxVolume = Math.max(0, Math.min(100, Number(refs.sfxVolumeInput.value || SETTINGS_DEFAULTS.sfxVolume)));
    settings.musicEnabled = refs.musicEnabledInput.checked;
    settings.musicVolume = Math.max(0, Math.min(100, Number(refs.musicVolumeInput.value || SETTINGS_DEFAULTS.musicVolume)));
    settings.ditherEnabled = refs.ditherEnabledInput.checked;
    settings.cleanLabels = refs.cleanLabelsInput.checked;
    applyDitherOverlay(refs.ditherOverlay, settings.ditherEnabled);
    applyCleanLabels(settings.cleanLabels);
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
    populateSettingsInputs(settings);
    settingsEditing = false;
    renderCurrentView();
  }

  function applyControlBinding(source: ControlBindingSource, action: ControlAction, binding: string) {
    setBindingSet(source, assignBinding(getBindingSet(source), action, binding));
    saveStorage(storage);
    const sourceLabel = source === 'gamepadControls' ? 'Controller' : 'Keyboard / mouse';
    stopBindingCapture(`${CONTROL_LABELS[action]} set to ${formatBindingLabel(binding)} for ${sourceLabel}.`);
    renderControlsList();
    syncControllerFocusVisuals();
  }

  function clearControlBinding(source: ControlBindingSource, action: ControlAction) {
    setBindingSet(source, assignBinding(getBindingSet(source), action, ''));
    saveStorage(storage);
    const sourceLabel = source === 'gamepadControls' ? 'controller' : 'keyboard / mouse';
    stopBindingCapture(`${CONTROL_LABELS[action]} ${sourceLabel} binding cleared.`);
    renderControlsList();
    syncControllerFocusVisuals();
  }

  function handleBindingKeyCapture(event: KeyboardEvent) {
    if (!awaitingBindingTarget || controlsView.classList.contains('hidden')) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (event.code === 'Escape') {
      stopBindingCapture();
      renderControlsList();
      return;
    }
    if (event.code === 'Backspace' || event.code === 'Delete') {
      clearControlBinding(awaitingBindingTarget.source, awaitingBindingTarget.action);
      return;
    }

    if (awaitingBindingTarget.source !== 'controls') {
      return;
    }

    applyControlBinding('controls', awaitingBindingTarget.action, keyboardToken(event.code));
  }

  function handleBindingMouseCapture(event: MouseEvent) {
    if (!awaitingBindingTarget || controlsView.classList.contains('hidden')) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (awaitingBindingTarget.source !== 'controls') {
      return;
    }
    applyControlBinding('controls', awaitingBindingTarget.action, mouseToken(event.button));
  }

  function handleBindingContextMenu(event: MouseEvent) {
    if (!awaitingBindingTarget || controlsView.classList.contains('hidden')) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
  }

  function closeRecordModal() {
    pendingRecord = null;
    recordModal.classList.add('hidden');
    nicknameForm.reset();
    resetRecordControllerState();
    syncControllerFocusVisuals();
  }

  function closeResumeModal() {
    pendingResumeMode = null;
    resumeModal.classList.add('hidden');
    resumeFocusIndex = 0;
    syncControllerFocusVisuals();
  }

  function formatSavedRunSummary(mode: GameMode): string {
    const savedRun = getSavedRun(storage, mode);
    if (!savedRun) {
      return '';
    }

    const savedAt = new Date(savedRun.savedAt).toLocaleString();
    switch (mode) {
      case 'sprint40':
        return `Continue your ${MODE_LABELS[mode]} run from ${savedRun.state.lines} cleared lines. Saved ${savedAt}.`;
      case 'training':
        return `Continue your ${MODE_LABELS[mode]} run from ${savedRun.state.pieces} pieces with ${savedRun.state.trainingFaults} faults. Saved ${savedAt}.`;
      case 'arcade':
      default:
        return `Continue your ${MODE_LABELS[mode]} run from ${savedRun.state.score} points and ${savedRun.state.lines} cleared lines. Saved ${savedAt}.`;
    }
  }

  function openResumeModal(mode: GameMode) {
    pendingResumeMode = mode;
    resumeFocusIndex = 0;
    resumeTitle.textContent = `${MODE_LABELS[mode]} Save Found`;
    resumeSummary.textContent = formatSavedRunSummary(mode);
    resumeModal.classList.remove('hidden');
    syncControllerFocusVisuals();
  }

  function openRecordModal(record: PendingRecord) {
    pendingRecord = record;
    recordTitle.textContent = record.mode === 'sprint40' ? 'Sprint Entry' : 'Arcade Entry';
    recordSummary.textContent = record.summary;
    nicknameInput.maxLength = MAX_NICKNAME_LENGTH;
    resetRecordControllerState();
    recordModal.classList.remove('hidden');
    nicknameInput.focus();
    syncControllerFocusVisuals();
  }

  function doRecordCheck() {
    if (!state.gameOver || !state.startTime) return;
    if (state.mode === 'training') return;

    const runKey = [
      state.mode,
      state.completedTime,
      state.score,
      state.lines,
      state.pieces,
      state.sprintComplete ? 'clear' : 'end',
    ].join(':');

    if (runKey === handledRunKey) {
      return;
    }

    handledRunKey = runKey;

    if (state.mode === 'sprint40') {
      if (!state.sprintComplete) return;
      const timeMs = elapsed(state);
      if (!qualifiesSprintRecord(storage, timeMs)) return;
      openRecordModal({
        mode: 'sprint40',
        summary: `Top ${MAX_LEADERBOARD_ENTRIES} time! Enter a ${MIN_NICKNAME_LENGTH}-${MAX_NICKNAME_LENGTH} character nickname for your ${MODE_LABELS.sprint40} record: ${timeMs} ms.`,
        timeMs,
        lines: state.lines,
        pieces: state.pieces,
      });
      return;
    }

    if (!qualifiesScoreRecord(storage, state.score)) return;
    openRecordModal({
      mode: 'arcade',
      summary: `New high score! Enter a ${MIN_NICKNAME_LENGTH}-${MAX_NICKNAME_LENGTH} character nickname for your ${state.score}-point ${MODE_LABELS.arcade} run.`,
      score: state.score,
      timeMs: elapsed(state),
      lines: state.lines,
      pieces: state.pieces,
    });
  }

  function doReset(nextMode: GameMode = state.mode) {
    clearHorizontalRepeat(input);
    handledRunKey = '';
    closeRecordModal();
    closeResumeModal();
    reset(state, nextMode);
    lastLockSoundAt = 0;
    lastLineClearSoundAt = 0;
    gameOverSounded = false;
    lastCountdownMarker = -1;
  }

  function transitionTo(nextPhase: AppPhase, nextMode?: GameMode) {
    resetRenderCache();
    switch (nextPhase) {
      case 'menu':
        clearHorizontalRepeat(input);
        closeRecordModal();
        closeResumeModal();
        closeSettingsModal();
        audio.stopMonsterNeutral();
        doReset(state.mode);
        showHomeScreen();
        lastMenuPreviewFrameAt = 0;
        pausedAt = 0;
        lastActiveType = '';
        homeControllerFocus = 'arcade';
        break;
      case 'countdown':
        closeSettingsModal();
        closeRecordModal();
        closeResumeModal();
        showGameScreen();
        doReset(nextMode ?? state.mode);
        scheduleBoardFit();
        pausedAt = 0;
        break;
      case 'playing':
        state.startTime = state.countdownUntil;
        state.lastGravity = state.startTime;
        pausedAt = 0;
        lastActiveType = state.active?.type ?? '';
        if (lastActiveType) {
          audio.playMonsterNeutral(lastActiveType as PieceType, settings);
        }
        break;
      case 'paused':
        pausedControllerFocus = 'settings';
        break;
      case 'game-over':
      case 'sprint-clear':
        clearSavedRun(storage, state.mode);
        doRecordCheck();
        pausedAt = 0;
        break;
      default:
        break;
    }

    appPhase = nextPhase;
  }

  function installDebugApi() {
    if (!debugMode) {
      return;
    }

    window.monstackaDebug = {
      forceArcadeTopOut(score = 13337, lines = 22, elapsedMs = 94500) {
        showGameScreen();
        closeResumeModal();
        closeRecordModal();
        closeSettingsModal();
        state.mode = 'arcade';
        state.score = score;
        state.lines = lines;
        state.pieces = Math.max(state.pieces, lines * 3);
        state.startTime = performance.now() - elapsedMs;
        state.countdownUntil = 0;
        state.completedTime = performance.now();
        state.sprintComplete = false;
        state.gameOver = true;
        handledRunKey = '';
        transitionTo('game-over');
        renderCurrentView();
      },
      forceSprintClear(timeMs = 52890, pieces = 96) {
        showGameScreen();
        closeResumeModal();
        closeRecordModal();
        closeSettingsModal();
        state.mode = 'sprint40';
        state.lines = 40;
        state.pieces = pieces;
        state.score = 0;
        state.startTime = performance.now() - timeMs;
        state.countdownUntil = 0;
        state.completedTime = performance.now();
        state.sprintComplete = true;
        state.gameOver = true;
        handledRunKey = '';
        transitionTo('sprint-clear');
        renderCurrentView();
      },
        snapshot() {
          return {
            appPhase,
            mode: state.mode,
            score: state.score,
            lines: state.lines,
            pieces: state.pieces,
            activeType: state.active?.type ?? null,
            activeX: state.active?.x ?? null,
            activeY: state.active?.y ?? null,
            hold: state.hold,
          };
        },
      };
    }

  function saveCurrentRunIfResumable() {
    if (appPhase !== 'playing' && appPhase !== 'paused') {
      return;
    }

    const captureTime = appPhase === 'paused' && pausedAt ? pausedAt : performance.now();
    const savedPhase = appPhase === 'paused' ? 'paused' : appPhase;
    const savedRun = captureSavedRun(state, savedPhase, captureTime);
    setSavedRun(storage, savedRun);
  }

  function returnToMenu(preserveRun = false) {
    try {
      if (preserveRun) {
        saveCurrentRunIfResumable();
      }
      transitionTo('menu');
      renderCurrentView();
    } catch (error) {
      console.error('MonStacka menu recovery failed', error);
      try {
        clearHorizontalRepeat(input);
        closeRecordModal();
        closeResumeModal();
        closeSettingsModal();
        doReset(state.mode);
      } catch (recoveryError) {
        console.error('MonStacka hard reset failed', recoveryError);
      }

      appPhase = 'menu';
      showHomeScreen();

      try {
        renderCurrentView();
      } catch (renderError) {
        console.error('MonStacka menu fallback render failed', renderError);
      }
    }
  }

  function startMode(mode: GameMode) {
    audio.ensureReady(settings);
    clearSavedRun(storage, mode);
    transitionTo('countdown', mode);
    renderCurrentView();
  }

  function continueSavedMode(mode: GameMode) {
    const savedRun = getSavedRun(storage, mode);
    if (!savedRun) {
      startMode(mode);
      return;
    }

    audio.ensureReady(settings);
    closeResumeModal();
    closeRecordModal();
    closeSettingsModal();
    showGameScreen();
    const resumePhase = restoreSavedRun(state, savedRun, performance.now());
    lastLockSoundAt = 0;
    lastLineClearSoundAt = 0;
    gameOverSounded = false;
    lastCountdownMarker = -1;
    handledRunKey = '';
    appPhase = resumePhase;
    pausedAt = resumePhase === 'paused' ? performance.now() : 0;
    scheduleBoardFit();
    renderCurrentView();
  }

  function restartCurrentRun() {
    audio.ensureReady(settings);
    clearSavedRun(storage, state.mode);
    transitionTo('countdown', state.mode);
    renderCurrentView();
  }

  function pauseRun(now = performance.now()) {
    if (appPhase !== 'playing') {
      return;
    }

    pausedAt = now;
    state.completedTime = now;
    audio.stopMonsterNeutral();
    appPhase = 'paused';
    pausedControllerFocus = 'settings';
    renderCurrentView(now);
  }

  function resumeRun(now = performance.now()) {
    if (appPhase !== 'paused') {
      return;
    }

    const pausedDuration = pausedAt ? Math.max(0, now - pausedAt) : 0;
    if (state.startTime) {
      state.startTime += pausedDuration;
    }
    if (state.lastGravity) {
      state.lastGravity += pausedDuration;
    }
    if (state.lockDeadline) {
      state.lockDeadline += pausedDuration;
    }
    state.completedTime = 0;
    pausedAt = 0;
    if (state.active) {
      audio.playMonsterNeutral(state.active.type, settings);
    }
    appPhase = 'playing';
    renderCurrentView(now);
  }

  function requestModeStart(mode: GameMode) {
    const savedRun = getSavedRun(storage, mode);
    if (savedRun) {
      openResumeModal(mode);
      return;
    }

    startMode(mode);
  }

  function isGameplayInputBlocked() {
    return awaitingBindingTarget !== null
      || !settingsModal.classList.contains('hidden')
      || !recordModal.classList.contains('hidden')
      || !resumeModal.classList.contains('hidden');
  }

  function beginControlCapture(source: ControlBindingSource, action: ControlAction) {
    awaitingBindingTarget = { action, source };
    if (source === 'gamepadControls') {
      gamepadCaptureReadyAt = performance.now() + 260;
      controlsCaptureHint.textContent = `${CONTROL_LABELS[action]}: press a controller button or stick direction. Press B to cancel.`;
    } else {
      controlsCaptureHint.textContent = `${CONTROL_LABELS[action]}: press a key or mouse button. Press Escape to cancel or Backspace to clear.`;
    }
    renderControlsList();
  }

  function activateHomeControllerFocus() {
    switch (homeControllerFocus) {
      case 'arcade':
        requestModeStart('arcade');
        break;
      case 'sprint':
        requestModeStart('sprint40');
        break;
      case 'training':
        requestModeStart('training');
        break;
      case 'quit':
        void quitGame();
        break;
      case 'settings':
        openSettingsModal();
        break;
      case 'leaderboardArcade':
        homeState.leaderboardMode = 'arcade';
        renderCurrentView();
        break;
      case 'leaderboardSprint':
        homeState.leaderboardMode = 'sprint40';
        renderCurrentView();
        break;
      case 'preview':
      default:
        break;
    }
  }

  function activatePausedControllerFocus() {
    switch (pausedControllerFocus) {
      case 'settings':
        openSettingsModal();
        break;
      case 'quit':
        void quitGame();
        break;
      case 'home':
        returnToMenu(true);
        break;
      default:
        break;
    }
  }

  function activateSettingsField() {
    const field = settingsFieldOrder[settingsFieldFocusIndex] ?? settingsFieldOrder[0];
    if (field === 'sfxEnabled' || field === 'musicEnabled') {
      toggleSettingsField(field);
      syncControllerFocusVisuals();
      return;
    }
    settingsEditing = true;
    syncControllerFocusVisuals();
  }

  function activateControlsFocus() {
    const action = CONTROL_ORDER[controlsFocusIndex] ?? CONTROL_ORDER[0];
    beginControlCapture(controlsFocusSource, action);
  }

  function handleControllerUiInput(current: UiGamepadSnapshot) {
    const surface = getControllerUiSurface();

    if (surface === 'record') {
      if (recordTextEditing) {
        if (wasPressed(current, 'up')) {
          updateRecordPreviewFromDirection('up');
        } else if (wasPressed(current, 'down')) {
          updateRecordPreviewFromDirection('down');
        } else if (wasPressed(current, 'left')) {
          updateRecordPreviewFromDirection('left');
        } else if (wasPressed(current, 'right')) {
          updateRecordPreviewFromDirection('right');
        } else if (wasPressed(current, 'a')) {
          commitRecordPreview();
          syncControllerFocusVisuals();
        } else if (wasPressed(current, 'b')) {
          recordTextEditing = false;
          updateRecordInputDisplay();
          syncControllerFocusVisuals();
        } else if (wasPressed(current, 'view') && recordNicknameCommitted.length >= MIN_NICKNAME_LENGTH) {
          nicknameForm.requestSubmit();
        }
      } else if (wasPressed(current, 'up')) {
        moveRecordFocus('up');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'down')) {
        moveRecordFocus('down');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'left')) {
        moveRecordFocus('left');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'right')) {
        moveRecordFocus('right');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        if (recordFocusIndex === 0) {
          recordTextEditing = true;
          recordNicknamePreview = 'A';
          updateRecordInputDisplay();
          syncControllerFocusVisuals();
        } else if (recordFocusIndex === 1) {
          if (recordNicknameCommitted.length >= MIN_NICKNAME_LENGTH) {
            nicknameForm.requestSubmit();
          }
        } else {
          skipRecordButton.click();
        }
      } else if (wasPressed(current, 'b')) {
        skipRecordButton.click();
      } else if (wasPressed(current, 'view') && recordNicknameCommitted.length >= MIN_NICKNAME_LENGTH) {
        nicknameForm.requestSubmit();
      }
      return;
    }

    if (surface === 'resume') {
      if (wasPressed(current, 'left')) {
        moveResumeFocus('left');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'right')) {
        moveResumeFocus('right');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        [continueSavedButton, startFreshButton, cancelResumeButton][resumeFocusIndex]?.click();
      } else if (wasPressed(current, 'b')) {
        closeResumeModal();
      }
      return;
    }

    if (surface === 'settings-main') {
      if (settingsEditing) {
        const field = settingsFieldOrder[settingsFieldFocusIndex] ?? settingsFieldOrder[0];
        if (wasPressed(current, 'up')) {
          adjustSettingsField(field, 1);
        } else if (wasPressed(current, 'down')) {
          adjustSettingsField(field, -1);
        } else if (wasPressed(current, 'b')) {
          settingsEditing = false;
          syncControllerFocusVisuals();
        }
        return;
      }

      if (wasPressed(current, 'up')) {
        moveSettingsFocus('up');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'down')) {
        moveSettingsFocus('down');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        activateSettingsField();
      } else if (wasPressed(current, 'b')) {
        closeSettingsModal();
      } else if (wasPressed(current, 'x')) {
        applyDraftSettings();
      } else if (wasPressed(current, 'y')) {
        resetSettingsDraftToDefaults();
      } else if (wasPressed(current, 'rb')) {
        stopBindingCapture();
        showControlsView();
        renderControlsList();
      }
      return;
    }

    if (surface === 'settings-controls') {
      if (awaitingBindingTarget) {
        if (wasPressed(current, 'b')) {
          stopBindingCapture('Binding update canceled.');
          renderControlsList();
        }
        return;
      }

      if (wasPressed(current, 'up')) {
        moveControlsFocus('up');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'down')) {
        moveControlsFocus('down');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'left')) {
        moveControlsFocus('left');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'right')) {
        moveControlsFocus('right');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        activateControlsFocus();
      } else if (wasPressed(current, 'b')) {
        stopBindingCapture();
        showSettingsMainView();
      } else if (wasPressed(current, 'y')) {
        settings.controls = { ...DEFAULT_CONTROLS };
        settings.gamepadControls = { ...DEFAULT_GAMEPAD_CONTROLS };
        saveStorage(storage);
        stopBindingCapture('Controls reset to default.');
        renderControlsList();
      }
      return;
    }

    if (surface === 'paused') {
      if (wasPressed(current, 'up')) {
        movePausedControllerFocus('up');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'down')) {
        movePausedControllerFocus('down');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        activatePausedControllerFocus();
      }
      return;
    }

    if (surface === 'home') {
      if (homeControllerFocus === 'preview') {
        if (wasPressed(current, 'lb')) {
          monstosPrevButton.click();
        } else if (wasPressed(current, 'rb')) {
          monstosNextButton.click();
        } else if (wasPressed(current, 'lt')) {
          homeRefs.monstosVoiceButton.click();
        } else if (wasPressed(current, 'rt')) {
          homeRefs.monstosLoreButton.click();
        }
      }

      if (wasPressed(current, 'up')) {
        moveHomeControllerFocus('up');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'down')) {
        moveHomeControllerFocus('down');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'left')) {
        moveHomeControllerFocus('left');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'right')) {
        moveHomeControllerFocus('right');
        syncControllerFocusVisuals();
      } else if (wasPressed(current, 'a')) {
        activateHomeControllerFocus();
      }
    }
  }

  function setupControllerUi() {
    let rafId = 0;

    const loop = () => {
      const gamepad = getConnectedGamepad();
      if (!gamepad) {
        if (document.querySelector('.controller-focused')) {
          clearControllerFocusVisuals();
        }
        previousUiGamepad = {
          up: false,
          down: false,
          left: false,
          right: false,
          a: false,
          b: false,
          x: false,
          y: false,
          lb: false,
          rb: false,
          lt: false,
          rt: false,
          view: false,
          menu: false,
          l3: false,
        };
        rafId = window.requestAnimationFrame(loop);
        return;
      }

      if (!document.querySelector('.controller-focused') && getControllerUiSurface() !== 'none') {
        syncControllerFocusVisuals();
      }

      const current = readUiGamepadSnapshot(gamepad);
      handleControllerUiInput(current);
      previousUiGamepad = current;
      rafId = window.requestAnimationFrame(loop);
    };

    rafId = window.requestAnimationFrame(loop);
    return () => window.cancelAnimationFrame(rafId);
  }

  applyFixedArtboardLayout();
  applyCleanLabels(settings.cleanLabels);
  handleWindowResize();
  window.addEventListener('resize', handleWindowResize);
  installDebugApi();
  await prepareMonsterSkin();
  monsterSkinReady = true;
  resetRenderCache();
  void audio.loadMonsterSounds();

  const gameplayInputContext = {
    state,
    input,
    settings,
    onRender: renderCurrentView,
    onReset: () => {
      clearSavedRun(storage, state.mode);
      transitionTo('countdown', state.mode);
      renderCurrentView();
    },
    getAppPhase: () => appPhase,
    onPause: pauseRun,
    onResume: resumeRun,
    onRestartPaused: restartCurrentRun,
    isInputBlocked: isGameplayInputBlocked,
    onSound: (cue: SoundCue) => {
      audio.play(cue, settings);
    },
  };

  setupKeyboard(gameplayInputContext);
  setupGamepad(
    gameplayInputContext,
    () => (
      awaitingBindingTarget?.source === 'gamepadControls'
      && performance.now() >= gamepadCaptureReadyAt
        ? awaitingBindingTarget
        : null
    ),
    (binding) => {
      if (!awaitingBindingTarget || awaitingBindingTarget.source !== 'gamepadControls') {
        return;
      }
      applyControlBinding('gamepadControls', awaitingBindingTarget.action, binding);
    },
  );
  setupControllerUi();

  document.getElementById('retryButton')!.addEventListener('click', () => {
    restartCurrentRun();
  });

  homeButtonGame.addEventListener('click', () => returnToMenu(true));

  openSettingsButtonHome.addEventListener('click', openSettingsModal);
  openSettingsButtonGame.addEventListener('click', openSettingsModal);
  closeSettingsButton.addEventListener('click', closeSettingsModal);
  openControlsButton.addEventListener('click', () => {
    stopBindingCapture();
    showControlsView();
    renderControlsList();
  });
  controlsBackButton.addEventListener('click', () => {
    stopBindingCapture();
    showSettingsMainView();
    syncControllerFocusVisuals();
  });
  controlsDefaultsButton.addEventListener('click', () => {
    settings.controls = { ...DEFAULT_CONTROLS };
    settings.gamepadControls = { ...DEFAULT_GAMEPAD_CONTROLS };
    saveStorage(storage);
    stopBindingCapture('Controls reset to default.');
    renderControlsList();
    syncControllerFocusVisuals();
  });
  settingsModal.addEventListener('click', (event) => {
    if (event.target === settingsModal) {
      closeSettingsModal();
    }
  });
  resumeModal.addEventListener('click', (event) => {
    if (event.target === resumeModal) {
      closeResumeModal();
    }
  });
  document.addEventListener('keydown', handleBindingKeyCapture, true);
  document.addEventListener('mousedown', handleBindingMouseCapture, true);
  document.addEventListener('contextmenu', handleBindingContextMenu, true);

  async function quitGame() {
    closeSettingsModal();
    closeRecordModal();
    closeResumeModal();

    try {
      await invoke('exit_app');
      return;
    } catch (error) {
      console.error('Native MonStacka exit command failed, trying window close.', error);
      try {
        const currentWindow = getCurrentWindow();
        await currentWindow.close();
        return;
      } catch (closeError) {
        console.error('Native MonStacka close failed, trying destroy.', closeError);
        try {
          await getCurrentWindow().destroy();
          return;
        } catch (destroyError) {
          console.error('Native MonStacka destroy failed, falling back to menu.', destroyError);
        }
      }
    }

    returnToMenu();
  }

  quitGameButtonHome.addEventListener('click', quitGame);
  quitGameButtonGame.addEventListener('click', quitGame);

  monstosPrevButton.addEventListener('click', () => {
    audio.stopMonsterNeutral();
    cycleHomeMonstos(homeState, -1);
    homeState.loreTypingPiece = null;
    homeState.loreBubbleOpenedAt = performance.now();
    renderCurrentView();
  });

  monstosNextButton.addEventListener('click', () => {
    audio.stopMonsterNeutral();
    cycleHomeMonstos(homeState, 1);
    homeState.loreTypingPiece = null;
    homeState.loreBubbleOpenedAt = performance.now();
    renderCurrentView();
  });

  homeRefs.monstosLoreButton.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    homeState.loreOpen = !homeState.loreOpen;
    homeState.loreBubbleOpenedAt = performance.now();
    homeState.loreTypingPiece = homeState.loreOpen ? null : getActiveMonstos(homeState).pieceType;
    homeState.loreVisibleText = '';
    renderCurrentView();
  });

  homeRefs.monstosVoiceButton.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    if (audio.isNeutralPlaying) {
      audio.stopMonsterNeutral();
    } else {
      const active = getActiveMonstos(homeState);
      audio.playMonstosPreview(active.pieceType, settings);
    }
  });

  homeRefs.leaderboardArcadeButton.addEventListener('click', () => {
    homeState.leaderboardMode = 'arcade';
    renderCurrentView();
  });

  homeRefs.leaderboardSprintButton.addEventListener('click', () => {
    homeState.leaderboardMode = 'sprint40';
    renderCurrentView();
  });

  startArcadeButton.addEventListener('click', () => requestModeStart('arcade'));
  startSprintButton.addEventListener('click', () => requestModeStart('sprint40'));
  startTrainingButton.addEventListener('click', () => requestModeStart('training'));

  continueSavedButton.addEventListener('click', () => {
    if (!pendingResumeMode) return;
    continueSavedMode(pendingResumeMode);
  });

  startFreshButton.addEventListener('click', () => {
    if (!pendingResumeMode) return;
    const mode = pendingResumeMode;
    closeResumeModal();
    startMode(mode);
  });

  cancelResumeButton.addEventListener('click', closeResumeModal);

  const settingsForm = document.getElementById('settingsForm')!;
  const resetSettingsButton = document.getElementById('resetSettingsButton')!;

  settingsForm.addEventListener('submit', (event) => {
    event.preventDefault();
    applyDraftSettings();
  });

  refs.sfxVolumeInput.addEventListener('input', () => {
    settings.sfxVolume = Math.max(0, Math.min(100, Number(refs.sfxVolumeInput.value || settings.sfxVolume)));
    audio.syncSettings(settings);
  });
  refs.musicVolumeInput.addEventListener('input', () => {
    settings.musicVolume = Math.max(0, Math.min(100, Number(refs.musicVolumeInput.value || settings.musicVolume)));
    audio.syncSettings(settings);
  });
  refs.sfxEnabledInput.addEventListener('change', () => {
    settings.sfxEnabled = refs.sfxEnabledInput.checked;
    audio.syncSettings(settings);
  });
  refs.musicEnabledInput.addEventListener('change', () => {
    settings.musicEnabled = refs.musicEnabledInput.checked;
    audio.syncSettings(settings);
  });

  resetSettingsButton.addEventListener('click', () => {
    resetSettingsDraftToDefaults();
    settingsEditing = false;
    syncControllerFocusVisuals();
  });

  const clearScoresButton = document.getElementById('clearScoresButton') as HTMLButtonElement;
  clearScoresButton.addEventListener('click', () => {
    clearLeaderboards(storage);
    renderCurrentView();
  });

  nicknameInput.addEventListener('input', () => {
    if (recordTextEditing) {
      return;
    }
    recordNicknameCommitted = normalizeNickname(nicknameInput.value);
    nicknameInput.value = recordNicknameCommitted;
    updateRecordInputDisplay();
  });

  nicknameForm.addEventListener('submit', (event) => {
    event.preventDefault();
    if (!pendingRecord) return;

    const nickname = normalizeNickname(recordNicknameCommitted || nicknameInput.value);
    if (nickname.length < MIN_NICKNAME_LENGTH) {
      nicknameInput.focus();
      return;
    }

    if (pendingRecord.mode === 'sprint40' && pendingRecord.timeMs) {
      saveSprintRecord(storage, nickname, pendingRecord.timeMs, pendingRecord.lines, pendingRecord.pieces);
    } else if (pendingRecord.mode === 'arcade' && typeof pendingRecord.score === 'number' && pendingRecord.timeMs) {
      saveScoreRecord(storage, nickname, pendingRecord.score, pendingRecord.lines, pendingRecord.timeMs);
    }

    closeRecordModal();
    renderCurrentView();
  });

  skipRecordButton.addEventListener('click', () => {
    closeRecordModal();
    renderCurrentView();
  });

  function tick(now: number) {
    if (debugLabel) {
      debugLabel.textContent = `phase: ${appPhase}${state.active ? ` | active: ${state.active.type}` : ''}`;
    }

    switch (appPhase) {
      case 'menu':
        if (monsterSkinReady && now - lastMenuPreviewFrameAt >= 33) {
          renderActiveHomeMonstosPreview(homeRefs, homeState, now);
          lastMenuPreviewFrameAt = now;
        }
        break;
      case 'countdown': {
        const countdownMarker = Math.ceil(Math.max(0, state.countdownUntil - now) / 1000);
        if (countdownMarker !== lastCountdownMarker) {
          if (countdownMarker > 0) {
            audio.play('countdown', settings);
          } else {
            audio.play('go', settings);
          }
          lastCountdownMarker = countdownMarker;
        }

        if (now >= state.countdownUntil) {
          transitionTo('playing');
        }

        renderCurrentView(now);
        break;
      }
      case 'playing': {
        const gravityMs = getGravityMs(state.mode, state.lines);
        while (now - state.lastGravity >= gravityMs) {
          dropOnce(state, settings.lockDelayMs);
          state.lastGravity += gravityMs;
        }
        if (state.lockDeadline && now >= state.lockDeadline) {
          lockPiece(state);
        }

        if (state.lastLockAt > lastLockSoundAt) {
          audio.playMonsterImpact((lastActiveType || 'I') as PieceType, settings);
          lastLockSoundAt = state.lastLockAt;
        }

        if (state.lastLineClearAt > lastLineClearSoundAt) {
          audio.play('lineClear', settings);
          lastLineClearSoundAt = state.lastLineClearAt;
        }

        // Detect new piece spawn (active type changed after lock or first piece)
        const currentActiveType = state.active?.type ?? '';
        if (currentActiveType && currentActiveType !== lastActiveType) {
          lastActiveType = currentActiveType;
          audio.playMonsterNeutral(currentActiveType as PieceType, settings);
        }

        if (state.gameOver) {
          audio.stopMonsterNeutral();
          transitionTo(state.sprintComplete ? 'sprint-clear' : 'game-over');
        }

        renderCurrentView(now);
        break;
      }
      case 'paused':
        renderCurrentView(now);
        break;
      case 'game-over':
      case 'sprint-clear':
        if (appPhase === 'game-over' && !state.sprintComplete && !gameOverSounded) {
          audio.play('topOut', settings);
          gameOverSounded = true;
        }
        renderCurrentView(now);
        break;
      default:
        break;
    }

    requestAnimationFrame(tick);
  }

  renderCurrentView();
  requestAnimationFrame(tick);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
