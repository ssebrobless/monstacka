import './styles.css';
import { SETTINGS_DEFAULTS, DEFAULT_MODE, MAX_NICKNAME_LENGTH, MODE_LABELS } from './constants';
import { AudioManager } from './audio';
import { loadStorage, saveStorage, normalizeNickname, qualifiesScoreRecord, qualifiesSprintRecord, saveScoreRecord, saveSprintRecord } from './storage';
import { createGameState, reset, dropOnce, lockPiece, elapsed } from './engine/state';
import { getGravityMs } from './engine/gravity';
import { setupKeyboard, createInputState, clearHorizontalRepeat } from './input/keyboard';
import { getDomRefs, render } from './ui/render';
import { prepareMonsterSkin } from './monsterSkin';
import type { GameMode } from './types';

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
  state.trainingFeedback = settings.trainingFeedback;
  const input = createInputState();
  const refs = getDomRefs();
  const audio = new AudioManager();
  const modeSelect = document.getElementById('modeSelect') as HTMLSelectElement;
  const recordModal = document.getElementById('recordModal')!;
  const recordSummary = document.getElementById('recordSummary')!;
  const nicknameForm = document.getElementById('nicknameForm') as HTMLFormElement;
  const nicknameInput = document.getElementById('nicknameInput') as HTMLInputElement;
  const skipRecordButton = document.getElementById('skipRecordButton') as HTMLButtonElement;
  let handledRunKey = '';
  let pendingRecord: PendingRecord | null = null;
  let lastLockSoundAt = 0;
  let lastLineClearSoundAt = 0;
  let gameOverSounded = false;
  let lastCountdownMarker = -1;

  function closeRecordModal() {
    pendingRecord = null;
    recordModal.classList.add('hidden');
    nicknameForm.reset();
  }

  function openRecordModal(record: PendingRecord) {
    pendingRecord = record;
    recordSummary.textContent = record.summary;
    nicknameInput.maxLength = MAX_NICKNAME_LENGTH;
    nicknameInput.value = '';
    recordModal.classList.remove('hidden');
    nicknameInput.focus();
  }

  function doRender() {
    render(refs, state, settings, storage);
  }

  void prepareMonsterSkin(doRender);

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

  setupKeyboard(state, input, settings, doRender, doReset, doRecordCheck, (cue) => {
    audio.play(cue, settings);
  });

  document.getElementById('retryButton')!.addEventListener('click', doReset);
  modeSelect.addEventListener('change', () => {
    doReset(modeSelect.value as GameMode);
    doRender();
  });

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
    doRender();
  });

  resetSettingsButton.addEventListener('click', () => {
    Object.assign(settings, SETTINGS_DEFAULTS);
    state.trainingFeedback = settings.trainingFeedback;
    saveStorage(storage);
    audio.syncSettings(settings);
    doRender();
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
    doRender();
  });

  skipRecordButton.addEventListener('click', () => {
    closeRecordModal();
    doRender();
  });

  function tick(now: number) {
    if (!state.startTime && !state.gameOver) {
      const countdownMarker = Math.ceil(Math.max(0, state.countdownUntil - now) / 1000);
      if (countdownMarker !== lastCountdownMarker) {
        if (countdownMarker > 0) {
          audio.play('countdown', settings);
        } else {
          audio.play('go', settings);
        }
        lastCountdownMarker = countdownMarker;
      }
    }

    if (!state.startTime && now >= state.countdownUntil) {
      state.startTime = state.countdownUntil;
      state.lastGravity = state.startTime;
    }

    if (state.startTime && !state.gameOver) {
      const gravityMs = getGravityMs(state.mode, state.lines);
      while (now - state.lastGravity >= gravityMs) {
        dropOnce(state, settings.lockDelayMs);
        state.lastGravity += gravityMs;
      }
      if (state.lockDeadline && now >= state.lockDeadline) {
        lockPiece(state);
        doRecordCheck();
      }
    }

    if (state.lastLockAt > lastLockSoundAt) {
      audio.play('lock', settings);
      lastLockSoundAt = state.lastLockAt;
    }

    if (state.lastLineClearAt > lastLineClearSoundAt) {
      audio.play('lineClear', settings);
      lastLineClearSoundAt = state.lastLineClearAt;
    }

    if (state.gameOver && !state.sprintComplete && !gameOverSounded) {
      audio.play('topOut', settings);
      gameOverSounded = true;
    }

    doRender();
    requestAnimationFrame(tick);
  }

  doRender();
  requestAnimationFrame(tick);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
