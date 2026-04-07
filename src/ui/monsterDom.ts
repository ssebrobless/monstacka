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

const RIPPLE_FRAME_COUNT = 4;
const RIPPLE_AMPLITUDE = 20;
const RIPPLES_PER_SIDE = 5;
const RIPPLE_PAD = 22; // extra canvas margin so protrusions aren't clipped
const RIPPLE_TUCK_DEPTH = 4;
const RIPPLE_EDGE_FEATHER = 4.0;
const RIPPLE_SHADOW_WIDTH = 3.0;
const RIPPLE_SHADOW_OPACITY = 0.35;

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
): HTMLCanvasElement {
  const { alpha, edgePixels, width: w, height: h } = data;
  const pw = w + RIPPLE_PAD * 2;
  const ph = h + RIPPLE_PAD * 2;
  const canvas = document.createElement('canvas');
  canvas.className = 'monster-ripple-art';
  canvas.classList.add(`ripple-frame-${frameIndex}`);
  canvas.width = pw;
  canvas.height = ph;
  const ctx = canvas.getContext('2d')!;
  const output = ctx.createImageData(pw, ph);

  const phaseOffset = (frameIndex / RIPPLE_FRAME_COUNT) * Math.PI * 2;
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

  // Compute protrusion using abs(sin) traveling wave — 5 ripples per side, no gaps
  const protrusions = new Float32Array(edgePixels.length);
  for (let s = 0; s < 4; s += 1) {
    const group = sideGroups[s];
    if (!group.length) continue;
    for (let gi = 0; gi < group.length; gi += 1) {
      const t = group.length > 1 ? gi / (group.length - 1) : 0.5;
      const wave = Math.sin(t * RIPPLES_PER_SIDE * Math.PI * 2 + phaseOffset);
      protrusions[group[gi]] = Math.abs(wave) * RIPPLE_AMPLITUDE;
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
      const bubbleHalfWidth = 24.0 * (1 - normalizedDist * normalizedDist);
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

function createMonsterRippleFrames(source: HTMLCanvasElement): HTMLCanvasElement[] {
  const data = getEdgeData(source);
  if (data.empty) return [];

  const color = sampleEdgeColor(data);
  const frames: HTMLCanvasElement[] = [];
  for (let f = 0; f < RIPPLE_FRAME_COUNT; f += 1) {
    frames.push(createRippleFrameCanvas(data, f, color));
  }
  return frames;
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
  for (const frame of createMonsterRippleFrames(tile.canvas)) {
    rippleLayer.appendChild(frame);
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
      for (const frame of createMonsterRippleFrames(figureArt)) {
        rippleLayer.appendChild(frame);
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
    // Sprites not loaded yet — board cells show colored fallback via piece-* class
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
  for (const frame of createMonsterRippleFrames(figureArt)) {
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
