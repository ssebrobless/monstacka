import './styles.css';
import { SETTINGS_DEFAULTS, DEFAULT_MODE, MAX_NICKNAME_LENGTH, MODE_LABELS } from './constants';
import { AudioManager } from './audio';
import {
  loadStorage,
  saveStorage,
  normalizeNickname,
  qualifiesScoreRecord,
  qualifiesSprintRecord,
  saveScoreRecord,
  saveSprintRecord,
} from './storage';
import { createGameState, reset, dropOnce, lockPiece, elapsed } from './engine/state';
import { getGravityMs } from './engine/gravity';
import { setupKeyboard, createInputState, clearHorizontalRepeat } from './input/keyboard';
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
import type { AppPhase, GameMode } from './types';

interface PendingRecord {
  mode: GameMode;
  summary: string;
  score?: number;
  timeMs?: number;
  lines: number;
  pieces: number;
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

  const homeScreen = document.getElementById('homeScreen')!;
  const gameShell = document.getElementById('gameShell')!;
  const homeArtboard = document.getElementById('homeArtboard')!;
  const gameArtboard = document.getElementById('gameArtboard')!;
  const recordModal = document.getElementById('recordModal')!;
  const recordSummary = document.getElementById('recordSummary')!;
  const recordTitle = recordModal.querySelector('h2')!;
  const nicknameForm = document.getElementById('nicknameForm') as HTMLFormElement;
  const nicknameInput = document.getElementById('nicknameInput') as HTMLInputElement;
  const skipRecordButton = document.getElementById('skipRecordButton') as HTMLButtonElement;
  const settingsModal = document.getElementById('settingsModal')!;
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
  let lastLockSoundAt = 0;
  let lastLineClearSoundAt = 0;
  let gameOverSounded = false;
  let lastCountdownMarker = -1;
  let monsterSkinReady = false;

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

  function applyArtboardRegions() {
    applyRegionMap(homeArtboard, HOME_REGIONS);
    applyRegionMap(gameArtboard, GAME_REGIONS);
  }

  function closeSettingsModal() {
    settingsModal.classList.add('hidden');
  }

  function openSettingsModal() {
    settingsModal.classList.remove('hidden');
  }

  function closeRecordModal() {
    pendingRecord = null;
    recordModal.classList.add('hidden');
    nicknameForm.reset();
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
        summary: `Top 10 time! Enter a 5-character nickname for your ${MODE_LABELS.sprint40} record: ${timeMs} ms.`,
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
        closeSettingsModal();
        doReset(state.mode);
        gameShell.classList.add('hidden');
        homeScreen.classList.remove('hidden');
        break;
      case 'countdown':
        closeSettingsModal();
        closeRecordModal();
        gameShell.classList.remove('hidden');
        homeScreen.classList.add('hidden');
        doReset(nextMode ?? state.mode);
        break;
      case 'playing':
        state.startTime = state.countdownUntil;
        state.lastGravity = state.startTime;
        break;
      case 'game-over':
      case 'sprint-clear':
        doRecordCheck();
        break;
      default:
        break;
    }

    appPhase = nextPhase;
  }

  function returnToMenu() {
    transitionTo('menu');
    renderCurrentView();
  }

  function startMode(mode: GameMode) {
    audio.ensureReady(settings);
    transitionTo('countdown', mode);
    renderCurrentView();
  }

  applyArtboardRegions();
  window.addEventListener('resize', applyArtboardRegions);
  void prepareMonsterSkin(() => {
    monsterSkinReady = true;
    renderCurrentView();
  });

  setupKeyboard(state, input, settings, renderCurrentView, () => transitionTo('countdown'), (cue) => {
    audio.play(cue, settings);
  });

  document.getElementById('retryButton')!.addEventListener('click', () => {
    audio.ensureReady(settings);
    transitionTo('countdown');
    renderCurrentView();
  });

  homeButtonGame.addEventListener('click', returnToMenu);

  openSettingsButtonHome.addEventListener('click', openSettingsModal);
  openSettingsButtonGame.addEventListener('click', openSettingsModal);
  closeSettingsButton.addEventListener('click', closeSettingsModal);
  settingsModal.addEventListener('click', (event) => {
    if (event.target === settingsModal) {
      closeSettingsModal();
    }
  });

  const quitGame = () => {
    try {
      window.close();
    } catch {
      // Browser contexts can block window.close(); fall back to menu.
    }
    window.setTimeout(() => {
      if (!document.hidden) {
        returnToMenu();
      }
    }, 100);
  };

  quitGameButtonHome.addEventListener('click', quitGame);
  quitGameButtonGame.addEventListener('click', quitGame);

  monstosPrevButton.addEventListener('click', () => {
    cycleHomeMonstos(homeState, -1);
    renderCurrentView();
  });

  monstosNextButton.addEventListener('click', () => {
    cycleHomeMonstos(homeState, 1);
    renderCurrentView();
  });

  homeRefs.monstosLoreButton.addEventListener('click', () => {
    homeState.loreOpen = !homeState.loreOpen;
    renderCurrentView();
  });

  homeRefs.monstosVoiceButton.addEventListener('click', () => {
    const active = getActiveMonstos(homeState);
    audio.playMonstosPreview(active.pieceType, settings);
    renderCurrentView();
  });

  homeRefs.leaderboardArcadeButton.addEventListener('click', () => {
    homeState.leaderboardMode = 'arcade';
    renderCurrentView();
  });

  homeRefs.leaderboardSprintButton.addEventListener('click', () => {
    homeState.leaderboardMode = 'sprint40';
    renderCurrentView();
  });

  startArcadeButton.addEventListener('click', () => startMode('arcade'));
  startSprintButton.addEventListener('click', () => startMode('sprint40'));
  startTrainingButton.addEventListener('click', () => startMode('training'));

  const settingsForm = document.getElementById('settingsForm')!;
  const resetSettingsButton = document.getElementById('resetSettingsButton')!;

  settingsForm.addEventListener('submit', (event) => {
    event.preventDefault();
    settings.dasMs = Math.max(0, Number(refs.dasInput.value || SETTINGS_DEFAULTS.dasMs));
    settings.arrMs = Math.max(0, Number(refs.arrInput.value || SETTINGS_DEFAULTS.arrMs));
    settings.lockDelayMs = Math.max(0, Number(refs.lockDelayInput.value || SETTINGS_DEFAULTS.lockDelayMs));
    settings.trainingFeedback = refs.trainingFeedbackInput.value as typeof settings.trainingFeedback;
    settings.sfxVolume = Math.max(0, Math.min(100, Number(refs.sfxVolumeInput.value || SETTINGS_DEFAULTS.sfxVolume)));
    settings.musicVolume = Math.max(0, Math.min(100, Number(refs.musicVolumeInput.value || SETTINGS_DEFAULTS.musicVolume)));
    settings.muted = refs.mutedInput.checked;
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
    closeSettingsModal();
    renderCurrentView();
  });

  resetSettingsButton.addEventListener('click', () => {
    Object.assign(settings, SETTINGS_DEFAULTS);
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
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
