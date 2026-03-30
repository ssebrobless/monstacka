import { describe, expect, it } from 'vitest';
import { createBoard } from '../board';
import { getCells, isGrounded, isValid, rotate } from '../pieces';

describe('piece helpers', () => {
  it('returns the board cells occupied by a piece', () => {
    const cells = getCells({ type: 'T', rotation: 0, x: 3, y: 4 });

    expect(cells).toEqual([
      { x: 4, y: 4 },
      { x: 3, y: 5 },
      { x: 4, y: 5 },
      { x: 5, y: 5 },
    ]);
  });

  it('rejects placements that collide with occupied cells', () => {
    const board = createBoard();
    board[1][1] = 'S';

    expect(isValid(board, { type: 'O', rotation: 0, x: 0, y: 0 })).toBe(false);
  });

  it('detects when a piece is grounded', () => {
    const board = createBoard();

    expect(isGrounded(board, { type: 'O', rotation: 0, x: 0, y: 22 })).toBe(true);
    expect(isGrounded(board, { type: 'O', rotation: 0, x: 0, y: 0 })).toBe(false);
  });

  it('applies wall kicks for the I piece when rotating near the wall', () => {
    const board = createBoard();
    const result = rotate(board, { type: 'I', rotation: 1, x: -2, y: 0 }, -1, true);

    expect(result).toEqual({ type: 'I', rotation: 0, x: 0, y: 0 });
  });

  it('fails the same rotation without kicks', () => {
    const board = createBoard();
    const result = rotate(board, { type: 'I', rotation: 1, x: -2, y: 0 }, -1, false);

    expect(result).toBeNull();
  });
});
