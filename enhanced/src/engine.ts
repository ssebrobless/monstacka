export type PieceType = 'I' | 'O' | 'T' | 'S' | 'Z' | 'J' | 'L';

export interface Cell {
  x: number;
  y: number;
}

export interface Piece {
  type: PieceType;
  rotation: number;
  x: number;
  y: number;
}

export interface SprintRecord {
  nickname: string;
  timeMs: number;
  pieces: number;
  lines: number;
  timestamp: string;
}

export interface EngineState {
  columns: number;
  visibleRows: number;
  hiddenRows: number;
  targetLines: number;
  board: string[][];
  active: Piece | null;
  hold: PieceType | '';
  holdUsed: boolean;
  queue: PieceType[];
  hasSpawnedAny: boolean;
  lines: number;
  pieces: number;
  sprintComplete: boolean;
  gameOver: boolean;
}

const DEFINITIONS: Record<PieceType, Cell[][]> = {
  I: [
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 3, y: 1 }],
    [{ x: 2, y: 0 }, { x: 2, y: 1 }, { x: 2, y: 2 }, { x: 2, y: 3 }],
    [{ x: 0, y: 2 }, { x: 1, y: 2 }, { x: 2, y: 2 }, { x: 3, y: 2 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 1, y: 3 }]
  ],
  O: [
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }]
  ],
  T: [
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }]
  ],
  S: [
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 2, y: 2 }],
    [{ x: 1, y: 1 }, { x: 2, y: 1 }, { x: 0, y: 2 }, { x: 1, y: 2 }],
    [{ x: 0, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }]
  ],
  Z: [
    [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 2, y: 2 }],
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 0, y: 2 }]
  ],
  J: [
    [{ x: 0, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 2, y: 2 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 0, y: 2 }, { x: 1, y: 2 }]
  ],
  L: [
    [{ x: 2, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 2, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 0, y: 2 }],
    [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }]
  ]
};

const KICKS: Record<string, number[][]> = {
  '0>1': [[0, 0], [-1, 0], [-1, 1], [0, -2], [-1, -2]],
  '1>0': [[0, 0], [1, 0], [1, -1], [0, 2], [1, 2]],
  '1>2': [[0, 0], [1, 0], [1, -1], [0, 2], [1, 2]],
  '2>1': [[0, 0], [-1, 0], [-1, 1], [0, -2], [-1, -2]],
  '2>3': [[0, 0], [1, 0], [1, 1], [0, -2], [1, -2]],
  '3>2': [[0, 0], [-1, 0], [-1, -1], [0, 2], [-1, 2]],
  '3>0': [[0, 0], [-1, 0], [-1, -1], [0, 2], [-1, 2]],
  '0>3': [[0, 0], [1, 0], [1, 1], [0, -2], [1, -2]]
};

function createBoard(rows: number, columns: number): string[][] {
  return Array.from({ length: rows }, () => Array(columns).fill(''));
}

function getCells(piece: Piece): Cell[] {
  return DEFINITIONS[piece.type][piece.rotation].map(cell => ({
    x: piece.x + cell.x,
    y: piece.y + cell.y
  }));
}

function isValid(state: EngineState, piece: Piece): boolean {
  return getCells(piece).every(cell =>
    cell.x >= 0 &&
    cell.x < state.columns &&
    cell.y < state.visibleRows + state.hiddenRows &&
    (cell.y < 0 || !state.board[cell.y][cell.x])
  );
}

function makeOpeningBag(): PieceType[] {
  const firstPool: PieceType[] = ['I', 'J', 'L', 'T'];
  const first = firstPool[Math.floor(Math.random() * firstPool.length)];
  const rest = (['I', 'O', 'T', 'S', 'Z', 'J', 'L'] as PieceType[])
    .filter(piece => piece !== first)
    .sort(() => Math.random() - 0.5);
  return [first, ...rest];
}

function makeBag(): PieceType[] {
  return (['I', 'O', 'T', 'S', 'Z', 'J', 'L'] as PieceType[]).sort(() => Math.random() - 0.5);
}

function ensureQueue(state: EngineState): void {
  while (state.queue.length < 7) {
    state.queue.push(...(state.hasSpawnedAny ? makeBag() : makeOpeningBag()));
  }
}

export function createEngineState(): EngineState {
  const state: EngineState = {
    columns: 10,
    visibleRows: 20,
    hiddenRows: 4,
    targetLines: 40,
    board: createBoard(24, 10),
    active: null,
    hold: '',
    holdUsed: false,
    queue: [],
    hasSpawnedAny: false,
    lines: 0,
    pieces: 0,
    sprintComplete: false,
    gameOver: false
  };
  resetEngineState(state);
  return state;
}

export function resetEngineState(state: EngineState): void {
  state.board = createBoard(state.visibleRows + state.hiddenRows, state.columns);
  state.active = null;
  state.hold = '';
  state.holdUsed = false;
  state.queue = [];
  state.hasSpawnedAny = false;
  state.lines = 0;
  state.pieces = 0;
  state.sprintComplete = false;
  state.gameOver = false;
  spawnNext(state);
}

export function spawnNext(state: EngineState, forcedType?: PieceType): boolean {
  ensureQueue(state);
  const piece: Piece = { type: forcedType ?? state.queue.shift()!, rotation: 0, x: 3, y: 0 };
  if (!isValid(state, piece)) {
    state.active = null;
    state.gameOver = true;
    return false;
  }
  state.active = piece;
  state.holdUsed = false;
  state.hasSpawnedAny = true;
  ensureQueue(state);
  return true;
}

export function moveActive(state: EngineState, dx: number, dy: number): boolean {
  if (!state.active || state.gameOver) return false;
  const next = { ...state.active, x: state.active.x + dx, y: state.active.y + dy };
  if (!isValid(state, next)) return false;
  state.active = next;
  return true;
}

export function rotateActive(state: EngineState, step: number, useKicks = true): boolean {
  if (!state.active || state.gameOver) return false;
  const from = state.active.rotation;
  const to = (from + step + 4) % 4;
  const candidate = { ...state.active, rotation: to };
  if (!useKicks) {
    if (isValid(state, candidate)) {
      state.active = candidate;
      return true;
    }
    return false;
  }
  for (const [ox, oy] of KICKS[`${from}>${to}`] || [[0, 0]]) {
    const kicked = { ...candidate, x: candidate.x + ox, y: candidate.y - oy };
    if (isValid(state, kicked)) {
      state.active = kicked;
      return true;
    }
  }
  return false;
}

export function holdActive(state: EngineState): void {
  if (!state.active || state.holdUsed || state.gameOver) return;
  const current = state.active.type;
  if (state.hold) {
    const swap = state.hold;
    state.hold = current;
    spawnNext(state, swap);
  } else {
    state.hold = current;
    spawnNext(state);
  }
  state.holdUsed = true;
}

export function getGhostCells(state: EngineState): Cell[] {
  if (!state.active) return [];
  let ghost = { ...state.active };
  while (isValid(state, { ...ghost, y: ghost.y + 1 })) {
    ghost = { ...ghost, y: ghost.y + 1 };
  }
  return getCells(ghost);
}

export function hardDrop(state: EngineState): void {
  while (moveActive(state, 0, 1)) {
  }
  lockActive(state);
}

export function lockActive(state: EngineState): void {
  if (!state.active) return;
  for (const cell of getCells(state.active)) {
    if (cell.y >= 0) {
      state.board[cell.y][cell.x] = state.active.type;
    }
  }
  state.active = null;
  state.pieces += 1;
  clearLines(state);
  if (!state.sprintComplete) {
    spawnNext(state);
  }
}

export function clearLines(state: EngineState): void {
  const nextRows: string[][] = [];
  let cleared = 0;
  for (const row of state.board) {
    if (row.every(Boolean)) {
      cleared += 1;
    } else {
      nextRows.push([...row]);
    }
  }
  while (nextRows.length < state.board.length) {
    nextRows.unshift(Array(state.columns).fill(''));
  }
  state.board = nextRows;
  state.lines += cleared;
  if (state.lines >= state.targetLines) {
    state.sprintComplete = true;
    state.gameOver = true;
  }
}
