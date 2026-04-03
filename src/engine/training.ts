import type { Piece, PieceType } from '../types';
import { createBoard } from './board';
import { isValid, rotate } from './pieces';

export interface TrainingEvaluation {
  optimalInputs: number | null;
  actualInputs: number;
  isFault: boolean;
  message: string;
}

type Action =
  | 'tapLeft'
  | 'tapRight'
  | 'dashLeft'
  | 'dashRight'
  | 'cw'
  | 'ccw'
  | 'flip';

const EMPTY_BOARD = createBoard();
const ACTIONS: Action[] = ['tapLeft', 'tapRight', 'dashLeft', 'dashRight', 'cw', 'ccw', 'flip'];
const ALL_PIECES: PieceType[] = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'];

function stateKey(piece: Piece): string {
  return `${piece.x}:${piece.y}:${piece.rotation}`;
}

function placementKey(piece: Piece): string {
  return `${piece.x}:${piece.rotation}`;
}

function isValidPosition(piece: Piece): boolean {
  return isValid(EMPTY_BOARD, piece);
}

function movePiece(piece: Piece, dx: number): Piece | null {
  const next = { ...piece, x: piece.x + dx };
  return isValidPosition(next) ? next : null;
}

function dashPiece(piece: Piece, dx: number): Piece {
  let next = { ...piece };
  while (true) {
    const moved = movePiece(next, dx);
    if (!moved) {
      return next;
    }
    next = moved;
  }
}

function dropToGround(piece: Piece): Piece {
  let next = { ...piece };
  while (true) {
    const dropped = { ...next, y: next.y + 1 };
    if (!isValidPosition(dropped)) {
      return next;
    }
    next = dropped;
  }
}

function applyAction(piece: Piece, action: Action): Piece | null {
  switch (action) {
    case 'tapLeft':
      return movePiece(piece, -1);
    case 'tapRight':
      return movePiece(piece, 1);
    case 'dashLeft': {
      const dashed = dashPiece(piece, -1);
      return dashed.x === piece.x ? null : dashed;
    }
    case 'dashRight': {
      const dashed = dashPiece(piece, 1);
      return dashed.x === piece.x ? null : dashed;
    }
    case 'cw':
      return rotate(EMPTY_BOARD, piece, 1, true);
    case 'ccw':
      return rotate(EMPTY_BOARD, piece, -1, true);
    case 'flip':
      return rotate(EMPTY_BOARD, piece, 2, false);
    default:
      return null;
  }
}

function buildPieceLookup(type: PieceType): Record<string, number> {
  const start: Piece = { type, rotation: 0, x: 3, y: 0 };
  const queue: Piece[] = [start];
  const costs = new Map<string, number>([[stateKey(start), 0]]);
  const placements = new Map<string, number>();

  while (queue.length) {
    const current = queue.shift()!;
    const currentCost = costs.get(stateKey(current))!;
    const landed = dropToGround(current);
    const landedKey = placementKey(landed);
    const knownPlacementCost = placements.get(landedKey);
    if (knownPlacementCost === undefined || currentCost < knownPlacementCost) {
      placements.set(landedKey, currentCost);
    }

    for (const action of ACTIONS) {
      const next = applyAction(current, action);
      if (!next) continue;

      const key = stateKey(next);
      const nextCost = currentCost + 1;
      const knownCost = costs.get(key);
      if (knownCost !== undefined && knownCost <= nextCost) continue;

      costs.set(key, nextCost);
      queue.push(next);
    }
  }

  return Object.fromEntries(placements.entries());
}

export const FINESSE_LOOKUP: Record<PieceType, Record<string, number>> = Object.fromEntries(
  ALL_PIECES.map((piece) => [piece, buildPieceLookup(piece)]),
) as Record<PieceType, Record<string, number>>;

export function getOptimalInputCount(pieceType: PieceType, x: number, rotation: number): number | null {
  const result = FINESSE_LOOKUP[pieceType][`${x}:${rotation}`];
  return typeof result === 'number' ? result : null;
}

export function evaluateTrainingPlacement(piece: Piece, actualInputs: number): TrainingEvaluation {
  const optimalInputs = getOptimalInputCount(piece.type, piece.x, piece.rotation);
  const isFault = optimalInputs !== null && actualInputs > optimalInputs;

  return {
    optimalInputs,
    actualInputs,
    isFault,
    message: optimalInputs === null
      ? 'No lookup data for this placement.'
      : `${actualInputs} inputs used / ${optimalInputs} optimal`,
  };
}
