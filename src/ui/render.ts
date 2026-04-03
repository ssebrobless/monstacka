import type { AppPhase, GameState, Settings, StorageData } from '../types';
import { HIDDEN_ROWS, COLS, TARGET_LINES, MODE_LABELS, MODE_DESCRIPTIONS, DEFINITIONS } from '../constants';
import { getCells, getGhostCells } from '../engine/pieces';
import { elapsed, formatTime } from '../engine/state';
import { populateMonsterBoardFigure, populateMonsterCell, populateMonsterFigure } from './monsterDom';
import { getVisibleScoreRecords, getVisibleSprintRecords } from '../demoRecords';
import { isMonsterSkinReady } from '../monsterSkin';

export interface DomRefs {
  boardWrap: HTMLElement;
  board: HTMLElement;
  boardMonsterLayer: HTMLElement;
  overlay: HTMLElement;
  faultToast: HTMLElement;
  endStatePanel: HTMLElement;
  endStateEyebrow: HTMLElement;
  endStateTitle: HTMLElement;
  endStateSummary: HTMLElement;
  countdownPanel: HTMLElement;
  countdownValue: HTMLElement;
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
  retryButton: HTMLButtonElement;
  leaderboardTitle: HTMLElement;
  leaderboard: HTMLElement;
  statusText: HTMLElement;
  dasInput: HTMLInputElement;
  arrInput: HTMLInputElement;
  lockDelayInput: HTMLInputElement;
  trainingFeedbackInput: HTMLSelectElement;
  sfxEnabledInput: HTMLInputElement;
  sfxVolumeInput: HTMLInputElement;
  musicEnabledInput: HTMLInputElement;
  musicVolumeInput: HTMLInputElement;
  ditherEnabledInput: HTMLInputElement;
  ditherOverlay: HTMLDivElement;
}

// --- Render cache: avoid rebuilding DOM that hasn't changed ---
let cachedLockedKey = '';
let cachedActiveKey = '';
let cachedGap = -1;
let cachedCellWidth = 0;
let cachedCellHeight = 0;
let cachedHoldPiece: string | null | undefined = undefined;
let cachedNextQueue = '';
let cachedLeaderboardKey = '';
let activeOverlayNode: HTMLElement | null = null;

export function resetRenderCache(): void {
  cachedLockedKey = '';
  cachedActiveKey = '';
  cachedGap = -1;
  cachedCellWidth = 0;
  cachedCellHeight = 0;
  cachedHoldPiece = undefined;
  cachedNextQueue = '';
  cachedLeaderboardKey = '';
  activeOverlayNode = null;
}

export function getDomRefs(): DomRefs {
  return {
    boardWrap: document.getElementById('boardWrap')!,
    board: document.getElementById('board')!,
    boardMonsterLayer: document.getElementById('boardMonsterLayer')!,
    overlay: document.getElementById('overlay')!,
    faultToast: document.getElementById('faultToast')!,
    endStatePanel: document.getElementById('endStatePanel')!,
    endStateEyebrow: document.getElementById('endStateEyebrow')!,
    endStateTitle: document.getElementById('endStateTitle')!,
    endStateSummary: document.getElementById('endStateSummary')!,
    countdownPanel: document.getElementById('countdownPanel')!,
    countdownValue: document.getElementById('countdownValue')!,
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
    retryButton: document.getElementById('retryButton') as HTMLButtonElement,
    leaderboardTitle: document.getElementById('leaderboardTitle')!,
    leaderboard: document.getElementById('leaderboard')!,
    statusText: document.getElementById('statusText')!,
    dasInput: document.getElementById('dasInput') as HTMLInputElement,
    arrInput: document.getElementById('arrInput') as HTMLInputElement,
    lockDelayInput: document.getElementById('lockDelayInput') as HTMLInputElement,
    trainingFeedbackInput: document.getElementById('trainingFeedbackInput') as HTMLSelectElement,
    sfxEnabledInput: document.getElementById('sfxEnabledInput') as HTMLInputElement,
    sfxVolumeInput: document.getElementById('sfxVolumeInput') as HTMLInputElement,
    musicEnabledInput: document.getElementById('musicEnabledInput') as HTMLInputElement,
    musicVolumeInput: document.getElementById('musicVolumeInput') as HTMLInputElement,
    ditherEnabledInput: document.getElementById('ditherEnabledInput') as HTMLInputElement,
    ditherOverlay: document.getElementById('ditherOverlay') as HTMLDivElement,
  };
}

interface ParsedSkinKey {
  pieceType: keyof typeof DEFINITIONS;
  rotation: number;
  index: number;
}

interface BoardPieceGroup {
  pieceType: keyof typeof DEFINITIONS;
  rotation: number;
  anchorX: number;
  anchorY: number;
  indices: Set<number>;
  cells: Array<{ x: number; y: number }>;
  complete: boolean;
}

function parseSkinKey(skinKey: string): ParsedSkinKey | null {
  const [pieceType, rotationText, indexText] = skinKey.split(':');
  if (!pieceType || rotationText === undefined || indexText === undefined) {
    return null;
  }

  return {
    pieceType: pieceType as keyof typeof DEFINITIONS,
    rotation: Number(rotationText),
    index: Number(indexText),
  };
}

function buildLockedPieceGroups(skinRows: string[][]): BoardPieceGroup[] {
  const groups = new Map<string, BoardPieceGroup>();

  for (let rowIndex = 0; rowIndex < skinRows.length; rowIndex += 1) {
    for (let colIndex = 0; colIndex < COLS; colIndex += 1) {
      const skinKey = skinRows[rowIndex][colIndex];
      if (!skinKey) {
        continue;
      }

      const parsed = parseSkinKey(skinKey);
      if (!parsed) {
        continue;
      }

      const definition = DEFINITIONS[parsed.pieceType][parsed.rotation];
      const anchorX = colIndex - definition[parsed.index].x;
      const anchorY = rowIndex - definition[parsed.index].y;
      const groupKey = `${parsed.pieceType}:${parsed.rotation}:${anchorX}:${anchorY}`;
      const group = groups.get(groupKey) ?? {
        pieceType: parsed.pieceType,
        rotation: parsed.rotation,
        anchorX,
        anchorY,
        indices: new Set<number>(),
        cells: [],
        complete: false,
      };

      group.indices.add(parsed.index);
      group.cells.push({ x: colIndex, y: rowIndex });
      groups.set(groupKey, group);
    }
  }

  for (const group of groups.values()) {
    const definition = DEFINITIONS[group.pieceType][group.rotation];
    group.complete = definition.every((cell, index) => {
      const y = group.anchorY + cell.y;
      const x = group.anchorX + cell.x;
      if (y < 0 || y >= skinRows.length || x < 0 || x >= COLS) {
        return false;
      }
      return skinRows[y][x] === `${group.pieceType}:${group.rotation}:${index}`;
    });
  }

  return [...groups.values()];
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
  populateMonsterFigure(container, pieceType, {
    rotation: 0,
    now: performance.now(),
    animate: false,
    fillRatio: 0.8,
    cellClassName: 'preview-cell',
    filledClassName: 'filled monster-preview',
    layout: 'absolute',
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
  const lookX = Math.max(-0.26, Math.min(0.26, Math.sin(now / 520) * 0.12 + (state.active ? (state.active.x - 4.5) / 14 : 0.02)));
  const lookY = Math.max(-0.18, Math.min(0.18, Math.cos(now / 760) * 0.08 + (state.active ? (state.active.y - 8) / 48 : 0.03)));

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

  const skinReady = isMonsterSkinReady();
  const lockedGroups = buildLockedPieceGroups(skinRows);
  const completeCellKeys = new Set<string>();
  if (skinReady) {
    for (const group of lockedGroups) {
      if (!group.complete) {
        continue;
      }
      for (const cell of group.cells) {
        completeCellKeys.add(`${cell.x}:${cell.y}`);
      }
    }
  }

  let activeGroup: BoardPieceGroup | null = null;
  if (skinReady && state.active) {
    const definition = DEFINITIONS[state.active.type][state.active.rotation];
    const activeCells = getCells(state.active)
      .filter((cell) => cell.y >= HIDDEN_ROWS)
      .map((cell) => ({ x: cell.x, y: cell.y - HIDDEN_ROWS }));
    if (activeCells.length) {
      activeGroup = {
        pieceType: state.active.type,
        rotation: state.active.rotation,
        anchorX: state.active.x,
        anchorY: state.active.y - HIDDEN_ROWS,
        indices: new Set(definition.map((_, index) => index)),
        cells: activeCells,
        complete: true,
      };
      for (const cell of activeCells) {
        completeCellKeys.add(`${cell.x}:${cell.y}`);
      }
    }
  }

  rows.flat().forEach((value, index) => {
    const cell = refs.board.children[index] as HTMLElement;
    const rowIndex = Math.floor(index / COLS);
    const colIndex = index % COLS;
    const skinKey = skinRows[rowIndex][colIndex];
    const occupied = Boolean(skinKey);
    const isWholePieceCell = completeCellKeys.has(`${colIndex}:${rowIndex}`);

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
      if (isWholePieceCell) {
        cell.className = `cell piece-${skinKey.split(':')[0].toLowerCase()}`;
        cell.replaceChildren();
        cell.style.removeProperty('--squish-scale-x');
        cell.style.removeProperty('--squish-scale-y');
        cell.style.removeProperty('--squish-shift-x');
        cell.style.removeProperty('--squish-shift-y');
        return;
      }
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

  // Ensure layout measurements are cached
  if (cachedGap < 0) {
    cachedGap = Number.parseFloat(getComputedStyle(refs.board).gap || '3') || 3;
  }
  const gap = cachedGap;
  const innerWidth = refs.board.clientWidth - 20;
  const innerHeight = refs.board.clientHeight - 20;
  cachedCellWidth = (innerWidth - gap * (COLS - 1)) / COLS;
  cachedCellHeight = (innerHeight - gap * (rows.length - 1)) / rows.length;

  const renderBoardGroup = (group: BoardPieceGroup): HTMLElement => {
    const definition = DEFINITIONS[group.pieceType][group.rotation];
    const minX = Math.min(...definition.map((cell) => cell.x));
    const maxX = Math.max(...definition.map((cell) => cell.x));
    const minY = Math.min(...definition.map((cell) => cell.y));
    const maxY = Math.max(...definition.map((cell) => cell.y));
    const leftCell = group.anchorX + minX;
    const topCell = group.anchorY + minY;
    const widthCells = maxX - minX + 1;
    const heightCells = maxY - minY + 1;

    const pieceFigure = document.createElement('div');
    pieceFigure.className = 'board-piece-figure';
    pieceFigure.style.left = `${leftCell * (cachedCellWidth + gap)}px`;
    pieceFigure.style.top = `${topCell * (cachedCellHeight + gap)}px`;
    pieceFigure.style.width = `${widthCells * cachedCellWidth + Math.max(0, widthCells - 1) * gap}px`;
    pieceFigure.style.height = `${heightCells * cachedCellHeight + Math.max(0, heightCells - 1) * gap}px`;
    populateMonsterBoardFigure(pieceFigure, group.pieceType, group.rotation, {
      now,
      animate: true,
    });
    return pieceFigure;
  };

  // Rebuild locked piece overlays only when the board changes (lock/clear)
  const lockedKey = skinReady ? `${state.lastLockAt}:${state.lastLineClearAt}` : '';
  if (lockedKey !== cachedLockedKey) {
    cachedLockedKey = lockedKey;
    refs.boardMonsterLayer.replaceChildren();
    activeOverlayNode = null;
    if (skinReady) {
      lockedGroups.filter((group) => group.complete).forEach((group) => {
        refs.boardMonsterLayer.appendChild(renderBoardGroup(group));
      });
    }
  }

  // Update active piece overlay — only rebuild when active piece state changes
  const activeKey = skinReady && state.active ? `${state.active.type}:${state.active.rotation}:${state.active.x}:${state.active.y}` : '';
  if (activeKey !== cachedActiveKey) {
    cachedActiveKey = activeKey;
    if (activeOverlayNode) {
      activeOverlayNode.remove();
      activeOverlayNode = null;
    }
    if (activeGroup) {
      activeOverlayNode = renderBoardGroup(activeGroup);
      refs.boardMonsterLayer.appendChild(activeOverlayNode);
    }
  }

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

  const holdPiece = state.hold || null;
  if (holdPiece !== cachedHoldPiece) {
    cachedHoldPiece = holdPiece;
    renderPiecePreview(refs.hold, holdPiece);
  }

  const nextKey = state.queue.slice(0, 3).join(',');
  if (nextKey !== cachedNextQueue) {
    cachedNextQueue = nextKey;
    refs.nextQueue.innerHTML = '';
    state.queue.slice(0, 3).forEach((piece) => {
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
  }

  const lbKey = `${state.mode}:${isTraining}:${storage.score.length}:${storage.sprint.length}`;
  if (lbKey !== cachedLeaderboardKey) {
    cachedLeaderboardKey = lbKey;
    refs.leaderboardTitle.textContent = isTraining
      ? 'Training Notes'
      : state.mode === 'sprint40'
        ? 'Top 3 40L Times'
        : 'Top 3 Scores';
    refs.leaderboard.innerHTML = '';
    if (isTraining) {
      const item = document.createElement('li');
      const faultRate = state.pieces ? Math.round((state.trainingFaults / state.pieces) * 1000) / 10 : 0;
      item.textContent = `No leaderboard in Training mode. Fault rate ${faultRate}% with ${settings.trainingFeedback.toUpperCase()} feedback.`;
      refs.leaderboard.appendChild(item);
    } else if (state.mode === 'sprint40') {
      const entries = getVisibleSprintRecords(storage.sprint);
      if (!entries.length) {
        const item = document.createElement('li');
        item.textContent = 'No completed 40-line runs yet.';
        refs.leaderboard.appendChild(item);
      } else {
        entries.slice(0, 3).forEach((entry, index) => {
          const item = document.createElement('li');
          item.textContent = `${index + 1}. ${entry.nickname} - ${formatTime(entry.timeMs)} - ${entry.lines}L`;
          refs.leaderboard.appendChild(item);
        });
      }
    } else {
      const entries = getVisibleScoreRecords(storage.score);
      if (!entries.length) {
        const item = document.createElement('li');
        item.textContent = 'No high scores yet. Survive a run to set the first record.';
        refs.leaderboard.appendChild(item);
      } else {
      entries.slice(0, 3).forEach((entry, index) => {
        const item = document.createElement('li');
        item.textContent = `${index + 1}. ${entry.nickname} - ${entry.score} pts - ${entry.lines}L`;
        refs.leaderboard.appendChild(item);
      });
    }
  }
  }

  const settingsModalHidden = document.getElementById('settingsModal')?.classList.contains('hidden') ?? true;
  if (settingsModalHidden) {
    if (document.activeElement !== refs.dasInput) refs.dasInput.value = String(settings.dasMs);
    if (document.activeElement !== refs.arrInput) refs.arrInput.value = String(settings.arrMs);
    if (document.activeElement !== refs.lockDelayInput) refs.lockDelayInput.value = String(settings.lockDelayMs);
    refs.trainingFeedbackInput.value = settings.trainingFeedback;
    refs.sfxEnabledInput.checked = settings.sfxEnabled;
    if (document.activeElement !== refs.sfxVolumeInput) refs.sfxVolumeInput.value = String(settings.sfxVolume);
    refs.musicEnabledInput.checked = settings.musicEnabled;
    if (document.activeElement !== refs.musicVolumeInput) refs.musicVolumeInput.value = String(settings.musicVolume);
  }

  if (isTraining && state.lastTrainingFaultMessage && now - state.lastTrainingFaultAt < 1500) {
    refs.faultToast.textContent = state.lastTrainingFaultMessage;
    refs.faultToast.classList.remove('hidden');
  } else {
    refs.faultToast.classList.add('hidden');
  }

  refs.countdownPanel.classList.add('hidden');
  refs.endStatePanel.classList.add('hidden');
  refs.retryButton.classList.add('hidden');

  switch (appPhase) {
    case 'countdown': {
      const count = Math.ceil(Math.max(0, state.countdownUntil - now) / 1000);
      refs.countdownValue.textContent = count > 0 ? String(count) : 'GO!';
      refs.countdownPanel.classList.remove('hidden');
      refs.overlay.classList.add('hidden');
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
    case 'paused':
      refs.overlay.textContent = 'PAUSED';
      refs.overlay.classList.remove('hidden');
      refs.retryButton.classList.remove('hidden');
      refs.statusText.textContent = 'Paused. Press P to continue or O to restart this run fresh.';
      break;
    case 'sprint-clear':
      refs.overlay.classList.add('hidden');
      refs.endStatePanel.classList.remove('hidden');
      refs.retryButton.classList.remove('hidden');
      refs.endStateEyebrow.textContent = 'Sprint Complete';
      refs.endStateTitle.textContent = '40 CLEAR';
      refs.endStateSummary.textContent = `Finished 40 Lines in ${formatTime(elapsed(state))}.`;
      refs.statusText.textContent = `Sprint complete in ${formatTime(elapsed(state))}.`;
      break;
    case 'game-over':
      refs.overlay.classList.add('hidden');
      refs.endStatePanel.classList.remove('hidden');
      refs.retryButton.classList.remove('hidden');
      refs.endStateEyebrow.textContent = state.mode === 'arcade' ? 'Run Over' : 'Sprint Failed';
      refs.endStateTitle.textContent = 'TOP OUT';
      refs.endStateSummary.textContent = state.mode === 'arcade'
        ? `${state.score} points after ${state.lines} cleared lines.`
        : 'Top out before clearing all 40 lines.';
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
