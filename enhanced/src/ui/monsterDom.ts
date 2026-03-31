import { DEFINITIONS } from '../constants';
import { getMonsterTile } from '../monsterSkin';
import type { PieceType } from '../types';

interface OccupiedNeighbors {
  left: boolean;
  right: boolean;
  up: boolean;
  down: boolean;
}

interface MonsterCellOptions {
  now: number;
  lookX?: number;
  lookY?: number;
  animate?: boolean;
  allowSquish?: boolean;
  baseClassName?: string;
}

interface MonsterFigureOptions {
  rotation?: number;
  now: number;
  lookX?: number;
  lookY?: number;
  animate?: boolean;
  cellClassName?: string;
  filledClassName?: string;
  layout?: 'grid' | 'absolute';
}

function createEyeNode(
  x: number,
  y: number,
  size: number,
  blinkAmount: number,
  lookX: number,
  lookY: number,
): HTMLElement {
  const eye = document.createElement('span');
  eye.className = 'monster-eye';
  eye.style.setProperty('--eye-x', `${Math.round(x * 100)}%`);
  eye.style.setProperty('--eye-y', `${Math.round(y * 100)}%`);
  eye.style.setProperty('--eye-size', `${size}`);
  eye.style.setProperty('--blink', `${blinkAmount}`);
  eye.style.setProperty('--look-x', `${lookX.toFixed(3)}`);
  eye.style.setProperty('--look-y', `${lookY.toFixed(3)}`);
  return eye;
}

function createMonsterArtNode(source: HTMLCanvasElement): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.className = 'monster-art';
  canvas.width = source.width;
  canvas.height = source.height;
  const ctx = canvas.getContext('2d')!;
  ctx.drawImage(source, 0, 0);
  return canvas;
}

function createTongueNode(
  x: number,
  y: number,
  width: number,
  height: number,
  sway: number,
): HTMLElement {
  const tongue = document.createElement('span');
  tongue.className = 'monster-tongue';
  tongue.style.setProperty('--tongue-x', `${Math.round(x * 100)}%`);
  tongue.style.setProperty('--tongue-y', `${Math.round(y * 100)}%`);
  tongue.style.setProperty('--tongue-width', `${width}`);
  tongue.style.setProperty('--tongue-height', `${height}`);
  tongue.style.setProperty('--tongue-sway', `${sway.toFixed(3)}`);
  return tongue;
}

function blinkAmount(now: number, seed: number): number {
  const period = 3200 + (seed % 4) * 540;
  const phase = (now + seed * 173) % period;
  if (phase > period - 280) {
    const t = (phase - (period - 280)) / 280;
    if (t < 0.35) {
      return t / 0.35;
    }
    if (t < 0.65) {
      return 1;
    }
    return 1 - ((t - 0.65) / 0.35);
  }
  return 0;
}

export function populateMonsterCell(
  cell: HTMLElement,
  skinKey: string,
  occupiedNeighbors: OccupiedNeighbors,
  options: MonsterCellOptions,
): void {
  const tile = getMonsterTile(skinKey);
  const animate = options.animate ?? true;
  const allowSquish = options.allowSquish ?? animate;
  const baseClassName = options.baseClassName || 'cell';

  cell.replaceChildren();
  cell.className = baseClassName;
  cell.style.removeProperty('--squish-scale-x');
  cell.style.removeProperty('--squish-scale-y');
  cell.style.removeProperty('--squish-shift-x');
  cell.style.removeProperty('--squish-shift-y');

  if (!tile) {
    const [pieceType] = skinKey.split(':');
    cell.classList.add(`piece-${pieceType.toLowerCase()}`);
    return;
  }

  const [pieceType] = skinKey.split(':');
  const scaleX = allowSquish && (occupiedNeighbors.left || occupiedNeighbors.right) ? 0.05 : 0;
  const scaleY = allowSquish && (occupiedNeighbors.up || occupiedNeighbors.down) ? 0.03 : 0;
  const shiftX = allowSquish && occupiedNeighbors.left && !occupiedNeighbors.right
    ? 0.02
    : allowSquish && occupiedNeighbors.right && !occupiedNeighbors.left
      ? -0.02
      : 0;
  const shiftY = allowSquish && occupiedNeighbors.up && !occupiedNeighbors.down
    ? 0.01
    : allowSquish && occupiedNeighbors.down && !occupiedNeighbors.up
      ? -0.02
      : 0;

  cell.classList.add('monster-cell', `piece-${pieceType.toLowerCase()}`);
  cell.classList.toggle('is-animated', animate);
  cell.style.setProperty('--squish-scale-x', `${scaleX}`);
  cell.style.setProperty('--squish-scale-y', `${scaleY}`);
  cell.style.setProperty('--squish-shift-x', `${shiftX}`);
  cell.style.setProperty('--squish-shift-y', `${shiftY}`);
  cell.appendChild(createMonsterArtNode(tile.canvas));

  for (const eye of tile.eyes) {
    const blink = animate && eye.blink ? blinkAmount(options.now, eye.seed) : 0;
    const reactiveX = animate
      ? (options.lookX || 0) + Math.sin((options.now + eye.seed * 41) / 1100) * 0.08
      : options.lookX || 0;
    const reactiveY = animate
      ? (options.lookY || 0) + Math.cos((options.now + eye.seed * 61) / 1400) * 0.05
      : options.lookY || 0;
    cell.appendChild(createEyeNode(eye.x, eye.y, eye.size, blink, reactiveX, reactiveY));
  }

  if (tile.tongue) {
    const sway = animate ? Math.sin((options.now + tile.tongue.seed * 97) / 520) * 0.16 : 0;
    cell.appendChild(createTongueNode(tile.tongue.x, tile.tongue.y, tile.tongue.width, tile.tongue.height, sway));
  }
}

export function populateMonsterFigure(
  container: HTMLElement,
  pieceType: PieceType,
  options: MonsterFigureOptions,
): void {
  const rotation = options.rotation ?? 0;
  const definition = DEFINITIONS[pieceType][rotation];
  const layout = options.layout ?? 'grid';

  container.replaceChildren();
  container.classList.toggle('monster-figure-absolute', layout === 'absolute');

  if (layout === 'absolute') {
    for (const [index, cellDef] of definition.entries()) {
      const cell = document.createElement('div');
      cell.className = options.cellClassName || 'preview-cell';
      cell.classList.add(options.filledClassName || 'filled');
      cell.style.position = 'absolute';
      cell.style.left = `${cellDef.x * 25}%`;
      cell.style.top = `${cellDef.y * 25}%`;
      cell.style.width = '25%';
      cell.style.height = '25%';

      const occupiedNeighbors = {
        left: definition.some(({ x, y }) => x === cellDef.x - 1 && y === cellDef.y),
        right: definition.some(({ x, y }) => x === cellDef.x + 1 && y === cellDef.y),
        up: definition.some(({ x, y }) => x === cellDef.x && y === cellDef.y - 1),
        down: definition.some(({ x, y }) => x === cellDef.x && y === cellDef.y + 1),
      };

      populateMonsterCell(
        cell,
        `${pieceType}:${rotation}:${index}`,
        occupiedNeighbors,
        {
          now: options.now,
          lookX: options.lookX,
          lookY: options.lookY,
          animate: options.animate,
          allowSquish: options.animate,
          baseClassName: cell.className,
        },
      );

      container.appendChild(cell);
    }

    return;
  }

  for (let row = 0; row < 4; row += 1) {
    for (let col = 0; col < 4; col += 1) {
      const cell = document.createElement('div');
      cell.className = options.cellClassName || 'preview-cell';
      const index = definition.findIndex(({ x, y }) => x === col && y === row);

      if (index !== -1) {
        cell.classList.add(options.filledClassName || 'filled');
        const occupiedNeighbors = {
          left: definition.some(({ x, y }) => x === col - 1 && y === row),
          right: definition.some(({ x, y }) => x === col + 1 && y === row),
          up: definition.some(({ x, y }) => x === col && y === row - 1),
          down: definition.some(({ x, y }) => x === col && y === row + 1),
        };
        populateMonsterCell(
          cell,
          `${pieceType}:${rotation}:${index}`,
          occupiedNeighbors,
          {
            now: options.now,
            lookX: options.lookX,
            lookY: options.lookY,
            animate: options.animate,
            allowSquish: options.animate,
            baseClassName: cell.className,
          },
        );
      }

      container.appendChild(cell);
    }
  }
}

export function populateMonsterPreviewFigure(
  container: HTMLElement,
  pieceType: PieceType,
  options: Pick<MonsterFigureOptions, 'rotation' | 'now' | 'lookX' | 'lookY' | 'animate'>,
): void {
  populateMonsterFigure(container, pieceType, {
    rotation: options.rotation,
    now: options.now,
    lookX: options.lookX,
    lookY: options.lookY,
    animate: options.animate,
    cellClassName: 'preview-cell',
    filledClassName: 'filled monster-preview',
    layout: 'absolute',
  });
}
