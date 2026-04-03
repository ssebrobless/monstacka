export interface HitRegion {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

export function applyRegionMap(root: ParentNode, regions: readonly HitRegion[]): void {
  for (const region of regions) {
    const elements = root.querySelectorAll<HTMLElement>(`[data-region="${region.id}"]`);
    if (!elements.length) continue;

    for (const el of elements) {
      el.style.left = `${region.x}px`;
      el.style.top = `${region.y}px`;
      el.style.width = `${region.width}px`;
      el.style.height = `${region.height}px`;
    }
  }
}

export const HOME_REGIONS: readonly HitRegion[] = [
  { id: 'home-settings', x: 1724, y: 10, width: 145, height: 108 },
  { id: 'home-quit', x: 1720, y: 118, width: 150, height: 118 },
  { id: 'home-name-fill', x: 152, y: 427, width: 328, height: 73 },
  { id: 'home-voice', x: 500, y: 454, width: 72, height: 72 },
  { id: 'home-lore', x: 590, y: 484, width: 82, height: 82 },
  { id: 'home-lore-fill', x: 604, y: 336, width: 612, height: 212 },
  { id: 'home-preview-left', x: 42, y: 752, width: 315, height: 257 },
  { id: 'home-preview-center', x: 230, y: 531, width: 522, height: 445 },
  { id: 'home-preview-right', x: 609, y: 782, width: 353, height: 266 },
  { id: 'home-prev', x: 123, y: 683, width: 261, height: 164 },
  { id: 'home-next', x: 588, y: 684, width: 276, height: 164 },
  { id: 'home-score-sprint', x: 970, y: 706, width: 128, height: 58 },
  { id: 'home-score-arcade', x: 1117, y: 706, width: 136, height: 58 },
  { id: 'home-score-grid', x: 985, y: 804, width: 252, height: 222 },
  { id: 'home-play-arcade', x: 1386, y: 617, width: 407, height: 121 },
  { id: 'home-play-sprint', x: 1386, y: 763, width: 407, height: 121 },
  { id: 'home-play-training', x: 1386, y: 907, width: 407, height: 121 },
] as const;

export const GAME_REGIONS: readonly HitRegion[] = [
  { id: 'game-settings', x: 1780, y: 11, width: 100, height: 89 },
  { id: 'game-quit', x: 1768, y: 86, width: 116, height: 105 },
  { id: 'game-home', x: 1784, y: 180, width: 96, height: 93 },
  { id: 'game-board-zone', x: 635, y: 20, width: 650, height: 1040 },
  { id: 'game-primary-zone', x: 110, y: 170, width: 470, height: 730 },
  { id: 'game-secondary-zone', x: 1300, y: 170, width: 500, height: 730 },
] as const;
