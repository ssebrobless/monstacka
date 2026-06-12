export function createDitherPattern(): string {
  const bayer4 = [
    [0 / 16, 8 / 16, 2 / 16, 10 / 16],
    [12 / 16, 4 / 16, 14 / 16, 6 / 16],
    [3 / 16, 11 / 16, 1 / 16, 9 / 16],
    [15 / 16, 7 / 16, 13 / 16, 5 / 16],
  ];
  const size = 4;
  const scale = 2;
  const canvas = document.createElement('canvas');
  canvas.width = size * scale;
  canvas.height = size * scale;
  const ctx = canvas.getContext('2d')!;
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const alpha = bayer4[y][x] * 0.35;
      ctx.fillStyle = `rgba(0, 0, 0, ${alpha})`;
      ctx.fillRect(x * scale, y * scale, scale, scale);
    }
  }
  return canvas.toDataURL('image/png');
}

export function applyDitherOverlay(overlay: HTMLDivElement, enabled: boolean): void {
  if (enabled) {
    if (!overlay.style.backgroundImage) {
      overlay.style.backgroundImage = `url(${createDitherPattern()})`;
    }
    overlay.classList.remove('hidden');
  } else {
    overlay.classList.add('hidden');
  }
}
