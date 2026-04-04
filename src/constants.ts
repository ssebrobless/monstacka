import type { PieceType, Cell, Settings, GameMode, ControlAction, ControlBindings } from './types';

export const COLS = 10;
export const VISIBLE_ROWS = 20;
export const HIDDEN_ROWS = 4;
export const TOTAL_ROWS = VISIBLE_ROWS + HIDDEN_ROWS;
export const TARGET_LINES = 40;
export const GRAVITY_MS = 650;
export const COUNTDOWN_MS = 1000;
export const STORAGE_KEY = 'monstacka_local_v1';
export const LEGACY_STORAGE_KEYS = ['eris_preview_tetris_v2'];
export const MAX_NICKNAME_LENGTH = 5;
export const MIN_NICKNAME_LENGTH = 3;
export const MAX_LEADERBOARD_ENTRIES = 8;
export const DEFAULT_MODE: GameMode = 'arcade';
export const CONTROL_ORDER: ControlAction[] = [
  'left',
  'right',
  'soft',
  'hard',
  'ccw',
  'cw',
  'flip',
  'hold',
  'retry',
  'pause',
  'restartPaused',
];
export const CONTROL_LABELS: Record<ControlAction, string> = {
  left: 'Move Left',
  right: 'Move Right',
  soft: 'Soft Drop',
  hard: 'Hard Drop',
  ccw: 'Rotate CCW',
  cw: 'Rotate CW',
  flip: 'Rotate 180',
  hold: 'Hold',
  retry: 'Retry',
  pause: 'Pause / Resume',
  restartPaused: 'Restart Paused',
};
export const DEFAULT_CONTROLS: ControlBindings = {
  left: 'Key:ArrowLeft',
  right: 'Key:ArrowRight',
  soft: 'Key:ArrowDown',
  hard: 'Key:Space',
  ccw: 'Key:KeyZ',
  cw: 'Key:KeyX',
  flip: 'Key:KeyA',
  hold: 'Key:KeyC',
  retry: 'Key:KeyR',
  pause: 'Key:KeyP',
  restartPaused: 'Key:KeyO',
};
export const DEFAULT_GAMEPAD_CONTROLS: ControlBindings = {
  left: 'Pad:Button14',
  right: 'Pad:Button15',
  soft: 'Pad:Button13',
  hard: 'Pad:Button12',
  ccw: 'Pad:Button0',
  cw: 'Pad:Button1',
  flip: 'Pad:Button3',
  hold: 'Pad:Button4',
  retry: 'Pad:Button9',
  pause: 'Pad:Button8',
  restartPaused: 'Pad:Button10',
};
export const SCORE_TABLE: Record<number, number> = {
  1: 100,
  2: 300,
  3: 500,
  4: 800,
};

export const MODE_LABELS: Record<GameMode, string> = {
  arcade: 'Arcade',
  sprint40: '40 Lines',
  training: 'Training',
};

export const MODE_DESCRIPTIONS: Record<GameMode, string> = {
  arcade: 'Play until you top out. Chase the highest score you can post.',
  sprint40: 'Clear 40 lines as fast as possible in a TETR.IO-style sprint mode.',
  training: 'Finesse practice on an empty board with Show and Redo feedback modes.',
};

export const SETTINGS_DEFAULTS: Settings = {
  dasMs: 110,
  arrMs: 0,
  lockDelayMs: 250,
  sfxEnabled: true,
  sfxVolume: 70,
  musicEnabled: true,
  musicVolume: 35,
  trainingFeedback: 'show',
  ditherEnabled: true,
  cleanLabels: true,
  controls: { ...DEFAULT_CONTROLS },
  gamepadControls: { ...DEFAULT_GAMEPAD_CONTROLS },
};

export const PIECE_COLORS: Record<PieceType, string> = {
  I: '#53d4ff',
  O: '#ffd452',
  T: '#be78ff',
  S: '#53d37b',
  Z: '#ff6f6f',
  J: '#5c7cff',
  L: '#ff9a45',
};

export const DEFINITIONS: Record<PieceType, Cell[][]> = {
  I: [
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 3, y: 1 }],
    [{ x: 2, y: 0 }, { x: 2, y: 1 }, { x: 2, y: 2 }, { x: 2, y: 3 }],
    [{ x: 0, y: 2 }, { x: 1, y: 2 }, { x: 2, y: 2 }, { x: 3, y: 2 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 1, y: 3 }],
  ],
  O: [
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
  ],
  T: [
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }],
  ],
  S: [
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 2, y: 2 }],
    [{ x: 1, y: 1 }, { x: 2, y: 1 }, { x: 0, y: 2 }, { x: 1, y: 2 }],
    [{ x: 0, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }],
  ],
  Z: [
    [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 2, y: 2 }],
    [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 0, y: 2 }],
  ],
  J: [
    [{ x: 0, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 2, y: 2 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 0, y: 2 }, { x: 1, y: 2 }],
  ],
  L: [
    [{ x: 2, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }],
    [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 2, y: 2 }],
    [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 0, y: 2 }],
    [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }],
  ],
};

export const KICKS_JLSTZ: Record<string, number[][]> = {
  '0>1': [[0, 0], [-1, 0], [-1, 1], [0, -2], [-1, -2]],
  '1>0': [[0, 0], [1, 0], [1, -1], [0, 2], [1, 2]],
  '1>2': [[0, 0], [1, 0], [1, -1], [0, 2], [1, 2]],
  '2>1': [[0, 0], [-1, 0], [-1, 1], [0, -2], [-1, -2]],
  '2>3': [[0, 0], [1, 0], [1, 1], [0, -2], [1, -2]],
  '3>2': [[0, 0], [-1, 0], [-1, -1], [0, 2], [-1, 2]],
  '3>0': [[0, 0], [-1, 0], [-1, -1], [0, 2], [-1, 2]],
  '0>3': [[0, 0], [1, 0], [1, 1], [0, -2], [1, -2]],
};

export const KICKS_I: Record<string, number[][]> = {
  '0>1': [[0, 0], [-2, 0], [1, 0], [-2, -1], [1, 2]],
  '1>0': [[0, 0], [2, 0], [-1, 0], [2, 1], [-1, -2]],
  '1>2': [[0, 0], [-1, 0], [2, 0], [-1, 2], [2, -1]],
  '2>1': [[0, 0], [1, 0], [-2, 0], [1, -2], [-2, 1]],
  '2>3': [[0, 0], [2, 0], [-1, 0], [2, 1], [-1, -2]],
  '3>2': [[0, 0], [-2, 0], [1, 0], [-2, -1], [1, 2]],
  '3>0': [[0, 0], [1, 0], [-2, 0], [1, -2], [-2, 1]],
  '0>3': [[0, 0], [-1, 0], [2, 0], [-1, 2], [2, -1]],
};
