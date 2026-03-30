import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SETTINGS_DEFAULTS } from '../../constants';
import { normalizeNickname, qualifiesScoreRecord, qualifiesSprintRecord, saveScoreRecord, saveSprintRecord } from '../../storage';
import type { StorageData } from '../../types';

function createStorage(): StorageData {
  return {
    sprint: [],
    score: [],
    settings: { ...SETTINGS_DEFAULTS },
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
    data.sprint = Array.from({ length: 10 }, (_, index) => ({
      nickname: `S${index}`,
      timeMs: 1000 + index * 100,
      lines: 40,
      pieces: 60,
      timestamp: `2026-03-3${index}T00:00:00.000Z`,
    }));
    data.score = Array.from({ length: 10 }, (_, index) => ({
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

  it('saves records in sorted top-10 order', () => {
    const data = createStorage();

    for (let index = 0; index < 12; index += 1) {
      saveSprintRecord(data, `s${index}`, 1000 + index * 10, 40, 80);
      saveScoreRecord(data, `p${index}`, (index + 1) * 100, 20, 15000);
    }

    expect(data.sprint).toHaveLength(10);
    expect(data.sprint[0].nickname).toBe('S0');
    expect(data.sprint[0].timeMs).toBe(1000);
    expect(data.sprint[9].timeMs).toBe(1090);

    expect(data.score).toHaveLength(10);
    expect(data.score[0].nickname).toBe('P11');
    expect(data.score[0].score).toBe(1200);
    expect(data.score[9].score).toBe(300);
  });
});
