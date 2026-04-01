import monsterSheetUrl from './assets/monster-sheet.png';
import { DEFINITIONS } from './constants';
import type { PieceType } from './types';

interface Bounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface EyeSeed {
  cellIndex: number;
  x: number;
  y: number;
  size: number;
  pupilScale?: number;
}

interface TongueSeed {
  cellIndex: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

interface PieceArtSpec {
  bounds: Bounds;
  baseRotation: number;
  boxSize: number;
  eyes?: EyeSeed[];
  tongue?: TongueSeed;
}

export interface MonsterEye {
  x: number;
  y: number;
  size: number;
  regionScale: number;
  socketCanvas: HTMLCanvasElement;
  pupilCanvas: HTMLCanvasElement;
  blink: boolean;
  seed: number;
}

export interface MonsterTongue {
  x: number;
  y: number;
  width: number;
  height: number;
  seed: number;
}

export interface MonsterTile {
  canvas: HTMLCanvasElement;
  eyes: MonsterEye[];
  tongue: MonsterTongue | null;
  blinkFamily: 'red' | 'pink' | 'orange' | 'none';
}

const TILE_SIZE = 112;
// The custom art sheet maps S=red, Z=green, J=pink, and L=orange.
const BLINKING_PIECES = new Set<PieceType>(['S', 'J', 'L']);

const MONSTER_SPECS: Record<PieceType, PieceArtSpec> = {
  I: {
    bounds: { x: 520, y: 120, width: 151, height: 572 },
    baseRotation: 1,
    boxSize: 4,
    eyes: [
      { cellIndex: 0, x: 0.74, y: 0.22, size: 0.28, pupilScale: 0.24 },
      { cellIndex: 1, x: 0.49, y: 0.42, size: 0.36, pupilScale: 0.22 },
      { cellIndex: 3, x: 0.5, y: 0.68, size: 0.34, pupilScale: 0.22 },
    ],
  },
  O: {
    bounds: { x: 40, y: 360, width: 304, height: 292 },
    baseRotation: 0,
    boxSize: 4,
  },
  T: {
    bounds: { x: 860, y: 340, width: 432, height: 291 },
    baseRotation: 2,
    boxSize: 3,
    eyes: [
      { cellIndex: 0, x: 0.18, y: 0.28, size: 0.14, pupilScale: 0.24 },
      { cellIndex: 2, x: 0.79, y: 0.28, size: 0.14, pupilScale: 0.24 },
    ],
    tongue: { cellIndex: 3, x: 0.5, y: 0.52, width: 0.36, height: 0.54 },
  },
  S: {
    bounds: { x: 40, y: 20, width: 430, height: 289 },
    baseRotation: 0,
    boxSize: 3,
    eyes: [
      { cellIndex: 1, x: 0.59, y: 0.22, size: 0.24, pupilScale: 0.24 },
      { cellIndex: 2, x: 0.24, y: 0.22, size: 0.24, pupilScale: 0.24 },
    ],
  },
  Z: {
    bounds: { x: 900, y: 20, width: 437, height: 292 },
    baseRotation: 0,
    boxSize: 3,
    eyes: [
      { cellIndex: 0, x: 0.5, y: 0.28, size: 0.24, pupilScale: 0.22 },
    ],
  },
  J: {
    bounds: { x: 80, y: 700, width: 296, height: 429 },
    baseRotation: 3,
    boxSize: 3,
    eyes: [
      { cellIndex: 2, x: 0.23, y: 0.19, size: 0.2, pupilScale: 0.26 },
      { cellIndex: 2, x: 0.62, y: 0.19, size: 0.2, pupilScale: 0.26 },
    ],
  },
  L: {
    bounds: { x: 860, y: 700, width: 291, height: 430 },
    baseRotation: 1,
    boxSize: 3,
    eyes: [
      { cellIndex: 0, x: 0.23, y: 0.19, size: 0.18, pupilScale: 0.26 },
    ],
  },
};

const tiles = new Map<string, MonsterTile>();
const figures = new Map<string, HTMLCanvasElement>();
let loadPromise: Promise<void> | null = null;
let loadCallbacks: Array<() => void> = [];

function createCanvas(size: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  return canvas;
}

function createReadbackCanvas(width: number, height: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  canvas.getContext('2d', { willReadFrequently: true });
  return canvas;
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = reject;
    image.src = url;
  });
}

function rotateCanvas(source: HTMLCanvasElement, turns: number): HTMLCanvasElement {
  const normalizedTurns = ((turns % 4) + 4) % 4;
  let current = source;

  for (let step = 0; step < normalizedTurns; step += 1) {
    const next = createCanvas(current.width);
    const ctx = next.getContext('2d')!;
    ctx.translate(next.width, 0);
    ctx.rotate(Math.PI / 2);
    ctx.drawImage(current, 0, 0);
    current = next;
  }

  return current;
}

function rotatePoint(x: number, y: number, turns: number, boxSize: number): { x: number; y: number } {
  const normalizedTurns = ((turns % 4) + 4) % 4;
  let point = { x, y };

  for (let step = 0; step < normalizedTurns; step += 1) {
    point = {
      x: boxSize - point.y,
      y: point.x,
    };
  }

  return point;
}

function cropCellCanvas(canvas: HTMLCanvasElement, cellX: number, cellY: number): HTMLCanvasElement {
  const tile = createReadbackCanvas(TILE_SIZE, TILE_SIZE);
  const ctx = tile.getContext('2d', { willReadFrequently: true })!;
  ctx.drawImage(
    canvas,
    cellX * TILE_SIZE,
    cellY * TILE_SIZE,
    TILE_SIZE,
    TILE_SIZE,
    0,
    0,
    TILE_SIZE,
    TILE_SIZE,
  );
  return tile;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function extractEyeMotion(
  tileCanvas: HTMLCanvasElement,
  x: number,
  y: number,
  size: number,
  pupilScale = 0.24,
): {
  regionScale: number;
  socketCanvas: HTMLCanvasElement;
  pupilCanvas: HTMLCanvasElement;
} {
  const ctx = tileCanvas.getContext('2d', { willReadFrequently: true })!;
  const centerX = x * TILE_SIZE;
  const centerY = y * TILE_SIZE;
  const sampleRadius = Math.max(10, size * TILE_SIZE * 0.5);
  const pupilRadius = Math.max(4, sampleRadius * pupilScale);
  const left = clamp(Math.floor(centerX - sampleRadius), 0, TILE_SIZE - 1);
  const top = clamp(Math.floor(centerY - sampleRadius), 0, TILE_SIZE - 1);
  const width = clamp(Math.ceil(sampleRadius * 2), 1, TILE_SIZE - left);
  const height = clamp(Math.ceil(sampleRadius * 2), 1, TILE_SIZE - top);
  const imageData = ctx.getImageData(left, top, width, height);
  const data = imageData.data;

  let socketR = 0;
  let socketG = 0;
  let socketB = 0;
  let socketA = 0;
  let socketCount = 0;

  for (let py = 0; py < height; py += 1) {
    for (let px = 0; px < width; px += 1) {
      const dx = px + left - centerX;
      const dy = py + top - centerY;
      const distance = Math.sqrt(dx * dx + dy * dy);
      if (distance > sampleRadius) {
        continue;
      }

      const index = (py * width + px) * 4;
      const alpha = data[index + 3];
      if (alpha < 18) {
        continue;
      }

      if (distance >= pupilRadius * 1.12 && distance <= sampleRadius * 0.82) {
        socketR += data[index];
        socketG += data[index + 1];
        socketB += data[index + 2];
        socketA += alpha;
        socketCount += 1;
      }
    }
  }

  const socketColor = socketCount
    ? [
      Math.round(socketR / socketCount),
      Math.round(socketG / socketCount),
      Math.round(socketB / socketCount),
      Math.round(socketA / socketCount),
    ] as const
    : [240, 230, 228, 245] as const;

  const pupilCanvas = createCanvas(width);
  pupilCanvas.height = height;
  const pupilCtx = pupilCanvas.getContext('2d')!;
  const pupilImage = pupilCtx.createImageData(width, height);
  const pupilData = pupilImage.data;
  const socketCanvas = createCanvas(width);
  socketCanvas.height = height;
  const socketCtx = socketCanvas.getContext('2d')!;
  const socketImage = socketCtx.createImageData(width, height);
  const socketData = socketImage.data;

  for (let py = 0; py < height; py += 1) {
    for (let px = 0; px < width; px += 1) {
      const srcIndex = (py * width + px) * 4;
      const alpha = data[srcIndex + 3];
      if (alpha < 18) {
        continue;
      }

      const r = data[srcIndex];
      const g = data[srcIndex + 1];
      const b = data[srcIndex + 2];
      const distanceToCenter = Math.hypot(px + left - centerX, py + top - centerY);
      const feather = clamp(1 - Math.max(0, distanceToCenter - pupilRadius) / 2.2, 0, 1);

      socketData[srcIndex] = r;
      socketData[srcIndex + 1] = g;
      socketData[srcIndex + 2] = b;
      socketData[srcIndex + 3] = alpha;

      if (feather > 0) {
        socketData[srcIndex] = Math.round(r * (1 - feather) + socketColor[0] * feather);
        socketData[srcIndex + 1] = Math.round(g * (1 - feather) + socketColor[1] * feather);
        socketData[srcIndex + 2] = Math.round(b * (1 - feather) + socketColor[2] * feather);
        socketData[srcIndex + 3] = Math.round(alpha * (1 - feather) + socketColor[3] * feather);

        pupilData[srcIndex] = r;
        pupilData[srcIndex + 1] = g;
        pupilData[srcIndex + 2] = b;
        pupilData[srcIndex + 3] = Math.round(alpha * feather);
      } else {
        pupilData[srcIndex] = r;
        pupilData[srcIndex + 1] = g;
        pupilData[srcIndex + 2] = b;
        pupilData[srcIndex + 3] = 0;
      }
    }
  }

  pupilCtx.putImageData(pupilImage, 0, 0);
  socketCtx.putImageData(socketImage, 0, 0);

  return {
    regionScale: width / TILE_SIZE,
    socketCanvas,
    pupilCanvas,
  };
}

function familyForPiece(pieceType: PieceType): 'red' | 'pink' | 'orange' | 'none' {
  if (pieceType === 'S') return 'red';
  if (pieceType === 'J') return 'pink';
  if (pieceType === 'L') return 'orange';
  return 'none';
}

function shouldBlinkPiece(pieceType: PieceType): boolean {
  return BLINKING_PIECES.has(pieceType);
}

async function buildMonsterTiles(): Promise<void> {
  const image = await loadImage(monsterSheetUrl);

  for (const [pieceType, spec] of Object.entries(MONSTER_SPECS) as Array<[PieceType, PieceArtSpec]>) {
    const baseCanvas = createCanvas(spec.boxSize * TILE_SIZE);
    const ctx = baseCanvas.getContext('2d')!;
    const baseCells = DEFINITIONS[pieceType][spec.baseRotation];
    const minX = Math.min(...baseCells.map((cell) => cell.x));
    const minY = Math.min(...baseCells.map((cell) => cell.y));
    const maxX = Math.max(...baseCells.map((cell) => cell.x));
    const maxY = Math.max(...baseCells.map((cell) => cell.y));
    const width = (maxX - minX + 1) * TILE_SIZE;
    const height = (maxY - minY + 1) * TILE_SIZE;

    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(
      image,
      spec.bounds.x,
      spec.bounds.y,
      spec.bounds.width,
      spec.bounds.height,
      minX * TILE_SIZE,
      minY * TILE_SIZE,
      width,
      height,
    );

    for (let rotation = 0; rotation < 4; rotation += 1) {
      const turns = ((rotation - spec.baseRotation) % 4 + 4) % 4;
      const rotatedCanvas = rotateCanvas(baseCanvas, turns);
      figures.set(`${pieceType}:${rotation}`, rotatedCanvas);
      const definition = DEFINITIONS[pieceType][rotation];
      const rotatedEyes = (spec.eyes || [])
        .map((eye, eyeIndex) => {
          const sourceCell = baseCells[eye.cellIndex];
          const point = rotatePoint(sourceCell.x + eye.x, sourceCell.y + eye.y, turns, spec.boxSize);
          const cellX = Math.max(0, Math.min(spec.boxSize - 1, Math.floor(point.x)));
          const cellY = Math.max(0, Math.min(spec.boxSize - 1, Math.floor(point.y)));
          const localX = point.x - cellX;
          const localY = point.y - cellY;
          const targetIndex = definition.findIndex((cell) => cell.x === cellX && cell.y === cellY);

          return targetIndex === -1
            ? null
            : {
              targetIndex,
              x: localX,
              y: localY,
              size: eye.size,
              pupilScale: eye.pupilScale,
              blink: shouldBlinkPiece(pieceType),
              seed: (rotation + 1) * 100 + eyeIndex * 37 + eye.cellIndex * 11,
            };
        })
        .filter(Boolean) as Array<{
          targetIndex: number;
          x: number;
          y: number;
          size: number;
          pupilScale?: number;
          blink: boolean;
          seed: number;
        }>;

      const rotatedTongue = spec.tongue
        ? (() => {
          const sourceCell = baseCells[spec.tongue.cellIndex];
          const point = rotatePoint(sourceCell.x + spec.tongue.x, sourceCell.y + spec.tongue.y, turns, spec.boxSize);
          const cellX = Math.max(0, Math.min(spec.boxSize - 1, Math.floor(point.x)));
          const cellY = Math.max(0, Math.min(spec.boxSize - 1, Math.floor(point.y)));
          const localX = point.x - cellX;
          const localY = point.y - cellY;
          const targetIndex = definition.findIndex((cell) => cell.x === cellX && cell.y === cellY);
          return targetIndex === -1
            ? null
            : {
              targetIndex,
              x: localX,
              y: localY,
              width: spec.tongue.width,
              height: spec.tongue.height,
              seed: 17 + rotation * 13,
            };
        })()
        : null;

      definition.forEach((cell, index) => {
        const key = `${pieceType}:${rotation}:${index}`;
        const tileCanvas = cropCellCanvas(rotatedCanvas, cell.x, cell.y);
        tiles.set(key, {
          canvas: tileCanvas,
          eyes: rotatedEyes
            .filter((eye) => eye.targetIndex === index)
            .map(({ x, y, size, pupilScale, blink, seed }) => {
              const motion = extractEyeMotion(tileCanvas, x, y, size, pupilScale);
              return {
                x,
                y,
                size,
                regionScale: motion.regionScale,
                socketCanvas: motion.socketCanvas,
                pupilCanvas: motion.pupilCanvas,
                blink,
                seed,
              };
            }),
          tongue: rotatedTongue && rotatedTongue.targetIndex === index
            ? {
              x: rotatedTongue.x,
              y: rotatedTongue.y,
              width: rotatedTongue.width,
              height: rotatedTongue.height,
              seed: rotatedTongue.seed,
            }
            : null,
          blinkFamily: familyForPiece(pieceType),
        });
      });
    }
  }
}

export function prepareMonsterSkin(onReady?: () => void): Promise<void> {
  if (onReady) {
    loadCallbacks.push(onReady);
  }

  if (!loadPromise) {
    loadPromise = buildMonsterTiles().then(() => {
      for (const callback of loadCallbacks) {
        callback();
      }
      loadCallbacks = [];
    }).catch((error) => {
      console.error('Failed to load monster sprite sheet.', error);
      loadCallbacks = [];
    });
  }

  return loadPromise;
}

export function getMonsterTile(skinKey: string): MonsterTile | null {
  return tiles.get(skinKey) ?? null;
}

export function getMonsterFigureCanvas(pieceType: PieceType, rotation = 0): HTMLCanvasElement | null {
  return figures.get(`${pieceType}:${rotation}`) ?? null;
}

export function getMonsterFigureBoxSize(pieceType: PieceType): number {
  return MONSTER_SPECS[pieceType].boxSize;
}
