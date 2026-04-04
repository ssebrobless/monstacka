export type PieceType = 'I' | 'O' | 'T' | 'S' | 'Z' | 'J' | 'L';
export type GameMode = 'arcade' | 'sprint40' | 'training';
export type TrainingFeedbackMode = 'off' | 'show' | 'redo';
export type AppPhase = 'menu' | 'countdown' | 'playing' | 'paused' | 'game-over' | 'sprint-clear';
export type ResumableAppPhase = 'countdown' | 'playing' | 'paused';
export type ControlAction =
  | 'left'
  | 'right'
  | 'soft'
  | 'hard'
  | 'ccw'
  | 'cw'
  | 'flip'
  | 'hold'
  | 'retry'
  | 'pause'
  | 'restartPaused';
export type ControlBindings = Record<ControlAction, string>;
export type ControlBindingSource = 'controls' | 'gamepadControls';

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
  sfxEnabled: boolean;
  sfxVolume: number;
  musicEnabled: boolean;
  musicVolume: number;
  trainingFeedback: TrainingFeedbackMode;
  ditherEnabled: boolean;
  cleanLabels: boolean;
  controls: ControlBindings;
  gamepadControls: ControlBindings;
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

export interface SavedRunState {
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
  trainingFeedback: TrainingFeedbackMode;
  currentPieceInputs: number;
  trainingFaults: number;
  trainingPerfectStreak: number;
  lastTrainingFaultMessage: string;
  trainingSnapshot: TrainingSnapshot | null;
}

export interface SavedRun {
  mode: GameMode;
  phase: ResumableAppPhase;
  savedAt: string;
  elapsedMs: number;
  remainingCountdownMs: number;
  gravityElapsedMs: number;
  lockRemainingMs: number;
  state: SavedRunState;
}

export type SavedRunsByMode = Record<GameMode, SavedRun | null>;

export interface StorageData {
  sprint: SprintRecord[];
  score: ScoreRecord[];
  settings: Settings;
  savedRuns: SavedRunsByMode;
}
