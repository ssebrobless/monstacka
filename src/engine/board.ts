import { COLS, TOTAL_ROWS } from '../constants';

export function createBoard(): string[][] {
  return Array.from({ length: TOTAL_ROWS }, () => Array(COLS).fill(''));
}

export function clearLines(board: string[][]): { newBoard: string[][]; clearedCount: number; clearedRows: number[] } {
  let clearedCount = 0;
  const clearedRows: number[] = [];
  const kept: string[][] = [];
  for (const [index, row] of board.entries()) {
    if (row.every(Boolean)) {
      clearedCount++;
      clearedRows.push(index);
    } else {
      kept.push([...row]);
    }
  }
  while (kept.length < board.length) {
    kept.unshift(Array(COLS).fill(''));
  }
  return { newBoard: kept, clearedCount, clearedRows };
}

export function collapseRows<T>(rows: T[][], clearedRows: number[], makeEmptyRow: () => T[]): T[][] {
  if (!clearedRows.length) {
    return rows.map((row) => [...row]);
  }

  const clearedSet = new Set(clearedRows);
  const kept = rows
    .map((row) => [...row])
    .filter((_, index) => !clearedSet.has(index));

  while (kept.length < rows.length) {
    kept.unshift(makeEmptyRow());
  }

  return kept;
}
