import type { Piece, Cell, GameState } from '../types';
import { DEFINITIONS, KICKS_JLSTZ, KICKS_I, COLS, TOTAL_ROWS } from '../constants';

export function getCells(piece: Piece): Cell[] {
  return DEFINITIONS[piece.type][piece.rotation].map(c => ({
    x: piece.x + c.x,
    y: piece.y + c.y,
  }));
}

export function isValid(board: string[][], piece: Piece): boolean {
  return getCells(piece).every(
    c =>
      c.x >= 0 &&
      c.x < COLS &&
      c.y < TOTAL_ROWS &&
      (c.y < 0 || !board[c.y][c.x]),
  );
}

export function isGrounded(board: string[][], piece: Piece): boolean {
  return !isValid(board, { ...piece, y: piece.y + 1 });
}

export function getGhostCells(board: string[][], piece: Piece): Cell[] {
  let ghost = { ...piece };
  while (isValid(board, { ...ghost, y: ghost.y + 1 })) {
    ghost = { ...ghost, y: ghost.y + 1 };
  }
  return getCells(ghost);
}

export function rotate(
  board: string[][],
  piece: Piece,
  step: number,
  useKicks: boolean,
): Piece | null {
  const from = piece.rotation;
  const to = (from + step + 4) % 4;
  const candidate = { ...piece, rotation: to };

  if (!useKicks) {
    return isValid(board, candidate) ? candidate : null;
  }

  const kickTable = piece.type === 'I' ? KICKS_I : KICKS_JLSTZ;
  const offsets = kickTable[`${from}>${to}`] || [[0, 0]];

  for (const [ox, oy] of offsets) {
    const kicked = { ...candidate, x: candidate.x + ox, y: candidate.y - oy };
    if (isValid(board, kicked)) {
      return kicked;
    }
  }
  return null;
}
