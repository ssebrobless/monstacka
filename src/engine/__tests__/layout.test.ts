import { describe, expect, it } from 'vitest';
import { ARTBOARD_HEIGHT, ARTBOARD_WIDTH, computeArtboardScale, computeBoardFit } from '../../layout';

describe('layout invariants', () => {
  it('scales the fixed artboard uniformly to fit the window', () => {
    const scale = computeArtboardScale(1600, 900);
    expect(scale).toBeCloseTo(Math.min((1600 - 20) / ARTBOARD_WIDTH, (900 - 20) / ARTBOARD_HEIGHT), 6);
  });

  it('continues shrinking for very small windows instead of clamping to a large minimum', () => {
    const scale = computeArtboardScale(300, 200);
    expect(scale).toBeCloseTo(Math.min((300 - 20) / ARTBOARD_WIDTH, (200 - 20) / ARTBOARD_HEIGHT), 6);
    expect(scale).toBeLessThan(0.2);
  });

  it('never changes internal layout math when window shape changes', () => {
    const wideScale = computeArtboardScale(1920, 1080);
    const narrowScale = computeArtboardScale(1280, 720);
    expect(wideScale).not.toBe(narrowScale);
    expect(ARTBOARD_WIDTH).toBe(1920);
    expect(ARTBOARD_HEIGHT).toBe(1080);
  });

  it('fits the board inside its fixed zone while preserving a 1:2 ratio', () => {
    const fit = computeBoardFit(600, 960);
    expect(fit).not.toBeNull();
    expect(fit!.width).toBe(480);
    expect(fit!.height).toBe(960);
  });

  it('returns null when the fixed board zone has no size yet', () => {
    expect(computeBoardFit(0, 960)).toBeNull();
    expect(computeBoardFit(600, 0)).toBeNull();
  });
});
