import { COLS, TOTAL_ROWS } from '../constants';

export function createBoard(): string[][] {
  return Array.from({ length: TOTAL_ROWS }, () => Array(COLS).fill(''));
}

export function clearLines(board: string[][]): { newBoard: string[][]; clearedCount: number } {
  let clearedCount = 0;
  const kept: string[][] = [];
  for (const row of board) {
    if (row.every(Boolean)) {
      clearedCount++;
    } else {
      kept.push([...row]);
    }
  }
  while (kept.length < board.length) {
    kept.unshift(Array(COLS).fill(''));
  }
  return { newBoard: kept, clearedCount };
}
