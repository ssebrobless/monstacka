import type { GameMode } from '../types';

interface GravityBand {
  minLines: number;
  gravityMs: number;
}

const ARCADE_GRAVITY_BANDS: GravityBand[] = [
  { minLines: 140, gravityMs: 33 },
  { minLines: 120, gravityMs: 50 },
  { minLines: 100, gravityMs: 70 },
  { minLines: 80, gravityMs: 100 },
  { minLines: 60, gravityMs: 150 },
  { minLines: 40, gravityMs: 220 },
  { minLines: 30, gravityMs: 300 },
  { minLines: 20, gravityMs: 400 },
  { minLines: 10, gravityMs: 500 },
  { minLines: 0, gravityMs: 650 },
];

export function getGravityMs(mode: GameMode, linesCleared: number): number {
  if (mode !== 'arcade') {
    return 650;
  }

  for (const band of ARCADE_GRAVITY_BANDS) {
    if (linesCleared >= band.minLines) {
      return band.gravityMs;
    }
  }

  return 650;
}
