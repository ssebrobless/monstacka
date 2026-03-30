import { describe, expect, it } from 'vitest';
import { evaluateTrainingPlacement, FINESSE_LOOKUP, getOptimalInputCount } from '../training';

describe('training finesse lookup', () => {
  it('contains a spawn placement entry for all seven pieces', () => {
    expect(getOptimalInputCount('I', 3, 0)).toBe(0);
    expect(getOptimalInputCount('O', 3, 0)).toBe(0);
    expect(getOptimalInputCount('T', 3, 0)).toBe(0);
    expect(getOptimalInputCount('S', 3, 0)).toBe(0);
    expect(getOptimalInputCount('Z', 3, 0)).toBe(0);
    expect(getOptimalInputCount('J', 3, 0)).toBe(0);
    expect(getOptimalInputCount('L', 3, 0)).toBe(0);
  });

  it('uses low optimal counts for well-known wall and rotate placements', () => {
    expect(getOptimalInputCount('I', 0, 0)).toBe(1);
    expect(getOptimalInputCount('O', 7, 0)).toBe(1);
    expect(getOptimalInputCount('T', 3, 1)).toBe(1);
  });

  it('tracks lookup data for all piece types', () => {
    expect(Object.keys(FINESSE_LOOKUP)).toEqual(['I', 'O', 'T', 'S', 'Z', 'J', 'L']);
    for (const pieceType of Object.keys(FINESSE_LOOKUP) as Array<keyof typeof FINESSE_LOOKUP>) {
      expect(Object.keys(FINESSE_LOOKUP[pieceType]).length).toBeGreaterThan(0);
    }
  });

  it('flags placements that use more inputs than optimal', () => {
    expect(evaluateTrainingPlacement({ type: 'T', rotation: 0, x: 3, y: 0 }, 0).isFault).toBe(false);
    expect(evaluateTrainingPlacement({ type: 'T', rotation: 0, x: 3, y: 0 }, 2)).toEqual({
      optimalInputs: 0,
      actualInputs: 2,
      isFault: true,
      message: '2 inputs used / 0 optimal',
    });
  });
});
