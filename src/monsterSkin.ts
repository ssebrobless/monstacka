import monsterSheetUrl from './assets/monster-sheet.png';
import monsterSheetFrame2Url from './assets/monster-sheet-frame2.png';
import monsterSheetFrame3Url from './assets/monster-sheet-frame3.png';
import { DEFINITIONS } from './constants';
import type { PieceType } from './types';

interface Bounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface PieceArtSpec {
  boundsByFrame: [Bounds, Bounds, Bounds];
  baseRotation: number;
  boxSize: number;
}

type EyeAnimationStyle = 'roam' | 'blink';

interface EyeSeedSpec {
  cellIndex: number;
  x: number;
  y: number;
  width: number;
  height: number;
  style: EyeAnimationStyle;
}

interface SourceFrame {
  width: number;
  height: number;
  data: Uint8ClampedArray;
  backgroundMask: Uint8Array;
}

interface SilhouetteMask {
  width: number;
  height: number;
  data: Uint8Array;
}

interface TransformedEyePlacement {
  x: number;
  y: number;
  width: number;
  height: number;
  tileIndex: number;
  localX: number;
  localY: number;
  style: EyeAnimationStyle;
  seed: number;
}

export interface MonsterEye {
  x: number;
  y: number;
  width: number;
  height: number;
  frames: Array<HTMLCanvasElement | null>;
  style: EyeAnimationStyle;
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
const FRAME_SEQUENCE = [0, 1, 2, 1, 0] as const;
const FRAME_DURATION_MS = 320;
const FRAME_COOLDOWN_MIN_MS = 1700;
const FRAME_COOLDOWN_VARIATION_MS = 900;
const MONSTER_SHEET_URLS = [monsterSheetUrl, monsterSheetFrame2Url, monsterSheetFrame3Url] as const;
const OVERLAY_FRAME_SEQUENCE = [0, 1, 2, 1, 0] as const;
const OVERLAY_ROAM_FRAME_MS = 260;
const OVERLAY_ROAM_COOLDOWN_MIN_MS = 950;
const OVERLAY_ROAM_COOLDOWN_VARIATION_MS = 1100;
const OVERLAY_BLINK_FRAME_MS = 180;
const OVERLAY_BLINK_COOLDOWN_MIN_MS = 1400;
const OVERLAY_BLINK_COOLDOWN_VARIATION_MS = 1700;

const MONSTER_SPECS: Record<PieceType, PieceArtSpec> = {
  I: {
    boundsByFrame: [
      { x: 403, y: 182, width: 151, height: 571 },
      { x: 401, y: 182, width: 153, height: 571 },
      { x: 401, y: 182, width: 153, height: 571 },
    ],
    baseRotation: 1,
    boxSize: 4,
  },
  O: {
    boundsByFrame: [
      { x: 43, y: 314, width: 299, height: 289 },
      { x: 43, y: 314, width: 299, height: 289 },
      { x: 43, y: 314, width: 299, height: 289 },
    ],
    baseRotation: 0,
    boxSize: 4,
  },
  T: {
    boundsByFrame: [
      { x: 622, y: 371, width: 432, height: 291 },
      { x: 622, y: 371, width: 432, height: 291 },
      { x: 622, y: 371, width: 432, height: 291 },
    ],
    baseRotation: 2,
    boxSize: 3,
  },
  S: {
    boundsByFrame: [
      { x: 38, y: 5, width: 430, height: 289 },
      { x: 38, y: 5, width: 430, height: 289 },
      { x: 38, y: 5, width: 430, height: 289 },
    ],
    baseRotation: 0,
    boxSize: 3,
  },
  Z: {
    boundsByFrame: [
      { x: 585, y: 19, width: 435, height: 292 },
      { x: 585, y: 19, width: 435, height: 292 },
      { x: 585, y: 19, width: 435, height: 292 },
    ],
    baseRotation: 0,
    boxSize: 3,
  },
  J: {
    boundsByFrame: [
      { x: 49, y: 626, width: 293, height: 427 },
      { x: 49, y: 626, width: 293, height: 427 },
      { x: 49, y: 626, width: 293, height: 427 },
    ],
    baseRotation: 3,
    boxSize: 3,
  },
  L: {
    boundsByFrame: [
      { x: 595, y: 622, width: 291, height: 430 },
      { x: 595, y: 622, width: 291, height: 430 },
      { x: 595, y: 622, width: 291, height: 430 },
    ],
    baseRotation: 1,
    boxSize: 3,
  },
};

const INDEPENDENT_EYE_SEEDS: Partial<Record<PieceType, EyeSeedSpec[]>> = {
  S: [
    { cellIndex: 0, x: 14, y: 8, width: 28, height: 28, style: 'blink' },
    { cellIndex: 1, x: 76, y: 12, width: 24, height: 24, style: 'blink' },
  ],
  Z: [
    { cellIndex: 1, x: 12, y: 8, width: 62, height: 58, style: 'roam' },
  ],
  O: [
    { cellIndex: 0, x: 22, y: 10, width: 68, height: 64, style: 'roam' },
  ],
  J: [
    { cellIndex: 3, x: 10, y: 18, width: 42, height: 42, style: 'blink' },
    { cellIndex: 3, x: 62, y: 22, width: 38, height: 38, style: 'blink' },
  ],
  L: [
    { cellIndex: 0, x: 20, y: 16, width: 52, height: 52, style: 'blink' },
  ],
  I: [
    { cellIndex: 0, x: 0, y: 2, width: 54, height: 50, style: 'roam' },
    { cellIndex: 0, x: 58, y: 40, width: 54, height: 52, style: 'roam' },
    { cellIndex: 1, x: 10, y: 8, width: 74, height: 74, style: 'roam' },
    { cellIndex: 1, x: 79, y: 43, width: 33, height: 33, style: 'roam' },
    { cellIndex: 1, x: 0, y: 56, width: 40, height: 40, style: 'roam' },
    { cellIndex: 2, x: 18, y: 12, width: 42, height: 40, style: 'roam' },
    { cellIndex: 2, x: 0, y: 50, width: 36, height: 40, style: 'roam' },
    { cellIndex: 2, x: 72, y: 46, width: 36, height: 36, style: 'roam' },
    { cellIndex: 3, x: 0, y: 30, width: 32, height: 40, style: 'roam' },
    { cellIndex: 3, x: 60, y: 10, width: 42, height: 36, style: 'roam' },
    { cellIndex: 3, x: 16, y: 53, width: 70, height: 59, style: 'roam' },
    { cellIndex: 3, x: 84, y: 42, width: 28, height: 34, style: 'roam' },
  ],
  T: [
    { cellIndex: 0, x: 0, y: 24, width: 46, height: 46, style: 'roam' },
    { cellIndex: 0, x: 42, y: 0, width: 24, height: 24, style: 'roam' },
    { cellIndex: 0, x: 61, y: 22, width: 28, height: 28, style: 'roam' },
    { cellIndex: 2, x: 30, y: 0, width: 24, height: 24, style: 'roam' },
    { cellIndex: 2, x: 8, y: 24, width: 28, height: 28, style: 'roam' },
    { cellIndex: 2, x: 66, y: 22, width: 46, height: 46, style: 'roam' },
  ],
};

const tiles = new Map<string, MonsterTile[]>();
const figures = new Map<string, HTMLCanvasElement[]>();
const figureEyes = new Map<string, MonsterEye[]>();
let loadPromise: Promise<void> | null = null;
let loadCallbacks: Array<() => void> = [];

function createCanvas(size: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  return canvas;
}

function createCanvasWithSize(width: number, height: number): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  return canvas;
}

function cloneCanvas(source: HTMLCanvasElement): HTMLCanvasElement {
  const canvas = createCanvasWithSize(source.width, source.height);
  const ctx = canvas.getContext('2d')!;
  ctx.drawImage(source, 0, 0);
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

function isNearBlack(data: Uint8ClampedArray, offset: number): boolean {
  return data[offset + 3] > 0 && data[offset] < 12 && data[offset + 1] < 12 && data[offset + 2] < 12;
}

function createSourceFrame(image: HTMLImageElement): SourceFrame {
  const canvas = createCanvasWithSize(image.width, image.height);
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  ctx.drawImage(image, 0, 0);
  const imageData = ctx.getImageData(0, 0, image.width, image.height);
  const { data } = imageData;
  const width = image.width;
  const height = image.height;
  const backgroundMask = new Uint8Array(width * height);
  const queue = new Int32Array(width * height);
  let head = 0;
  let tail = 0;

  const enqueue = (x: number, y: number): void => {
    const index = y * width + x;
    if (backgroundMask[index]) {
      return;
    }
    const offset = index * 4;
    if (!isNearBlack(data, offset)) {
      return;
    }
    backgroundMask[index] = 1;
    queue[tail] = index;
    tail += 1;
  };

  for (let x = 0; x < width; x += 1) {
    enqueue(x, 0);
    enqueue(x, height - 1);
  }
  for (let y = 1; y < height - 1; y += 1) {
    enqueue(0, y);
    enqueue(width - 1, y);
  }

  while (head < tail) {
    const index = queue[head];
    head += 1;
    const x = index % width;
    const y = Math.floor(index / width);

    if (x > 0) enqueue(x - 1, y);
    if (x + 1 < width) enqueue(x + 1, y);
    if (y > 0) enqueue(x, y - 1);
    if (y + 1 < height) enqueue(x, y + 1);
  }

  return {
    width,
    height,
    data,
    backgroundMask,
  };
}

function buildSilhouetteMask(frame: SourceFrame, bounds: Bounds): SilhouetteMask {
  const size = bounds.width * bounds.height;
  const outside = new Uint8Array(size);
  const queue = new Int32Array(size);
  let head = 0;
  let tail = 0;

  const enqueue = (localX: number, localY: number): void => {
    const localIndex = localY * bounds.width + localX;
    if (outside[localIndex]) {
      return;
    }
    const fullX = bounds.x + localX;
    const fullY = bounds.y + localY;
    const sourceOffset = (fullY * frame.width + fullX) * 4;
    if (!isNearBlack(frame.data, sourceOffset)) {
      return;
    }
    outside[localIndex] = 1;
    queue[tail] = localIndex;
    tail += 1;
  };

  for (let x = 0; x < bounds.width; x += 1) {
    enqueue(x, 0);
    enqueue(x, bounds.height - 1);
  }
  for (let y = 1; y < bounds.height - 1; y += 1) {
    enqueue(0, y);
    enqueue(bounds.width - 1, y);
  }

  while (head < tail) {
    const index = queue[head];
    head += 1;
    const x = index % bounds.width;
    const y = Math.floor(index / bounds.width);

    if (x > 0) enqueue(x - 1, y);
    if (x + 1 < bounds.width) enqueue(x + 1, y);
    if (y > 0) enqueue(x, y - 1);
    if (y + 1 < bounds.height) enqueue(x, y + 1);
  }

  const mask = new Uint8Array(size);
  for (let index = 0; index < size; index += 1) {
    mask[index] = outside[index] ? 0 : 1;
  }

  return {
    width: bounds.width,
    height: bounds.height,
    data: mask,
  };
}

function dilateMask(mask: SilhouetteMask, passes: number): SilhouetteMask {
  let current = mask.data;

  for (let pass = 0; pass < passes; pass += 1) {
    const next = new Uint8Array(current);
    for (let y = 0; y < mask.height; y += 1) {
      for (let x = 0; x < mask.width; x += 1) {
        const index = y * mask.width + x;
        if (current[index]) {
          continue;
        }
        if (
          (x > 0 && current[index - 1]) ||
          (x + 1 < mask.width && current[index + 1]) ||
          (y > 0 && current[index - mask.width]) ||
          (y + 1 < mask.height && current[index + mask.width])
        ) {
          next[index] = 1;
        }
      }
    }
    current = next;
  }

  return {
    width: mask.width,
    height: mask.height,
    data: current,
  };
}

function applySilhouetteMask(frame: SourceFrame, bounds: Bounds, mask: SilhouetteMask): HTMLCanvasElement {
  const canvas = createCanvasWithSize(bounds.width, bounds.height);
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  const output = ctx.createImageData(bounds.width, bounds.height);

  for (let localY = 0; localY < bounds.height; localY += 1) {
    const sampleY = Math.min(
      mask.height - 1,
      Math.max(0, Math.floor((localY + 0.5) * mask.height / bounds.height)),
    );
    for (let localX = 0; localX < bounds.width; localX += 1) {
      const sampleX = Math.min(
        mask.width - 1,
        Math.max(0, Math.floor((localX + 0.5) * mask.width / bounds.width)),
      );
      const maskIndex = sampleY * mask.width + sampleX;
      if (!mask.data[maskIndex]) {
        continue;
      }

      const fullX = bounds.x + localX;
      const fullY = bounds.y + localY;
      const sourceOffset = (fullY * frame.width + fullX) * 4;
      const targetOffset = (localY * bounds.width + localX) * 4;
      output.data[targetOffset] = frame.data[sourceOffset];
      output.data[targetOffset + 1] = frame.data[sourceOffset + 1];
      output.data[targetOffset + 2] = frame.data[sourceOffset + 2];
      output.data[targetOffset + 3] = frame.data[sourceOffset + 3];
    }
  }

  ctx.putImageData(output, 0, 0);
  return canvas;
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

function cropCellCanvas(canvas: HTMLCanvasElement, cellX: number, cellY: number): HTMLCanvasElement {
  const tile = createCanvasWithSize(TILE_SIZE, TILE_SIZE);
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

function cropRectCanvas(
  canvas: HTMLCanvasElement,
  x: number,
  y: number,
  width: number,
  height: number,
): HTMLCanvasElement {
  const safeWidth = Math.max(1, Math.round(width));
  const safeHeight = Math.max(1, Math.round(height));
  const crop = createCanvasWithSize(safeWidth, safeHeight);
  const ctx = crop.getContext('2d')!;
  ctx.drawImage(
    canvas,
    Math.round(x),
    Math.round(y),
    safeWidth,
    safeHeight,
    0,
    0,
    safeWidth,
    safeHeight,
  );
  return crop;
}

function findNearestOpaquePixel(
  data: Uint8ClampedArray,
  canvasWidth: number,
  xStart: number,
  yStart: number,
  size: number,
): { x: number; y: number } | null {
  const centerX = xStart + size / 2;
  const centerY = yStart + size / 2;
  let best: { x: number; y: number; score: number } | null = null;

  for (let y = yStart; y < yStart + size; y += 1) {
    for (let x = xStart; x < xStart + size; x += 1) {
      const offset = (y * canvasWidth + x) * 4 + 3;
      if (data[offset] < 24) {
        continue;
      }
      const dx = x + 0.5 - centerX;
      const dy = y + 0.5 - centerY;
      const score = dx * dx + dy * dy;
      if (!best || score < best.score) {
        best = { x, y, score };
      }
    }
  }

  return best ? { x: best.x, y: best.y } : null;
}

function retainConnectedPiece(
  source: HTMLCanvasElement,
  definition: Array<{ x: number; y: number }>,
): HTMLCanvasElement {
  const canvas = cloneCanvas(source);
  const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
  const { data } = imageData;
  const keep = new Uint8Array(canvas.width * canvas.height);
  const queue = new Int32Array(canvas.width * canvas.height);
  let head = 0;
  let tail = 0;

  for (const cell of definition) {
    const seed = findNearestOpaquePixel(data, canvas.width, cell.x * TILE_SIZE, cell.y * TILE_SIZE, TILE_SIZE);
    if (!seed) {
      continue;
    }
    const index = seed.y * canvas.width + seed.x;
    if (keep[index]) {
      continue;
    }
    keep[index] = 1;
    queue[tail] = index;
    tail += 1;
  }

  while (head < tail) {
    const index = queue[head];
    head += 1;
    const x = index % canvas.width;
    const y = Math.floor(index / canvas.width);

    for (let dy = -1; dy <= 1; dy += 1) {
      for (let dx = -1; dx <= 1; dx += 1) {
        if (dx === 0 && dy === 0) {
          continue;
        }
        const nx = x + dx;
        const ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= canvas.width || ny >= canvas.height) {
          continue;
        }
        const neighborIndex = ny * canvas.width + nx;
        if (keep[neighborIndex]) {
          continue;
        }
        const alpha = data[neighborIndex * 4 + 3];
        if (alpha < 24) {
          continue;
        }
        keep[neighborIndex] = 1;
        queue[tail] = neighborIndex;
        tail += 1;
      }
    }
  }

  for (let index = 0; index < keep.length; index += 1) {
    if (keep[index]) {
      continue;
    }
    const offset = index * 4;
    data[offset + 3] = 0;
  }

  ctx.putImageData(imageData, 0, 0);
  return canvas;
}

function setFramedValue<T>(map: Map<string, T[]>, key: string, frameIndex: number, value: T): void {
  const frames = map.get(key) ?? [];
  frames[frameIndex] = value;
  map.set(key, frames);
}

function hashSeed(seedKey: string): number {
  let hash = 0;
  for (let index = 0; index < seedKey.length; index += 1) {
    hash = (hash * 131 + seedKey.charCodeAt(index)) >>> 0;
  }
  return hash;
}

function getSharedPieceSeedKey(skinKey: string): string {
  const [pieceType, rotation] = skinKey.split(':');
  if (!pieceType || rotation === undefined) {
    return skinKey;
  }
  return `${pieceType}:${rotation}`;
}

function resolveFrameIndex(now: number, animate: boolean, seedKey: string): number {
  if (!animate) {
    return 0;
  }

  const loopMs = FRAME_SEQUENCE.length * FRAME_DURATION_MS;
  const seed = hashSeed(seedKey);
  const cooldownMs = FRAME_COOLDOWN_MIN_MS + (seed % FRAME_COOLDOWN_VARIATION_MS);
  const cycleMs = loopMs + cooldownMs;
  const offsetMs = seed % cycleMs;
  const cycleTime = (Math.floor(now) + offsetMs) % cycleMs;

  if (cycleTime >= loopMs) {
    return 0;
  }

  const step = Math.min(FRAME_SEQUENCE.length - 1, Math.floor(cycleTime / FRAME_DURATION_MS));
  return FRAME_SEQUENCE[step];
}

function rotateRectWithinSquare(
  x: number,
  y: number,
  width: number,
  height: number,
  size: number,
  turns: number,
): Bounds {
  const normalizedTurns = ((turns % 4) + 4) % 4;

  if (normalizedTurns === 0) {
    return { x, y, width, height };
  }

  if (normalizedTurns === 1) {
    return {
      x: size - (y + height),
      y: x,
      width: height,
      height: width,
    };
  }

  if (normalizedTurns === 2) {
    return {
      x: size - (x + width),
      y: size - (y + height),
      width,
      height,
    };
  }

  return {
    x: y,
    y: size - (x + width),
    width: height,
    height: width,
  };
}

function buildEyePlacements(pieceType: PieceType, rotation: number): TransformedEyePlacement[] {
  const eyeSeeds = INDEPENDENT_EYE_SEEDS[pieceType] ?? [];
  if (!eyeSeeds.length) {
    return [];
  }

  const spec = MONSTER_SPECS[pieceType];
  const baseDefinition = DEFINITIONS[pieceType][spec.baseRotation];
  const definition = DEFINITIONS[pieceType][rotation];
  const turns = ((rotation - spec.baseRotation) % 4 + 4) % 4;
  const canvasSize = spec.boxSize * TILE_SIZE;

  return eyeSeeds.flatMap((eye, index) => {
    const baseCell = baseDefinition[eye.cellIndex];
    if (!baseCell) {
      return [];
    }

    const absoluteRect = rotateRectWithinSquare(
      baseCell.x * TILE_SIZE + eye.x,
      baseCell.y * TILE_SIZE + eye.y,
      eye.width,
      eye.height,
      canvasSize,
      turns,
    );
    const centerX = absoluteRect.x + absoluteRect.width / 2;
    const centerY = absoluteRect.y + absoluteRect.height / 2;
    const cellX = Math.floor(centerX / TILE_SIZE);
    const cellY = Math.floor(centerY / TILE_SIZE);
    const tileIndex = definition.findIndex((cell) => cell.x === cellX && cell.y === cellY);

    if (tileIndex === -1) {
      return [];
    }

    return [{
      x: absoluteRect.x,
      y: absoluteRect.y,
      width: absoluteRect.width,
      height: absoluteRect.height,
      tileIndex,
      localX: absoluteRect.x - cellX * TILE_SIZE,
      localY: absoluteRect.y - cellY * TILE_SIZE,
      style: eye.style,
      seed: hashSeed(`${pieceType}:${rotation}:eye:${index}`),
    }];
  });
}

function neutralizeEyeRegions(
  framedCanvases: HTMLCanvasElement[],
  eyePlacements: TransformedEyePlacement[],
): HTMLCanvasElement[] {
  if (!eyePlacements.length) {
    return framedCanvases;
  }

  const reference = framedCanvases[0];
  return framedCanvases.map((frameCanvas, frameIndex) => {
    if (frameIndex === 0) {
      return frameCanvas;
    }

    const neutralized = cloneCanvas(frameCanvas);
    const ctx = neutralized.getContext('2d')!;
    for (const eye of eyePlacements) {
      ctx.drawImage(
        reference,
        eye.x,
        eye.y,
        eye.width,
        eye.height,
        eye.x,
        eye.y,
        eye.width,
        eye.height,
      );
    }
    return neutralized;
  });
}

function resolveOverlayFrameIndex(now: number, animate: boolean, eye: MonsterEye): number {
  if (!animate) {
    return 0;
  }

  const isBlink = eye.style === 'blink';
  const frameDuration = isBlink ? OVERLAY_BLINK_FRAME_MS : OVERLAY_ROAM_FRAME_MS;
  const cooldownBase = isBlink ? OVERLAY_BLINK_COOLDOWN_MIN_MS : OVERLAY_ROAM_COOLDOWN_MIN_MS;
  const cooldownVariation = isBlink
    ? OVERLAY_BLINK_COOLDOWN_VARIATION_MS
    : OVERLAY_ROAM_COOLDOWN_VARIATION_MS;
  const loopMs = OVERLAY_FRAME_SEQUENCE.length * frameDuration;
  const cooldownMs = cooldownBase + (eye.seed % cooldownVariation);
  const cycleMs = loopMs + cooldownMs;
  const offsetMs = eye.seed % cycleMs;
  const cycleTime = (Math.floor(now) + offsetMs) % cycleMs;

  if (cycleTime >= loopMs) {
    return 0;
  }

  const step = Math.min(
    OVERLAY_FRAME_SEQUENCE.length - 1,
    Math.floor(cycleTime / frameDuration),
  );
  return OVERLAY_FRAME_SEQUENCE[step];
}

async function buildMonsterTiles(): Promise<void> {
  tiles.clear();
  figures.clear();
  figureEyes.clear();

  const images = await Promise.all(MONSTER_SHEET_URLS.map((url) => loadImage(url)));
  const frames = images.map((image) => createSourceFrame(image));
  const silhouetteMasks = new Map<PieceType, SilhouetteMask>();

  for (const [pieceType, spec] of Object.entries(MONSTER_SPECS) as Array<[PieceType, PieceArtSpec]>) {
    const baseMask = buildSilhouetteMask(frames[0], spec.boundsByFrame[0]);
    silhouetteMasks.set(pieceType, baseMask);
  }

  for (const [pieceType, spec] of Object.entries(MONSTER_SPECS) as Array<[PieceType, PieceArtSpec]>) {
    const silhouetteMask = silhouetteMasks.get(pieceType)!;
    const baseCells = DEFINITIONS[pieceType][spec.baseRotation];
    const minX = Math.min(...baseCells.map((cell) => cell.x));
    const minY = Math.min(...baseCells.map((cell) => cell.y));
    const maxX = Math.max(...baseCells.map((cell) => cell.x));
    const maxY = Math.max(...baseCells.map((cell) => cell.y));
    const width = (maxX - minX + 1) * TILE_SIZE;
    const height = (maxY - minY + 1) * TILE_SIZE;

    const baseCanvases = frames.map((frame, frameIndex) => {
      const bounds = spec.boundsByFrame[frameIndex] ?? spec.boundsByFrame[0];
      const isolatedCanvas = applySilhouetteMask(frame, bounds, silhouetteMask);
      const baseCanvas = createCanvas(spec.boxSize * TILE_SIZE);
      const ctx = baseCanvas.getContext('2d')!;
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(
        isolatedCanvas,
        0,
        0,
        isolatedCanvas.width,
        isolatedCanvas.height,
        minX * TILE_SIZE,
        minY * TILE_SIZE,
        width,
        height,
      );
      return retainConnectedPiece(baseCanvas, baseCells);
    });

    for (let rotation = 0; rotation < 4; rotation += 1) {
      const turns = ((rotation - spec.baseRotation) % 4 + 4) % 4;
      const definition = DEFINITIONS[pieceType][rotation];
      const rawRotatedCanvases = baseCanvases.map((canvas) =>
        retainConnectedPiece(rotateCanvas(canvas, turns), definition));
      const eyePlacements = buildEyePlacements(pieceType, rotation);
      const bodyCanvases = eyePlacements.length
        ? neutralizeEyeRegions(rawRotatedCanvases, eyePlacements).map((canvas) =>
          retainConnectedPiece(canvas, definition))
        : rawRotatedCanvases;
      const figureKey = `${pieceType}:${rotation}`;

      bodyCanvases.forEach((canvas, frameIndex) => {
        setFramedValue(figures, figureKey, frameIndex, canvas);
      });
      if (eyePlacements.length) {
        figureEyes.set(figureKey, eyePlacements.map((eye) => ({
          x: eye.x,
          y: eye.y,
          width: eye.width,
          height: eye.height,
          frames: rawRotatedCanvases.map((canvas) =>
            cropRectCanvas(canvas, eye.x, eye.y, eye.width, eye.height)),
          style: eye.style,
          seed: eye.seed,
        })));
      } else {
        figureEyes.delete(figureKey);
      }

      definition.forEach((cell, index) => {
        const key = `${pieceType}:${rotation}:${index}`;
        const tileEyeLayers = eyePlacements
          .filter((eye) => eye.tileIndex === index)
          .map((eye) => ({
            x: eye.localX,
            y: eye.localY,
            width: eye.width,
            height: eye.height,
            frames: rawRotatedCanvases.map((canvas) =>
              cropRectCanvas(canvas, eye.x, eye.y, eye.width, eye.height)),
            style: eye.style,
            seed: eye.seed,
          }));

        bodyCanvases.forEach((canvas, frameIndex) => {
          const tileCanvas = cropCellCanvas(canvas, cell.x, cell.y);
          setFramedValue(tiles, key, frameIndex, {
            canvas: tileCanvas,
            eyes: tileEyeLayers,
            tongue: null,
            blinkFamily: 'none',
          });
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
    loadPromise = buildMonsterTiles()
      .then(() => {
        for (const callback of loadCallbacks) {
          callback();
        }
        loadCallbacks = [];
      })
      .catch((error) => {
        console.error('Failed to load monster sprite sheets.', error);
        loadCallbacks = [];
      });
  }

  return loadPromise;
}

export function getMonsterTile(skinKey: string, now = 0, animate = false): MonsterTile | null {
  const framedTiles = tiles.get(skinKey);
  if (!framedTiles?.length) {
    return null;
  }

  const frameIndex = resolveFrameIndex(now, animate, getSharedPieceSeedKey(skinKey));
  return framedTiles[frameIndex] ?? framedTiles[0] ?? null;
}

export function getMonsterFigureCanvas(
  pieceType: PieceType,
  rotation = 0,
  now = 0,
  animate = false,
): HTMLCanvasElement | null {
  const framedFigures = figures.get(`${pieceType}:${rotation}`);
  if (!framedFigures?.length) {
    return null;
  }

  const frameIndex = resolveFrameIndex(now, animate, `${pieceType}:${rotation}`);
  return framedFigures[frameIndex] ?? framedFigures[0] ?? null;
}

export function getMonsterFigureEyes(pieceType: PieceType, rotation = 0): MonsterEye[] {
  return figureEyes.get(`${pieceType}:${rotation}`) ?? [];
}

export function getMonsterEyeFrame(
  eye: MonsterEye,
  now = 0,
  animate = false,
): HTMLCanvasElement | null {
  const frameIndex = resolveOverlayFrameIndex(now, animate, eye);
  return eye.frames[frameIndex] ?? eye.frames[0] ?? null;
}

export function getMonsterFigureBoxSize(pieceType: PieceType): number {
  return MONSTER_SPECS[pieceType].boxSize;
}
