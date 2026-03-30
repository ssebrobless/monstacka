export type PieceType = 'I' | 'O' | 'T' | 'S' | 'Z' | 'J' | 'L';
export type GameMode = 'arcade' | 'sprint40' | 'training';
export type TrainingFeedbackMode = 'off' | 'show' | 'redo';

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

export interface TrainingSnapshot {
  active: Piece;
  queue: PieceType[];
}

export interface GameState {
  board: string[][];
  boardSkin: string[][];
  active: Piece | null;
  hold: PieceType | '';
  holdUsed: boolean;
  queue: PieceType[];
  hasSpawned: boolean;
  mode: GameMode;
  lines: number;
  score: number;
  pieces: number;
  startTime: number;
  completedTime: number;
  countdownUntil: number;
  lastGravity: number;
  lockDeadline: number;
  lastLockAt: number;
  lastLineClearAt: number;
  trainingFeedback: TrainingFeedbackMode;
  currentPieceInputs: number;
  trainingFaults: number;
  trainingPerfectStreak: number;
  lastTrainingFaultAt: number;
  lastTrainingFaultMessage: string;
  trainingSnapshot: TrainingSnapshot | null;
  sprintComplete: boolean;
  gameOver: boolean;
}

export interface Settings {
  dasMs: number;
  arrMs: number;
  lockDelayMs: number;
  sfxVolume: number;
  musicVolume: number;
  muted: boolean;
  trainingFeedback: TrainingFeedbackMode;
}

export interface SprintRecord {
  nickname: string;
  timeMs: number;
  lines: number;
  pieces: number;
  timestamp: string;
}

export interface ScoreRecord {
  nickname: string;
  score: number;
  lines: number;
  timeMs: number;
  timestamp: string;
}

export interface StorageData {
  sprint: SprintRecord[];
  score: ScoreRecord[];
  settings: Settings;
}
