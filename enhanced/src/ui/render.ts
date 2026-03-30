import type { GameState, Settings, StorageData } from '../types';
import { HIDDEN_ROWS, COLS, TARGET_LINES, MODE_LABELS, MODE_DESCRIPTIONS, DEFINITIONS } from '../constants';
import { getCells, getGhostCells } from '../engine/pieces';
import { elapsed, formatTime } from '../engine/state';

export interface DomRefs {
  boardWrap: HTMLElement;
  board: HTMLElement;
  overlay: HTMLElement;
  faultToast: HTMLElement;
  modeSelect: HTMLSelectElement;
  modeDescription: HTMLElement;
  timer: HTMLElement;
  scoreLabel: HTMLElement;
  score: HTMLElement;
  goalLabel: HTMLElement;
  goalValue: HTMLElement;
  linesLabel: HTMLElement;
  lines: HTMLElement;
  hold: HTMLElement;
  nextQueue: HTMLElement;
  leaderboardTitle: HTMLElement;
  leaderboard: HTMLElement;
  statusText: HTMLElement;
  dasInput: HTMLInputElement;
  arrInput: HTMLInputElement;
  lockDelayInput: HTMLInputElement;
  trainingFeedbackInput: HTMLSelectElement;
  mutedInput: HTMLInputElement;
  sfxVolumeInput: HTMLInputElement;
  musicVolumeInput: HTMLInputElement;
}

export function getDomRefs(): DomRefs {
  return {
    boardWrap: document.getElementById('boardWrap')!,
    board: document.getElementById('board')!,
    overlay: document.getElementById('overlay')!,
    faultToast: document.getElementById('faultToast')!,
    modeSelect: document.getElementById('modeSelect') as HTMLSelectElement,
    modeDescription: document.getElementById('modeDescription')!,
    timer: document.getElementById('timer')!,
    scoreLabel: document.getElementById('scoreLabel')!,
    score: document.getElementById('score')!,
    goalLabel: document.getElementById('goalLabel')!,
    goalValue: document.getElementById('goalValue')!,
    linesLabel: document.getElementById('linesLabel')!,
    lines: document.getElementById('lines')!,
    hold: document.getElementById('hold')!,
    nextQueue: document.getElementById('nextQueue')!,
    leaderboardTitle: document.getElementById('leaderboardTitle')!,
    leaderboard: document.getElementById('leaderboard')!,
    statusText: document.getElementById('statusText')!,
    dasInput: document.getElementById('dasInput') as HTMLInputElement,
    arrInput: document.getElementById('arrInput') as HTMLInputElement,
    lockDelayInput: document.getElementById('lockDelayInput') as HTMLInputElement,
    trainingFeedbackInput: document.getElementById('trainingFeedbackInput') as HTMLSelectElement,
    mutedInput: document.getElementById('mutedInput') as HTMLInputElement,
    sfxVolumeInput: document.getElementById('sfxVolumeInput') as HTMLInputElement,
    musicVolumeInput: document.getElementById('musicVolumeInput') as HTMLInputElement,
  };
}

function renderPiecePreview(container: HTMLElement, piece: string | null): void {
  container.innerHTML = '';
  container.className = 'piece-preview';

  if (!piece) {
    container.classList.add('empty');
    container.textContent = '-';
    return;
  }

  const pieceType = piece as keyof typeof DEFINITIONS;
  container.classList.add(`piece-${pieceType.toLowerCase()}`);

  for (let row = 0; row < 4; row += 1) {
    for (let col = 0; col < 4; col += 1) {
      const cell = document.createElement('div');
      cell.className = 'preview-cell';
      if (DEFINITIONS[pieceType][0].some(({ x, y }) => x === col && y === row)) {
        cell.classList.add('filled', `piece-${pieceType.toLowerCase()}`);
      }
      container.appendChild(cell);
    }
  }
}

export function render(
  refs: DomRefs,
  state: GameState,
  settings: Settings,
  storage: StorageData,
): void {
  const isTraining = state.mode === 'training';

  // Build visible rows with ghost + active piece overlay
  const rows = state.board.slice(HIDDEN_ROWS).map(row => [...row]);

  if (state.active) {
    getGhostCells(state.board, state.active).forEach(cell => {
      if (cell.y >= HIDDEN_ROWS && !rows[cell.y - HIDDEN_ROWS][cell.x]) {
        rows[cell.y - HIDDEN_ROWS][cell.x] = `ghost-${state.active!.type}`;
      }
    });
    getCells(state.active).forEach(cell => {
      if (cell.y >= HIDDEN_ROWS) {
        rows[cell.y - HIDDEN_ROWS][cell.x] = state.active!.type;
      }
    });
  }

  // Board cells
  const total = rows.length * COLS;
  if (refs.board.children.length !== total) {
    refs.board.innerHTML = '';
    refs.board.style.gridTemplateColumns = `repeat(${COLS}, 1fr)`;
    for (let i = 0; i < total; i++) {
      const cell = document.createElement('div');
      cell.className = 'cell';
      refs.board.appendChild(cell);
    }
  }
  rows.flat().forEach((value, index) => {
    const cell = refs.board.children[index] as HTMLElement;
    cell.className = 'cell';
    if (!value) return;
    if (value.startsWith('ghost-')) {
      cell.classList.add('ghost', `piece-${value.replace('ghost-', '').toLowerCase()}`);
      return;
    }
    cell.classList.add(`piece-${value.toLowerCase()}`);
  });

  refs.board.classList.toggle('lock-flash', performance.now() - state.lastLockAt < 120);
  refs.boardWrap.classList.toggle('line-clear-flash', performance.now() - state.lastLineClearAt < 180);
  refs.boardWrap.classList.toggle('training-fault-flash', isTraining && performance.now() - state.lastTrainingFaultAt < 320);

  // Stats
  refs.modeSelect.value = state.mode;
  refs.modeDescription.textContent = MODE_DESCRIPTIONS[state.mode];
  refs.timer.textContent = formatTime(elapsed(state));
  refs.scoreLabel.textContent = isTraining ? 'Faults' : 'Score';
  refs.score.textContent = isTraining ? String(state.trainingFaults) : String(state.score);
  refs.goalLabel.textContent = isTraining ? 'Streak' : state.mode === 'sprint40' ? 'Remain' : 'Goal';
  refs.goalValue.textContent = isTraining
    ? String(state.trainingPerfectStreak)
    : state.mode === 'sprint40'
      ? String(Math.max(0, TARGET_LINES - state.lines))
      : 'ENDLESS';
  refs.linesLabel.textContent = isTraining ? 'Pieces' : 'Lines';
  refs.lines.textContent = isTraining ? String(state.pieces) : String(state.lines);

  // Hold
  renderPiecePreview(refs.hold, state.hold || null);

  // Next queue
  refs.nextQueue.innerHTML = '';
  state.queue.slice(0, 5).forEach(piece => {
    const item = document.createElement('li');
    item.className = 'queue-item';
    const preview = document.createElement('div');
    renderPiecePreview(preview, piece);
    const label = document.createElement('span');
    label.className = 'queue-label';
    label.textContent = piece;
    item.appendChild(preview);
    item.appendChild(label);
    refs.nextQueue.appendChild(item);
  });

  // Leaderboard
  refs.leaderboardTitle.textContent = isTraining
    ? 'Training Notes'
    : state.mode === 'sprint40'
      ? 'Best 40L Times'
      : 'Top 10 Scores';
  refs.leaderboard.innerHTML = '';
  const entries = state.mode === 'sprint40' ? storage.sprint : storage.score;
  if (isTraining) {
    const item = document.createElement('li');
    const faultRate = state.pieces ? Math.round((state.trainingFaults / state.pieces) * 1000) / 10 : 0;
    item.textContent = `No leaderboard in Training mode. Fault rate ${faultRate}% with ${settings.trainingFeedback.toUpperCase()} feedback.`;
    refs.leaderboard.appendChild(item);
  } else if (!entries.length) {
    const item = document.createElement('li');
    item.textContent = state.mode === 'sprint40'
      ? 'No completed 40-line runs yet.'
      : 'No high scores yet. Survive a run to set the first record.';
    refs.leaderboard.appendChild(item);
  } else if (state.mode === 'sprint40') {
    storage.sprint.forEach((entry, index) => {
      const item = document.createElement('li');
      item.textContent = `${index + 1}. ${entry.nickname} - ${formatTime(entry.timeMs)} - ${entry.lines}L`;
      refs.leaderboard.appendChild(item);
    });
  } else {
    storage.score.forEach((entry, index) => {
      const item = document.createElement('li');
      item.textContent = `${index + 1}. ${entry.nickname} - ${entry.score} pts - ${entry.lines}L`;
      refs.leaderboard.appendChild(item);
    });
  }

  // Settings inputs
  if (document.activeElement !== refs.dasInput) refs.dasInput.value = String(settings.dasMs);
  if (document.activeElement !== refs.arrInput) refs.arrInput.value = String(settings.arrMs);
  if (document.activeElement !== refs.lockDelayInput) refs.lockDelayInput.value = String(settings.lockDelayMs);
  refs.trainingFeedbackInput.value = settings.trainingFeedback;
  refs.mutedInput.checked = settings.muted;
  if (document.activeElement !== refs.sfxVolumeInput) refs.sfxVolumeInput.value = String(settings.sfxVolume);
  if (document.activeElement !== refs.musicVolumeInput) refs.musicVolumeInput.value = String(settings.musicVolume);

  // Overlay & status
  if (isTraining && state.lastTrainingFaultMessage && performance.now() - state.lastTrainingFaultAt < 1500) {
    refs.faultToast.textContent = state.lastTrainingFaultMessage;
    refs.faultToast.classList.remove('hidden');
  } else {
    refs.faultToast.classList.add('hidden');
  }

  if (!state.startTime && !state.gameOver) {
    const count = Math.ceil(Math.max(0, state.countdownUntil - performance.now()) / 1000);
    refs.overlay.textContent = count > 0 ? String(count) : 'GO';
    refs.overlay.classList.remove('hidden');
    refs.statusText.textContent = isTraining
      ? 'Training ready. Place each piece with the fewest movement and rotation inputs you can.'
      : `${MODE_LABELS[state.mode]} ready. ${MODE_DESCRIPTIONS[state.mode]}`;
  } else if (state.sprintComplete) {
    refs.overlay.textContent = '40 CLEAR';
    refs.overlay.classList.remove('hidden');
    refs.statusText.textContent = `Sprint complete in ${formatTime(elapsed(state))}.`;
  } else if (state.gameOver) {
    refs.overlay.textContent = 'TOP OUT';
    refs.overlay.classList.remove('hidden');
    refs.statusText.textContent = state.mode === 'arcade'
      ? `Arcade run ended with ${state.score} points after ${state.lines} cleared lines.`
      : 'Run ended by top out before clearing 40 lines.';
  } else {
    refs.overlay.classList.add('hidden');
    if (isTraining) {
      const faultRate = state.pieces ? Math.round((state.trainingFaults / state.pieces) * 1000) / 10 : 0;
      refs.statusText.textContent = `Training active. ${state.trainingFaults} faults, ${faultRate}% fault rate, ${state.trainingPerfectStreak} perfect streak, feedback ${settings.trainingFeedback.toUpperCase()}.`;
    } else {
      refs.statusText.textContent = `${MODE_LABELS[state.mode]} active. DAS ${settings.dasMs}ms, ARR ${settings.arrMs}ms, lock delay ${settings.lockDelayMs}ms.`;
    }
  }
}
