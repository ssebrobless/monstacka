import { describe, expect, it } from 'vitest';
import { createBoard } from '../board';
import { captureSavedRun, createGameState, hardDrop, hold, lockPiece, restoreSavedRun, spawn } from '../state';

describe('game state helpers', () => {
  it('tops out when a spawn position is blocked', () => {
    const state = createGameState('arcade');
    state.board = createBoard();
    state.board[0][4] = 'X';

    const spawned = spawn(state, 'O');

    expect(spawned).toBe(false);
    expect(state.active).toBeNull();
    expect(state.gameOver).toBe(true);
    expect(state.completedTime).toBeGreaterThan(0);
  });

  it('locks a piece, clears a line, and awards score in arcade mode', () => {
    const state = createGameState('arcade');
    state.board = createBoard();
    state.queue = ['O', 'T', 'S', 'Z', 'J', 'L', 'I'];
    state.hasSpawned = true;
    state.lines = 0;
    state.score = 0;
    state.pieces = 0;
    state.active = { type: 'I', rotation: 0, x: 0, y: 22 };
    state.board[23][4] = 'X';
    state.board[23][5] = 'X';
    state.board[23][6] = 'X';
    state.board[23][7] = 'X';
    state.board[23][8] = 'X';
    state.board[23][9] = 'X';

    lockPiece(state);

    expect(state.lines).toBe(1);
    expect(state.score).toBe(100);
    expect(state.pieces).toBe(1);
    expect(state.board[23].every((cell) => cell === '')).toBe(true);
  });

  it('marks sprint40 as complete once the target lines are cleared', () => {
    const state = createGameState('sprint40');
    state.board = createBoard();
    state.queue = ['O', 'T', 'S', 'Z', 'J', 'L', 'I'];
    state.hasSpawned = true;
    state.lines = 39;
    state.active = { type: 'I', rotation: 0, x: 0, y: 22 };
    state.board[23][4] = 'X';
    state.board[23][5] = 'X';
    state.board[23][6] = 'X';
    state.board[23][7] = 'X';
    state.board[23][8] = 'X';
    state.board[23][9] = 'X';

    lockPiece(state);

    expect(state.lines).toBe(40);
    expect(state.sprintComplete).toBe(true);
    expect(state.gameOver).toBe(true);
    expect(state.completedTime).toBeGreaterThan(0);
  });

  it('awards hard-drop score based on distance and locks the piece', () => {
    const state = createGameState('arcade');
    state.board = createBoard();
    state.queue = ['I', 'T', 'S', 'Z', 'J', 'L', 'O'];
    state.hasSpawned = true;
    state.score = 0;
    state.pieces = 0;
    state.active = { type: 'O', rotation: 0, x: 0, y: 0 };

    hardDrop(state, 250);

    expect(state.score).toBe(44);
    expect(state.pieces).toBe(1);
    expect(state.board[22][1]).toBe('O');
    expect(state.board[23][2]).toBe('O');
  });

  it('supports hold once per active piece', () => {
    const state = createGameState('arcade');
    state.board = createBoard();
    state.queue = ['I', 'O', 'S', 'Z', 'J', 'L', 'T'];
    state.hasSpawned = true;
    state.active = { type: 'T', rotation: 0, x: 3, y: 0 };
    state.hold = '';
    state.holdUsed = false;

    const firstHold = hold(state, 250);
    const secondHold = hold(state, 250);

    expect(firstHold).toBe(true);
    expect(state.hold).toBe('T');
    expect(state.holdUsed).toBe(true);
    expect(state.active?.type).toBe('I');
    expect(secondHold).toBe(false);
  });

  it('retries the same piece in training redo mode after a finesse fault', () => {
    const state = createGameState('training');
    state.board = createBoard();
    state.trainingFeedback = 'redo';
    state.queue = ['O', 'S', 'Z', 'J', 'L', 'T', 'I'];
    state.hasSpawned = true;
    state.active = { type: 'I', rotation: 0, x: 3, y: 0 };
    state.trainingSnapshot = {
      active: { ...state.active },
      queue: [...state.queue],
    };
    state.currentPieceInputs = 2;

    lockPiece(state);

    expect(state.trainingFaults).toBe(1);
    expect(state.trainingPerfectStreak).toBe(0);
    expect(state.pieces).toBe(1);
    expect(state.active).toEqual({ type: 'I', rotation: 0, x: 3, y: 0 });
    expect(state.queue).toEqual(['O', 'S', 'Z', 'J', 'L', 'T', 'I']);
    expect(state.board.flat().every((cell) => cell === '')).toBe(true);
  });

  it('advances to the next piece on a clean training placement', () => {
    const state = createGameState('training');
    state.board = createBoard();
    state.trainingFeedback = 'show';
    state.queue = ['O', 'S', 'Z', 'J', 'L', 'T', 'I'];
    state.hasSpawned = true;
    state.active = { type: 'T', rotation: 0, x: 3, y: 0 };
    state.trainingSnapshot = {
      active: { ...state.active },
      queue: [...state.queue],
    };
    state.currentPieceInputs = 0;

    lockPiece(state);

    expect(state.trainingFaults).toBe(0);
    expect(state.trainingPerfectStreak).toBe(1);
    expect(state.pieces).toBe(1);
    expect(state.active?.type).toBe('O');
    expect(state.board.flat().every((cell) => cell === '')).toBe(true);
  });

  it('captures and restores a playing run without changing the gameplay footprint', () => {
    const state = createGameState('arcade');
    state.board = createBoard();
    state.boardSkin = createBoard();
    state.active = { type: 'L', rotation: 1, x: 4, y: 5 };
    state.hold = 'T';
    state.holdUsed = true;
    state.queue = ['I', 'O', 'S', 'Z'];
    state.hasSpawned = true;
    state.lines = 12;
    state.score = 3400;
    state.pieces = 18;
    state.startTime = 2000;
    state.lastGravity = 6880;
    state.lockDeadline = 7240;
    state.board[23][0] = 'X';
    state.boardSkin[23][0] = 'L:1:0';

    const savedRun = captureSavedRun(state, 'playing', 7000);
    const restored = createGameState('arcade');
    const restoredPhase = restoreSavedRun(restored, savedRun, 9000);

    expect(restoredPhase).toBe('playing');
    expect(restored.active).toEqual(state.active);
    expect(restored.hold).toBe('T');
    expect(restored.holdUsed).toBe(true);
    expect(restored.queue).toEqual(['I', 'O', 'S', 'Z']);
    expect(restored.board[23][0]).toBe('X');
    expect(restored.boardSkin[23][0]).toBe('L:1:0');
    expect(restored.startTime).toBe(4000);
    expect(restored.lastGravity).toBe(8880);
    expect(restored.lockDeadline).toBe(9240);
  });

  it('captures and restores a countdown run with remaining countdown time', () => {
    const state = createGameState('sprint40');
    state.countdownUntil = 5200;

    const savedRun = captureSavedRun(state, 'countdown', 4300);
    const restored = createGameState('sprint40');
    const restoredPhase = restoreSavedRun(restored, savedRun, 10000);

    expect(restoredPhase).toBe('countdown');
    expect(restored.startTime).toBe(0);
    expect(restored.countdownUntil).toBe(10900);
    expect(restored.mode).toBe('sprint40');
  });
});
