import type { AppPhase, GameState, Settings, StorageData } from '../types';
import { HIDDEN_ROWS, COLS, TARGET_LINES, MODE_LABELS, MODE_DESCRIPTIONS, DEFINITIONS } from '../constants';
import { getCells, getGhostCells } from '../engine/pieces';
import { elapsed, formatTime } from '../engine/state';
import { populateMonsterCell, populateMonsterFigure } from './monsterDom';

export interface DomRefs {
  boardWrap: HTMLElement;
  board: HTMLElement;
  overlay: HTMLElement;
  faultToast: HTMLElement;
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
  populateMonsterFigure(container, pieceType, {
    rotation: 0,
    now: performance.now(),
    animate: false,
    cellClassName: 'preview-cell',
    filledClassName: 'filled monster-preview',
  });
}

export function render(
  refs: DomRefs,
  state: GameState,
  settings: Settings,
  storage: StorageData,
  appPhase: AppPhase,
  now: number,
): void {
  const isTraining = state.mode === 'training';
  const lookX = Math.max(-0.18, Math.min(0.18, Math.sin(now / 680) * 0.08 + (state.active ? (state.active.x - 4.5) / 18 : 0)));
  const lookY = Math.max(-0.12, Math.min(0.12, Math.cos(now / 920) * 0.04 + (state.active ? (state.active.y - 8) / 70 : 0.02)));

  const rows = state.board.slice(HIDDEN_ROWS).map((row) => [...row]);
  const skinRows = state.boardSkin.slice(HIDDEN_ROWS).map((row) => [...row]);

  if (state.active) {
    getGhostCells(state.board, state.active).forEach((cell) => {
      if (cell.y >= HIDDEN_ROWS && !rows[cell.y - HIDDEN_ROWS][cell.x]) {
        rows[cell.y - HIDDEN_ROWS][cell.x] = `ghost-${state.active!.type}`;
      }
    });

    getCells(state.active).forEach((cell, index) => {
      if (cell.y >= HIDDEN_ROWS) {
        rows[cell.y - HIDDEN_ROWS][cell.x] = state.active!.type;
        skinRows[cell.y - HIDDEN_ROWS][cell.x] = `${state.active!.type}:${state.active!.rotation}:${index}`;
      }
    });
  }

  const total = rows.length * COLS;
  if (refs.board.children.length !== total) {
    refs.board.innerHTML = '';
    refs.board.style.gridTemplateColumns = `repeat(${COLS}, 1fr)`;
    for (let i = 0; i < total; i += 1) {
      const cell = document.createElement('div');
      cell.className = 'cell';
      refs.board.appendChild(cell);
    }
  }

  rows.flat().forEach((value, index) => {
    const cell = refs.board.children[index] as HTMLElement;
    const rowIndex = Math.floor(index / COLS);
    const colIndex = index % COLS;
    const skinKey = skinRows[rowIndex][colIndex];
    const occupied = Boolean(skinKey);

    if (!value) {
      cell.className = 'cell';
      cell.replaceChildren();
      cell.style.removeProperty('--squish-scale-x');
      cell.style.removeProperty('--squish-scale-y');
      cell.style.removeProperty('--squish-shift-x');
      cell.style.removeProperty('--squish-shift-y');
      return;
    }

    if (value.startsWith('ghost-')) {
      cell.className = 'cell';
      cell.replaceChildren();
      cell.classList.add('ghost', `piece-${value.replace('ghost-', '').toLowerCase()}`);
      return;
    }

    const occupiedNeighbors = {
      left: colIndex > 0 && Boolean(skinRows[rowIndex][colIndex - 1]),
      right: colIndex < COLS - 1 && Boolean(skinRows[rowIndex][colIndex + 1]),
      up: rowIndex > 0 && Boolean(skinRows[rowIndex - 1][colIndex]),
      down: rowIndex < skinRows.length - 1 && Boolean(skinRows[rowIndex + 1][colIndex]),
    };

    if (occupied) {
      populateMonsterCell(cell, skinKey, occupiedNeighbors, {
        now,
        lookX,
        lookY,
        animate: true,
        allowSquish: true,
        baseClassName: 'cell',
      });
    } else {
      cell.className = 'cell';
      cell.replaceChildren();
      cell.classList.add(`piece-${value.toLowerCase()}`);
    }
  });

  refs.board.classList.toggle('lock-flash', now - state.lastLockAt < 120);
  refs.boardWrap.classList.toggle('line-clear-flash', now - state.lastLineClearAt < 180);
  refs.boardWrap.classList.toggle('training-fault-flash', isTraining && now - state.lastTrainingFaultAt < 320);

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

  renderPiecePreview(refs.hold, state.hold || null);

  refs.nextQueue.innerHTML = '';
  state.queue.slice(0, 5).forEach((piece) => {
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

  if (document.activeElement !== refs.dasInput) refs.dasInput.value = String(settings.dasMs);
  if (document.activeElement !== refs.arrInput) refs.arrInput.value = String(settings.arrMs);
  if (document.activeElement !== refs.lockDelayInput) refs.lockDelayInput.value = String(settings.lockDelayMs);
  refs.trainingFeedbackInput.value = settings.trainingFeedback;
  refs.mutedInput.checked = settings.muted;
  if (document.activeElement !== refs.sfxVolumeInput) refs.sfxVolumeInput.value = String(settings.sfxVolume);
  if (document.activeElement !== refs.musicVolumeInput) refs.musicVolumeInput.value = String(settings.musicVolume);

  if (isTraining && state.lastTrainingFaultMessage && now - state.lastTrainingFaultAt < 1500) {
    refs.faultToast.textContent = state.lastTrainingFaultMessage;
    refs.faultToast.classList.remove('hidden');
  } else {
    refs.faultToast.classList.add('hidden');
  }

  switch (appPhase) {
    case 'countdown': {
      const count = Math.ceil(Math.max(0, state.countdownUntil - now) / 1000);
      refs.overlay.textContent = count > 0 ? String(count) : 'GO';
      refs.overlay.classList.remove('hidden');
      refs.statusText.textContent = isTraining
        ? 'Training ready. Place each piece with the fewest movement and rotation inputs you can.'
        : `${MODE_LABELS[state.mode]} ready. ${MODE_DESCRIPTIONS[state.mode]}`;
      break;
    }
    case 'playing':
      refs.overlay.classList.add('hidden');
      if (isTraining) {
        const faultRate = state.pieces ? Math.round((state.trainingFaults / state.pieces) * 1000) / 10 : 0;
        refs.statusText.textContent = `Training active. ${state.trainingFaults} faults, ${faultRate}% fault rate, ${state.trainingPerfectStreak} perfect streak, feedback ${settings.trainingFeedback.toUpperCase()}.`;
      } else {
        refs.statusText.textContent = `${MODE_LABELS[state.mode]} active. DAS ${settings.dasMs}ms, ARR ${settings.arrMs}ms, lock delay ${settings.lockDelayMs}ms.`;
      }
      break;
    case 'sprint-clear':
      refs.overlay.textContent = '40 CLEAR';
      refs.overlay.classList.remove('hidden');
      refs.statusText.textContent = `Sprint complete in ${formatTime(elapsed(state))}.`;
      break;
    case 'game-over':
      refs.overlay.textContent = 'TOP OUT';
      refs.overlay.classList.remove('hidden');
      refs.statusText.textContent = state.mode === 'arcade'
        ? `Arcade run ended with ${state.score} points after ${state.lines} cleared lines.`
        : 'Run ended by top out before clearing 40 lines.';
      break;
    case 'menu':
    default:
      refs.overlay.classList.add('hidden');
      refs.statusText.textContent = 'Choose a mode from the home menu and drop into the run.';
      break;
  }
}
