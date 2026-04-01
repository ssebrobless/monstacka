import { DEFINITIONS } from '../constants';
import {
  getMonsterEyeFrame,
  getMonsterFigureBoxSize,
  getMonsterFigureCanvas,
  getMonsterFigureEyes,
  getMonsterTile,
} from '../monsterSkin';
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

interface MonsterBoardFigureOptions {
  now: number;
  animate?: boolean;
}

function addClassNames(target: HTMLElement, classNames?: string): void {
  if (!classNames) return;
  const tokens = classNames.split(/\s+/).map((token) => token.trim()).filter(Boolean);
  if (tokens.length) {
    target.classList.add(...tokens);
  }
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

function createMonsterEdgeOutlineNode(source: HTMLCanvasElement): HTMLCanvasElement | null {
  const sourceCtx = source.getContext('2d', { willReadFrequently: true });
  if (!sourceCtx) {
    return null;
  }

  const imageData = sourceCtx.getImageData(0, 0, source.width, source.height);
  const alpha = new Uint8Array(source.width * source.height);
  const edge = new Uint8Array(source.width * source.height);
  const expanded = new Uint8Array(source.width * source.height);

  for (let y = 0; y < source.height; y += 1) {
    for (let x = 0; x < source.width; x += 1) {
      const index = y * source.width + x;
      alpha[index] = imageData.data[index * 4 + 3] > 8 ? 1 : 0;
    }
  }

  for (let y = 0; y < source.height; y += 1) {
    for (let x = 0; x < source.width; x += 1) {
      const index = y * source.width + x;
      if (!alpha[index]) {
        continue;
      }

      let isEdge = false;
      for (let offsetY = -1; offsetY <= 1 && !isEdge; offsetY += 1) {
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          if (!offsetX && !offsetY) {
            continue;
          }
          const sampleX = x + offsetX;
          const sampleY = y + offsetY;
          if (
            sampleX < 0 ||
            sampleY < 0 ||
            sampleX >= source.width ||
            sampleY >= source.height ||
            !alpha[sampleY * source.width + sampleX]
          ) {
            isEdge = true;
            break;
          }
        }
      }

      if (isEdge) {
        edge[index] = 1;
      }
    }
  }

  for (let y = 0; y < source.height; y += 1) {
    for (let x = 0; x < source.width; x += 1) {
      const index = y * source.width + x;
      if (!edge[index]) {
        continue;
      }

      for (let offsetY = -1; offsetY <= 1; offsetY += 1) {
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          const sampleX = x + offsetX;
          const sampleY = y + offsetY;
          if (
            sampleX < 0 ||
            sampleY < 0 ||
            sampleX >= source.width ||
            sampleY >= source.height
          ) {
            continue;
          }

          const sampleIndex = sampleY * source.width + sampleX;
          if (alpha[sampleIndex]) {
            expanded[sampleIndex] = 1;
          }
        }
      }
    }
  }

  const canvas = document.createElement('canvas');
  canvas.className = 'monster-ripple-art';
  canvas.width = source.width;
  canvas.height = source.height;
  const ctx = canvas.getContext('2d')!;
  const output = ctx.createImageData(source.width, source.height);
  let visiblePixels = 0;

  for (let index = 0; index < expanded.length; index += 1) {
    if (!expanded[index]) {
      continue;
    }
    visiblePixels += 1;
    const sourceOffset = index * 4;
    output.data[sourceOffset] = imageData.data[sourceOffset];
    output.data[sourceOffset + 1] = imageData.data[sourceOffset + 1];
    output.data[sourceOffset + 2] = imageData.data[sourceOffset + 2];
    output.data[sourceOffset + 3] = imageData.data[sourceOffset + 3];
  }

  if (!visiblePixels) {
    return null;
  }

  ctx.putImageData(output, 0, 0);
  return canvas;
}

function createMonsterOverlayNode(
  source: HTMLCanvasElement,
  x: number,
  y: number,
  width: number,
  height: number,
  hostWidth: number,
  hostHeight: number,
): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.className = 'monster-eye-overlay';
  canvas.width = source.width;
  canvas.height = source.height;
  const ctx = canvas.getContext('2d')!;
  ctx.drawImage(source, 0, 0);
  canvas.style.left = `${(x / hostWidth) * 100}%`;
  canvas.style.top = `${(y / hostHeight) * 100}%`;
  canvas.style.width = `${(width / hostWidth) * 100}%`;
  canvas.style.height = `${(height / hostHeight) * 100}%`;
  return canvas;
}

function cropMonsterFigureCanvas(
  pieceType: PieceType,
  rotation: number,
  minX: number,
  minY: number,
  widthCells: number,
  heightCells: number,
  now: number,
  animate: boolean,
): HTMLCanvasElement | null {
  const source = getMonsterFigureCanvas(pieceType, rotation, now, animate);
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

function getFigureCropBounds(pieceType: PieceType, rotation: number): {
  minX: number;
  minY: number;
  widthCells: number;
  heightCells: number;
} {
  const definition = DEFINITIONS[pieceType][rotation];
  const minX = Math.min(...definition.map((cell) => cell.x));
  const maxX = Math.max(...definition.map((cell) => cell.x));
  const minY = Math.min(...definition.map((cell) => cell.y));
  const maxY = Math.max(...definition.map((cell) => cell.y));

  return {
    minX,
    minY,
    widthCells: maxX - minX + 1,
    heightCells: maxY - minY + 1,
  };
}

function createMonsterBodyNode(
  pieceType: string,
  animate: boolean,
  scaleX: number,
  scaleY: number,
  shiftX: number,
  shiftY: number,
  motionSeed: number,
): {
  body: HTMLDivElement;
  motion: HTMLDivElement;
  artLayer: HTMLDivElement;
  rippleLayer: HTMLDivElement;
} {
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
  const artLayer = document.createElement('div');
  artLayer.className = 'monster-art-layer';
  const rippleLayer = document.createElement('div');
  rippleLayer.className = 'monster-ripple-layer';
  motion.appendChild(artLayer);
  motion.appendChild(rippleLayer);
  body.appendChild(motion);

  return { body, motion, artLayer, rippleLayer };
}

export function populateMonsterCell(
  cell: HTMLElement,
  skinKey: string,
  occupiedNeighbors: OccupiedNeighbors,
  options: MonsterCellOptions,
): void {
  const animate = options.animate ?? true;
  const tile = getMonsterTile(skinKey, options.now, animate);
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
  const { body, artLayer, rippleLayer } = createMonsterBodyNode(
    pieceType,
    animate,
    scaleX,
    scaleY,
    shiftX,
    shiftY,
    motionSeed,
  );
  artLayer.appendChild(createMonsterArtNode(tile.canvas));
  const outlineNode = createMonsterEdgeOutlineNode(tile.canvas);
  if (outlineNode) {
    rippleLayer.appendChild(outlineNode);
  }
  for (const eye of tile.eyes) {
    const eyeFrame = getMonsterEyeFrame(eye, options.now, animate);
    if (!eyeFrame) {
      continue;
    }
    artLayer.appendChild(
      createMonsterOverlayNode(
        eyeFrame,
        eye.x,
        eye.y,
        eye.width,
        eye.height,
        tile.canvas.width,
        tile.canvas.height,
      ),
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
    const figureArt = cropMonsterFigureCanvas(
      pieceType,
      rotation,
      minX,
      minY,
      widthCells,
      heightCells,
      options.now,
      options.animate ?? false,
    );

    if (figureArt) {
      const motionSeed = pieceType.charCodeAt(0) + rotation * 37;
      const { body, artLayer, rippleLayer } = createMonsterBodyNode(
        pieceType,
        options.animate ?? false,
        0,
        0,
        0,
        0,
        motionSeed,
      );
      body.classList.add('monster-figure-body');
      artLayer.appendChild(createMonsterArtNode(figureArt));
      const outlineNode = createMonsterEdgeOutlineNode(figureArt);
      if (outlineNode) {
        rippleLayer.appendChild(outlineNode);
      }
      const cellPx = figureArt.width / widthCells;
      const cropOffsetX = minX * cellPx;
      const cropOffsetY = minY * cellPx;
      for (const eye of getMonsterFigureEyes(pieceType, rotation)) {
        const eyeFrame = getMonsterEyeFrame(eye, options.now, options.animate ?? false);
        if (!eyeFrame) {
          continue;
        }
        const localX = eye.x - cropOffsetX;
        const localY = eye.y - cropOffsetY;
        if (
          localX + eye.width <= 0 ||
          localY + eye.height <= 0 ||
          localX >= figureArt.width ||
          localY >= figureArt.height
        ) {
          continue;
        }
        artLayer.appendChild(
          createMonsterOverlayNode(
            eyeFrame,
            localX,
            localY,
            eye.width,
            eye.height,
            figureArt.width,
            figureArt.height,
          ),
        );
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

export function populateMonsterBoardFigure(
  container: HTMLElement,
  pieceType: PieceType,
  rotation: number,
  options: MonsterBoardFigureOptions,
): void {
  const { minX, minY, widthCells, heightCells } = getFigureCropBounds(pieceType, rotation);
  const figureArt = cropMonsterFigureCanvas(
    pieceType,
    rotation,
    minX,
    minY,
    widthCells,
    heightCells,
    options.now,
    options.animate ?? true,
  );

  container.replaceChildren();

  if (!figureArt) {
    return;
  }

  const motionSeed = pieceType.charCodeAt(0) + rotation * 37;
  const { body, artLayer, rippleLayer } = createMonsterBodyNode(
    pieceType,
    options.animate ?? true,
    0,
    0,
    0,
    0,
    motionSeed,
  );
  body.classList.add('monster-figure-body');
  artLayer.appendChild(createMonsterArtNode(figureArt));
  const outlineNode = createMonsterEdgeOutlineNode(figureArt);
  if (outlineNode) {
    rippleLayer.appendChild(outlineNode);
  }

  const cellPx = figureArt.width / widthCells;
  const cropOffsetX = minX * cellPx;
  const cropOffsetY = minY * cellPx;
  for (const eye of getMonsterFigureEyes(pieceType, rotation)) {
    const eyeFrame = getMonsterEyeFrame(eye, options.now, options.animate ?? true);
    if (!eyeFrame) {
      continue;
    }
    const localX = eye.x - cropOffsetX;
    const localY = eye.y - cropOffsetY;
    if (
      localX + eye.width <= 0 ||
      localY + eye.height <= 0 ||
      localX >= figureArt.width ||
      localY >= figureArt.height
    ) {
      continue;
    }
    artLayer.appendChild(
      createMonsterOverlayNode(
        eyeFrame,
        localX,
        localY,
        eye.width,
        eye.height,
        figureArt.width,
        figureArt.height,
      ),
    );
  }

  container.appendChild(body);
}
