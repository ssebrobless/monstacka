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

const RIPPLE_FRAME_COUNT = 8;
const RIPPLE_AMPLITUDE = 40;
const RIPPLE_PAD = 44; // extra canvas margin so protrusions aren't clipped
const RIPPLE_TUCK_DEPTH = 8;
const RIPPLE_EDGE_FEATHER = 4.0;
const RIPPLE_SHADOW_WIDTH = 3.0;
const RIPPLE_SHADOW_OPACITY = 0.35;

const RIPPLE_AMP_SCALE: Record<string, number> = {
  I: 0.7, // cyan — long thin shape makes ripples overly prominent
  O: 1.0, S: 1.0, Z: 1.0, T: 1.0, J: 1.0, L: 1.0,
};

const rippleFrameCache = new WeakMap<HTMLCanvasElement, HTMLCanvasElement[]>();
const rippleStringCache = new Map<string, HTMLCanvasElement[]>();

interface EdgeData {
  imageData: ImageData;
  alpha: Uint8Array;
  edge: Uint8Array;
  expanded: Uint8Array;
  edgePixels: Array<{ x: number; y: number; nx: number; ny: number; arc: number }>;
  width: number;
  height: number;
  empty: boolean;
}

const edgeDataCache = new WeakMap<HTMLCanvasElement, EdgeData>();

function getEdgeData(source: HTMLCanvasElement): EdgeData {
  const cached = edgeDataCache.get(source);
  if (cached) return cached;

  const w = source.width;
  const h = source.height;
  const sourceCtx = source.getContext('2d', { willReadFrequently: true });
  if (!sourceCtx) {
    const empty: EdgeData = { imageData: new ImageData(1, 1), alpha: new Uint8Array(0), edge: new Uint8Array(0), expanded: new Uint8Array(0), edgePixels: [], width: w, height: h, empty: true };
    edgeDataCache.set(source, empty);
    return empty;
  }

  const imageData = sourceCtx.getImageData(0, 0, w, h);
  const total = w * h;
  const alpha = new Uint8Array(total);
  const edge = new Uint8Array(total);
  const expanded = new Uint8Array(total);

  for (let y = 0; y < h; y += 1) {
    for (let x = 0; x < w; x += 1) {
      alpha[y * w + x] = imageData.data[(y * w + x) * 4 + 3] > 8 ? 1 : 0;
    }
  }

  for (let y = 0; y < h; y += 1) {
    for (let x = 0; x < w; x += 1) {
      const idx = y * w + x;
      if (!alpha[idx]) continue;
      let isEdge = false;
      for (let oy = -1; oy <= 1 && !isEdge; oy += 1) {
        for (let ox = -1; ox <= 1; ox += 1) {
          if (!ox && !oy) continue;
          const sx = x + ox;
          const sy = y + oy;
          if (sx < 0 || sy < 0 || sx >= w || sy >= h || !alpha[sy * w + sx]) {
            isEdge = true;
            break;
          }
        }
      }
      if (isEdge) edge[idx] = 1;
    }
  }

  for (let y = 0; y < h; y += 1) {
    for (let x = 0; x < w; x += 1) {
      if (!edge[y * w + x]) continue;
      for (let oy = -2; oy <= 2; oy += 1) {
        for (let ox = -2; ox <= 2; ox += 1) {
          const sx = x + ox;
          const sy = y + oy;
          if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
          if (ox * ox + oy * oy > 5) continue;
          const si = sy * w + sx;
          expanded[si] = 1;
        }
      }
    }
  }

  // Compute edge pixel normals (pointing outward from interior)
  const edgePixels: EdgeData['edgePixels'] = [];
  let arcLen = 0;
  for (let y = 0; y < h; y += 1) {
    for (let x = 0; x < w; x += 1) {
      if (!edge[y * w + x]) continue;
      // Normal = direction away from alpha interior, computed from gradient of alpha
      let gx = 0;
      let gy = 0;
      for (let oy = -1; oy <= 1; oy += 1) {
        for (let ox = -1; ox <= 1; ox += 1) {
          if (!ox && !oy) continue;
          const sx = x + ox;
          const sy = y + oy;
          const a = (sx >= 0 && sy >= 0 && sx < w && sy < h) ? alpha[sy * w + sx] : 0;
          gx -= ox * a;
          gy -= oy * a;
        }
      }
      const len = Math.sqrt(gx * gx + gy * gy) || 1;
      edgePixels.push({ x, y, nx: gx / len, ny: gy / len, arc: arcLen });
      arcLen += 1;
    }
  }

  // Normalize arc to 0..2π for sinusoidal displacement
  if (edgePixels.length > 1) {
    const maxArc = edgePixels[edgePixels.length - 1].arc;
    for (const p of edgePixels) {
      p.arc = (p.arc / maxArc) * Math.PI * 2;
    }
  }

  let hasVisible = false;
  for (let i = 0; i < expanded.length; i += 1) {
    if (expanded[i]) { hasVisible = true; break; }
  }

  const data: EdgeData = { imageData, alpha, edge, expanded, edgePixels, width: w, height: h, empty: !hasVisible };
  edgeDataCache.set(source, data);
  return data;
}

function sampleEdgeColor(data: EdgeData): [number, number, number] {
  const { imageData, edgePixels, width } = data;
  let rSum = 0;
  let gSum = 0;
  let bSum = 0;
  let count = 0;
  for (const p of edgePixels) {
    const idx = (p.y * width + p.x) * 4;
    if (imageData.data[idx + 3] < 128) continue;
    rSum += imageData.data[idx];
    gSum += imageData.data[idx + 1];
    bSum += imageData.data[idx + 2];
    count += 1;
  }
  if (count === 0) return [200, 200, 200];
  return [
    Math.round(rSum / count),
    Math.round(gSum / count),
    Math.round(bSum / count),
  ];
}

function createRippleFrameCanvas(
  data: EdgeData,
  frameIndex: number,
  pieceColor: [number, number, number],
  ampScale = 1.0,
): HTMLCanvasElement {
  const { alpha, edgePixels, width: w, height: h } = data;
  const pw = w + RIPPLE_PAD * 2;
  const ph = h + RIPPLE_PAD * 2;
  const canvas = document.createElement('canvas');
  canvas.className = 'monster-ripple-art';
  canvas.classList.add(`ripple-frame-${frameIndex}`);
  canvas.width = pw;
  canvas.height = ph;
  // Position the padded canvas so its center aligns with the art canvas
  const padXPct = (RIPPLE_PAD / w) * 100;
  const padYPct = (RIPPLE_PAD / h) * 100;
  canvas.style.left = `${-padXPct}%`;
  canvas.style.top = `${-padYPct}%`;
  canvas.style.width = `${100 + padXPct * 2}%`;
  canvas.style.height = `${100 + padYPct * 2}%`;
  const ctx = canvas.getContext('2d')!;
  const output = ctx.createImageData(pw, ph);

  const [cr, cg, cb] = pieceColor;

  // Step 1: Classify edge pixels by side (normal direction) and compute wave protrusion.
  // Each side gets exactly RIPPLES_PER_SIDE evenly-spaced traveling-wave bubbles.
  const sideGroups: number[][] = [[], [], [], []]; // 0=top, 1=right, 2=bottom, 3=left

  for (let i = 0; i < edgePixels.length; i += 1) {
    const p = edgePixels[i];
    const absNx = Math.abs(p.nx);
    const absNy = Math.abs(p.ny);
    let side: number;
    if (absNy >= absNx) {
      side = p.ny < 0 ? 0 : 2;
    } else {
      side = p.nx > 0 ? 1 : 3;
    }
    sideGroups[side].push(i);
  }

  // Sort each side's pixels along the tangent direction
  for (let s = 0; s < 4; s += 1) {
    const group = sideGroups[s];
    if (s === 0 || s === 2) {
      group.sort((a, b) => edgePixels[a].x - edgePixels[b].x);
    } else {
      group.sort((a, b) => edgePixels[a].y - edgePixels[b].y);
    }
  }

  // Compute protrusion using abs(sin) traveling wave — ripple count adapts to side length
  // Each bubble gets an independent phase offset so they fire at different times
  const frameT = frameIndex / RIPPLE_FRAME_COUNT; // 0..0.875 across 8 frames
  const protrusions = new Float32Array(edgePixels.length);
  for (let s = 0; s < 4; s += 1) {
    const group = sideGroups[s];
    if (!group.length) continue;
    // Compute side pixel extent to derive ripple count
    const tangentKey = (s === 0 || s === 2) ? 'x' : 'y';
    const coords = group.map((i) => edgePixels[i][tangentKey as 'x' | 'y']);
    const sideLength = Math.max(1, Math.max(...coords) - Math.min(...coords));
    const ripplesForSide = Math.max(3, Math.round(sideLength / 65));
    for (let gi = 0; gi < group.length; gi += 1) {
      const t = group.length > 1 ? gi / (group.length - 1) : 0.5;
      const wave = Math.sin(t * ripplesForSide * Math.PI * 2);

      // Per-bubble independent phase — alternate even/odd so adjacent bubbles don't peak together
      const bubbleIndex = Math.round(t * (ripplesForSide - 1));
      const bubblePhase = (bubbleIndex % 2 === 0) ? 0.0 : 0.5;
      const jitter = ((bubbleIndex * 7 + s * 13) % 11) / 11 * 0.25;
      const totalPhase = (frameT + bubblePhase + jitter) % 1.0;

      // Triangle wave envelope: 0→1→0 over one cycle
      const envelope = totalPhase <= 0.5
        ? totalPhase * 2
        : (1 - totalPhase) * 2;
      const clampedEnvelope = 0.45 + 0.55 * envelope;

      // Per-bubble size variation: 50-100% of max amplitude
      const sizeVar = 0.5 + ((bubbleIndex * 31 + s * 17) % 23) / 23 * 0.5;

      protrusions[group[gi]] = Math.abs(wave) * RIPPLE_AMPLITUDE * clampedEnvelope * sizeVar * ampScale;
    }
  }

  // Step 2: Build nearest-edge lookup for pixels in the padded canvas
  const maxRange = RIPPLE_AMPLITUDE + RIPPLE_TUCK_DEPTH + 2;
  const nearestIdx = new Int32Array(pw * ph).fill(-1);
  const nearestDist = new Float32Array(pw * ph).fill(1e9);

  for (let ei = 0; ei < edgePixels.length; ei += 1) {
    const ep = edgePixels[ei];
    const epx = ep.x + RIPPLE_PAD;
    const epy = ep.y + RIPPLE_PAD;
    const r = Math.ceil(maxRange);
    for (let oy = -r; oy <= r; oy += 1) {
      for (let ox = -r; ox <= r; ox += 1) {
        const px = epx + ox;
        const py = epy + oy;
        if (px < 0 || py < 0 || px >= pw || py >= ph) continue;
        const d = Math.sqrt(ox * ox + oy * oy);
        if (d > maxRange) continue;
        const pi = py * pw + px;
        if (d < nearestDist[pi]) {
          nearestDist[pi] = d;
          nearestIdx[pi] = ei;
        }
      }
    }
  }

  // Step 3: Render filled bubble protrusions
  for (let py = 0; py < ph; py += 1) {
    for (let px = 0; px < pw; px += 1) {
      const pi = py * pw + px;
      const ei = nearestIdx[pi];
      if (ei < 0) continue;

      const ep = edgePixels[ei];
      const protrusionMax = protrusions[ei];
      if (protrusionMax < 0.5) continue;

      // Signed distance along outward normal (convert back from padded coords)
      const dx = (px - RIPPLE_PAD) - ep.x;
      const dy = (py - RIPPLE_PAD) - ep.y;
      const signedDist = dx * ep.nx + dy * ep.ny;

      if (signedDist > protrusionMax) continue;
      if (signedDist < -RIPPLE_TUCK_DEPTH) continue;

      // Don't paint over interior sprite pixels
      const srcX = px - RIPPLE_PAD;
      const srcY = py - RIPPLE_PAD;
      if (srcX >= 0 && srcY >= 0 && srcX < w && srcY < h && alpha[srcY * w + srcX]) {
        if (signedDist < -0.5) continue;
      }

      // Perpendicular distance for bubble width falloff
      const perpDist = Math.abs(dx * (-ep.ny) + dy * ep.nx);
      const normalizedDist = signedDist / Math.max(protrusionMax, 0.1);
      // Wide rounded bubble shape: widest at base, narrows at tip
      const bubbleHalfWidth = 26.0 * Math.sqrt(Math.max(0, 1 - normalizedDist * normalizedDist));
      if (perpDist > bubbleHalfWidth) continue;

      let pixelAlpha = 220;

      // Feathered tip
      const distFromTip = protrusionMax - signedDist;
      if (distFromTip < RIPPLE_EDGE_FEATHER) {
        pixelAlpha *= 0.5 * (1 + Math.cos(Math.PI * (1 - distFromTip / RIPPLE_EDGE_FEATHER)));
      }

      // Feathered lateral edges
      if (perpDist > bubbleHalfWidth - 2.5) {
        const edgeFade = (bubbleHalfWidth - perpDist) / 2.5;
        pixelAlpha *= Math.max(0, edgeFade);
      }

      // Tuck zone fade
      if (signedDist < 0) {
        pixelAlpha *= 1 - (-signedDist / RIPPLE_TUCK_DEPTH);
      }

      if (pixelAlpha < 1) continue;

      const oi = pi * 4;

      // Dithered shadow near the tip
      const isShadowZone = distFromTip < RIPPLE_SHADOW_WIDTH && signedDist > 0;
      const isDitherPixel = (px + py) % 2 === 0;

      if (isShadowZone && isDitherPixel) {
        const shadowStrength = RIPPLE_SHADOW_OPACITY * (1 - distFromTip / RIPPLE_SHADOW_WIDTH);
        output.data[oi] = cr * (1 - shadowStrength) * 0.3;
        output.data[oi + 1] = cg * (1 - shadowStrength) * 0.3;
        output.data[oi + 2] = cb * (1 - shadowStrength) * 0.3;
        output.data[oi + 3] = Math.min(255, pixelAlpha * 1.2);
      } else {
        output.data[oi] = cr;
        output.data[oi + 1] = cg;
        output.data[oi + 2] = cb;
        output.data[oi + 3] = Math.min(255, pixelAlpha);
      }
    }
  }

  ctx.putImageData(output, 0, 0);
  return canvas;
}

// Cache: ripple canvas → data URL (computed once, reused for all img clones)
const rippleDataUrlCache = new WeakMap<HTMLCanvasElement, string>();

function createRippleImgElement(src: HTMLCanvasElement): HTMLImageElement {
  let url = rippleDataUrlCache.get(src);
  if (!url) {
    url = src.toDataURL();
    rippleDataUrlCache.set(src, url);
  }
  const img = new Image();
  img.className = src.className;
  img.style.cssText = src.style.cssText;
  img.width = src.width;
  img.height = src.height;
  img.src = url;
  return img;
}

function createMonsterRippleFrames(source: HTMLCanvasElement, cacheKey?: string, ampScale = 1.0): HTMLElement[] {
  if (cacheKey) {
    const cached = rippleStringCache.get(cacheKey);
    if (cached) return cached.map(createRippleImgElement);
  }

  const cached = rippleFrameCache.get(source);
  if (cached) return cached.map(createRippleImgElement);

  const data = getEdgeData(source);
  if (data.empty) return [];

  const color = sampleEdgeColor(data);
  const frames: HTMLCanvasElement[] = [];
  for (let f = 0; f < RIPPLE_FRAME_COUNT; f += 1) {
    frames.push(createRippleFrameCanvas(data, f, color, ampScale));
  }
  rippleFrameCache.set(source, frames);
  if (cacheKey) rippleStringCache.set(cacheKey, frames);
  return frames.map(createRippleImgElement);
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
  motion.appendChild(rippleLayer);
  motion.appendChild(artLayer);
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

  if (!tile) {
    cell.replaceChildren();
    cell.className = baseClassName;
    cell.style.removeProperty('--squish-scale-x');
    cell.style.removeProperty('--squish-scale-y');
    cell.style.removeProperty('--squish-shift-x');
    cell.style.removeProperty('--squish-shift-y');
    const [pieceType] = skinKey.split(':');
    cell.classList.add(`piece-${pieceType.toLowerCase()}`);
    return;
  }

  // Two-layer cache: body/ripple DOM is stable across eye blinks
  const bodyCacheKey = `${skinKey}:${tile.canvas === (cell as any).__mscTileCanvas ? 'same' : 'new'}`;
  const needsBodyRebuild = (cell as any).__mscBodyKey !== bodyCacheKey || cell.childElementCount === 0;

  if (needsBodyRebuild) {
    (cell as any).__mscBodyKey = bodyCacheKey;
    (cell as any).__mscTileCanvas = tile.canvas;

    cell.replaceChildren();
    cell.className = baseClassName;
    cell.style.removeProperty('--squish-scale-x');
    cell.style.removeProperty('--squish-scale-y');
    cell.style.removeProperty('--squish-shift-x');
    cell.style.removeProperty('--squish-shift-y');

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
    for (const frame of createMonsterRippleFrames(tile.canvas, undefined, RIPPLE_AMP_SCALE[pieceType] ?? 1.0)) {
      rippleLayer.appendChild(frame);
    }

    cell.appendChild(body);
    (cell as any).__mscArtLayer = artLayer;
    (cell as any).__mscEyeKey = ''; // force eye rebuild below
  }

  // Eye overlay: lightweight update without touching body/ripple DOM
  const eyeFrameIds = tile.eyes.map((eye) => {
    const frame = getMonsterEyeFrame(eye, options.now, animate);
    return frame ? tile.eyes.indexOf(eye) + ':' + (eye.frames.indexOf(frame)) : 'x';
  }).join(',');

  if ((cell as any).__mscEyeKey !== eyeFrameIds) {
    (cell as any).__mscEyeKey = eyeFrameIds;
    const artLayer = (cell as any).__mscArtLayer as HTMLElement | undefined;
    if (artLayer) {
      artLayer.querySelectorAll('.monster-eye-overlay').forEach((el) => el.remove());
      for (const eye of tile.eyes) {
        const eyeFrame = getMonsterEyeFrame(eye, options.now, animate);
        if (!eyeFrame) continue;
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
    }
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
  const minX = Math.min(...definition.map((cell) => cell.x));
  const maxX = Math.max(...definition.map((cell) => cell.x));
  const minY = Math.min(...definition.map((cell) => cell.y));
  const maxY = Math.max(...definition.map((cell) => cell.y));
  const widthCells = maxX - minX + 1;
  const heightCells = maxY - minY + 1;

  if (layout === 'absolute') {
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

    // Separate cache: body/ripple structure vs. eye overlays
    // Body cache key does NOT include eye frames — prevents ripple animation reset on blink
    const animate = options.animate ?? false;
    const bodyCacheKey = `${pieceType}:${rotation}:${animate}`;
    const needsBodyRebuild = (container as any).__mfcBodyKey !== bodyCacheKey || container.childElementCount === 0;

    if (needsBodyRebuild) {
      (container as any).__mfcBodyKey = bodyCacheKey;
      container.replaceChildren();
      container.classList.toggle('monster-figure-absolute', layout === 'absolute');

      const frame = document.createElement('div');
      frame.className = 'monster-figure-frame';
      frame.style.width = `${(widthCells / 4) * 100}%`;
      frame.style.height = `${(heightCells / 4) * 100}%`;
      const dominantSpan = Math.max(widthCells / 4, heightCells / 4);
      const fillRatio = options.fillRatio ?? 0.82;
      frame.style.setProperty('--figure-scale', `${(fillRatio / dominantSpan).toFixed(3)}`);

      if (figureArt) {
        const motionSeed = pieceType.charCodeAt(0) + rotation * 37;
        const { body, artLayer, rippleLayer } = createMonsterBodyNode(
          pieceType,
          animate,
          0, 0, 0, 0,
          motionSeed,
        );
        body.classList.add('monster-figure-body');
        const artNode = createMonsterArtNode(figureArt);
        artLayer.appendChild(artNode);
        for (const rf of createMonsterRippleFrames(figureArt, `fig:${pieceType}:${rotation}`, RIPPLE_AMP_SCALE[pieceType] ?? 1.0)) {
          rippleLayer.appendChild(rf);
        }
        frame.appendChild(body);
        // Store refs for incremental updates
        (container as any).__mfcArtLayer = artLayer;
        (container as any).__mfcArtCanvas = artNode;
        (container as any).__mfcFigureArt = figureArt;
      } else {
        // Sprites not loaded — fall back to cell-based rendering
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
    }

    // Refresh body art canvas every call (enables O/L body-frame blinks without DOM rebuild)
    if (figureArt && !needsBodyRebuild) {
      const artNode = (container as any).__mfcArtCanvas as HTMLCanvasElement | undefined;
      if (artNode) {
        if (artNode.width !== figureArt.width) artNode.width = figureArt.width;
        if (artNode.height !== figureArt.height) artNode.height = figureArt.height;
        const ctx = artNode.getContext('2d')!;
        ctx.clearRect(0, 0, artNode.width, artNode.height);
        ctx.drawImage(figureArt, 0, 0);
        (container as any).__mfcFigureArt = figureArt;
      }
    }

    // Update eye overlays without touching body/ripple DOM
    const eyes = getMonsterFigureEyes(pieceType, rotation);
    const eyeFrameIds = eyes.map((eye) => {
      const ef = getMonsterEyeFrame(eye, options.now, animate);
      return ef ? eye.frames.indexOf(ef) : -1;
    }).join(',');

    if ((container as any).__mfcEyeKey !== eyeFrameIds) {
      (container as any).__mfcEyeKey = eyeFrameIds;
      const artLayer = (container as any).__mfcArtLayer as HTMLElement | undefined;
      const cachedFigureArt = (container as any).__mfcFigureArt as HTMLCanvasElement | undefined;
      if (artLayer && cachedFigureArt) {
        // Remove old eye overlays
        artLayer.querySelectorAll('.monster-eye-overlay').forEach((el) => el.remove());
        const cellPx = cachedFigureArt.width / widthCells;
        const cropOffsetX = minX * cellPx;
        const cropOffsetY = minY * cellPx;
        for (const eye of eyes) {
          const eyeFrame = getMonsterEyeFrame(eye, options.now, animate);
          if (!eyeFrame) continue;
          const localX = eye.x - cropOffsetX;
          const localY = eye.y - cropOffsetY;
          if (
            localX + eye.width <= 0 ||
            localY + eye.height <= 0 ||
            localX >= cachedFigureArt.width ||
            localY >= cachedFigureArt.height
          ) continue;
          artLayer.appendChild(
            createMonsterOverlayNode(
              eyeFrame, localX, localY,
              eye.width, eye.height,
              cachedFigureArt.width, cachedFigureArt.height,
            ),
          );
        }
      }
    }

    return;
  }

  container.replaceChildren();
  container.classList.toggle('monster-figure-absolute', false);

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

  if (!figureArt) {
    container.replaceChildren();
    return;
  }

  const bfigCacheKey = `${pieceType}:${rotation}`;
  if ((container as any).__mbfKey === bfigCacheKey && container.childElementCount > 0) {
    return;
  }
  (container as any).__mbfKey = bfigCacheKey;

  container.replaceChildren();

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
  for (const frame of createMonsterRippleFrames(figureArt, `bfig:${pieceType}:${rotation}`, RIPPLE_AMP_SCALE[pieceType] ?? 1.0)) {
    rippleLayer.appendChild(frame);
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
