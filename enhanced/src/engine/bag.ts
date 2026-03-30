import type { PieceType } from '../types';

const ALL_PIECES: PieceType[] = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'];
const FIRST_POOL: PieceType[] = ['I', 'J', 'L', 'T'];

function shuffle<T>(arr: T[]): T[] {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

export function makeOpeningBag(): PieceType[] {
  const first = FIRST_POOL[Math.floor(Math.random() * FIRST_POOL.length)];
  const rest = shuffle(ALL_PIECES.filter(p => p !== first));
  return [first, ...rest];
}

export function makeBag(): PieceType[] {
  return shuffle(ALL_PIECES);
}

export function ensureQueue(queue: PieceType[], hasSpawned: boolean): void {
  while (queue.length < 7) {
    queue.push(...(!hasSpawned && queue.length === 0 ? makeOpeningBag() : makeBag()));
  }
}
