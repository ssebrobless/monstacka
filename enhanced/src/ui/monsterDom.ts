import { DEFINITIONS } from '../constants';
import { getMonsterFigureBoxSize, getMonsterFigureCanvas, getMonsterTile } from '../monsterSkin';
import type { MonsterEye } from '../monsterSkin';
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
  fillRatio?: number;
}

function addClassNames(target: HTMLElement, classNames?: string): void {
  if (!classNames) return;
  const tokens = classNames.split(/\s+/).map((token) => token.trim()).filter(Boolean);
  if (tokens.length) {
    target.classList.add(...tokens);
  }
}

function createEyeNode(
  eyeSpec: MonsterEye,
  blinkAmount: number,
  lookX: number,
  lookY: number,
): HTMLElement {
  const eye = document.createElement('span');
  eye.className = 'monster-eye';
  eye.style.setProperty('--eye-x', `${Math.round(eyeSpec.x * 100)}%`);
  eye.style.setProperty('--eye-y', `${Math.round(eyeSpec.y * 100)}%`);
  eye.style.setProperty('--eye-size', `${eyeSpec.regionScale.toFixed(3)}`);
  eye.style.setProperty('--blink', `${blinkAmount}`);
  eye.style.setProperty('--look-x', `${lookX.toFixed(3)}`);
  eye.style.setProperty('--look-y', `${lookY.toFixed(3)}`);

  const socket = cloneCanvas(eyeSpec.socketCanvas, 'monster-eye-socket');
  eye.appendChild(socket);

  const pupil = cloneCanvas(eyeSpec.pupilCanvas, 'monster-eye-pupil');
  eye.appendChild(pupil);

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

function cloneCanvas(source: HTMLCanvasElement, className: string): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.className = className;
  canvas.width = source.width;
  canvas.height = source.height;
  const ctx = canvas.getContext('2d')!;
  ctx.drawImage(source, 0, 0);
  return canvas;
}

function cropMonsterFigureCanvas(
  pieceType: PieceType,
  rotation: number,
  minX: number,
  minY: number,
  widthCells: number,
  heightCells: number,
): HTMLCanvasElement | null {
  const source = getMonsterFigureCanvas(pieceType, rotation);
  if (!source) {
    return null;
  }

  const boxSize = getMonsterFigureBoxSize(pieceType);
  const cellPx = source.width / boxSize;
  const canvas = document.createElement('canvas');
  canvas.className = 'monster-art';
  canvas.width = Math.round(widthCells * cellPx);
  canvas.height = Math.round(heightCells * cellPx);
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    return null;
  }
  ctx.drawImage(
    source,
    minX * cellPx,
    minY * cellPx,
    widthCells * cellPx,
    heightCells * cellPx,
    0,
    0,
    canvas.width,
    canvas.height,
  );
  return canvas;
}

function createTongueNode(
  source: HTMLCanvasElement,
  x: number,
  y: number,
  width: number,
  height: number,
  sway: number,
): HTMLElement {
  const tongue = document.createElement('div');
  tongue.className = 'monster-tongue';
  tongue.style.setProperty('--tongue-x', `${(x * 100).toFixed(2)}%`);
  tongue.style.setProperty('--tongue-y', `${(y * 100).toFixed(2)}%`);
  tongue.style.setProperty('--tongue-width', `${(width * 100).toFixed(2)}%`);
  tongue.style.setProperty('--tongue-height', `${(height * 100).toFixed(2)}%`);
  tongue.style.setProperty('--tongue-sway', `${sway.toFixed(3)}`);
  tongue.appendChild(cloneCanvas(source, 'monster-tongue-art'));
  return tongue;
}

function createMonsterBodyNode(
  pieceType: string,
  animate: boolean,
  scaleX: number,
  scaleY: number,
  shiftX: number,
  shiftY: number,
  motionSeed: number,
): { body: HTMLDivElement; motion: HTMLDivElement } {
  const body = document.createElement('div');
  body.className = 'monster-body';
  body.classList.toggle('is-animated', animate);
  body.classList.add(`piece-${pieceType.toLowerCase()}`);
  body.style.setProperty('--squish-scale-x', `${scaleX}`);
  body.style.setProperty('--squish-scale-y', `${scaleY}`);
  body.style.setProperty('--squish-shift-x', `${shiftX}`);
  body.style.setProperty('--squish-shift-y', `${shiftY}`);

  const motion = document.createElement('div');
  motion.className = 'monster-motion';
  const driftX = 0;
  const driftY = 0;
  const driftTilt = 0;
  motion.style.setProperty('--motion-x', `${driftX.toFixed(3)}px`);
  motion.style.setProperty('--motion-y', `${driftY.toFixed(3)}px`);
  motion.style.setProperty('--motion-tilt', `${driftTilt.toFixed(3)}deg`);
  motion.style.setProperty('--motion-delay', `${((motionSeed % 9) * -0.23).toFixed(2)}s`);
  body.appendChild(motion);

  return { body, motion };
}

function blinkAmount(now: number, seed: number): number {
  const period = 1580 + (seed % 4) * 220;
  const phase = (now + seed * 173) % period;
  const blinkWindow = 300;
  if (phase > period - blinkWindow) {
    const t = (phase - (period - blinkWindow)) / blinkWindow;
    if (t < 0.22) {
      return t / 0.22;
    }
    if (t < 0.62) {
      return 1;
    }
    return 1 - ((t - 0.62) / 0.38);
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
  const scaleX = allowSquish && (occupiedNeighbors.left || occupiedNeighbors.right) ? 0.016 : 0;
  const scaleY = allowSquish && (occupiedNeighbors.up || occupiedNeighbors.down) ? 0.012 : 0;
  const shiftX = 0;
  const shiftY = 0;

  cell.classList.add('monster-cell', `piece-${pieceType.toLowerCase()}`);
  const motionSeed = [...skinKey].reduce((total, char) => total + char.charCodeAt(0), 0);
  const { body, motion } = createMonsterBodyNode(pieceType, animate, scaleX, scaleY, shiftX, shiftY, motionSeed);
  motion.appendChild(createMonsterArtNode(tile.canvas));

  for (const eye of tile.eyes) {
    const blink = animate && eye.blink ? blinkAmount(options.now, eye.seed) : 0;
    const reactiveX = animate
      ? (options.lookX || 0) + Math.sin((options.now + eye.seed * 41) / 760) * 0.3
      : options.lookX || 0;
    const reactiveY = animate
      ? (options.lookY || 0) + Math.cos((options.now + eye.seed * 61) / 930) * 0.22
      : options.lookY || 0;
    motion.appendChild(createEyeNode(eye, blink, reactiveX, reactiveY));
  }

  if (tile.tongue) {
    const sway = animate ? Math.sin((options.now + tile.tongue.seed * 97) / 460) * 0.24 : 0;
    motion.appendChild(
      createTongueNode(tile.canvas, tile.tongue.x, tile.tongue.y, tile.tongue.width, tile.tongue.height, sway),
    );
  }

  cell.appendChild(body);
}

export function populateMonsterFigure(
  container: HTMLElement,
  pieceType: PieceType,
  options: MonsterFigureOptions,
): void {
  const rotation = options.rotation ?? 0;
  const definition = DEFINITIONS[pieceType][rotation];
  const layout = options.layout ?? 'grid';
  const minX = Math.min(...definition.map((cell) => cell.x));
  const maxX = Math.max(...definition.map((cell) => cell.x));
  const minY = Math.min(...definition.map((cell) => cell.y));
  const maxY = Math.max(...definition.map((cell) => cell.y));
  const widthCells = maxX - minX + 1;
  const heightCells = maxY - minY + 1;

  container.replaceChildren();
  container.classList.toggle('monster-figure-absolute', layout === 'absolute');

  if (layout === 'absolute') {
    const frame = document.createElement('div');
    frame.className = 'monster-figure-frame';
    frame.style.width = `${(widthCells / 4) * 100}%`;
    frame.style.height = `${(heightCells / 4) * 100}%`;
    const dominantSpan = Math.max(widthCells / 4, heightCells / 4);
    const fillRatio = options.fillRatio ?? 0.82;
    frame.style.setProperty('--figure-scale', `${(fillRatio / dominantSpan).toFixed(3)}`);
    const figureArt = cropMonsterFigureCanvas(pieceType, rotation, minX, minY, widthCells, heightCells);

    if (figureArt) {
      const motionSeed = pieceType.charCodeAt(0) + rotation * 37;
      const { body, motion } = createMonsterBodyNode(pieceType, options.animate ?? false, 0, 0, 0, 0, motionSeed);
      body.classList.add('monster-figure-body');
      motion.appendChild(createMonsterArtNode(figureArt));

      for (const [index, cellDef] of definition.entries()) {
        const tile = getMonsterTile(`${pieceType}:${rotation}:${index}`);
        if (!tile || (!tile.eyes.length && !tile.tongue)) {
          continue;
        }

        const overlay = document.createElement('div');
        overlay.className = 'monster-figure-overlay';
        overlay.style.left = `${((cellDef.x - minX) / widthCells) * 100}%`;
        overlay.style.top = `${((cellDef.y - minY) / heightCells) * 100}%`;
        overlay.style.width = `${100 / widthCells}%`;
        overlay.style.height = `${100 / heightCells}%`;

        for (const eye of tile.eyes) {
          const blink = (options.animate ?? false) && eye.blink ? blinkAmount(options.now, eye.seed) : 0;
          const reactiveX = options.animate
            ? (options.lookX || 0) + Math.sin((options.now + eye.seed * 41) / 760) * 0.3
            : options.lookX || 0;
          const reactiveY = options.animate
            ? (options.lookY || 0) + Math.cos((options.now + eye.seed * 61) / 930) * 0.22
            : options.lookY || 0;
          overlay.appendChild(createEyeNode(eye, blink, reactiveX, reactiveY));
        }

        if (tile.tongue) {
          const sway = options.animate ? Math.sin((options.now + tile.tongue.seed * 97) / 460) * 0.24 : 0;
          overlay.appendChild(
            createTongueNode(tile.canvas, tile.tongue.x, tile.tongue.y, tile.tongue.width, tile.tongue.height, sway),
          );
        }

        motion.appendChild(overlay);
      }

      frame.appendChild(body);
    } else {
      for (const [index, cellDef] of definition.entries()) {
        const cell = document.createElement('div');
        cell.className = options.cellClassName || 'preview-cell';
        addClassNames(cell, options.filledClassName || 'filled');
        cell.style.position = 'absolute';
        cell.style.left = `${((cellDef.x - minX) / widthCells) * 100}%`;
        cell.style.top = `${((cellDef.y - minY) / heightCells) * 100}%`;
        cell.style.width = `${100 / widthCells}%`;
        cell.style.height = `${100 / heightCells}%`;

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

        frame.appendChild(cell);
      }
    }

    container.appendChild(frame);

    return;
  }

  for (let row = 0; row < 4; row += 1) {
    for (let col = 0; col < 4; col += 1) {
      const cell = document.createElement('div');
      cell.className = options.cellClassName || 'preview-cell';
      const index = definition.findIndex(({ x, y }) => x === col && y === row);

      if (index !== -1) {
        addClassNames(cell, options.filledClassName || 'filled');
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
  options: Pick<MonsterFigureOptions, 'rotation' | 'now' | 'lookX' | 'lookY' | 'animate' | 'fillRatio'>,
): void {
  populateMonsterFigure(container, pieceType, {
    rotation: options.rotation,
    now: options.now,
    lookX: options.lookX,
    lookY: options.lookY,
    animate: options.animate,
    fillRatio: options.fillRatio,
    cellClassName: 'preview-cell',
    filledClassName: 'filled monster-preview',
    layout: 'absolute',
  });
}
