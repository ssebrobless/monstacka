export const ARTBOARD_WIDTH = 1920;
export const ARTBOARD_HEIGHT = 1080;
export const STAGE_PADDING = 20;

export function computeArtboardScale(windowWidth: number, windowHeight: number): number {
  const availableWidth = Math.max(1, windowWidth - STAGE_PADDING);
  const availableHeight = Math.max(1, windowHeight - STAGE_PADDING);
  return Math.max(0.001, Math.min(availableWidth / ARTBOARD_WIDTH, availableHeight / ARTBOARD_HEIGHT));
}

export function computeBoardFit(zoneWidth: number, zoneHeight: number): { width: number; height: number } | null {
  if (!zoneWidth || !zoneHeight) {
    return null;
  }

  const width = Math.min(zoneWidth, zoneHeight / 2);
  return {
    width,
    height: width * 2,
  };
}
