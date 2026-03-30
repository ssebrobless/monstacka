import { describe, expect, it } from 'vitest';
import { createBoard } from '../board';
import { createGameState, hardDrop, hold, lockPiece, spawn } from '../state';

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
});
