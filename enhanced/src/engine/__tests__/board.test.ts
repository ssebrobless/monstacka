import { describe, expect, it } from 'vitest';
import { COLS, TOTAL_ROWS } from '../../constants';
import { clearLines, createBoard } from '../board';

describe('board helpers', () => {
  it('creates an empty board with the expected dimensions', () => {
    const board = createBoard();

    expect(board).toHaveLength(TOTAL_ROWS);
    expect(board[0]).toHaveLength(COLS);
    expect(board.flat().every((cell) => cell === '')).toBe(true);
    expect(board[0]).not.toBe(board[1]);
  });

  it('keeps the board unchanged when no lines are full', () => {
    const board = createBoard();
    board[TOTAL_ROWS - 1][0] = 'T';

    const { newBoard, clearedCount } = clearLines(board);

    expect(clearedCount).toBe(0);
    expect(newBoard[TOTAL_ROWS - 1][0]).toBe('T');
  });

  it('clears one full line and inserts an empty row at the top', () => {
    const board = createBoard();
    board[TOTAL_ROWS - 1] = Array(COLS).fill('I');
    board[TOTAL_ROWS - 2][0] = 'O';

    const { newBoard, clearedCount } = clearLines(board);

    expect(clearedCount).toBe(1);
    expect(newBoard[0].every((cell) => cell === '')).toBe(true);
    expect(newBoard[TOTAL_ROWS - 1][0]).toBe('O');
  });

  it('clears multiple full lines in one pass', () => {
    const board = createBoard();
    board[TOTAL_ROWS - 1] = Array(COLS).fill('I');
    board[TOTAL_ROWS - 2] = Array(COLS).fill('T');
    board[TOTAL_ROWS - 3][2] = 'L';

    const { newBoard, clearedCount } = clearLines(board);

    expect(clearedCount).toBe(2);
    expect(newBoard[0].every((cell) => cell === '')).toBe(true);
    expect(newBoard[1].every((cell) => cell === '')).toBe(true);
    expect(newBoard[TOTAL_ROWS - 1][2]).toBe('L');
  });
});
