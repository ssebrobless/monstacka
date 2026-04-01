import './styles.css';
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';
import {
  SETTINGS_DEFAULTS,
  DEFAULT_CONTROLS,
  CONTROL_LABELS,
  CONTROL_ORDER,
  DEFAULT_MODE,
  MAX_NICKNAME_LENGTH,
  MAX_LEADERBOARD_ENTRIES,
  MODE_LABELS,
} from './constants';
import { AudioManager } from './audio';
import {
  loadStorage,
  saveStorage,
  normalizeNickname,
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
import { setupKeyboard, createInputState, clearHorizontalRepeat } from './input/keyboard';
import { assignBinding, formatBindingLabel, keyboardToken, mouseToken } from './input/bindings';
import { getDomRefs, render } from './ui/render';
import {
  createHomeMenuState,
  cycleHomeMonstos,
  getActiveMonstos,
  getHomeMenuRefs,
  renderHomeMenu,
} from './ui/homeMenu';
import { applyRegionMap, GAME_REGIONS, HOME_REGIONS } from './ui/regionMap';
import { prepareMonsterSkin } from './monsterSkin';
import { computeArtboardScale, computeBoardFit } from './layout';
import type { AppPhase, ControlAction, GameMode } from './types';

interface PendingRecord {
  mode: GameMode;
  summary: string;
  score?: number;
  timeMs?: number;
  lines: number;
  pieces: number;
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

function init() {
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
  let lastCountdownMarker = -1;
  let monsterSkinReady = false;
  let lastMenuPreviewFrameAt = 0;
  let pausedAt = 0;
  let awaitingControlAction: ControlAction | null = null;

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
      return;
    }

    render(refs, state, settings, storage, appPhase, now);
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

  function showSettingsMainView() {
    settingsMainView.classList.remove('hidden');
    controlsView.classList.add('hidden');
  }

  function showControlsView() {
    settingsMainView.classList.add('hidden');
    controlsView.classList.remove('hidden');
  }

  function stopBindingCapture(message = 'Click a binding, then press any key or mouse button. Changes save automatically.') {
    awaitingControlAction = null;
    controlsCaptureHint.textContent = message;
  }

  function renderControlsList() {
    controlsList.innerHTML = '';

    for (const { action, label, binding } of CONTROL_ORDER.map((controlAction) => ({
      action: controlAction,
      label: CONTROL_LABELS[controlAction],
      binding: settings.controls[controlAction],
    }))) {
      const row = document.createElement('div');
      row.className = 'controls-row';
      row.dataset.action = action;

      const labelEl = document.createElement('div');
      labelEl.className = 'controls-label';
      labelEl.textContent = label;

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'secondary controls-binding';
      button.dataset.action = action;
      if (awaitingControlAction === action) {
        button.classList.add('is-listening');
        button.textContent = 'Press input...';
      } else {
        button.textContent = formatBindingLabel(binding);
      }
      button.addEventListener('click', () => {
        awaitingControlAction = action;
        controlsCaptureHint.textContent = `${label}: press a key or mouse button. Press Escape to cancel or Backspace to clear.`;
        renderControlsList();
      });

      row.appendChild(labelEl);
      row.appendChild(button);
      controlsList.appendChild(row);
    }
  }

  function closeSettingsModal() {
    stopBindingCapture();
    showSettingsMainView();
    settingsModal.classList.add('hidden');
  }

  function openSettingsModal() {
    stopBindingCapture();
    showSettingsMainView();
    renderControlsList();
    settingsModal.classList.remove('hidden');
  }

  function applyControlBinding(action: ControlAction, binding: string) {
    settings.controls = assignBinding(settings.controls, action, binding);
    saveStorage(storage);
    stopBindingCapture(`${CONTROL_LABELS[action]} set to ${formatBindingLabel(binding)}.`);
    renderControlsList();
  }

  function clearControlBinding(action: ControlAction) {
    settings.controls = assignBinding(settings.controls, action, '');
    saveStorage(storage);
    stopBindingCapture(`${CONTROL_LABELS[action]} cleared.`);
    renderControlsList();
  }

  function handleBindingKeyCapture(event: KeyboardEvent) {
    if (!awaitingControlAction || controlsView.classList.contains('hidden')) {
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
      clearControlBinding(awaitingControlAction);
      return;
    }

    applyControlBinding(awaitingControlAction, keyboardToken(event.code));
  }

  function handleBindingMouseCapture(event: MouseEvent) {
    if (!awaitingControlAction || controlsView.classList.contains('hidden')) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    applyControlBinding(awaitingControlAction, mouseToken(event.button));
  }

  function handleBindingContextMenu(event: MouseEvent) {
    if (!awaitingControlAction || controlsView.classList.contains('hidden')) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
  }

  function closeRecordModal() {
    pendingRecord = null;
    recordModal.classList.add('hidden');
    nicknameForm.reset();
  }

  function closeResumeModal() {
    pendingResumeMode = null;
    resumeModal.classList.add('hidden');
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
    resumeTitle.textContent = `${MODE_LABELS[mode]} Save Found`;
    resumeSummary.textContent = formatSavedRunSummary(mode);
    resumeModal.classList.remove('hidden');
  }

  function openRecordModal(record: PendingRecord) {
    pendingRecord = record;
    recordTitle.textContent = record.mode === 'sprint40' ? 'Sprint Entry' : 'Arcade Entry';
    recordSummary.textContent = record.summary;
    nicknameInput.maxLength = MAX_NICKNAME_LENGTH;
    nicknameInput.value = '';
    recordModal.classList.remove('hidden');
    nicknameInput.focus();
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
        summary: `Top ${MAX_LEADERBOARD_ENTRIES} time! Enter a 5-character nickname for your ${MODE_LABELS.sprint40} record: ${timeMs} ms.`,
        timeMs,
        lines: state.lines,
        pieces: state.pieces,
      });
      return;
    }

    if (!qualifiesScoreRecord(storage, state.score)) return;
    openRecordModal({
      mode: 'arcade',
      summary: `New high score! Enter a 5-character nickname for your ${state.score}-point ${MODE_LABELS.arcade} run.`,
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
    switch (nextPhase) {
      case 'menu':
        clearHorizontalRepeat(input);
        closeRecordModal();
        closeResumeModal();
        closeSettingsModal();
        doReset(state.mode);
        showHomeScreen();
        lastMenuPreviewFrameAt = 0;
        pausedAt = 0;
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
        break;
      case 'paused':
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
    appPhase = 'paused';
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
    pausedAt = 0;
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
    return awaitingControlAction !== null
      || !settingsModal.classList.contains('hidden')
      || !recordModal.classList.contains('hidden')
      || !resumeModal.classList.contains('hidden');
  }

  applyFixedArtboardLayout();
  handleWindowResize();
  window.addEventListener('resize', handleWindowResize);
  installDebugApi();
  void prepareMonsterSkin(() => {
    monsterSkinReady = true;
    renderCurrentView();
  });

  setupKeyboard(
    state,
    input,
    settings,
    renderCurrentView,
    () => {
      clearSavedRun(storage, state.mode);
      transitionTo('countdown', state.mode);
      renderCurrentView();
    },
    () => appPhase,
    pauseRun,
    resumeRun,
    restartCurrentRun,
    isGameplayInputBlocked,
    (cue) => {
      audio.play(cue, settings);
    },
  );

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
  });
  controlsDefaultsButton.addEventListener('click', () => {
    settings.controls = { ...DEFAULT_CONTROLS };
    saveStorage(storage);
    stopBindingCapture('Controls reset to default.');
    renderControlsList();
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

  const quitGame = async () => {
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
  };

  quitGameButtonHome.addEventListener('click', quitGame);
  quitGameButtonGame.addEventListener('click', quitGame);

  monstosPrevButton.addEventListener('click', () => {
    cycleHomeMonstos(homeState, -1);
    homeState.loreTypingPiece = null;
    homeState.loreBubbleOpenedAt = performance.now();
    renderCurrentView();
  });

  monstosNextButton.addEventListener('click', () => {
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
    audio.play('previewBeep', settings);
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
    settings.dasMs = Math.max(0, Number(refs.dasInput.value || SETTINGS_DEFAULTS.dasMs));
    settings.arrMs = Math.max(0, Number(refs.arrInput.value || SETTINGS_DEFAULTS.arrMs));
    settings.lockDelayMs = Math.max(0, Number(refs.lockDelayInput.value || SETTINGS_DEFAULTS.lockDelayMs));
    settings.trainingFeedback = refs.trainingFeedbackInput.value as typeof settings.trainingFeedback;
    settings.sfxEnabled = refs.sfxEnabledInput.checked;
    settings.sfxVolume = Math.max(0, Math.min(100, Number(refs.sfxVolumeInput.value || SETTINGS_DEFAULTS.sfxVolume)));
    settings.musicEnabled = refs.musicEnabledInput.checked;
    settings.musicVolume = Math.max(0, Math.min(100, Number(refs.musicVolumeInput.value || SETTINGS_DEFAULTS.musicVolume)));
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
    closeSettingsModal();
    renderCurrentView();
  });

  resetSettingsButton.addEventListener('click', () => {
    Object.assign(settings, {
      ...SETTINGS_DEFAULTS,
      controls: { ...DEFAULT_CONTROLS },
    });
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
    renderControlsList();
    renderCurrentView();
  });

  nicknameForm.addEventListener('submit', (event) => {
    event.preventDefault();
    if (!pendingRecord) return;

    const nickname = normalizeNickname(nicknameInput.value);
    if (!nickname) {
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
          renderCurrentView(now);
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
          audio.play('lock', settings);
          lastLockSoundAt = state.lastLockAt;
        }

        if (state.lastLineClearAt > lastLineClearSoundAt) {
          audio.play('lineClear', settings);
          lastLineClearSoundAt = state.lastLineClearAt;
        }

        if (state.gameOver) {
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
