import { describe, expect, it } from 'vitest';
import type { PieceType } from '../../types';
import { ensureQueue, makeBag, makeOpeningBag } from '../bag';

const ALL_PIECES: PieceType[] = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'];
const OPENING_PIECES: PieceType[] = ['I', 'J', 'L', 'T'];

function sortedBag(bag: PieceType[]): PieceType[] {
  return [...bag].sort();
}

describe('bag helpers', () => {
  it('builds an opening bag with each piece exactly once', () => {
    const bag = makeOpeningBag();

    expect(bag).toHaveLength(7);
    expect(OPENING_PIECES).toContain(bag[0]);
    expect(sortedBag(bag)).toEqual(sortedBag(ALL_PIECES));
  });

  it('builds a standard bag with each piece exactly once', () => {
    const bag = makeBag();

    expect(bag).toHaveLength(7);
    expect(sortedBag(bag)).toEqual(sortedBag(ALL_PIECES));
  });

  it('fills an empty queue with an opening bag before the first spawn', () => {
    const queue: PieceType[] = [];

    ensureQueue(queue, false);

    expect(queue).toHaveLength(7);
    expect(OPENING_PIECES).toContain(queue[0]);
    expect(sortedBag(queue)).toEqual(sortedBag(ALL_PIECES));
  });

  it('fills an empty queue with a standard bag after the first spawn', () => {
    const queue: PieceType[] = [];

    ensureQueue(queue, true);

    expect(queue).toHaveLength(7);
    expect(sortedBag(queue)).toEqual(sortedBag(ALL_PIECES));
  });
});
