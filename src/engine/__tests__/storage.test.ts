import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SETTINGS_DEFAULTS, MAX_LEADERBOARD_ENTRIES } from '../../constants';
import {
  clearSavedRun,
  getSavedRun,
  loadStorage,
  normalizeNickname,
  qualifiesScoreRecord,
  qualifiesSprintRecord,
  saveScoreRecord,
  saveSprintRecord,
  setSavedRun,
} from '../../storage';
import type { SavedRun, StorageData } from '../../types';

function createStorage(): StorageData {
  return {
    sprint: [],
    score: [],
    settings: {
      ...SETTINGS_DEFAULTS,
      controls: { ...SETTINGS_DEFAULTS.controls },
      gamepadControls: { ...SETTINGS_DEFAULTS.gamepadControls },
    },
    savedRuns: {
      arcade: null,
      sprint40: null,
      training: null,
    },
  };
}

function createSavedRun(mode: SavedRun['mode']): SavedRun {
  return {
    mode,
    phase: 'playing',
    savedAt: '2026-03-31T00:00:00.000Z',
    elapsedMs: 5000,
    remainingCountdownMs: 0,
    gravityElapsedMs: 120,
    lockRemainingMs: 200,
    state: {
      board: Array.from({ length: 24 }, () => Array(10).fill('')),
      boardSkin: Array.from({ length: 24 }, () => Array(10).fill('')),
      active: { type: 'T', rotation: 0, x: 3, y: 0 },
      hold: '',
      holdUsed: false,
      queue: ['I', 'O', 'L'],
      hasSpawned: true,
      mode,
      lines: 8,
      score: 1200,
      pieces: 15,
      trainingFeedback: 'show',
      lockResets: 0,
      currentPieceInputs: 0,
      trainingFaults: 1,
      trainingPerfectStreak: 2,
      lastTrainingFaultMessage: '',
      trainingSnapshot: null,
    },
  };
}

describe('storage helpers', () => {
  beforeEach(() => {
    const backingStore = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: vi.fn((key: string) => backingStore.get(key) ?? null),
      setItem: vi.fn((key: string, value: string) => backingStore.set(key, value)),
      removeItem: vi.fn((key: string) => backingStore.delete(key)),
      clear: vi.fn(() => backingStore.clear()),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('normalizes nicknames to uppercase alphanumeric 5-character tags', () => {
    expect(normalizeNickname('a*b 12xyz')).toBe('AB12X');
    expect(normalizeNickname('')).toBe('');
  });

  it('checks qualification rules for sprint and score leaderboards', () => {
    const data = createStorage();
    data.sprint = Array.from({ length: MAX_LEADERBOARD_ENTRIES }, (_, index) => ({
      nickname: `S${index}`,
      timeMs: 1000 + index * 100,
      lines: 40,
      pieces: 60,
      timestamp: `2026-03-3${index}T00:00:00.000Z`,
    }));
    data.score = Array.from({ length: MAX_LEADERBOARD_ENTRIES }, (_, index) => ({
      nickname: `P${index}`,
      score: 1000 - index * 50,
      lines: 20,
      timeMs: 20000,
      timestamp: `2026-03-2${index}T00:00:00.000Z`,
    }));

    expect(qualifiesSprintRecord(data, 950)).toBe(true);
    expect(qualifiesSprintRecord(data, 1900)).toBe(false);
    expect(qualifiesScoreRecord(data, 1200)).toBe(true);
    expect(qualifiesScoreRecord(data, 200)).toBe(false);
  });

  it('saves records in sorted top-8 order', () => {
    const data = createStorage();

    for (let index = 0; index < 12; index += 1) {
      saveSprintRecord(data, `s${index}`, 1000 + index * 10, 40, 80);
      saveScoreRecord(data, `p${index}`, (index + 1) * 100, 20, 15000);
    }

    expect(data.sprint).toHaveLength(MAX_LEADERBOARD_ENTRIES);
    expect(data.sprint[0].nickname).toBe('S0');
    expect(data.sprint[0].timeMs).toBe(1000);
    expect(data.sprint[MAX_LEADERBOARD_ENTRIES - 1].timeMs).toBe(1070);

    expect(data.score).toHaveLength(MAX_LEADERBOARD_ENTRIES);
    expect(data.score[0].nickname).toBe('P11');
    expect(data.score[0].score).toBe(1200);
    expect(data.score[MAX_LEADERBOARD_ENTRIES - 1].score).toBe(500);
  });

  it('stores and clears saved runs per mode', () => {
    const data = createStorage();
    const sprintSave = createSavedRun('sprint40');

    setSavedRun(data, sprintSave);
    expect(getSavedRun(data, 'sprint40')?.state.lines).toBe(8);
    expect(getSavedRun(data, 'arcade')).toBeNull();

    clearSavedRun(data, 'sprint40');
    expect(getSavedRun(data, 'sprint40')).toBeNull();
  });

  it('hydrates controller bindings when loading storage', () => {
    localStorage.setItem('monstacka_local_v1', JSON.stringify({
      sprint: [],
      score: [],
      settings: {
        controls: {
          ...SETTINGS_DEFAULTS.controls,
          hold: 'Key:KeyV',
        },
      },
      savedRuns: {
        arcade: null,
        sprint40: null,
        training: null,
      },
    }));

    const storage = loadStorage();
    expect(storage.settings.controls.hold).toBe('Key:KeyV');
    expect(storage.settings.gamepadControls.left).toBe(SETTINGS_DEFAULTS.gamepadControls.left);
    expect(storage.settings.gamepadControls.pause).toBe(SETTINGS_DEFAULTS.gamepadControls.pause);
  });
});
