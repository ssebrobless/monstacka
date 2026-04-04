import type {
  GameState, Piece, PieceType, GameMode, ResumableAppPhase, SavedRun, SavedRunState, TrainingSnapshot,
} from '../types';
import { COUNTDOWN_MS, DEFAULT_MODE, MAX_LOCK_RESETS, SCORE_TABLE, SETTINGS_DEFAULTS, TARGET_LINES } from '../constants';
import { getGravityMs } from './gravity';
import { clearLines, collapseRows, createBoard } from './board';
import { ensureQueue } from './bag';
import { isGrounded, isValid, getCells, rotate as rotatePiece } from './pieces';
import { evaluateTrainingPlacement } from './training';

function newPiece(type: PieceType): Piece {
  return { type, rotation: 0, x: 3, y: 0 };
}

function captureTrainingSnapshot(state: GameState): void {
  if (state.mode !== 'training' || !state.active) {
    state.trainingSnapshot = null;
    state.currentPieceInputs = 0;
    return;
  }

  const snapshot: TrainingSnapshot = {
    active: { ...state.active },
    queue: [...state.queue],
  };

  state.trainingSnapshot = snapshot;
  state.currentPieceInputs = 0;
}

function restoreTrainingSnapshot(state: GameState): void {
  if (!state.trainingSnapshot) return;

  state.board = createBoard();
  state.boardSkin = createBoard();
  state.active = { ...state.trainingSnapshot.active };
  state.queue = [...state.trainingSnapshot.queue];
  state.hold = '';
  state.holdUsed = false;
  state.lines = 0;
  state.score = 0;
  state.lockDeadline = 0;
  state.lockResets = 0;
  state.lastLockAt = 0;
  state.lastLineClearAt = 0;
  state.sprintComplete = false;
  state.gameOver = false;
  state.currentPieceInputs = 0;
}

function cloneBoard(board: string[][]): string[][] {
  return board.map((row) => [...row]);
}

function clonePiece(piece: Piece | null): Piece | null {
  return piece ? { ...piece } : null;
}

function cloneTrainingSnapshot(snapshot: TrainingSnapshot | null): TrainingSnapshot | null {
  if (!snapshot) return null;
  return {
    active: { ...snapshot.active },
    queue: [...snapshot.queue],
  };
}

function createSavedRunState(state: GameState): SavedRunState {
  return {
    board: cloneBoard(state.board),
    boardSkin: cloneBoard(state.boardSkin),
    active: clonePiece(state.active),
    hold: state.hold,
    holdUsed: state.holdUsed,
    queue: [...state.queue],
    hasSpawned: state.hasSpawned,
    mode: state.mode,
    lines: state.lines,
    score: state.score,
    pieces: state.pieces,
    trainingFeedback: state.trainingFeedback,
    currentPieceInputs: state.currentPieceInputs,
    trainingFaults: state.trainingFaults,
    trainingPerfectStreak: state.trainingPerfectStreak,
    lastTrainingFaultMessage: state.lastTrainingFaultMessage,
    lockResets: state.lockResets,
    trainingSnapshot: cloneTrainingSnapshot(state.trainingSnapshot),
  };
}

export function createGameState(mode: GameMode = DEFAULT_MODE): GameState {
  const state: GameState = {
    board: createBoard(),
    boardSkin: createBoard(),
    active: null,
    hold: '',
    holdUsed: false,
    queue: [],
    hasSpawned: false,
    mode,
    lines: 0,
    score: 0,
    pieces: 0,
    startTime: 0,
    completedTime: 0,
    countdownUntil: performance.now() + COUNTDOWN_MS,
    lastGravity: 0,
    lockDeadline: 0,
    lockResets: 0,
    lastLockAt: 0,
    lastLineClearAt: 0,
    trainingFeedback: SETTINGS_DEFAULTS.trainingFeedback,
    currentPieceInputs: 0,
    trainingFaults: 0,
    trainingPerfectStreak: 0,
    lastTrainingFaultAt: 0,
    lastTrainingFaultMessage: '',
    trainingSnapshot: null,
    sprintComplete: false,
    gameOver: false,
  };
  spawn(state);
  return state;
}

export function spawn(state: GameState, forceType?: PieceType): boolean {
  ensureQueue(state.queue, state.hasSpawned);
  const piece = newPiece(forceType || state.queue.shift()!);
  if (!isValid(state.board, piece)) {
    state.active = null;
    state.gameOver = true;
    state.completedTime = performance.now();
    return false;
  }

  state.active = piece;
  state.holdUsed = false;
  state.hasSpawned = true;
  state.lockDeadline = 0;
  state.lockResets = 0;
  ensureQueue(state.queue, state.hasSpawned);
  captureTrainingSnapshot(state);
  return true;
}

export function reset(state: GameState, mode: GameMode = state.mode): void {
  state.board = createBoard();
  state.boardSkin = createBoard();
  state.active = null;
  state.hold = '';
  state.holdUsed = false;
  state.queue = [];
  state.hasSpawned = false;
  state.mode = mode;
  state.lines = 0;
  state.score = 0;
  state.pieces = 0;
  state.startTime = 0;
  state.completedTime = 0;
  state.countdownUntil = performance.now() + COUNTDOWN_MS;
  state.lastGravity = 0;
  state.lockDeadline = 0;
  state.lockResets = 0;
  state.lastLockAt = 0;
  state.lastLineClearAt = 0;
  state.currentPieceInputs = 0;
  state.trainingFaults = 0;
  state.trainingPerfectStreak = 0;
  state.lastTrainingFaultAt = 0;
  state.lastTrainingFaultMessage = '';
  state.trainingSnapshot = null;
  state.sprintComplete = false;
  state.gameOver = false;
  spawn(state);
}

export function captureSavedRun(state: GameState, phase: ResumableAppPhase, now: number): SavedRun {
  return {
    mode: state.mode,
    phase,
    savedAt: new Date().toISOString(),
    elapsedMs: (phase === 'playing' || phase === 'paused') && state.startTime ? Math.max(0, Math.floor(now - state.startTime)) : 0,
    remainingCountdownMs: phase === 'countdown' ? Math.max(0, Math.ceil(state.countdownUntil - now)) : 0,
    gravityElapsedMs: (phase === 'playing' || phase === 'paused') ? Math.max(0, Math.floor(now - state.lastGravity)) : 0,
    lockRemainingMs: state.lockDeadline ? Math.max(0, Math.ceil(state.lockDeadline - now)) : 0,
    state: createSavedRunState(state),
  };
}

export function restoreSavedRun(state: GameState, savedRun: SavedRun, now: number): ResumableAppPhase {
  const saved = savedRun.state;
  state.board = cloneBoard(saved.board);
  state.boardSkin = cloneBoard(saved.boardSkin);
  state.active = clonePiece(saved.active);
  state.hold = saved.hold;
  state.holdUsed = saved.holdUsed;
  state.queue = [...saved.queue];
  state.hasSpawned = saved.hasSpawned;
  state.mode = saved.mode;
  state.lines = saved.lines;
  state.score = saved.score;
  state.pieces = saved.pieces;
  state.trainingFeedback = saved.trainingFeedback;
  state.currentPieceInputs = saved.currentPieceInputs;
  state.trainingFaults = saved.trainingFaults;
  state.trainingPerfectStreak = saved.trainingPerfectStreak;
  state.lastTrainingFaultMessage = saved.lastTrainingFaultMessage;
  state.trainingSnapshot = cloneTrainingSnapshot(saved.trainingSnapshot);
  state.lockResets = (saved as unknown as Record<string, unknown>).lockResets as number ?? 0;
  state.completedTime = 0;
  state.lastLockAt = 0;
  state.lastLineClearAt = 0;
  state.lastTrainingFaultAt = 0;
  state.sprintComplete = false;
  state.gameOver = false;

  if (savedRun.phase === 'countdown' && savedRun.remainingCountdownMs > 0) {
    state.startTime = 0;
    state.countdownUntil = now + savedRun.remainingCountdownMs;
    state.lastGravity = 0;
    state.lockDeadline = savedRun.lockRemainingMs ? now + savedRun.lockRemainingMs : 0;
    return 'countdown';
  }

  const gravityMs = getGravityMs(saved.mode, saved.lines);
  const safeGravityElapsed = Math.min(savedRun.gravityElapsedMs, Math.max(0, gravityMs - 1));
  state.startTime = now - savedRun.elapsedMs;
  state.countdownUntil = 0;
  state.lastGravity = now - safeGravityElapsed;
  state.lockDeadline = savedRun.lockRemainingMs ? now + savedRun.lockRemainingMs : 0;
  return savedRun.phase === 'paused' ? 'paused' : 'playing';
}

export function move(state: GameState, dx: number, dy: number, lockDelayMs: number): boolean {
  if (!state.active || state.gameOver) return false;

  const next = { ...state.active, x: state.active.x + dx, y: state.active.y + dy };
  if (!isValid(state.board, next)) return false;

  state.active = next;
  if (isGrounded(state.board, state.active)) {
    if (state.lockResets < MAX_LOCK_RESETS) {
      state.lockDeadline = performance.now() + lockDelayMs;
      state.lockResets += 1;
    }
  } else {
    state.lockDeadline = 0;
  }
  return true;
}

export function rotate(state: GameState, step: number, useKicks: boolean, lockDelayMs: number): boolean {
  if (!state.active || state.gameOver) return false;

  const result = rotatePiece(state.board, state.active, step, useKicks);
  if (!result) return false;

  state.active = result;
  if (isGrounded(state.board, state.active)) {
    if (state.lockResets < MAX_LOCK_RESETS) {
      state.lockDeadline = performance.now() + lockDelayMs;
      state.lockResets += 1;
    }
  } else {
    state.lockDeadline = 0;
  }
  return true;
}

export function lockPiece(state: GameState): void {
  if (!state.active) return;

  const lockedPiece = { ...state.active };

  if (state.mode === 'training') {
    const evaluation = evaluateTrainingPlacement(lockedPiece, state.currentPieceInputs);
    state.pieces += 1;
    state.lastLockAt = performance.now();

    if (evaluation.isFault) {
      state.trainingFaults += 1;
      state.trainingPerfectStreak = 0;
      state.lastTrainingFaultAt = performance.now();
      state.lastTrainingFaultMessage = evaluation.message;
    } else {
      state.trainingPerfectStreak += 1;
      state.lastTrainingFaultMessage = '';
    }

    if (evaluation.isFault && state.trainingFeedback === 'redo') {
      restoreTrainingSnapshot(state);
      return;
    }

    state.board = createBoard();
    state.boardSkin = createBoard();
    state.active = null;
    state.hold = '';
    state.holdUsed = false;
    state.lines = 0;
    state.score = 0;
    state.lockDeadline = 0;
    spawn(state);
    return;
  }

  state.lastLockAt = performance.now();
  for (const [index, cell] of getCells(state.active).entries()) {
    if (cell.y >= 0) {
      state.board[cell.y][cell.x] = state.active.type;
      state.boardSkin[cell.y][cell.x] = `${state.active.type}:${state.active.rotation}:${index}`;
    }
  }

  state.active = null;
  state.pieces += 1;

  const { newBoard, clearedCount, clearedRows } = clearLines(state.board);
  state.board = newBoard;
  state.boardSkin = collapseRows(state.boardSkin, clearedRows, () => Array(state.board[0].length).fill(''));
  state.lines += clearedCount;
  state.score += SCORE_TABLE[clearedCount] ?? 0;

  if (clearedCount > 0) {
    state.lastLineClearAt = performance.now();
  }

  if (state.mode === 'sprint40' && state.lines >= TARGET_LINES) {
    state.sprintComplete = true;
    state.gameOver = true;
    state.completedTime = performance.now();
  }

  state.lockDeadline = 0;
  if (!state.sprintComplete) {
    spawn(state);
  }
}

export function dropOnce(state: GameState, lockDelayMs: number, awardSoftDrop = false): boolean {
  if (move(state, 0, 1, lockDelayMs)) {
    if (awardSoftDrop && state.mode !== 'training') {
      state.score += 1;
    }
    return true;
  }

  if (!state.lockDeadline) {
    state.lockDeadline = performance.now() + lockDelayMs;
  }
  return false;
}

export function hardDrop(state: GameState, lockDelayMs: number): void {
  let distance = 0;
  while (move(state, 0, 1, lockDelayMs)) {
    distance += 1;
  }

  if (state.mode !== 'training') {
    state.score += distance * 2;
  }

  lockPiece(state);
}

export function hold(state: GameState, lockDelayMs: number): boolean {
  if (!state.active || state.holdUsed || state.gameOver) return false;

  const current = state.active.type;
  if (state.hold) {
    const swap = state.hold as PieceType;
    state.hold = current;
    const piece = newPiece(swap);
    if (!isValid(state.board, piece)) {
      state.gameOver = true;
      state.completedTime = performance.now();
      return false;
    }
    state.active = piece;
  } else {
    state.hold = current;
    state.active = null;
    spawn(state);
  }

  state.holdUsed = true;
  if (state.active) {
    if (isGrounded(state.board, state.active)) {
      if (state.lockResets < MAX_LOCK_RESETS) {
        state.lockDeadline = performance.now() + lockDelayMs;
        state.lockResets += 1;
      }
    } else {
      state.lockDeadline = 0;
    }
  }
  captureTrainingSnapshot(state);
  return true;
}

export function elapsed(state: GameState): number {
  if (!state.startTime) return 0;
  return Math.floor((state.completedTime || performance.now()) - state.startTime);
}

export function formatTime(ms: number): string {
  const minutes = Math.floor(ms / 60000);
  const seconds = Math.floor((ms % 60000) / 1000);
  const millis = Math.floor(ms % 1000);
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(millis).padStart(3, '0')}`;
}
