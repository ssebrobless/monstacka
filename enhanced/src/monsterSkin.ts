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
    bounds: { x: 403, y: 182, width: 151, height: 571 },
    baseRotation: 1,
    boxSize: 4,
    eyes: [
      { cellIndex: 0, x: 0.26, y: 0.18, size: 0.12 },
      { cellIndex: 1, x: 0.52, y: 0.45, size: 0.15 },
      { cellIndex: 2, x: 0.18, y: 0.54, size: 0.1 },
      { cellIndex: 3, x: 0.5, y: 0.26, size: 0.13 },
    ],
  },
  O: {
    bounds: { x: 43, y: 314, width: 299, height: 289 },
    baseRotation: 0,
    boxSize: 4,
  },
  T: {
    bounds: { x: 622, y: 371, width: 432, height: 291 },
    baseRotation: 2,
    boxSize: 3,
    eyes: [
      { cellIndex: 0, x: 0.26, y: 0.24, size: 0.08 },
      { cellIndex: 1, x: 0.48, y: 0.18, size: 0.07 },
      { cellIndex: 2, x: 0.72, y: 0.26, size: 0.09 },
    ],
    tongue: { cellIndex: 3, x: 0.5, y: 0.52, width: 0.36, height: 0.54 },
  },
  S: {
    bounds: { x: 38, y: 5, width: 430, height: 289 },
    baseRotation: 0,
    boxSize: 3,
    eyes: [
      { cellIndex: 1, x: 0.56, y: 0.22, size: 0.11 },
      { cellIndex: 2, x: 0.22, y: 0.18, size: 0.11 },
    ],
  },
  Z: {
    bounds: { x: 585, y: 20, width: 435, height: 291 },
    baseRotation: 0,
    boxSize: 3,
    eyes: [
      { cellIndex: 1, x: 0.46, y: 0.22, size: 0.11 },
    ],
  },
  J: {
    bounds: { x: 49, y: 626, width: 293, height: 427 },
    baseRotation: 3,
    boxSize: 3,
    eyes: [
      { cellIndex: 2, x: 0.24, y: 0.2, size: 0.11 },
      { cellIndex: 2, x: 0.62, y: 0.2, size: 0.11 },
    ],
  },
  L: {
    bounds: { x: 595, y: 622, width: 291, height: 430 },
    baseRotation: 1,
    boxSize: 3,
    eyes: [
      { cellIndex: 0, x: 0.5, y: 0.18, size: 0.09 },
      { cellIndex: 1, x: 0.64, y: 0.28, size: 0.14 },
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
  const tile = createCanvas(TILE_SIZE);
  const ctx = tile.getContext('2d')!;
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
              blink: shouldBlinkPiece(pieceType),
              seed: (rotation + 1) * 100 + eyeIndex * 37 + eye.cellIndex * 11,
            };
        })
        .filter(Boolean) as Array<{
          targetIndex: number;
          x: number;
          y: number;
          size: number;
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
        tiles.set(key, {
          canvas: cropCellCanvas(rotatedCanvas, cell.x, cell.y),
          eyes: rotatedEyes
            .filter((eye) => eye.targetIndex === index)
            .map(({ x, y, size, blink, seed }) => ({ x, y, size, blink, seed })),
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
